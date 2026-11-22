import { formatDate, loc, t, type AiAssessment, usesAi } from '@dsp/core';
import { Panel } from '../layout/Page';
import { Tag } from '../feedback/Feedback';

const recommendationVariant = {
  approve: 'aegreen',
  request_information: 'camel',
  reject: 'aered',
  unavailable: 'slate',
} as const;

const severityVariant = { issue: 'aered', warning: 'camel', info: 'slate' } as const;

/** The AI pre-review's recommendation, as a label. */
export function RecommendationTag({ value, short }: { value?: string | null; short?: boolean }) {
  if (!value) return <span className="text-aeblack-400">—</span>;
  const variant = recommendationVariant[value as keyof typeof recommendationVariant] ?? 'slate';
  return <Tag variant={variant}>{t((short ? `ai.recShort.${value}` : `ai.rec.${value}`))}</Tag>;
}

/** What the AI review found, for the officer. The AI advises; staff decide. */
export function AiReviewPanel({ assessments, workflowVersion }: { assessments?: AiAssessment[]; workflowVersion: number }) {
  if (!usesAi(workflowVersion)) {
    return <Panel title={t('ai.title')}><p className="text-aeblack-600">{t('ai.none')}</p></Panel>;
  }
  if (!assessments) return null;
  const [latest, ...earlier] = assessments;

  return (
    <Panel title={t('ai.title')} actions={latest && <RecommendationTag value={latest.recommendation} />}>
      {!latest && <p className="text-aeblack-600">{t('status.aiChecking')}</p>}
      {latest && <AssessmentRound a={latest} />}
      {earlier.length > 0 && (
        <details className="mt-4 text-sm">
          <summary className="cursor-pointer text-aeblack-600">{earlier.map(a => t('ai.round', { n: a.round })).join(' · ')}</summary>
          <div className="mt-3 flex flex-col gap-4">{earlier.map(a => <AssessmentRound key={a.id} a={a} />)}</div>
        </details>
      )}
      <p className="mt-4 border-t border-aeblack-100 pt-3 text-xs text-aeblack-500">{t('ai.advice')}</p>
    </Panel>
  );
}

function AssessmentRound({ a }: { a: AiAssessment }) {
  const mode = a.mode === 'llm' ? t('ai.mode.llm', { model: a.model ?? '' }) : t(`ai.mode.${a.mode}`);
  const order = { issue: 0, warning: 1, info: 2 } as const;
  const findings = [...a.findings].sort((x, y) => order[x.severity] - order[y.severity]);
  return (
    <div>
      <p className="text-xs text-aeblack-500">{t('ai.round', { n: a.round })} · {mode} · {formatDate(a.createdAt, true)}</p>
      {loc(a.summary) && <p className="mt-2 text-aeblack-800" dir="auto">{loc(a.summary)}</p>}
      {findings.length > 0 && (
        <ul className="m-0 mt-3 flex list-none flex-col gap-2 p-0 text-sm">
          {findings.map((f, i) => (
            <li key={i} className="flex items-baseline gap-2">
              <Tag variant={severityVariant[f.severity]}>{t(`ai.sev.${f.severity}`)}</Tag>
              <span dir="auto" className="text-aeblack-800">{loc(f.message)}</span>
            </li>
          ))}
        </ul>
      )}
      {a.questions.length > 0 && (
        <>
          <p className="mt-3 text-sm font-semibold text-aeblack-800">{t('ai.questions')}</p>
          <ul className="mt-1 ps-5 text-sm text-aeblack-800">
            {a.questions.map((q, i) => <li key={i}>{loc(q)}</li>)}
          </ul>
        </>
      )}
    </div>
  );
}
