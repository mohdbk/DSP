import type { AiAssessment } from './types';
import { formatDate } from './ui';
import { getLang, t, type TKey } from './i18n';

/** What the AI pre-review found, for the officer. The AI advises; staff decide. */
export function AiReviewPanel({ assessments, workflowVersion }: { assessments?: AiAssessment[]; workflowVersion: number }) {
  if (workflowVersion !== 2) {
    return (
      <div className="card ai-panel">
        <h3>{t('ai.title')}</h3>
        <p className="muted" style={{ margin: 0 }}>{t('ai.none')}</p>
      </div>
    );
  }
  if (!assessments) return null;

  const [latest, ...earlier] = assessments;
  return (
    <div className="card ai-panel">
      <div className="ai-head">
        <h3 style={{ margin: 0 }}>✨ {t('ai.title')}</h3>
        {latest && <RecommendationChip value={latest.recommendation} />}
      </div>
      {!latest && <p className="muted">{t('status.aiChecking')}</p>}
      {latest && <Assessment a={latest} />}
      {earlier.length > 0 && (
        <details className="ai-earlier">
          <summary>{earlier.map(a => t('ai.round', { n: a.round })).join(' · ')}</summary>
          {earlier.map(a => <Assessment key={a.id} a={a} />)}
        </details>
      )}
      <p className="ai-advice">{t('ai.advice')}</p>
    </div>
  );
}

export function RecommendationChip({ value, short }: { value?: string | null; short?: boolean }) {
  if (!value) return <span className="muted">—</span>;
  const key = (short ? `ai.recShort.${value}` : `ai.rec.${value}`) as TKey;
  return <span className={`ai-chip ai-chip-${value}`}>{t(key)}</span>;
}

function Assessment({ a }: { a: AiAssessment }) {
  const mode = a.mode === 'llm' ? t('ai.mode.llm', { model: a.model ?? '' }) : t(`ai.mode.${a.mode}` as TKey);
  const order = { issue: 0, warning: 1, info: 2 } as const;
  const findings = [...a.findings].sort((x, y) => order[x.severity] - order[y.severity]);
  return (
    <div className="ai-assessment">
      <div className="ai-meta">{t('ai.round', { n: a.round })} · {mode} · {formatDate(a.createdAt, true)}</div>
      {a.summary && <p className="ai-summary" dir="auto">{a.summary}</p>}
      {findings.length > 0 && (
        <ul className="ai-findings">
          {findings.map((f, i) => (
            <li key={i} className={`sev-${f.severity}`}>
              <span className="sev">{t(`ai.sev.${f.severity}` as TKey)}</span>
              <span dir="auto">{f.message}</span>
            </li>
          ))}
        </ul>
      )}
      {a.questions.length > 0 && (
        <>
          <div className="ai-sub">{t('ai.questions')}</div>
          <ul className="ai-questions">
            {a.questions.map((q, i) => <li key={i}>{getLang() === 'ar' ? q.ar : q.en}</li>)}
          </ul>
        </>
      )}
    </div>
  );
}
