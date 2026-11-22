import { useEffect, useRef, useState } from 'react';
import {
  applyA2ui, t, uploadDocument, type A2uiMessage, type A2uiSurfaceState, type Application, type ChatResponse,
} from '@dsp/core';
import {
  A2uiSurface, AegovButton, ChatThread, IconLabel, Notice, Panel, PaperPlaneRight, TextField, type ThreadItem,
} from '@dsp/ui';
import { api } from '../session';

/** What the conversation shows: text from either side, or an A2UI screen from the agent. */
type Entry =
  | { kind: 'text'; id: string; role: 'user' | 'assistant'; content: string }
  | { kind: 'screen'; id: string; surfaceId: string };

interface ChatState {
  entries: Entry[];
  surfaces: Record<string, A2uiSurfaceState>;
  pendingField: string | null;
  mode?: string;
}

interface Action { name: string; context: Record<string, unknown> }

const empty: ChatState = { entries: [], surfaces: {}, pendingField: null };
const uid = () => (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

function loadState(key: string): ChatState {
  try { return { ...empty, ...(JSON.parse(sessionStorage.getItem(key) ?? '') as ChatState) }; } catch { return empty; }
}

/**
 * The chat view of the same application, over AG-UI: the assistant's reply streams in, and it can answer with
 * A2UI screens (the next questions as real fields, uploads included, and a summary with a submit button) drawn with
 * the same AEGOV fields as the form. Everything saves through the same E-Services API, with the user's permissions.
 */
export function ChatView({ app, onApplication }: {
  app: Application;
  onApplication: (partial: ChatResponse['application']) => void;
}) {
  const storageKey = `dsp.agui.${app.id}`;
  const [state, setState] = useState<ChatState>(() => loadState(storageKey));
  const stateRef = useRef(state);
  stateRef.current = state;
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => { try { sessionStorage.setItem(storageKey, JSON.stringify(state)); } catch { /* not kept */ } }, [state, storageKey]);
  useEffect(() => { if (state.entries.length === 0) void run(null, null); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  /** One AG-UI run: a typed message, a screen action, or (first time) the greeting. */
  async function run(text: string | null, action: Action | null, userMessage?: string) {
    const said = text ?? userMessage;
    let entries = stateRef.current.entries;
    if (said) entries = [...entries, { kind: 'text', id: uid(), role: 'user', content: said }];
    setState(s => ({ ...s, entries }));
    setInput('');
    setBusy(true);
    setError(undefined);

    const messages = entries.filter((e): e is Extract<Entry, { kind: 'text' }> => e.kind === 'text')
      .map(e => ({ id: e.id, role: e.role, content: e.content }));
    try {
      await api.stream('/api/v1/ai/agui', {
        threadId: app.id, runId: uid(), messages, tools: [], context: [], state: {},
        forwardedProps: { applicationId: app.id, pendingField: stateRef.current.pendingField, action },
      }, event => handle(event));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function handle(event: Record<string, unknown>) {
    switch (event.type) {
      case 'TEXT_MESSAGE_START':
        setState(s => ({ ...s, entries: [...s.entries, { kind: 'text', id: String(event.messageId), role: 'assistant', content: '' }] }));
        break;
      case 'TEXT_MESSAGE_CONTENT':
        setState(s => ({
          ...s,
          entries: s.entries.map(e => (e.kind === 'text' && e.id === event.messageId ? { ...e, content: e.content + String(event.delta ?? '') } : e)),
        }));
        break;
      case 'STATE_SNAPSHOT': {
        const snapshot = event.snapshot as { application?: ChatResponse['application']; pendingField?: string | null; mode?: string };
        setState(s => ({ ...s, pendingField: snapshot.pendingField ?? null, mode: snapshot.mode ?? s.mode }));
        if (snapshot.application) onApplication(snapshot.application);
        break;
      }
      case 'ACTIVITY_SNAPSHOT': {
        if (event.activityType !== 'a2ui') break;
        const messages = ((event.content as { messages?: A2uiMessage[] })?.messages) ?? [];
        const created = messages.flatMap(m => ('createSurface' in m ? [m.createSurface.surfaceId] : []));
        setState(s => ({
          ...s,
          surfaces: applyA2ui(s.surfaces, messages),
          entries: [...s.entries, ...created.map(surfaceId => ({ kind: 'screen' as const, id: uid(), surfaceId }))],
        }));
        break;
      }
      case 'RUN_ERROR':
        setError(String(event.message ?? t('chat.error')));
        break;
    }
  }

  const lastScreen = [...state.entries].reverse().find(e => e.kind === 'screen')?.id;
  const items: ThreadItem[] = state.entries.map(e => {
    if (e.kind === 'text') return { key: e.id, role: e.role, content: e.content };
    const surface = state.surfaces[e.surfaceId];
    return {
      key: e.id, role: 'assistant', screen: true,
      content: surface && (
        <A2uiSurface surface={surface} disabled={busy || e.id !== lastScreen} upload={file => uploadDocument(api, file)}
          onChange={data => setState(s => ({ ...s, surfaces: { ...s.surfaces, [e.surfaceId]: { ...s.surfaces[e.surfaceId], data } } }))}
          onAction={(name, context, userMessage) => {
            if (name === 'open_form') {
              setState(s => ({ ...s, entries: [...s.entries, { kind: 'text', id: uid(), role: 'assistant', content: t('chat.a2ui.useFormTab') }] }));
              return;
            }
            void run(null, { name, context }, userMessage);
          }} />
      ),
    };
  });

  return (
    <Panel title={state.mode === 'llm' ? t('chat.ai') : t('chat.guided')}>
      <p className="-mt-2 mb-4 text-sm text-aeblack-600">{t('chat.saved')}</p>
      {error && <Notice kind="error">{error}</Notice>}
      <ChatThread items={items} typing={busy ? t('chat.typing') : undefined} footer={(
        <form className="flex items-end gap-3" onSubmit={e => { e.preventDefault(); if (input.trim() && !busy) void run(input.trim(), null); }}>
          <div className="flex-1">
            <TextField id="chat-message" label={t('chat.message')} placeholder={t('chat.placeholder')} value={input}
              onChange={setInput} disabled={busy} />
          </div>
          <div className="mb-5">
            <AegovButton type="submit" isDisabled={busy || !input.trim()}><IconLabel icon={PaperPlaneRight} mirror>{t('chat.send')}</IconLabel></AegovButton>
          </div>
        </form>
      )} />
    </Panel>
  );
}
