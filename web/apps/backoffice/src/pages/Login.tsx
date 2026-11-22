import { useState } from 'react';
import { Navigate, useNavigate } from '@tanstack/react-router';
import { t } from '@dsp/core';
import { AegovButton, Notice, PageHeader, Panel, TextField } from '@dsp/ui';
import { api, currentUser } from '../session';

/** Staff sign-in with ministry credentials (local accounts now; Active Directory later). */
export function LoginPage() {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  if (currentUser()) return <Navigate to="/inbox" replace />;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      const res = await api.post<{ accessToken: string }>('/api/v1/identity/staff/login', { username, password });
      api.setToken(res.accessToken);
      navigate({ to: '/inbox', replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader title={t('staffLogin.title')} intro={t('staffLogin.intro')} />
      <Panel>
        <form onSubmit={submit}>
          {error && <Notice kind="error">{error}</Notice>}
          <TextField id="username" label={t('staffLogin.username')} value={username} onChange={setUsername} required dir="ltr" />
          <TextField id="password" label={t('staffLogin.password')} type="password" value={password} onChange={setPassword} required dir="ltr" />
          <AegovButton type="submit" isFull isLoading={busy} isDisabled={!username || !password}>{t('staffLogin.signIn')}</AegovButton>
        </form>
      </Panel>
    </div>
  );
}
