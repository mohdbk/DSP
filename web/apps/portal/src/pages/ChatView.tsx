import { useEffect, useRef, useState } from 'react';
import { Alert, getLang, t, type Application, type ChatMessage, type ChatResponse } from '@dsp/shared';
import { api } from '../session';

interface ChatState { messages: ChatMessage[]; pendingField: string | null; mode?: string }

/**
 * Chat view of the same application. The assistant saves answers through the same
 * E-Services API as the form, with the user's own permissions.
 */
export function ChatView({ app, onApplication }: {
  app: Application;
  onApplication: (partial: ChatResponse['application']) => void;
}) {
  const storageKey = `dsp.chat.${app.id}`;
  const [state, setState] = useState<ChatState>(() => {
    try { return JSON.parse(sessionStorage.getItem(storageKey) ?? '') as ChatState; }
    catch { return { messages: [], pendingField: null }; }
  });
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const logRef = useRef<HTMLDivElement>(null);

  useEffect(() => { sessionStorage.setItem(storageKey, JSON.stringify(state)); }, [state, storageKey]);
  useEffect(() => { logRef.current?.scrollTo({ top: logRef.current.scrollHeight, behavior: 'smooth' }); }, [state.messages, busy]);
  useEffect(() => { if (state.messages.length === 0) send(null); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function send(text: string | null) {
    const messages = text ? [...state.messages, { role: 'user' as const, content: text }] : state.messages;
    setState(s => ({ ...s, messages }));
    setInput('');
    setBusy(true);
    setError(undefined);
    try {
      const res = await api.post<ChatResponse>('/api/ai/chat', {
        applicationId: app.id,
        messages,
        pendingField: state.pendingField,
        language: getLang(),
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
    <div className="card chat">
      <div className="chat-mode">
        {state.mode === 'llm' ? t('chat.ai') : t('chat.guided')} · {t('chat.saved')}
      </div>
      <div className="chat-log" ref={logRef} aria-live="polite">
        {state.messages.map((m, i) => <div key={i} className={`msg ${m.role}`} dir="auto">{m.content}</div>)}
        {busy && <div className="msg assistant typing">{t('chat.typing')}</div>}
      </div>
      {error && <Alert kind="error">{error}</Alert>}
      <form className="chat-input" onSubmit={e => { e.preventDefault(); if (input.trim() && !busy) send(input.trim()); }}>
        <input value={input} onChange={e => setInput(e.target.value)} placeholder={t('chat.placeholder')} aria-label={t('chat.message')} disabled={busy} dir="auto" />
        <button className="btn btn-primary" disabled={busy || !input.trim()}>{t('chat.send')}</button>
      </form>
    </div>
  );
}
