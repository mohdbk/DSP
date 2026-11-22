import { useEffect, useState } from 'react';
import { ApiError, formatDate, t, type AiSettingsView, type AiTestResult, type FieldError } from '@dsp/core';
import {
  Actions, AegovButton, CheckboxField, Loading, Notice, NumberField, PageHeader, Panel, Tag, TextAreaField, TextField,
} from '@dsp/ui';
import { api } from '../../session';
import { SettingsNav } from './SettingsNav';

const base = '/api/v1/platform/admin/ai';

/**
 * The model behind the chat assistant and the AI pre-review: provider, address, model, key, sampling, limits and
 * extra instructions. Any OpenAI-compatible endpoint works (Net0 in production). The key is write-only.
 */
export function AiSettingsPage() {
  const [settings, setSettings] = useState<AiSettingsView>();
  const [form, setForm] = useState<AiSettingsView>();
  const [apiKey, setApiKey] = useState('');
  const [removeKey, setRemoveKey] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [busy, setBusy] = useState<'save' | 'test'>();
  const [message, setMessage] = useState<{ kind: 'success' | 'error' | 'info'; text: string }>();

  const adopt = (s: AiSettingsView) => { setSettings(s); setForm(s); setApiKey(''); setRemoveKey(false); };
  useEffect(() => { api.get<AiSettingsView>(base).then(adopt).catch(e => setMessage({ kind: 'error', text: e.message })); }, []);

  if (!form || !settings) return message ? <Notice kind="error">{message.text}</Notice> : <Loading />;
  const err = (field: string) => errors.find(e => e.field === field)?.message;
  const set = <K extends keyof AiSettingsView>(key: K, value: AiSettingsView[K]) => setForm({ ...form, [key]: value });

  async function save() {
    setBusy('save');
    setMessage(undefined);
    try {
      adopt(await api.put<AiSettingsView>(base, {
        ...form,
        // null keeps the saved key, "" removes it, anything else replaces it.
        apiKey: removeKey ? '' : apiKey.trim() ? apiKey.trim() : null,
      }));
      setErrors([]);
      setMessage({ kind: 'success', text: t('ai.settings.saved') });
    } catch (e) {
      if (e instanceof ApiError && e.errors.length) setErrors(e.errors);
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  async function test() {
    setBusy('test');
    setMessage(undefined);
    try {
      const result = await api.post<AiTestResult>(`${base}/test`);
      const parts = [t(result.messageKey), result.detail, result.latencyMs != null ? t('ai.test.latency', { ms: result.latencyMs }) : null];
      setMessage({ kind: result.ok ? 'success' : 'error', text: parts.filter(Boolean).join(' · ') });
    } catch (e) {
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  const dirty = JSON.stringify(form) !== JSON.stringify(settings) || !!apiKey || removeKey;

  return (
    <>
      <PageHeader title={t('settings.ai')} intro={t('ai.settings.intro')} actions={<SettingsNav current="ai" />}
        badges={settings.isConfigured
          ? <Tag variant="aegreen">{t('ai.settings.configured', { model: settings.model })}</Tag>
          : <Tag variant="slate">{t('ai.settings.notConfigured')}</Tag>} />
      {message && <Notice kind={message.kind}>{message.text}</Notice>}

      <Panel className="mb-6 block" title={t('ai.settings.connection')}>
        <div className="grid gap-x-4 md:grid-cols-2">
          <TextField id="provider" label={t('ai.settings.provider')} hint={t('ai.settings.providerHint')} value={form.providerName}
            markOptional onChange={v => set('providerName', v)} />
          <TextField id="model" label={t('ai.settings.model')} hint={t('ai.settings.modelHint')} dir="ltr" value={form.model}
            error={err('model')} onChange={v => set('model', v)} />
        </div>
        <TextField id="baseUrl" label={t('ai.settings.baseUrl')} hint={t('ai.settings.baseUrlHint')} dir="ltr" type="text"
          value={form.baseUrl} error={err('baseUrl')} onChange={v => set('baseUrl', v)} />
        <TextField id="apiKey" label={t('ai.settings.apiKey')} type="password" dir="ltr" markOptional disabled={removeKey}
          hint={settings.hasApiKey ? t('ai.settings.apiKeySaved') : t('ai.settings.apiKeyNone')}
          value={apiKey} onChange={setApiKey} />
        {settings.hasApiKey && (
          <CheckboxField id="removeKey" label={t('ai.settings.removeKey')} checked={removeKey} onChange={setRemoveKey} />
        )}
      </Panel>

      <Panel className="mb-6 block" title={t('ai.settings.behaviour')}>
        <CheckboxField id="chat" label={t('ai.settings.chatEnabled')} hint={t('ai.settings.chatEnabledHint')}
          checked={form.chatEnabled} onChange={v => set('chatEnabled', v)} />
        <CheckboxField id="review" label={t('ai.settings.reviewEnabled')} hint={t('ai.settings.reviewEnabledHint')}
          checked={form.reviewEnabled} onChange={v => set('reviewEnabled', v)} />
        <div className="grid gap-x-4 md:grid-cols-4">
          <NumberField id="temperature" label={t('ai.settings.temperature')} hint={t('ai.settings.temperatureHint')}
            value={form.temperature} error={err('temperature')} onChange={v => set('temperature', v ?? 0)} />
          <NumberField id="timeout" label={t('ai.settings.timeout')} hint={t('ai.settings.timeoutHint')}
            value={form.timeoutSeconds} error={err('timeoutSeconds')} onChange={v => set('timeoutSeconds', v ?? 90)} />
          <NumberField id="iterations" label={t('ai.settings.iterations')} hint={t('ai.settings.iterationsHint')}
            value={form.maxToolIterations} error={err('maxToolIterations')} onChange={v => set('maxToolIterations', v ?? 6)} />
          <NumberField id="history" label={t('ai.settings.history')} hint={t('ai.settings.historyHint')}
            value={form.maxHistoryMessages} error={err('maxHistoryMessages')} onChange={v => set('maxHistoryMessages', v ?? 30)} />
        </div>
      </Panel>

      <Panel className="mb-6 block" title={t('ai.settings.instructions')}>
        <TextAreaField id="chatInstructions" label={t('ai.settings.chatInstructions')} hint={t('ai.settings.instructionsHint')}
          markOptional value={form.chatInstructions ?? ''} onChange={v => set('chatInstructions', v || null)} />
        <TextAreaField id="reviewInstructions" label={t('ai.settings.reviewInstructions')} hint={t('ai.settings.instructionsHint')}
          markOptional value={form.reviewInstructions ?? ''} onChange={v => set('reviewInstructions', v || null)} />
      </Panel>

      <Actions align="between">
        <span className="text-sm text-aeblack-600">{t('ai.settings.lastChange', { when: formatDate(settings.updatedAt, true), who: settings.updatedBy })}</span>
        <span className="flex flex-wrap gap-3">
          <AegovButton appearance="outline" isDisabled={!!busy || dirty} isLoading={busy === 'test'} onAegovButtonClick={test}>
            {t('ai.settings.test')}
          </AegovButton>
          <AegovButton isDisabled={!!busy || !dirty} isLoading={busy === 'save'} onAegovButtonClick={save}>{t('save')}</AegovButton>
        </span>
      </Actions>
    </>
  );
}
