import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { ApiError, getCurrencies, getDefaultLang, getLang, t, type BuilderService, type FieldError, type LocalizedText, type ServiceDefinition } from '@dsp/core';
import { AegovButton, AegovLink, MultilingualField, Notice, PageHeader, Panel, TextField } from '@dsp/ui';
import { api } from '../../session';

/** A text in the editor's language (staff fill in the other languages in the editor). */
const textNow = (text: string): LocalizedText => ({ [getLang()]: text });

/** A starting point for a new service: one step, one field, ready to edit. */
function blankService(code: string, name: LocalizedText): ServiceDefinition {
  return {
    code, name, description: {}, category: textNow(t('builder.defaultCategory')),
    fee: 100, currency: getCurrencies().find(c => c.isDefault)?.code ?? 'AED', referenceField: null, workflowVersion: 1, reviewChecklist: [],
    steps: [{
      id: 'applicant', title: textNow(t('builder.defaultStep')), fields: [
        { key: 'fullName', label: textNow(t('builder.defaultFullName')), type: 'text', required: true, maxLength: 150, prefillFrom: 'name' },
      ],
    }],
  };
}

/** New service: choose its code and name, then edit it as a draft. */
export function NewServicePage() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [name, setName] = useState<LocalizedText | null>(null);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [error, setError] = useState<string>();
  const err = (field: string) => errors.find(e => e.field === field)?.message;

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    setErrors([]);
    try {
      const created = await api.post<BuilderService>('/api/v1/eservices/builder/services', blankService(code.trim(), name ?? {}));
      navigate({ to: '/services/$code', params: { code: created.code }, replace: true });
    } catch (ex) {
      if (ex instanceof ApiError && ex.errors.length) setErrors(ex.errors);
      else setError((ex as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title={t('builder.createTitle')} actions={<AegovLink href="/services">{t('builder.backToList')}</AegovLink>} />
      <Panel>
        <form onSubmit={create}>
          {error && <Notice kind="error">{error}</Notice>}
          <TextField id="code" label={t('builder.code')} hint={t('builder.codeHelp')} error={err('code')} dir="ltr" required
            placeholder="BUSINESS_PERMIT" value={code} onChange={v => setCode(v.toUpperCase().replace(/[^A-Z0-9_]/g, '_'))} />
          <MultilingualField id="name" label={t('builder.name')} value={name} onChange={setName}
            error={c => err(`name.${c}`) ?? err('name')} />
          <AegovButton type="submit" isLoading={busy} isDisabled={!code || !name?.[getDefaultLang()]}>{t('builder.create')}</AegovButton>
        </form>
      </Panel>
    </div>
  );
}
