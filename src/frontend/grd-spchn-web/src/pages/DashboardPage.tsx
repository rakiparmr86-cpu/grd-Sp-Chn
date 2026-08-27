import { useEffect, useState } from 'react'
import type { LoginResponse } from '../api'
import { hasPermission } from '../auth'
import { Brand, GridIcon } from '../components/Brand'
import { CreateUserPanel } from '../components/CreateUserPanel'

interface DashboardPageProps {
  session: LoginResponse
  onSignOut: () => void
}

const navItems = ['Dashboard', 'Procurement', 'Inventory', 'Warehouse', 'Organization', 'Reports']

const processSteps = [
  { number: '1', title: 'Material request', detail: 'Store raises requirement' },
  { number: '2', title: 'Purchase control', detail: 'Review, approve and source' },
  { number: '3', title: 'Vendor fulfilment', detail: 'Purchase order dispatched' },
  { number: '4', title: 'Receive & stock', detail: 'GRN updates inventory' },
]

const permissionActions = [
  { permission: 'procurement.material-request.create', title: 'Create material request', detail: 'Raise a store or consumption-unit requirement', tone: 'blue' },
  { permission: 'procurement.purchase-order.create', title: 'Create purchase order', detail: 'Convert an approved request into a vendor PO', tone: 'violet' },
  { permission: 'warehouse.goods-receipt.post', title: 'Post goods receipt', detail: 'Receive vendor material against a purchase order', tone: 'amber' },
  { permission: 'inventory.stock.read', title: 'View location stock', detail: 'Check on-hand inventory by organization unit', tone: 'green' },
]

export function DashboardPage({ session, onSignOut }: DashboardPageProps) {
  const [createUserOpen, setCreateUserOpen] = useState(false)
  const [now, setNow] = useState(() => new Date())
  const canCreateUsers = hasPermission(session, 'identity.user.create')
  const actions = permissionActions.filter((action) => hasPermission(session, action.permission))

  useEffect(() => {
    const interval = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(interval)
  }, [])

  const dateLabel = new Intl.DateTimeFormat('en-IN', {
    weekday: 'short',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(now)
  const timeLabel = new Intl.DateTimeFormat('en-IN', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(now)
  const firstName = session.userName.split(/[.@]/)[0]

  return (
    <div className="dashboard-page">
      <div className="utility-rail"><GridIcon /></div>
      <header className="app-header">
        <Brand compact inverse />
        <nav className="main-nav" aria-label="Primary navigation">
          {navItems.map((item, index) => (
            <button key={item} className={index === 0 ? 'is-active' : ''} type="button">
              <span className="nav-dot" />{item}
            </button>
          ))}
        </nav>
        <div className="header-actions">
          <span className="environment-badge">Local</span>
          <button className="profile-menu" type="button">
            <span>{session.userName.slice(0, 1).toUpperCase()}</span>
              <span className="profile-menu__copy"><strong>{session.role}</strong><small>{session.userName}</small></span>
          </button>
          <button className="sign-out-button" type="button" onClick={onSignOut}>Sign out</button>
        </div>
      </header>

      <main className="dashboard-main">
        <section className="welcome-strip">
          <div className="welcome-copy">
            <span className="sun-icon">☀</span>
            <div><strong>Good day, {firstName}.</strong><span> Your supply chain command center is ready.</span></div>
          </div>
          <div className="welcome-meta">
            <span>◷ {dateLabel}</span>
            <span>◴ {timeLabel}</span>
          </div>
        </section>

        <section className="dashboard-title-row">
          <div>
            <span className="eyebrow">Enterprise operations</span>
            <h1>Today&apos;s command center</h1>
            <p>Follow the procure-to-receive flow across every branch and manufacturing unit.</p>
          </div>
          <div className="scope-pill">
            <span className="scope-pill__icon">⌖</span>
            <span><small>Organization scope</small><strong>{session.organizationUnitId}</strong></span>
          </div>
        </section>

        <section className="process-strip" aria-label="Procure to receive process">
          {processSteps.map((step, index) => (
            <article key={step.number} className={index === 0 ? 'process-step is-current' : 'process-step'}>
              <span className="process-step__number">{step.number}</span>
              <span><strong>{step.title}</strong><small>{step.detail}</small></span>
            </article>
          ))}
        </section>

        <section className="mode-switch" aria-label="Dashboard view">
          <button className="is-selected" type="button">▣ Command</button>
          <button type="button">⚙ Control</button>
          <button type="button">▥ Insight</button>
        </section>

        <section className="dashboard-grid">
          <div className="action-section">
            <div className="section-heading">
              <div><span className="eyebrow">Your permissions</span><h2>Available actions</h2></div>
              <span>{session.permissions.length} permissions active</span>
            </div>
            <div className="action-grid">
              {actions.map((action) => (
                <article className={`action-card action-card--${action.tone}`} key={action.title}>
                  <span className="action-card__icon">↗</span>
                  <span><strong>{action.title}</strong><small>{action.detail}</small></span>
                  <button type="button" aria-label={`Open ${action.title}`}>→</button>
                </article>
              ))}
              {canCreateUsers && (
                <article className="action-card action-card--navy">
                  <span className="action-card__icon">＋</span>
                  <span><strong>Create employee user</strong><small>HR-controlled role and organization access</small></span>
                  <button type="button" onClick={() => setCreateUserOpen(true)} aria-label="Create employee user">→</button>
                </article>
              )}
              {actions.length === 0 && !canCreateUsers && (
                <div className="empty-card">No command actions are assigned to this profile yet.</div>
              )}
            </div>
          </div>

          <aside className="identity-card">
            <div className="identity-card__top">
              <span className="identity-avatar">{session.userName.slice(0, 1).toUpperCase()}</span>
              <span><small>Signed in as</small><strong>{session.userName}</strong><em>{session.role} · {session.accessProfile}</em></span>
            </div>
            <div className="identity-card__detail">
              <span><small>Session expires</small><strong>{new Date(session.expiresOnUtc).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' })}</strong></span>
              <span><small>API route</small><strong>Gateway :7000</strong></span>
            </div>
            <div className="service-status"><span className="status-dot" /> Identity verified</div>
          </aside>
        </section>
      </main>

      {createUserOpen && (
        <CreateUserPanel accessToken={session.accessToken} onClose={() => setCreateUserOpen(false)} />
      )}
    </div>
  )
}
