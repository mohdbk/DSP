import { useState } from 'react';
import { ApiError, loc, openDocument, t, uploadDocument, type Application, type FieldError, type FormData, type SaveResult, type ServiceDefinition } from '@dsp/core';
import {
  Actions, AegovButton, ArrowLeft, ArrowRight, IconLabel, Notice, Panel, PaperPlaneRight, PencilSimple, ServiceAnswers, StepFields, StepProgress,
} from '@dsp/ui';
import { api } from '../session';

/** The form view: one step at a time, rendered from the service definition, then a review before submitting. */
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
    const result = await api.put<SaveResult>(`/api/v1/eservices/applications/${app.id}/data`, { values: payload });
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
      onChange(await api.post<Application>(`/api/v1/eservices/applications/${app.id}/submit`));
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

  const titles = [...steps.map(s => loc(s.title)), t('form.review')];

  return (
    <Panel>
      <StepProgress titles={titles} current={current} />
      {message && <Notice kind="error">{message}</Notice>}

      {!reviewing ? (
        <>
          <h2 className="mb-1 text-h5 font-bold text-aeblack-900">{titles[current]}</h2>
          <p className="mb-5 text-sm text-aeblack-600">{t('form.allRequired')}</p>
          <StepFields step={steps[current]} values={values} errors={errors} fileNames={fileNames}
            onChange={(k, v) => setValues(prev => ({ ...prev, [k]: v }))}
            onUpload={file => uploadDocument(api, file)}
            onFileName={(k, name) => setFileNames(n => ({ ...n, [k]: name }))} />
          <Actions align="between">
            {current > 0
              ? <AegovButton appearance="outline" isDisabled={busy} onAegovButtonClick={() => setCurrent(current - 1)}><IconLabel icon={ArrowLeft} mirror>{t('back')}</IconLabel></AegovButton>
              : <span />}
            <AegovButton isLoading={busy} onAegovButtonClick={next}>
              <IconLabel icon={ArrowRight} mirror>{current === steps.length - 1 ? t('form.saveReview') : t('form.saveContinue')}</IconLabel>
            </AegovButton>
          </Actions>
        </>
      ) : (
        <>
          <h2 className="mb-4 text-h5 font-bold text-aeblack-900">{t('form.reviewTitle')}</h2>
          <ServiceAnswers steps={steps} values={app.data} fileNames={fileNames}
            onOpenFile={id => openDocument(api, id)}
            stepAction={i => <AegovButton appearance="link" size="sm" onAegovButtonClick={() => setCurrent(i)}><IconLabel icon={PencilSimple}>{t('form.change')}</IconLabel></AegovButton>} />
          {!resubmit && <div className="mt-6"><Notice kind="info">{app.workflowVersion === 2 ? t('form.afterSubmitAi') : t('form.afterSubmit')}</Notice></div>}
          <Actions align="between">
            <AegovButton appearance="outline" isDisabled={busy} onAegovButtonClick={() => setCurrent(steps.length - 1)}><IconLabel icon={ArrowLeft} mirror>{t('back')}</IconLabel></AegovButton>
            <AegovButton isLoading={busy} onAegovButtonClick={submit}><IconLabel icon={PaperPlaneRight} mirror>{resubmit ? t('form.resubmit') : t('form.submit')}</IconLabel></AegovButton>
          </Actions>
        </>
      )}
    </Panel>
  );
}
