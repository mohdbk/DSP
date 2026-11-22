import { useState, type ReactNode } from 'react';
import { AegovButton } from '@aegov/designsystem-react';
import { A2UI_CATALOG, readPath, resolve, t, writePath, type A2uiComponent, type A2uiSurfaceState } from '@dsp/core';
import { DescriptionList } from '../data/DescriptionList';
import { CheckboxField, ChoiceField, DateField, FileField, TextAreaField, TextField } from '../forms/fields';

type Upload = (file: File) => Promise<{ id: string; fileName: string }>;

/**
 * Renders an A2UI surface from the agent with our AEGOV catalogue (the same fields as the form view). Only the
 * catalogue's components are drawn and every property is treated as data, so an agent can't inject markup; unknown
 * components are skipped. Inputs change the surface's own data model; a button sends its action with the resolved
 * context. A surface from another catalogue is not shown.
 */
export function A2uiSurface({ surface, disabled, onChange, onAction, upload }: {
  surface: A2uiSurfaceState;
  /** Earlier screens stay visible but can't be used again. */
  disabled?: boolean;
  onChange: (data: Record<string, unknown>) => void;
  onAction: (name: string, context: Record<string, unknown>, userMessage?: string) => void;
  upload?: Upload;
}) {
  const [fileNames, setFileNames] = useState<Record<string, string>>({});
  if (surface.catalogId !== A2UI_CATALOG) return null;
  const { components, data } = surface;
  const fieldId = (id: string) => `${surface.surfaceId}-${id}`;
  const str = (v: unknown) => (v == null ? '' : String(v));

  function bound(c: A2uiComponent): string | undefined {
    const v = c.value as { '@path'?: string } | undefined;
    return v && typeof v['@path'] === 'string' ? v['@path'] : undefined;
  }
  const set = (c: A2uiComponent, value: unknown) => { const path = bound(c); if (path) onChange(writePath(data, path, value)); };
  const get = (c: A2uiComponent) => { const path = bound(c); return path ? readPath(data, path) : resolve(c.value, data); };

  function render(id: string, depth = 0): ReactNode {
    const c = components[id];
    if (!c || depth > 20) return null;
    const label = str(resolve(c.label, data));
    const base = {
      id: fieldId(id), label, hint: c.hint ? str(resolve(c.hint, data)) : undefined, error: c.error ? str(c.error) : undefined,
      required: !!c.required, disabled,
    };
    switch (c.component) {
      case 'Column':
        return <div key={id} className="flex flex-col gap-1">{(c.children as string[] | undefined ?? []).map(child => render(child, depth + 1))}</div>;
      case 'Text': {
        const text = str(resolve(c.text, data));
        if (c.variant === 'heading') return <p key={id} className="mb-2 text-lg font-bold text-aeblack-900">{text}</p>;
        if (c.variant === 'hint') return <p key={id} className="mb-3 text-sm text-aeblack-600">{text}</p>;
        return <p key={id} className="mb-3">{text}</p>;
      }
      case 'TextField':
        return <TextField key={id} {...base} type={(['email', 'number', 'tel'] as const).find(x => x === c.inputType) ?? 'text'}
          value={str(get(c))} onChange={v => set(c, v)} />;
      case 'TextArea':
        return <TextAreaField key={id} {...base} value={str(get(c))} onChange={v => set(c, v)} />;
      case 'DateField':
        return <DateField key={id} {...base} value={str(get(c))} onChange={v => set(c, v)} />;
      case 'ChoiceField': {
        const options = (c.options as { value: string; label: string }[] | undefined) ?? [];
        return <ChoiceField key={id} {...base} value={str(get(c))} choices={options.map(o => ({ value: str(o.value), label: str(o.label) }))}
          onChange={v => set(c, v)} />;
      }
      case 'CheckBox':
        return <CheckboxField key={id} {...base} checked={get(c) === true || get(c) === 'true'} onChange={v => set(c, v)} />;
      case 'FileUpload':
        return upload
          ? <FileField key={id} {...base} value={str(get(c))} fileName={fileNames[id]} onUpload={upload}
              onChange={(docId, name) => { setFileNames(n => ({ ...n, [id]: name })); set(c, docId); }} />
          : null;
      case 'Facts': {
        const items = (resolve(c.items, data) as { label: unknown; value: unknown }[] | undefined) ?? [];
        return <div key={id} className="mb-3"><DescriptionList items={items.map(i => ({ label: str(i.label), value: <bdi>{str(i.value)}</bdi> }))} /></div>;
      }
      case 'Button': {
        const event = (c.action as { event?: { name?: string; context?: unknown; userMessage?: unknown } } | undefined)?.event;
        const child = c.child ? components[c.child as string] : undefined;
        const text = child ? str(resolve(child.text, data)) : '';
        return (
          <div key={id} className="mt-1">
            <AegovButton appearance={c.variant === 'secondary' ? 'link' : 'solid'} size="sm" isDisabled={disabled || !event?.name}
              onAegovButtonClick={() => {
                if (!event?.name || disabled) return;
                const context = (resolve(event.context ?? {}, data) ?? {}) as Record<string, unknown>;
                const userMessage = event.userMessage == null ? undefined : str(resolve(event.userMessage, data));
                onAction(event.name, context, userMessage);
              }}>
              {text}
            </AegovButton>
          </div>
        );
      }
      default:
        return null; // not in our catalogue
    }
  }

  return (
    <div className="w-full rounded-2xl border border-aeblack-100 bg-white p-4" aria-label={t('chat.screen')}>
      {render('root')}
    </div>
  );
}
