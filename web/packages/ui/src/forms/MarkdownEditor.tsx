import { Fragment, useId, useRef, useState, type ReactNode } from 'react';
import { t } from '@dsp/core';

// A Markdown editor for long instructions (AI prompts and the like): a formatting toolbar, Write and Preview
// tabs, and a character count. AEGOV has no rich-text editor, so this is built from plain elements styled to
// match the design system. The preview is rendered as React elements (never as HTML), so it can't inject markup.

type Action = { id: string; label: string; icon: ReactNode; apply: (text: string, start: number, end: number) => Edit };
interface Edit { text: string; start: number; end: number }

/** Wraps the selection (or a placeholder word) in a marker, e.g. ** for bold. */
const wrap = (marker: string, placeholder: string) => (text: string, start: number, end: number): Edit => {
  const selected = text.slice(start, end) || placeholder;
  const next = text.slice(0, start) + marker + selected + marker + text.slice(end);
  return { text: next, start: start + marker.length, end: start + marker.length + selected.length };
};

/** Puts a prefix at the start of every selected line, e.g. "- " for a list. */
const prefixLines = (prefix: (i: number) => string) => (text: string, start: number, end: number): Edit => {
  const lineStart = text.lastIndexOf('\n', start - 1) + 1;
  const lineEnd = text.indexOf('\n', end) === -1 ? text.length : text.indexOf('\n', end);
  const lines = text.slice(lineStart, lineEnd).split('\n').map((l, i) => prefix(i) + l);
  const block = lines.join('\n');
  return { text: text.slice(0, lineStart) + block + text.slice(lineEnd), start: lineStart, end: lineStart + block.length };
};

const link = (text: string, start: number, end: number): Edit => {
  const label = text.slice(start, end) || t('md.linkText');
  const insert = `[${label}](https://)`;
  const urlStart = start + label.length + 3;
  return { text: text.slice(0, start) + insert + text.slice(end), start: urlStart, end: urlStart + 8 };
};

const svg = (d: string) => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth={2}
    strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg>
);

function actions(): Action[] {
  return [
    { id: 'heading', label: t('md.heading'), icon: <span aria-hidden="true" className="font-bold">H</span>, apply: prefixLines(() => '## ') },
    { id: 'bold', label: t('md.bold'), icon: <span aria-hidden="true" className="font-bold">B</span>, apply: wrap('**', t('md.boldText')) },
    { id: 'italic', label: t('md.italic'), icon: <span aria-hidden="true" className="italic font-serif">I</span>, apply: wrap('*', t('md.italicText')) },
    { id: 'bullets', label: t('md.bullets'), icon: svg('M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01'), apply: prefixLines(() => '- ') },
    { id: 'numbers', label: t('md.numbers'), icon: svg('M10 6h10M10 12h10M10 18h10M4 4v4M3 18h3l-3 3h3'), apply: prefixLines(i => `${i + 1}. `) },
    { id: 'quote', label: t('md.quote'), icon: svg('M7 7h4v4c0 3-2 5-4 5M15 7h4v4c0 3-2 5-4 5'), apply: prefixLines(() => '> ') },
    { id: 'code', label: t('md.code'), icon: svg('M8 8l-4 4 4 4M16 8l4 4-4 4'), apply: wrap('`', 'code') },
    { id: 'link', label: t('md.link'), icon: svg('M10 14a4 4 0 005.66 0l3-3a4 4 0 00-5.66-5.66l-1 1M14 10a4 4 0 00-5.66 0l-3 3a4 4 0 005.66 5.66l1-1'), apply: link },
  ];
}

export function MarkdownEditor(props: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  hint?: string | null;
  error?: string | null;
  maxLength?: number;
  markOptional?: boolean;
  disabled?: boolean;
  /** Direction of the text; prompts are usually written in English, so they stay left to right in Arabic pages. */
  dir?: 'ltr' | 'rtl' | 'auto';
  rows?: number;
}) {
  const [tab, setTab] = useState<'write' | 'preview'>('write');
  const area = useRef<HTMLTextAreaElement>(null);
  const uid = useId();
  const hintId = `${uid}-hint`;
  const errorId = `${uid}-error`;

  function run(action: Action) {
    const el = area.current;
    if (!el) return;
    const edit = action.apply(props.value, el.selectionStart, el.selectionEnd);
    if (props.maxLength && edit.text.length > props.maxLength) return;
    props.onChange(edit.text);
    requestAnimationFrame(() => { el.focus(); el.setSelectionRange(edit.start, edit.end); });
  }

  function onKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (!(e.ctrlKey || e.metaKey)) return;
    const shortcut = { b: 'bold', i: 'italic', k: 'link' }[e.key.toLowerCase()];
    const action = actions().find(a => a.id === shortcut);
    if (action) { e.preventDefault(); run(action); }
  }

  const label = props.markOptional ? `${props.label} ${t('form.optional')}` : props.label;
  const tabClass = (active: boolean) =>
    `px-3 py-2 text-sm font-semibold border-b-2 ${active ? 'border-primary-600 text-aeblack-900' : 'border-transparent text-aeblack-600 hover:text-aeblack-900'}`;

  return (
    <div className="mb-5">
      <label htmlFor={props.id} className="mb-2 block font-semibold text-aeblack-800">{label}</label>
      <div className={`overflow-hidden rounded-xl border-2 ${props.error ? 'border-aered-600' : 'border-aeblack-100 focus-within:border-primary-600'}`}>
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-aeblack-100 bg-whitely-50 px-2">
          <div role="tablist" aria-label={label} className="flex">
            <button type="button" role="tab" aria-selected={tab === 'write'} className={tabClass(tab === 'write')} onClick={() => setTab('write')}>{t('md.write')}</button>
            <button type="button" role="tab" aria-selected={tab === 'preview'} className={tabClass(tab === 'preview')} onClick={() => setTab('preview')}>{t('md.preview')}</button>
          </div>
          {tab === 'write' && (
            <div role="toolbar" aria-label={t('md.toolbar')} aria-controls={props.id} className="flex flex-wrap gap-1 py-1">
              {actions().map(a => (
                <button key={a.id} type="button" title={a.label} aria-label={a.label} disabled={props.disabled}
                  className="flex size-9 items-center justify-center rounded-lg text-aeblack-700 hover:bg-aeblack-50 focus-visible:outline-2 focus-visible:outline-primary-600 disabled:opacity-40"
                  onClick={() => run(a)}>
                  {a.icon}
                </button>
              ))}
            </div>
          )}
        </div>
        {tab === 'write' ? (
          <textarea ref={area} id={props.id} name={props.id} dir={props.dir ?? 'auto'} value={props.value} rows={props.rows ?? 12}
            maxLength={props.maxLength} disabled={props.disabled} spellCheck
            aria-invalid={!!props.error} aria-describedby={[props.hint ? hintId : '', props.error ? errorId : ''].filter(Boolean).join(' ') || undefined}
            onKeyDown={onKeyDown} onChange={e => props.onChange(e.target.value)}
            className="block w-full resize-y border-0 bg-white p-4 font-mono text-sm leading-6 text-aeblack-900 focus:outline-none focus:ring-0" />
        ) : (
          <div role="tabpanel" dir={props.dir ?? 'auto'} className="min-h-48 bg-white p-4 text-aeblack-900">
            {props.value.trim() ? <MarkdownView text={props.value} /> : <p className="text-aeblack-500">{t('md.empty')}</p>}
          </div>
        )}
      </div>
      <div className="mt-2 flex flex-wrap justify-between gap-2 text-sm">
        <span>
          {props.error && <span id={errorId} className="block font-semibold text-aered-600">{props.error}</span>}
          {props.hint && <span id={hintId} className="block text-aeblack-600">{props.hint}</span>}
        </span>
        {props.maxLength && <span className="text-aeblack-500" aria-live="polite">{props.value.length.toLocaleString('en')} / {props.maxLength.toLocaleString('en')}</span>}
      </div>
    </div>
  );
}

/** Renders Markdown (headings, paragraphs, lists, quotes, code, bold, italic, links) as React elements. */
export function MarkdownView({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  for (let i = 0; i < lines.length;) {
    const line = lines[i];
    if (line.trim() === '') { i++; continue; }
    if (line.startsWith('```')) {
      const code: string[] = [];
      for (i++; i < lines.length && !lines[i].startsWith('```'); i++) code.push(lines[i]);
      i++;
      blocks.push(<pre key={i} dir="ltr" className="mb-3 overflow-x-auto rounded-lg bg-aeblack-50 p-3 text-sm"><code>{code.join('\n')}</code></pre>);
      continue;
    }
    const heading = /^(#{1,4})\s+(.*)$/.exec(line);
    if (heading) {
      const size = ['text-2xl', 'text-xl', 'text-lg', 'text-base'][heading[1].length - 1];
      blocks.push(<p key={i} role="heading" aria-level={heading[1].length + 2} className={`mb-2 mt-4 font-bold first:mt-0 ${size}`}>{inline(heading[2])}</p>);
      i++;
      continue;
    }
    const listMatch = (l: string) => /^\s*([-*+]|\d+[.)])\s+(.*)$/.exec(l);
    if (listMatch(line)) {
      const ordered = /^\s*\d/.test(line);
      const items: string[] = [];
      for (; i < lines.length && listMatch(lines[i]); i++) items.push(listMatch(lines[i])![2]);
      const List = ordered ? 'ol' : 'ul';
      blocks.push(<List key={i} className={`mb-3 ps-6 ${ordered ? 'list-decimal' : 'list-disc'}`}>{items.map((it, n) => <li key={n} className="mb-1">{inline(it)}</li>)}</List>);
      continue;
    }
    if (line.startsWith('>')) {
      const quote: string[] = [];
      for (; i < lines.length && lines[i].startsWith('>'); i++) quote.push(lines[i].replace(/^>\s?/, ''));
      blocks.push(<blockquote key={i} className="mb-3 border-s-4 border-aeblack-100 ps-4 text-aeblack-700">{inline(quote.join(' '))}</blockquote>);
      continue;
    }
    const para: string[] = [];
    for (; i < lines.length && lines[i].trim() !== '' && !/^(#{1,4}\s|```|>)/.test(lines[i]) && !listMatch(lines[i]); i++) para.push(lines[i]);
    blocks.push(<p key={i} className="mb-3">{para.map((p, n) => <Fragment key={n}>{n > 0 && <br />}{inline(p)}</Fragment>)}</p>);
  }
  return <div className="text-base leading-7">{blocks}</div>;
}

/** Inline Markdown: `code`, **bold**, *italic* or _italic_, and [links](https://…) (http and https only). */
function inline(text: string): ReactNode[] {
  const out: ReactNode[] = [];
  const pattern = /(`[^`]+`)|(\*\*[^*]+\*\*)|(\*[^*\s][^*]*\*|_[^_\s][^_]*_)|(\[[^\]]+\]\([^)\s]+\))/g;
  let last = 0;
  for (const m of text.matchAll(pattern)) {
    if (m.index! > last) out.push(text.slice(last, m.index));
    const token = m[0];
    const key = m.index;
    if (m[1]) out.push(<code key={key} dir="ltr" className="rounded bg-aeblack-50 px-1 text-sm">{token.slice(1, -1)}</code>);
    else if (m[2]) out.push(<strong key={key}>{inline(token.slice(2, -2))}</strong>);
    else if (m[3]) out.push(<em key={key}>{inline(token.slice(1, -1))}</em>);
    else {
      const [, label, url] = /^\[([^\]]+)\]\(([^)\s]+)\)$/.exec(token)!;
      out.push(/^https?:\/\//i.test(url)
        ? <a key={key} href={url} target="_blank" rel="noopener noreferrer" className="underline">{label}</a>
        : label);
    }
    last = m.index! + token.length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}
