import { useState } from 'react';
import {
  Alert, ApiError, DynamicForm, openBlob, Stepper,
  type Application, type DocumentInfo, type FieldDefinition, type FieldError, type FormData, type SaveResult, type ServiceDefinition,
} from '@dsp/shared';
import { api } from '../session';

/** Traditional view: one step at a time, rendered from the service definition. */
export function FormView({ app, service, onChange }: {
  app: Application;
  service: ServiceDefinition;
  onChange: (app: Application) => void;
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
        setMessage('Some details need your attention.');
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
      <Stepper steps={[...steps, { id: 'review', title: 'Review', fields: [] }]} current={current}
        onSelect={i => !busy && i <= current && setCurrent(i)} />
      {message && <Alert kind="error">{message}</Alert>}

      {!reviewing ? (
        <>
          <h2>{steps[current].title}</h2>
          <DynamicForm step={steps[current]} values={values} errors={errors} fileNames={fileNames}
            onChange={(k, v) => setValues(prev => ({ ...prev, [k]: v }))}
            onUpload={upload} />
          <div className="actions">
            {current > 0 && <button className="btn btn-ghost" disabled={busy} onClick={() => setCurrent(current - 1)}>Back</button>}
            <button className="btn btn-primary" disabled={busy} onClick={next}>
              {busy ? 'Saving…' : current === steps.length - 1 ? 'Save and review' : 'Save and continue'}
            </button>
          </div>
        </>
      ) : (
        <>
          <h2>Review your application</h2>
          {steps.map((s, i) => (
            <section key={s.id} style={{ marginBottom: 20 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h3>{s.title}</h3>
                <button className="link" onClick={() => setCurrent(i)}>Change</button>
              </div>
              <DynamicForm step={{ ...s, description: null }} values={app.data} readOnly fileNames={fileNames}
                onOpenFile={async docId => openBlob(await api.download(`/api/documents/documents/${docId}/content`))} />
            </section>
          ))}
          <Alert kind="info">
            After you submit, a ministry officer reviews your application. If approved, you pay the fee and receive your certificate.
          </Alert>
          <div className="actions">
            <button className="btn btn-ghost" disabled={busy} onClick={() => setCurrent(steps.length - 1)}>Back</button>
            <button className="btn btn-primary" disabled={busy} onClick={submit}>{busy ? 'Submitting…' : 'Submit application'}</button>
          </div>
        </>
      )}
    </div>
  );
}
