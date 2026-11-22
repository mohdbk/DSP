import { useEffect, useState } from 'react';
import { t } from '@dsp/core';
import { DescriptionList, Notice, Panel, Tag } from '@dsp/ui';
import { api } from '../../session';

interface Readiness {
  reviewed: number; agreed: number; comparable: number; agreement: number | null;
  clearCases: number; clearCasesApproved: number; clearCaseAccuracy: number | null; overturned: number; autoApproved: number;
  minClearCases: number; threshold: number; ready: boolean; concern: boolean; window: number;
}

const percent = (v: number | null) => (v == null ? '—' : `${Math.round(v * 1000) / 10}%`);

/**
 * How closely the AI's judgement matches the officers' for this service, from its recent decided applications: the
 * evidence for moving an AI-assisted service to the agentic process, and for keeping an agentic one there.
 */
export function AiReadinessPanel({ code, workflowVersion }: { code: string; workflowVersion: number }) {
  const [data, setData] = useState<Readiness>();
  const [error, setError] = useState<string>();
  useEffect(() => {
    api.get<Readiness>(`/api/v1/eservices/builder/services/${encodeURIComponent(code)}/ai-readiness`).then(setData)
      .catch(e => setError((e as Error).message));
  }, [code]);

  if (workflowVersion === 1) return null;
  if (error) return <Notice kind="error">{error}</Notice>;
  if (!data) return null;

  const verdict = data.concern ? 'concern' : data.ready ? (workflowVersion === 3 ? 'healthy' : 'ready') : 'collecting';
  const variant = { concern: 'aered', ready: 'aegreen', healthy: 'aegreen', collecting: 'slate' } as const;

  return (
    <Panel className="mb-6 block" title={t('readiness.title')} actions={<Tag variant={variant[verdict]}>{t(`readiness.${verdict}`)}</Tag>}>
      <p className="mb-4 text-aeblack-700">
        {t(`readiness.${verdict}Text`, { n: data.minClearCases, threshold: percent(data.threshold), remaining: Math.max(0, data.minClearCases - data.clearCases) })}
      </p>
      <DescriptionList items={[
        { label: t('readiness.clearAccuracy'), value: `${percent(data.clearCaseAccuracy)} (${data.clearCasesApproved}/${data.clearCases})` },
        { label: t('readiness.overturned'), value: String(data.overturned) },
        { label: t('readiness.agreement'), value: `${percent(data.agreement)} (${data.agreed}/${data.comparable})` },
        { label: t('readiness.reviewed'), value: String(data.reviewed) },
        { label: t('readiness.autoApproved'), value: String(data.autoApproved) },
      ]} />
      <p className="mt-4 text-sm text-aeblack-600">{t('readiness.note', { window: data.window })}</p>
    </Panel>
  );
}
