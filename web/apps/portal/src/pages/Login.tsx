import { useEffect, useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Spinner } from '@dsp/shared';
import { api, currentUser } from '../session';

interface Provider { name: string; displayName: string }

export function LoginPage() {
  const [params] = useSearchParams();
  const [providers, setProviders] = useState<Provider[]>();
  const [error, setError] = useState(params.get('error'));
  const returnUrl = params.get('returnUrl') ?? '/';

  useEffect(() => {
    api.get<Provider[]>('/api/identity/providers')
      .then(setProviders)
      .catch(() => setError('Sign-in is unavailable right now. Please try again shortly.'));
  }, []);

  if (currentUser()) return <Navigate to={returnUrl} replace />;

  return (
    <div className="login-wrap">
      <div className="card">
        <h1>Sign in</h1>
        <p className="muted">Sign in to apply for ministry services, track your applications and download certificates.</p>
        {error && <Alert kind="error">{error}</Alert>}
        {!providers && !error && <Spinner />}
        {providers?.length === 0 && <Alert kind="warning">No sign-in method is configured.</Alert>}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 16 }}>
          {providers?.map(p => (
            <a key={p.name}
              className={`btn ${p.name === 'uaepass' ? 'btn-uaepass' : 'btn-secondary'}`}
              href={`/api/identity/citizen/${p.name}/login?returnUrl=${encodeURIComponent(returnUrl)}`}>
              Sign in with {p.displayName}
            </a>
          ))}
        </div>
        <p className="muted" style={{ fontSize: 13, marginTop: 20 }}>
          This environment uses the UAE PASS staging service. Use a UAE PASS staging account to sign in.
        </p>
      </div>
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
      navigate(returnUrl.startsWith('/') ? returnUrl : '/', { replace: true });
    } else {
      navigate('/login?error=Sign-in%20failed', { replace: true });
    }
  }, [navigate]);
  return <main><Spinner label="Signing you in…" /></main>;
}
