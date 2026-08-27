import { useState, type FormEvent } from 'react'
import { api, ApiError, type LoginResponse } from '../api'
import { GridIcon } from '../components/Brand'

interface LoginPageProps {
  onAuthenticated: (session: LoginResponse) => void
}

export function LoginPage({ onAuthenticated }: LoginPageProps) {
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setError('')

    try {
      onAuthenticated(await api.login(userName.trim(), password))
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 502) {
        setError('Gateway cannot reach Identity. Start Gateway on 7000 and Identity on 7001.')
      } else if (reason instanceof ApiError) {
        setError(reason.message)
      } else {
        setError('Cannot reach the API Gateway at http://localhost:7000.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <header className="login-rail">
        <span className="icon-button" aria-label="Applications"><GridIcon /></span>
        <span>Enterprise workspace</span>
      </header>

      <main className="login-shell">
        <section className="login-card" aria-labelledby="login-title">
          <div className="login-visual">
            <div className="login-reference-logo">
              <img
                src="/assets/grd-logo.png"
                alt="GRD — Getting Recruitments Done"
              />
            </div>
          </div>

          <div className="login-form-panel">
            <div className="login-heading">
              <span className="login-heading__badge"><GridIcon /></span>
              <h1 id="login-title">Login To</h1>
              <p>Your Dream Board!</p>
            </div>

            <form onSubmit={handleSubmit} className="login-form">
              <label className="field">
                <span>User name</span>
                <input
                  type="email"
                  autoComplete="username"
                  value={userName}
                  onChange={(event) => setUserName(event.target.value)}
                  placeholder="name@grd.local"
                  required
                />
              </label>

              <label className="field">
                <span>Password</span>
                <div className="password-field">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((visible) => !visible)}
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                  >
                    {showPassword ? 'Hide' : 'Show'}
                  </button>
                </div>
              </label>

              {error && <div className="form-alert" role="alert">{error}</div>}

              <button className="primary-button" type="submit" disabled={submitting}>
                {submitting ? <span className="spinner" /> : null}
                {submitting ? 'Signing in…' : 'Sign in'}
              </button>

              <p className="demo-note">
                Identity assigns your role, organization scope and permissions after sign-in.
              </p>
            </form>
          </div>
        </section>
      </main>
    </div>
  )
}
