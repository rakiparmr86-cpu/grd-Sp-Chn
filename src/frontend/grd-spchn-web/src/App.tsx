import { useState } from 'react'
import type { LoginResponse } from './api'
import { clearSession, loadSession, saveSession } from './auth'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'

export default function App() {
  const [session, setSession] = useState<LoginResponse | null>(() => loadSession())

  function handleAuthenticated(nextSession: LoginResponse) {
    saveSession(nextSession)
    setSession(nextSession)
  }

  function handleSignOut() {
    clearSession()
    setSession(null)
  }

  return session ? (
    <DashboardPage session={session} onSignOut={handleSignOut} />
  ) : (
    <LoginPage onAuthenticated={handleAuthenticated} />
  )
}
