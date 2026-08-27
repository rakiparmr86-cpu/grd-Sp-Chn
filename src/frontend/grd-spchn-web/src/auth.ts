import type { LoginResponse } from './api'

const sessionKey = 'grd.spchn.session.v2'

export function loadSession(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(sessionKey)
    if (!raw) return null

    const session = JSON.parse(raw) as LoginResponse
    if (new Date(session.expiresOnUtc).getTime() <= Date.now()) {
      clearSession()
      return null
    }

    return session
  } catch {
    clearSession()
    return null
  }
}

export function saveSession(session: LoginResponse): void {
  sessionStorage.setItem(sessionKey, JSON.stringify(session))
}

export function clearSession(): void {
  sessionStorage.removeItem(sessionKey)
}

export function hasPermission(
  session: LoginResponse,
  permission: string,
): boolean {
  return session.permissions.includes(permission)
}
