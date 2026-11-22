import { useEffect, useState } from 'react';
import { useNavigate, useParams } from '@tanstack/react-router';
import { formatDate, openDocument, t, type Application, type ReviewTask } from '@dsp/core';
import { AegovButton, ApplicationCase, Loading, Notice, Panel, TextAreaField } from '@dsp/ui';
import { api } from '../session';
import { useCase } from './useCase';

/** Decide on one application, with the AI pre-review alongside. */
export function TaskPage() {
  const { id } = useParams({ strict: false }) as { id: string };
  const navigate = useNavigate();
  const [task, setTask] = useState<ReviewTask>();
  const [taskError, setTaskError] = useState<string>();
  const { app, setApp, service, assessments, error } = useCase(task?.applicationId);
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();
  const [done, setDone] = useState(false);

  useEffect(() => {
    api.get<ReviewTask>(`/api/v1/eservices/tasks/${id}`).then(setTask).catch(e => setTaskError(e.message));
  }, [id]);

  async function decide(approve: boolean) {
    if (!approve && !comment.trim()) { setMessage(t('task.reasonRequired')); return; }
    setBusy(true);
    setMessage(undefined);
    try {
      setApp(await api.post<Application>(`/api/v1/eservices/tasks/${id}/decision`, { approve, comment: comment.trim() || null }));
      setDone(true);
    } catch (e) {
      setMessage((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (taskError || error) return <Notice kind="error">{taskError ?? error}</Notice>;
  if (!task || !app || !service) return <Loading />;

  return (
    <ApplicationCase app={app} service={service} assessments={assessments} onOpenFile={docId => openDocument(api, docId)}
      summaryExtra={[{ label: t('slaDue'), value: formatDate(task.dueAt, true) }]}
      actions={<AegovButton appearance="outline" onAegovButtonClick={() => navigate({ to: '/inbox' })}>{t('task.backToInbox')}</AegovButton>}>
      <Panel title={t('decision')}>
        {done ? <Notice kind="success">{t('task.recorded')}</Notice>
          : task.status === 'Completed' ? <Notice kind="info">{t('task.alreadyDone', { outcome: task.outcome ?? '' })}</Notice>
            : (
              <>
                {message && <Notice kind="error">{message}</Notice>}
                <TextAreaField id="comment" label={`${t('task.commentLabel')} ${t('task.commentHint')}`} hint={t('task.commentPlaceholder')}
                  value={comment} onChange={setComment} />
                <div className="flex flex-wrap gap-3">
                  <AegovButton isLoading={busy} onAegovButtonClick={() => decide(true)}>{t('task.approve')}</AegovButton>
                  <AegovButton variant="danger" appearance="outline" isDisabled={busy} onAegovButtonClick={() => decide(false)}>{t('task.reject')}</AegovButton>
                </div>
              </>
            )}
      </Panel>
    </ApplicationCase>
  );
}
