import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  Alert, ApiError, DynamicForm, FIELD_TYPES, formatDate, loc, PROFILE_CLAIMS, Spinner, Stepper, t,
  type BuilderService, type FieldDefinition, type FieldError, type FieldOption, type FieldType, type FormData,
  type FormStep, type ServiceDefinition, type TKey,
} from '@dsp/shared';
import { api } from '../../session';

type Tab = 'details' | 'form' | 'review' | 'preview' | 'versions';

const base = '/api/eservices/builder';

/** A starting point for a new service: one step, one field, ready to edit. */
function blankService(code: string, name: string, nameAr: string): ServiceDefinition {
  return {
    code, name, nameAr, description: '', descriptionAr: '', category: 'General', categoryAr: 'عام',
    fee: 100, currency: 'AED', referenceField: null, workflowVersion: 1, reviewChecklist: [],
    steps: [{
      id: 'applicant', title: 'Applicant details', titleAr: 'بيانات مقدم الطلب', fields: [
        { key: 'fullName', label: 'Full name', labelAr: 'الاسم الكامل', type: 'text', required: true, maxLength: 150, prefillFrom: 'name' },
      ],
    }],
  };
}

/** New service: choose its code and name, then edit it as a draft. */
export function NewServicePage() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [error, setError] = useState<string>();

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    setErrors([]);
    try {
      const created = await api.post<BuilderService>(`${base}/services`, blankService(code.trim(), name.trim(), nameAr.trim()));
      navigate(`/services/${created.code}`, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.errors.length) setErrors(err.errors);
      else setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const codeError = errors.find(x => x.field === 'code')?.message;
  return (
    <div style={{ maxWidth: 560 }}>
      <Link to="/services" className="muted">{t('builder.backToList')}</Link>
      <form className="card" style={{ marginTop: 12 }} onSubmit={create}>
        <h1>{t('builder.createTitle')}</h1>
        {error && <Alert kind="error">{error}</Alert>}
        <div className={`field ${codeError ? 'has-error' : ''}`}>
          <label htmlFor="code">{t('builder.code')}</label>
          <input id="code" type="text" dir="ltr" value={code} required placeholder="BUSINESS_PERMIT"
            onChange={e => setCode(e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, '_'))} />
          {codeError ? <div className="error-text">{codeError}</div> : <div className="help-text">{t('builder.codeHelp')}</div>}
        </div>
        <div className="row">
          <div className="field">
            <label htmlFor="name">{t('builder.nameEn')}</label>
            <input id="name" type="text" dir="ltr" value={name} required onChange={e => setName(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="nameAr">{t('builder.nameAr')}</label>
            <input id="nameAr" type="text" dir="rtl" value={nameAr} onChange={e => setNameAr(e.target.value)} />
          </div>
        </div>
        <button className="btn btn-primary" disabled={busy || !code || !name}>{busy ? t('builder.creating') : t('builder.create')}</button>
      </form>
    </div>
  );
}

/** Edit a service as data, preview it, and publish it as the next version. */
export function ServiceEditorPage() {
  const { code } = useParams();
  const [server, setServer] = useState<BuilderService>();
  const [def, setDef] = useState<ServiceDefinition>();
  const [dirty, setDirty] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [tab, setTab] = useState<Tab>('details');
  const [busy, setBusy] = useState<'save' | 'publish' | 'discard'>();
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string }>();
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const navigate = useNavigate();

  function adopt(s: BuilderService) {
    setServer(s);
    setDef(structuredClone(s.draft ?? s.published!));
    setErrors(s.draft ? s.draftErrors : []);
    setDirty(false);
  }

  useEffect(() => {
    api.get<BuilderService>(`${base}/services/${code}`).then(adopt)
      .catch(e => setMessage({ kind: 'error', text: (e as Error).message }));
  }, [code]);

  // Live feedback while editing: the same rules that decide whether it can be published.
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => {
    if (!def || !dirty) return;
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      api.post<{ errors: FieldError[] }>(`${base}/validate`, def).then(r => setErrors(r.errors)).catch(() => {});
    }, 500);
    return () => window.clearTimeout(timer.current);
  }, [def, dirty]);

  function change(updater: (d: ServiceDefinition) => void) {
    setDef(prev => {
      const next = structuredClone(prev!);
      updater(next);
      return next;
    });
    setDirty(true);
    setMessage(undefined);
    setConfirmDiscard(false);
  }

  async function save() {
    setBusy('save');
    try {
      adopt(await api.put<BuilderService>(`${base}/services/${code}/draft`, def));
      setMessage({ kind: 'success', text: t('builder.saved') });
    } catch (e) {
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  async function publish() {
    setBusy('publish');
    try {
      if (dirty) await api.put<BuilderService>(`${base}/services/${code}/draft`, def);
      const result = await api.post<BuilderService>(`${base}/services/${code}/publish`);
      adopt(result);
      setMessage({ kind: 'success', text: t('builder.published', { n: result.published?.version ?? '' }) });
    } catch (e) {
      if (e instanceof ApiError && e.errors.length) setErrors(e.errors);
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  async function discard() {
    if (!confirmDiscard) { setConfirmDiscard(true); return; }
    setBusy('discard');
    try {
      const remaining = await api.del<BuilderService | undefined>(`${base}/services/${code}/draft`);
      if (!remaining) { navigate('/services', { replace: true }); return; }
      adopt(remaining);
      setMessage({ kind: 'success', text: t('builder.discarded') });
    } catch (e) {
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
      setConfirmDiscard(false);
    }
  }

  const err = useMemo(() => {
    const map = new Map(errors.map(e => [e.field, e.message]));
    return (path: string) => map.get(path);
  }, [errors]);

  if (!server || !def) return message ? <Alert kind="error">{message.text}</Alert> : <Spinner />;

  const liveVersion = server.published?.version;
  const draftVersion = server.draft?.version ?? Math.max(0, ...server.versions.map(v => v.version)) + 1;
  const hasDraft = !!server.draft;
  const stepErrors = errors.filter(e => e.field.startsWith('steps')).length;
  const detailErrors = errors.filter(e => !e.field.startsWith('steps') && !e.field.startsWith('reviewChecklist')).length;
  const reviewErrors = errors.filter(e => e.field.startsWith('reviewChecklist')).length;

  return (
    <>
      <Link to="/services" className="muted">{t('builder.backToList')}</Link>
      <div className="page-head" style={{ marginTop: 8 }}>
        <div>
          <div className="muted"><bdi>{def.code}</bdi></div>
          <h1>{loc(def.name, def.nameAr) || def.code}</h1>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {liveVersion ? <span className="pill pill-live">{t('builder.live', { n: liveVersion })}</span> : <span className="pill">{t('builder.notLive')}</span>}
            {hasDraft && <span className="pill pill-draft">{t('builder.draft', { n: draftVersion })}</span>}
            {def.workflowVersion === 2 && <span className="pill pill-ai">✨ {t('process.v2')}</span>}
          </div>
        </div>
      </div>

      <div className="card builder-bar">
        <div className="state">
          {dirty && <strong style={{ color: 'var(--gold-700)' }}>● {t('builder.unsaved')} · </strong>}
          {hasDraft || dirty
            ? t('builder.editingDraft', { n: draftVersion, live: liveVersion ? `v${liveVersion}` : t('builder.liveNone') })
            : t('builder.editingLive', { n: draftVersion, live: liveVersion ?? '' })}
          <div style={{ marginTop: 4 }}>
            {errors.length > 0
              ? <span className="error-text">⚠ {t('builder.errors', { n: errors.length })}</span>
              : (hasDraft || dirty) && <span className="ok-text">✓ {t('builder.ready')}</span>}
          </div>
        </div>
        <div className="buttons">
          {hasDraft && (
            <button className="btn btn-ghost" disabled={!!busy} onClick={discard}>
              {confirmDiscard ? t('builder.discardConfirm') : t('builder.discard')}
            </button>
          )}
          <button className="btn btn-secondary" disabled={!!busy || !dirty} onClick={save}>
            {busy === 'save' ? t('builder.saving') : t('builder.save')}
          </button>
          <button className="btn btn-primary" disabled={!!busy || errors.length > 0 || (!hasDraft && !dirty)} onClick={publish}>
            {busy === 'publish' ? t('builder.publishing') : t('builder.publish', { n: draftVersion })}
          </button>
        </div>
      </div>
      {message && <Alert kind={message.kind}>{message.text}</Alert>}

      <div className="tabs" role="tablist">
        {([
          ['details', 'builder.tab.details', detailErrors],
          ['form', 'builder.tab.form', stepErrors],
          ['review', 'builder.tab.review', reviewErrors],
          ['preview', 'builder.tab.preview', 0],
          ['versions', 'builder.tab.versions', 0],
        ] as [Tab, TKey, number][]).map(([id, label, count]) => (
          <button key={id} role="tab" aria-selected={tab === id} className={tab === id ? 'on' : ''} onClick={() => setTab(id)}>
            {t(label)}{count > 0 && <span className="count">{count}</span>}
          </button>
        ))}
      </div>

      {tab === 'details' && <DetailsTab def={def} change={change} err={err} />}
      {tab === 'form' && <FormTab def={def} change={change} err={err} />}
      {tab === 'review' && <ReviewTab def={def} change={change} err={err} />}
      {tab === 'preview' && <PreviewTab def={def} />}
      {tab === 'versions' && <VersionsTab server={server} />}
    </>
  );
}

type Change = (updater: (d: ServiceDefinition) => void) => void;
type Err = (path: string) => string | undefined;

// ---- Inputs ----

function TextInput({ id, label, value, onChange, error, help, dir, multiline, compact }: {
  id: string; label: string; value?: string | null; onChange: (v: string) => void; error?: string; help?: string;
  dir?: 'ltr' | 'rtl'; multiline?: boolean; compact?: boolean;
}) {
  return (
    <div className={`field ${compact ? 'compact' : ''} ${error ? 'has-error' : ''}`}>
      <label htmlFor={id}>{label}</label>
      {multiline
        ? <textarea id={id} rows={3} dir={dir} value={value ?? ''} onChange={e => onChange(e.target.value)} />
        : <input id={id} type="text" dir={dir} value={value ?? ''} onChange={e => onChange(e.target.value)} />}
      {error ? <div className="error-text">{error}</div> : help && <div className="help-text">{help}</div>}
    </div>
  );
}

function NumberInput({ id, label, value, onChange, error, compact }: {
  id: string; label: string; value?: number | null; onChange: (v: number | null) => void; error?: string; compact?: boolean;
}) {
  return (
    <div className={`field ${compact ? 'compact' : ''} ${error ? 'has-error' : ''}`}>
      <label htmlFor={id}>{label}</label>
      <input id={id} type="number" dir="ltr" value={value ?? ''}
        onChange={e => onChange(e.target.value === '' ? null : Number(e.target.value))} />
      {error && <div className="error-text">{error}</div>}
    </div>
  );
}

const nullIfEmpty = (v: string) => (v.trim() === '' ? null : v);

// ---- Tabs ----

function DetailsTab({ def, change, err }: { def: ServiceDefinition; change: Change; err: Err }) {
  const textFields = def.steps.flatMap(s => s.fields).filter(f => f.type === 'text' || f.type === 'email' || f.type === 'phone');
  return (
    <div className="card">
      <div className="row">
        <TextInput id="name" label={t('builder.nameEn')} dir="ltr" value={def.name} error={err('name')}
          onChange={v => change(d => { d.name = v; })} />
        <TextInput id="nameAr" label={t('builder.nameAr')} dir="rtl" value={def.nameAr} error={err('nameAr')}
          onChange={v => change(d => { d.nameAr = nullIfEmpty(v); })} />
      </div>
      <div className="row">
        <TextInput id="desc" label={t('builder.descEn')} dir="ltr" multiline value={def.description} error={err('description')}
          onChange={v => change(d => { d.description = v; })} />
        <TextInput id="descAr" label={t('builder.descAr')} dir="rtl" multiline value={def.descriptionAr} error={err('descriptionAr')}
          onChange={v => change(d => { d.descriptionAr = nullIfEmpty(v); })} />
      </div>
      <div className="row">
        <TextInput id="cat" label={t('builder.categoryEn')} dir="ltr" value={def.category}
          onChange={v => change(d => { d.category = v; })} />
        <TextInput id="catAr" label={t('builder.categoryAr')} dir="rtl" value={def.categoryAr}
          onChange={v => change(d => { d.categoryAr = nullIfEmpty(v); })} />
      </div>
      <div className="row">
        <NumberInput id="fee" label={t('builder.fee')} value={def.fee} error={err('fee')}
          onChange={v => change(d => { d.fee = v ?? 0; })} />
        <TextInput id="currency" label={t('builder.currency')} dir="ltr" value={def.currency} error={err('currency')}
          onChange={v => change(d => { d.currency = v.toUpperCase().slice(0, 3); })} />
        <div className={`field ${err('referenceField') ? 'has-error' : ''}`}>
          <label htmlFor="ref">{t('builder.referenceField')}</label>
          <select id="ref" value={def.referenceField ?? ''} onChange={e => change(d => { d.referenceField = e.target.value || null; })}>
            <option value="">{t('builder.none')}</option>
            {textFields.map(f => <option key={f.key} value={f.key}>{loc(f.label, f.labelAr)} ({f.key})</option>)}
          </select>
          {err('referenceField') ? <div className="error-text">{err('referenceField')}</div> : <div className="help-text">{t('builder.referenceHelp')}</div>}
        </div>
      </div>

      <div className="field">
        <label>{t('builder.process')}</label>
        <div className="radio-cards">
          {[1, 2].map(v => (
            <label key={v} className={`radio-card ${def.workflowVersion === v ? 'on' : ''}`}>
              <input type="radio" name="process" checked={def.workflowVersion === v}
                onChange={() => change(d => { d.workflowVersion = v; })} />
              <span>
                <strong>{v === 2 ? `✨ ${t('process.v2')}` : t('process.v1')}</strong>
                <div className="muted" style={{ fontSize: 13 }}>{t(v === 2 ? 'builder.processV2' : 'builder.processV1')}</div>
              </span>
            </label>
          ))}
        </div>
        {err('workflowVersion') && <div className="error-text">{err('workflowVersion')}</div>}
      </div>
    </div>
  );
}

function ReviewTab({ def, change, err }: { def: ServiceDefinition; change: Change; err: Err }) {
  const listError = err('reviewChecklist') ?? (def.reviewChecklist ?? []).map((_, i) => err(`reviewChecklist[${i}]`)).find(Boolean);
  return (
    <div className="card">
      {def.workflowVersion !== 2 && <Alert kind="info">{t('builder.checklistV1')}</Alert>}
      <div className={`field ${listError ? 'has-error' : ''}`}>
        <label htmlFor="checklist">{t('builder.checklist')}</label>
        <textarea id="checklist" rows={8} dir="auto" value={(def.reviewChecklist ?? []).join('\n')}
          onChange={e => change(d => { d.reviewChecklist = e.target.value.split('\n').map(l => l.trimStart()).filter((l, i, all) => l !== '' || i === all.length - 1); })}
          onBlur={() => change(d => { d.reviewChecklist = (d.reviewChecklist ?? []).map(l => l.trim()).filter(Boolean); })} />
        {listError ? <div className="error-text">{listError}</div> : <div className="help-text">{t('builder.checklistHelp')}</div>}
      </div>
    </div>
  );
}

function FormTab({ def, change, err }: { def: ServiceDefinition; change: Change; err: Err }) {
  function moveStep(i: number, by: number) {
    change(d => { const [s] = d.steps.splice(i, 1); d.steps.splice(i + by, 0, s); });
  }
  function addStep() {
    change(d => {
      const n = d.steps.length + 1;
      d.steps.push({
        id: `step-${n}`, title: `Step ${n}`, titleAr: `الخطوة ${n}`,
        fields: [{ key: `field${Date.now() % 100000}`, label: 'New field', labelAr: 'حقل جديد', type: 'text', required: false }],
      });
    });
  }

  return (
    <>
      {err('steps') && <Alert kind="error">{err('steps')}</Alert>}
      {def.steps.map((step, si) => (
        <StepEditor key={si} step={step} index={si} count={def.steps.length} err={err}
          update={fn => change(d => fn(d.steps[si]))}
          move={by => moveStep(si, by)}
          remove={() => change(d => { d.steps.splice(si, 1); })} />
      ))}
      <button className="btn btn-secondary" onClick={addStep}>+ {t('builder.addStep')}</button>
    </>
  );
}

function StepEditor({ step, index, count, update, move, remove, err }: {
  step: FormStep; index: number; count: number; err: Err;
  update: (fn: (s: FormStep) => void) => void; move: (by: number) => void; remove: () => void;
}) {
  const at = `steps[${index}]`;
  function addField() {
    update(s => { s.fields.push({ key: `field${Date.now() % 100000}`, label: 'New field', labelAr: 'حقل جديد', type: 'text', required: false }); });
  }
  function moveField(i: number, by: number) {
    update(s => { const [f] = s.fields.splice(i, 1); s.fields.splice(i + by, 0, f); });
  }

  return (
    <div className="card step-card">
      <div className="step-head">
        <h2 style={{ margin: 0 }}>{t('builder.step', { n: index + 1 })} · {loc(step.title, step.titleAr)}</h2>
        <div className="icon-buttons">
          <button className="icon-btn" disabled={index === 0} onClick={() => move(-1)} aria-label={t('builder.moveUp')}>↑</button>
          <button className="icon-btn" disabled={index === count - 1} onClick={() => move(1)} aria-label={t('builder.moveDown')}>↓</button>
          <button className="icon-btn danger" disabled={count === 1} onClick={remove}>{t('builder.remove')}</button>
        </div>
      </div>
      <div className="row-3">
        <TextInput compact id={`${at}-id`} label={t('builder.stepId')} dir="ltr" value={step.id} error={err(`${at}.id`)}
          onChange={v => update(s => { s.id = v.toLowerCase().replace(/[^a-z0-9-]/g, '-'); })} />
        <TextInput compact id={`${at}-title`} label={t('builder.titleEn')} dir="ltr" value={step.title} error={err(`${at}.title`)}
          onChange={v => update(s => { s.title = v; })} />
        <TextInput compact id={`${at}-titleAr`} label={t('builder.titleAr')} dir="rtl" value={step.titleAr} error={err(`${at}.titleAr`)}
          onChange={v => update(s => { s.titleAr = nullIfEmpty(v); })} />
      </div>
      <div className="row">
        <TextInput compact id={`${at}-desc`} label={t('builder.stepDescEn')} dir="ltr" value={step.description}
          onChange={v => update(s => { s.description = nullIfEmpty(v); })} />
        <TextInput compact id={`${at}-descAr`} label={t('builder.stepDescAr')} dir="rtl" value={step.descriptionAr}
          onChange={v => update(s => { s.descriptionAr = nullIfEmpty(v); })} />
      </div>
      {err(`${at}.fields`) && <Alert kind="error">{err(`${at}.fields`)}</Alert>}
      {step.fields.map((field, fi) => (
        <FieldEditor key={fi} field={field} at={`${at}.fields[${fi}]`} err={err}
          first={fi === 0} last={fi === step.fields.length - 1}
          update={fn => update(s => fn(s.fields[fi]))}
          move={by => moveField(fi, by)}
          remove={() => update(s => { s.fields.splice(fi, 1); })} />
      ))}
      <button className="btn btn-ghost btn-sm" onClick={addField}>+ {t('builder.addField')}</button>
    </div>
  );
}

function FieldEditor({ field, at, err, first, last, update, move, remove }: {
  field: FieldDefinition; at: string; err: Err; first: boolean; last: boolean;
  update: (fn: (f: FieldDefinition) => void) => void; move: (by: number) => void; remove: () => void;
}) {
  const fieldErrors = ['key', 'label', 'labelAr', 'type', 'options', 'minLength', 'maxLength', 'max', 'pattern', 'prefillFrom']
    .map(k => err(`${at}.${k}`)).filter(Boolean);
  const optionsHaveErrors = (field.options ?? []).some((_, i) =>
    err(`${at}.options[${i}].value`) || err(`${at}.options[${i}].label`) || err(`${at}.options[${i}].labelAr`));
  const [open, setOpen] = useState(false);
  const isText = field.type === 'text' || field.type === 'textarea' || field.type === 'phone' || field.type === 'email';

  return (
    <div className={`field-card ${fieldErrors.length || optionsHaveErrors ? 'has-error' : ''}`}>
      <div className="field-head">
        <strong>{loc(field.label, field.labelAr)} <span className="muted" style={{ fontWeight: 400 }}>· {t(`builder.type.${field.type}` as TKey)}{field.required ? ' · *' : ''}</span></strong>
        <div className="icon-buttons">
          <button className="icon-btn" disabled={first} onClick={() => move(-1)} aria-label={t('builder.moveUp')}>↑</button>
          <button className="icon-btn" disabled={last} onClick={() => move(1)} aria-label={t('builder.moveDown')}>↓</button>
          <button className="icon-btn danger" onClick={remove}>{t('builder.remove')}</button>
        </div>
      </div>
      <div className="row-3">
        <TextInput compact id={`${at}-key`} label={t('builder.fieldKey')} dir="ltr" value={field.key} error={err(`${at}.key`)}
          onChange={v => update(f => { f.key = v.replace(/[^A-Za-z0-9]/g, ''); })} />
        <TextInput compact id={`${at}-label`} label={t('builder.labelEn')} dir="ltr" value={field.label} error={err(`${at}.label`)}
          onChange={v => update(f => { f.label = v; })} />
        <TextInput compact id={`${at}-labelAr`} label={t('builder.labelAr')} dir="rtl" value={field.labelAr} error={err(`${at}.labelAr`)}
          onChange={v => update(f => { f.labelAr = nullIfEmpty(v); })} />
        <div className="field compact">
          <label htmlFor={`${at}-type`}>{t('builder.type')}</label>
          <select id={`${at}-type`} value={field.type} onChange={e => update(f => {
            f.type = e.target.value as FieldType;
            if (f.type === 'select' && !f.options?.length) f.options = [{ value: 'option1', label: 'Option 1', labelAr: 'الخيار 1' }];
          })}>
            {FIELD_TYPES.map(type => <option key={type} value={type}>{t(`builder.type.${type}` as TKey)}</option>)}
          </select>
        </div>
        <div className="field compact" style={{ justifyContent: 'flex-end' }}>
          <label className="check-inline">
            <input type="checkbox" checked={field.required} onChange={e => update(f => { f.required = e.target.checked; })} />
            {t('builder.required')}
          </label>
        </div>
      </div>

      {field.type === 'select' && (
        <OptionsEditor options={field.options ?? []} at={at} err={err}
          update={fn => update(f => { f.options = f.options ?? []; fn(f.options); })} />
      )}

      <button className="link" style={{ fontSize: 13 }} onClick={() => setOpen(!open)}>{open ? t('builder.less') : t('builder.more')}</button>
      {open && (
        <div style={{ marginTop: 10 }}>
          <div className="row">
            <TextInput compact id={`${at}-help`} label={t('builder.helpEn')} dir="ltr" value={field.help}
              onChange={v => update(f => { f.help = nullIfEmpty(v); })} />
            <TextInput compact id={`${at}-helpAr`} label={t('builder.helpAr')} dir="rtl" value={field.helpAr}
              onChange={v => update(f => { f.helpAr = nullIfEmpty(v); })} />
          </div>
          {isText && (
            <>
              <div className="row">
                <TextInput compact id={`${at}-ph`} label={t('builder.placeholderEn')} dir="ltr" value={field.placeholder}
                  onChange={v => update(f => { f.placeholder = nullIfEmpty(v); })} />
                <TextInput compact id={`${at}-phAr`} label={t('builder.placeholderAr')} dir="rtl" value={field.placeholderAr}
                  onChange={v => update(f => { f.placeholderAr = nullIfEmpty(v); })} />
              </div>
              <div className="row-3">
                <NumberInput compact id={`${at}-minl`} label={t('builder.minLength')} value={field.minLength} error={err(`${at}.minLength`)}
                  onChange={v => update(f => { f.minLength = v; })} />
                <NumberInput compact id={`${at}-maxl`} label={t('builder.maxLength')} value={field.maxLength} error={err(`${at}.maxLength`)}
                  onChange={v => update(f => { f.maxLength = v; })} />
                <div className="field compact">
                  <label htmlFor={`${at}-prefill`}>{t('builder.prefill')}</label>
                  <select id={`${at}-prefill`} value={field.prefillFrom ?? ''} onChange={e => update(f => { f.prefillFrom = e.target.value || null; })}>
                    <option value="">{t('builder.none')}</option>
                    {PROFILE_CLAIMS.map(c => <option key={c} value={c}>{t(`builder.prefill.${c}` as TKey)}</option>)}
                  </select>
                  {err(`${at}.prefillFrom`) && <div className="error-text">{err(`${at}.prefillFrom`)}</div>}
                </div>
              </div>
              <TextInput compact id={`${at}-pattern`} label={t('builder.pattern')} dir="ltr" value={field.pattern} error={err(`${at}.pattern`)}
                onChange={v => update(f => { f.pattern = nullIfEmpty(v); })} />
              <div className="row">
                <TextInput compact id={`${at}-pm`} label={t('builder.patternMsgEn')} dir="ltr" value={field.patternMessage}
                  onChange={v => update(f => { f.patternMessage = nullIfEmpty(v); })} />
                <TextInput compact id={`${at}-pmAr`} label={t('builder.patternMsgAr')} dir="rtl" value={field.patternMessageAr}
                  onChange={v => update(f => { f.patternMessageAr = nullIfEmpty(v); })} />
              </div>
            </>
          )}
          {field.type === 'number' && (
            <div className="row-3">
              <NumberInput compact id={`${at}-min`} label={t('builder.min')} value={field.min} onChange={v => update(f => { f.min = v; })} />
              <NumberInput compact id={`${at}-max`} label={t('builder.max')} value={field.max} error={err(`${at}.max`)}
                onChange={v => update(f => { f.max = v; })} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function OptionsEditor({ options, at, err, update }: {
  options: FieldOption[]; at: string; err: Err; update: (fn: (o: FieldOption[]) => void) => void;
}) {
  return (
    <div className="field compact">
      <label>{t('builder.options')}</label>
      {err(`${at}.options`) && <div className="error-text">{err(`${at}.options`)}</div>}
      {options.map((o, i) => {
        const e = err(`${at}.options[${i}].value`) ?? err(`${at}.options[${i}].label`) ?? err(`${at}.options[${i}].labelAr`);
        return (
          <div key={i}>
            <div className="option-row">
              <input dir="ltr" aria-label={t('builder.optionValue')} placeholder={t('builder.optionValue')} value={o.value}
                onChange={ev => update(list => { list[i].value = ev.target.value; })} />
              <input dir="ltr" aria-label={t('builder.labelEn')} placeholder={t('builder.labelEn')} value={o.label}
                onChange={ev => update(list => { list[i].label = ev.target.value; })} />
              <input dir="rtl" aria-label={t('builder.labelAr')} placeholder={t('builder.labelAr')} value={o.labelAr ?? ''}
                onChange={ev => update(list => { list[i].labelAr = ev.target.value || null; })} />
              <button className="icon-btn danger" onClick={() => update(list => { list.splice(i, 1); })} aria-label={t('builder.remove')}>✕</button>
            </div>
            {e && <div className="error-text" style={{ marginBottom: 6 }}>{e}</div>}
          </div>
        );
      })}
      <div>
        <button className="btn btn-ghost btn-sm" onClick={() => update(list => {
          const n = list.length + 1;
          list.push({ value: `option${n}`, label: `Option ${n}`, labelAr: `الخيار ${n}` });
        })}>+ {t('builder.addOption')}</button>
      </div>
    </div>
  );
}

/** The form exactly as citizens will see it, in the current language. Nothing is saved. */
function PreviewTab({ def }: { def: ServiceDefinition }) {
  const [current, setCurrent] = useState(0);
  const [values, setValues] = useState<FormData>({});
  const step = def.steps[Math.min(current, def.steps.length - 1)];
  return (
    <>
      <Alert kind="info">{t('builder.previewIntro')}</Alert>
      <div className="preview-frame">
        <h2>{loc(def.name, def.nameAr)}</h2>
        <p className="muted">{loc(def.description, def.descriptionAr)}</p>
        <Stepper steps={def.steps} current={current} onSelect={setCurrent} />
        {step && (
          <>
            <h3>{loc(step.title, step.titleAr)}</h3>
            <DynamicForm step={step} values={values} onChange={(k, v) => setValues(p => ({ ...p, [k]: v }))}
              onUpload={async () => 'preview'} />
          </>
        )}
        <div className="actions">
          {current > 0 && <button className="btn btn-ghost" onClick={() => setCurrent(current - 1)}>{t('back')}</button>}
          {current < def.steps.length - 1 && <button className="btn btn-primary" onClick={() => setCurrent(current + 1)}>{t('form.saveContinue')}</button>}
        </div>
      </div>
    </>
  );
}

function VersionsTab({ server }: { server: BuilderService }) {
  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr><th>{t('builder.version')}</th><th>{t('status')}</th><th>{t('builder.created')}</th><th>{t('builder.publishedAt')}</th><th>{t('builder.lastChange')}</th></tr>
        </thead>
        <tbody>
          {server.versions.map(v => (
            <tr key={v.version}>
              <td><strong>v{v.version}</strong></td>
              <td><span className={`pill ${v.status === 'Published' ? 'pill-live' : v.status === 'Draft' ? 'pill-draft' : ''}`}>{t(`builder.vstatus.${v.status}` as TKey)}</span></td>
              <td>{formatDate(v.createdAt, true)}<div className="muted" style={{ fontSize: 12 }}>{t('builder.by', { name: v.createdBy })}</div></td>
              <td>{v.publishedAt ? <>{formatDate(v.publishedAt, true)}<div className="muted" style={{ fontSize: 12 }}>{t('builder.by', { name: v.publishedBy ?? '' })}</div></> : '—'}</td>
              <td>{formatDate(v.updatedAt, true)}<div className="muted" style={{ fontSize: 12 }}>{t('builder.by', { name: v.updatedBy })}</div></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
