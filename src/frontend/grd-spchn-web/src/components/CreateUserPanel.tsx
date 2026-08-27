import { useEffect, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type AccessProfile,
  type CreatedUser,
  type OrganizationUnit,
} from '../api'

interface CreateUserPanelProps {
  accessToken: string
  onClose: () => void
}

export function CreateUserPanel({ accessToken, onClose }: CreateUserPanelProps) {
  const [profiles, setProfiles] = useState<AccessProfile[]>([])
  const [units, setUnits] = useState<OrganizationUnit[]>([])
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('1223456')
  const [accessProfile, setAccessProfile] = useState('')
  const [organizationUnitId, setOrganizationUnitId] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState<CreatedUser | null>(null)

  useEffect(() => {
    let active = true

    Promise.all([
      api.getAccessProfiles(accessToken),
      api.getOrganizationUnits(accessToken),
    ])
      .then(([availableProfiles, organizationUnits]) => {
        if (!active) return
        const activeUnits = organizationUnits.filter((unit) => unit.isActive)
        setProfiles(availableProfiles)
        setUnits(activeUnits)
        setAccessProfile(availableProfiles[0]?.code ?? '')
        setOrganizationUnitId(activeUnits[0]?.id ?? '')
      })
      .catch((reason: unknown) => {
        if (!active) return
        setError(reason instanceof ApiError ? reason.message : 'Could not load user setup data.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [accessToken])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    setCreated(null)

    try {
      const result = await api.createUser(accessToken, {
        userName: userName.trim(),
        password,
        accessProfile,
        organizationUnitId,
      })
      setCreated(result)
      setUserName('')
      setPassword('1223456')
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not create the user.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside
        className="user-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-user-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="drawer-header">
          <div>
            <span className="eyebrow">Identity & access</span>
            <h2 id="create-user-title">Create a user</h2>
            <p>Assign a controlled job profile and organization scope.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        {loading ? (
          <div className="drawer-loading"><span className="spinner spinner--dark" /> Loading access profiles…</div>
        ) : (
          <form className="create-user-form" onSubmit={handleSubmit}>
            <label className="field">
              <span>User name</span>
              <input
                type="email"
                value={userName}
                onChange={(event) => setUserName(event.target.value)}
                placeholder="employee@grd.local"
                autoComplete="off"
                required
              />
              <small>Used to sign in; must be unique.</small>
            </label>

            <label className="field">
              <span>Initial password</span>
              <input
                type="text"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                minLength={7}
                autoComplete="new-password"
                required
              />
              <small>For local development only. Production must require password change.</small>
            </label>

            <label className="field">
              <span>Job / access profile</span>
              <select
                value={accessProfile}
                onChange={(event) => setAccessProfile(event.target.value)}
                required
              >
                {profiles.map((profile) => (
                  <option key={profile.code} value={profile.code}>
                    {profile.displayName} · {profile.role}
                  </option>
                ))}
              </select>
              <small>Available profiles and permissions come from the Identity database.</small>
            </label>

            <label className="field">
              <span>Organization unit</span>
              <select
                value={organizationUnitId}
                onChange={(event) => setOrganizationUnitId(event.target.value)}
                required
              >
                {units.map((unit) => (
                  <option key={unit.id} value={unit.id}>
                    {unit.name} · {unit.type}
                  </option>
                ))}
              </select>
            </label>

            {error && <div className="form-alert" role="alert">{error}</div>}
            {created && (
              <div className="success-alert" role="status">
                <strong>{created.userName}</strong> was created as {created.role}.
              </div>
            )}

            <div className="drawer-actions">
              <button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
              <button
                className="primary-button"
                type="submit"
                disabled={submitting || profiles.length === 0 || units.length === 0}
              >
                {submitting ? 'Creating…' : 'Create user'}
              </button>
            </div>
          </form>
        )}
      </aside>
    </div>
  )
}
