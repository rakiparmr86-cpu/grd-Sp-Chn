import { useEffect, useMemo, useState } from 'react'
import {
  api,
  ApiError,
  type ManagedAccessProfile,
  type PermissionDefinition,
} from '../api'

interface AccessProfilePermissionsPanelProps {
  accessToken: string
  onClose: () => void
}

const permissionManagementCode = 'identity.access-profile.manage'

export function AccessProfilePermissionsPanel({
  accessToken,
  onClose,
}: AccessProfilePermissionsPanelProps) {
  const [profiles, setProfiles] = useState<ManagedAccessProfile[]>([])
  const [catalog, setCatalog] = useState<PermissionDefinition[]>([])
  const [selectedCode, setSelectedCode] = useState('')
  const [draftPermissions, setDraftPermissions] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let active = true

    Promise.all([
      api.getManagedAccessProfiles(accessToken),
      api.getPermissionCatalog(accessToken),
    ])
      .then(([availableProfiles, permissions]) => {
        if (!active) return
        const initialProfile = availableProfiles[0]
        setProfiles(availableProfiles)
        setCatalog(permissions.filter((permission) => permission.isActive))
        setSelectedCode(initialProfile?.code ?? '')
        setDraftPermissions(initialProfile?.permissions ?? [])
      })
      .catch((reason: unknown) => {
        if (!active) return
        setError(reason instanceof ApiError ? reason.message : 'Could not load permission settings.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [accessToken])

  const selectedProfile = profiles.find((profile) => profile.code === selectedCode)
  const groupedPermissions = useMemo(() => {
    return catalog.reduce<Record<string, PermissionDefinition[]>>((groups, permission) => {
      const modulePermissions = groups[permission.module] ?? []
      groups[permission.module] = [...modulePermissions, permission]
      return groups
    }, {})
  }, [catalog])
  const originalPermissions = selectedProfile?.permissions ?? []
  const hasChanges = [...originalPermissions].sort().join('|') !==
    [...draftPermissions].sort().join('|')

  function selectProfile(code: string) {
    const profile = profiles.find((item) => item.code === code)
    setSelectedCode(code)
    setDraftPermissions(profile?.permissions ?? [])
    setError('')
    setSaved(false)
  }

  function togglePermission(code: string, checked: boolean) {
    setDraftPermissions((current) => checked
      ? [...new Set([...current, code])]
      : current.filter((permission) => permission !== code))
    setSaved(false)
  }

  async function savePermissions() {
    if (!selectedProfile) return
    setSaving(true)
    setError('')
    setSaved(false)

    try {
      const updated = await api.replaceAccessProfilePermissions(
        accessToken,
        selectedProfile.code,
        draftPermissions,
      )
      setProfiles((current) => current.map((profile) =>
        profile.code === updated.code ? updated : profile))
      setDraftPermissions(updated.permissions)
      setSaved(true)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not save permissions.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside
        className="user-drawer permission-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="permission-panel-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="drawer-header">
          <div>
            <span className="eyebrow">Identity & access</span>
            <h2 id="permission-panel-title">Manage profile permissions</h2>
            <p>Add or remove backend-controlled access for each job profile.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        {loading ? (
          <div className="drawer-loading"><span className="spinner spinner--dark" /> Loading permissions…</div>
        ) : (
          <div className="permission-editor">
            <label className="field">
              <span>Access profile</span>
              <select value={selectedCode} onChange={(event) => selectProfile(event.target.value)}>
                {profiles.map((profile) => (
                  <option key={profile.code} value={profile.code}>
                    {profile.displayName} · {profile.role}{profile.isActive ? '' : ' · Inactive'}
                  </option>
                ))}
              </select>
              <small>HR assigns profiles to users; this screen controls what each profile can do.</small>
            </label>

            <div className="permission-summary">
              <span><strong>{draftPermissions.length}</strong> permissions selected</span>
              <span>{selectedProfile?.isHrAssignable ? 'HR assignable' : 'Privileged profile'}</span>
            </div>

            <div className="permission-groups">
              {Object.entries(groupedPermissions).map(([module, permissions]) => (
                <section className="permission-group" key={module}>
                  <h3>{module}</h3>
                  {permissions.map((permission) => {
                    const isRequiredDirectorPermission =
                      selectedCode.toLowerCase() === 'director' &&
                      permission.code === permissionManagementCode
                    return (
                      <label className="permission-option" key={permission.code}>
                        <input
                          type="checkbox"
                          checked={draftPermissions.includes(permission.code)}
                          disabled={isRequiredDirectorPermission}
                          onChange={(event) => togglePermission(permission.code, event.target.checked)}
                        />
                        <span>
                          <strong>{permission.displayName}</strong>
                          <small>{permission.description}</small>
                          <code>{permission.code}</code>
                        </span>
                      </label>
                    )
                  })}
                </section>
              ))}
            </div>

            <div className="permission-token-note">
              Saved changes apply when affected users sign in again or receive a new JWT.
            </div>
            {error && <div className="form-alert" role="alert">{error}</div>}
            {saved && <div className="success-alert" role="status">Permissions saved successfully.</div>}

            <div className="drawer-actions permission-actions">
              <button className="secondary-button" type="button" onClick={onClose}>Close</button>
              <button
                className="primary-button"
                type="button"
                disabled={saving || !hasChanges || !selectedProfile}
                onClick={savePermissions}
              >
                {saving ? 'Saving…' : 'Save permissions'}
              </button>
            </div>
          </div>
        )}
      </aside>
    </div>
  )
}
