import { useState } from 'react';
import {
  Alert, ApiError, DynamicForm, loc, openBlob, Stepper, t,
  type Application, type DocumentInfo, type FieldDefinition, type FieldError, type FormData, type SaveResult, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

/** Traditional view: one step at a time, rendered from the service definition. */
export function FormView({ app, service, onChange, resubmit }: {
  app: Application;
  service: ServiceDefinition;
  onChange: (app: Application) => void;
  /** The AI pre-review asked for more information: edit and send the application back. */
  resubmit?: boolean;
}) {
  const steps = service.steps;
  const [current, setCurrent] = useState(0);
  const [values, setValues] = useState<FormData>({ ...app.data });
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();
  const [fileNames, setFileNames] = useState<Record<string, string>>({});
  const reviewing = current === steps.length;

  async function saveStep(index: number) {
    const keys = steps[index].fields.map(f => f.key);
    const payload = Object.fromEntries(keys.map(k => [k, values[k] ?? '']));
    const result = await api.put<SaveResult>(`/api/eservices/applications/${app.id}/data`, { values: payload });
    onChange(result.application);
    setErrors(result.errors);
    return result.errors.length === 0;
  }

  async function next() {
    setBusy(true);
    setMessage(undefined);
    try {
      if (await saveStep(current)) setCurrent(current + 1);
    } catch (e) {
      setMessage((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function submit() {
    setBusy(true);
    setMessage(undefined);
    try {
      const submitted = await api.post<Application>(`/api/eservices/applications/${app.id}/submit`);
      onChange(submitted);
    } catch (e) {
      if (e instanceof ApiError && e.errors.length > 0) {
        setErrors(e.errors);
        const stepWithError = steps.findIndex(s => s.fields.some(f => e.errors.some(er => er.field === f.key)));
        if (stepWithError >= 0) setCurrent(stepWithError);
        setMessage(t('form.attention'));
      } else {
        setMessage((e as Error).message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function upload(field: FieldDefinition, file: File) {
    const doc = await api.upload<DocumentInfo>('/api/documents/documents', file);
    setFileNames(n => ({ ...n, [field.key]: doc.fileName }));
    return doc.id;
  }

  return (
    <div className="card">
      <Stepper steps={[...steps, { id: 'review', title: t('form.review'), fields: [] }]} current={current}
        onSelect={i => !busy && i <= current && setCurrent(i)} />
      {message && <Alert kind="error">{message}</Alert>}

      {!reviewing ? (
        <>
          <h2>{loc(steps[current].title, steps[current].titleAr)}</h2>
          <DynamicForm step={steps[current]} values={values} errors={errors} fileNames={fileNames}
            onChange={(k, v) => setValues(prev => ({ ...prev, [k]: v }))}
            onUpload={upload} />
          <div className="actions">
            {current > 0 && <button className="btn btn-ghost" disabled={busy} onClick={() => setCurrent(current - 1)}>{t('back')}</button>}
            <button className="btn btn-primary" disabled={busy} onClick={next}>
              {busy ? t('form.saving') : current === steps.length - 1 ? t('form.saveReview') : t('form.saveContinue')}
            </button>
          </div>
        </>
      ) : (
        <>
          <h2>{t('form.reviewTitle')}</h2>
          {steps.map((s, i) => (
            <section key={s.id} style={{ marginBottom: 20 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h3>{loc(s.title, s.titleAr)}</h3>
                <button className="link" onClick={() => setCurrent(i)}>{t('form.change')}</button>
              </div>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly fileNames={fileNames}
                onOpenFile={async docId => openBlob(await api.download(`/api/documents/documents/${docId}/content`))} />
            </section>
          ))}
          {!resubmit && <Alert kind="info">{app.workflowVersion === 2 ? t('form.afterSubmitAi') : t('form.afterSubmit')}</Alert>}
          <div className="actions">
            <button className="btn btn-ghost" disabled={busy} onClick={() => setCurrent(steps.length - 1)}>{t('back')}</button>
            <button className="btn btn-primary" disabled={busy} onClick={submit}>
              {busy ? t('form.submitting') : resubmit ? t('form.resubmit') : t('form.submit')}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
