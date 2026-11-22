import { useEffect, useState } from 'react';
import { getLang, t, type Application, type ChatMessage, type ChatResponse } from '@dsp/core';
import { AegovButton, ChatThread, Notice, Panel, TextField } from '@dsp/ui';
import { api } from '../session';

interface ChatState { messages: ChatMessage[]; pendingField: string | null; mode?: string }

function loadState(key: string): ChatState {
  try { return JSON.parse(sessionStorage.getItem(key) ?? '') as ChatState; } catch { return { messages: [], pendingField: null }; }
}

/**
 * The chat view of the same application. The assistant saves answers through the same
 * E-Services API as the form, with the user's own permissions, in the user's language.
 */
export function ChatView({ app, onApplication }: {
  app: Application;
  onApplication: (partial: ChatResponse['application']) => void;
}) {
  const storageKey = `dsp.chat.${app.id}`;
  const [state, setState] = useState<ChatState>(() => loadState(storageKey));
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => { try { sessionStorage.setItem(storageKey, JSON.stringify(state)); } catch { /* not kept */ } }, [state, storageKey]);
  useEffect(() => { if (state.messages.length === 0) send(null); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function send(text: string | null) {
    const messages = text ? [...state.messages, { role: 'user' as const, content: text }] : state.messages;
    setState(s => ({ ...s, messages }));
    setInput('');
    setBusy(true);
    setError(undefined);
    try {
      const res = await api.post<ChatResponse>('/api/ai/chat', {
        applicationId: app.id, messages, pendingField: state.pendingField, language: getLang(),
      });
      setState({ messages: [...messages, { role: 'assistant', content: res.reply }], pendingField: res.pendingField ?? null, mode: res.mode });
      onApplication(res.application);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Panel title={state.mode === 'llm' ? t('chat.ai') : t('chat.guided')}>
      <p className="-mt-2 mb-4 text-sm text-aeblack-600">{t('chat.saved')}</p>
      {error && <Notice kind="error">{error}</Notice>}
      <ChatThread messages={state.messages} typing={busy ? t('chat.typing') : undefined} footer={(
        <form className="flex items-end gap-3" onSubmit={e => { e.preventDefault(); if (input.trim() && !busy) send(input.trim()); }}>
          <div className="flex-1">
            <TextField id="chat-message" label={t('chat.message')} placeholder={t('chat.placeholder')} value={input}
              onChange={setInput} disabled={busy} />
          </div>
          <div className="mb-5">
            <AegovButton type="submit" isDisabled={busy || !input.trim()}>{t('chat.send')}</AegovButton>
          </div>
        </form>
      )} />
    </Panel>
  );
}
