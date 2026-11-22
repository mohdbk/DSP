import { useEffect, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { t } from '@dsp/core';
import { AegovLink, Loading, Notice, PageHeader, Panel } from '@dsp/ui';
import { api, currentUser } from '../session';

interface Provider { name: string; displayName: string }

const route = getRouteApi('/login');

/** UAE PASS sign-in (the only sign-in for citizens, as the design system requires). */
export function LoginPage() {
  const { returnUrl = '/', error: initialError } = route.useSearch();
  const [providers, setProviders] = useState<Provider[]>();
  const [error, setError] = useState(initialError);
  const navigate = useNavigate();
  const signedIn = !!currentUser();

  useEffect(() => {
    if (signedIn) navigate({ href: returnUrl.startsWith('/') ? returnUrl : '/', replace: true });
  }, [signedIn, returnUrl, navigate]);

  useEffect(() => {
    api.get<Provider[]>('/api/v1/identity/providers').then(setProviders).catch(() => setError(t('login.unavailable')));
  }, []);

  if (signedIn) return <Loading />;

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader title={t('login.title')} intro={t('login.intro')} />
      <Panel>
        {error && <Notice kind="error">{error}</Notice>}
        {!providers && !error && <Loading />}
        {providers?.length === 0 && <Notice kind="warning">{t('login.none')}</Notice>}
        <div className="flex flex-col gap-3">
          {providers?.map(p => (
            <AegovLink key={p.name} href={`/api/v1/identity/citizen/${p.name}/login?returnUrl=${encodeURIComponent(returnUrl)}`}
              appearance="solid" variant={p.name === 'uaepass' ? 'neutral-max' : 'primary'} size="lg" isFull>
              {t('login.with', { name: p.name === 'uaepass' ? t('uaePass') : p.displayName })}
            </AegovLink>
          ))}
        </div>
        <p className="mt-5 text-sm text-aeblack-600">{t('login.staging')}</p>
      </Panel>
    </div>
  );
}

/** UAE PASS returns here via the Identity service with the platform token in the URL fragment. */
export function AuthCallbackPage() {
  const navigate = useNavigate();
  useEffect(() => {
    const hash = new URLSearchParams(location.hash.slice(1));
    const token = hash.get('token');
    const returnUrl = hash.get('returnUrl') || '/';
    history.replaceState(null, '', location.pathname); // drop the token from the address bar
    if (token) {
      api.setToken(token);
      navigate({ href: returnUrl.startsWith('/') ? returnUrl : '/', replace: true });
    } else {
      navigate({ to: '/login', search: { error: t('login.failed') }, replace: true });
    }
  }, [navigate]);
  return <Loading label={t('login.signingIn')} />;
}
