import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Alert, t } from '@dsp/shared';
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
      const res = await api.post<{ accessToken: string }>('/api/identity/staff/login', { username, password });
      api.setToken(res.accessToken);
      navigate('/inbox', { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-wrap">
      <form className="card" onSubmit={submit}>
        <h1>{t('staffLogin.title')}</h1>
        <p className="muted">{t('staffLogin.intro')}</p>
        {error && <Alert kind="error">{error}</Alert>}
        <div className="field">
          <label htmlFor="username">{t('staffLogin.username')}</label>
          <input id="username" type="text" dir="ltr" autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required />
        </div>
        <div className="field">
          <label htmlFor="password">{t('staffLogin.password')}</label>
          <input id="password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required />
        </div>
        <button className="btn btn-primary" style={{ width: '100%' }} disabled={busy}>{busy ? t('staffLogin.signingIn') : t('staffLogin.signIn')}</button>
      </form>
    </div>
  );
}
