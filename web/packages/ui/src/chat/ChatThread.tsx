import { useEffect, useRef, type ReactNode } from 'react';
import type { ChatMessage } from '@dsp/core';

// The design system has no chat component. Messages are styled with AEGOV tokens; the input and
// send button around it are AEGOV components supplied by the page.

/** A conversation, newest at the bottom, announced to screen readers as it grows. */
/** One entry in a conversation: a text bubble, or a screen from the agent (rendered full width). */
export interface ThreadItem { key: string; role: ChatMessage['role']; content: ReactNode; screen?: boolean }

export function ChatThread({ messages = [], items, typing, footer }: {
  messages?: ChatMessage[];
  /** Richer entries (A2UI screens); used instead of messages when given. */
  items?: ThreadItem[];
  typing?: string;
  footer: ReactNode;
}) {
  const log = useRef<HTMLDivElement>(null);
  useEffect(() => { log.current?.scrollTo({ top: log.current.scrollHeight, behavior: 'smooth' }); }, [messages, items, typing]);

  return (
    <div className="flex h-[36rem] flex-col">
      <div ref={log} className="flex flex-1 flex-col gap-3 overflow-y-auto pb-3" aria-live="polite">
        {items
          ? items.map(i => (i.screen ? <div key={i.key} className="w-full max-w-xl self-start">{i.content}</div> : <Bubble key={i.key} role={i.role}>{i.content}</Bubble>))
          : messages.map((m, i) => <Bubble key={i} role={m.role}>{m.content}</Bubble>)}
        {typing && <Bubble role="assistant"><span className="italic text-aeblack-500">{typing}</span></Bubble>}
      </div>
      <div className="border-t border-aeblack-100 pt-3">{footer}</div>
    </div>
  );
}

function Bubble({ role, children }: { role: ChatMessage['role']; children: ReactNode }) {
  const mine = role === 'user';
  return (
    <div dir="auto" className={`max-w-[80%] whitespace-pre-wrap rounded-2xl px-4 py-3 ${mine
      ? 'self-end rounded-ee-sm bg-primary-600 text-whitely-50'
      : 'self-start rounded-es-sm border border-primary-100 bg-primary-50 text-aeblack-900'}`}>
      {children}
    </div>
  );
}
