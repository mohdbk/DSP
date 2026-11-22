import { useEffect, useState } from 'react';
import type { AiAssessment, Application, ServiceDefinition } from '@dsp/core';
import { api } from '../session';

/** Everything an officer needs about one application: the application, its service version and the AI pre-review. */
export function useCase(applicationId: string | undefined) {
  const [app, setApp] = useState<Application>();
  const [service, setService] = useState<ServiceDefinition>();
  const [assessments, setAssessments] = useState<AiAssessment[]>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!applicationId) return;
    (async () => {
      try {
        const a = await api.get<Application>(`/api/v1/eservices/applications/${applicationId}`);
        setApp(a);
        const [svc, ai] = await Promise.all([
          api.get<ServiceDefinition>(`/api/v1/eservices/services/${a.serviceCode}?version=${a.serviceVersion}`),
          api.get<AiAssessment[]>(`/api/v1/eservices/applications/${a.id}/assessments`),
        ]);
        setService(svc);
        setAssessments(ai);
      } catch (e) {
        setError((e as Error).message);
      }
    })();
  }, [applicationId]);

  return { app, setApp, service, assessments, error };
}
