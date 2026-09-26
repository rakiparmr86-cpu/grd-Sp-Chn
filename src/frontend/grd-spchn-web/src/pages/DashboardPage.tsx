import { useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import type { LoginResponse } from '../api'
import { hasPermission } from '../auth'
import { AccessProfilePermissionsPanel } from '../components/AccessProfilePermissionsPanel'
import { AccountLedgerWorkspace } from '../components/AccountLedgerWorkspace'
import { AccountingWorkspace } from '../components/AccountingWorkspace'
import { Brand, GridIcon } from '../components/Brand'
import { CreateUserPanel } from '../components/CreateUserPanel'
import {
  BalanceSheetWorkspace,
  ProfitAndLossWorkspace,
  TrialBalanceWorkspace,
} from '../components/FinancialStatements'
import { MaterialRequestWorkspace } from '../components/MaterialRequestWorkspace'
import { PurchaseOrderWorkspace } from '../components/PurchaseOrderWorkspace'
import { ScreenName } from '../components/ScreenName'
import { StockLedgerWorkspace } from '../components/StockLedgerWorkspace'
import { SCREEN } from '../config/screens'

interface DashboardPageProps {
  session: LoginResponse
  onSignOut: () => void
}

type MenuAction =
  | 'create-user'
  | 'manage-permissions'
  | 'create-material-request'
  | 'view-material-requests'
  | 'accounting'
  | 'stock-ledger'
  | 'purchase-orders'
  | 'account-ledger'
  | 'trial-balance'
  | 'profit-and-loss'
  | 'balance-sheet'
type DashboardView =
  | 'dashboard'
  | 'material-request'
  | 'purchase-order'
  | 'accounting'
  | 'stock-ledger'
  | 'account-ledger'
  | 'trial-balance'
  | 'profit-and-loss'
  | 'balance-sheet'
type MenuIconName = 'identity' | 'organization' | 'procurement' | 'inventory' | 'warehouse' | 'accounting'

interface MoreMenuItem {
  permission: string
  label: string
  description: string
  action?: MenuAction
}

interface MoreMenuGroup {
  title: string
  icon: MenuIconName
  items: MoreMenuItem[]
}

const moreMenuGroups: MoreMenuGroup[] = [
  {
    title: 'Accounting',
    icon: 'accounting',
    items: [
      {
        permission: 'accounting.invoice.create',
        label: 'Enter supplier invoice',
        description: 'Match the invoice to PO rates and quality-approved GRN quantity.',
        action: 'accounting',
      },
      {
        permission: 'accounting.payable.read',
        label: 'Vendor payable register',
        description: 'Track party invoices, approvals, due dates, and payment state.',
        action: 'accounting',
      },
      {
        permission: 'accounting.payable.approve',
        label: 'Approve vendor payable',
        description: 'Apply separation of duties before payment release.',
        action: 'accounting',
      },
      {
        permission: 'accounting.payment.release',
        label: 'Record bank payment',
        description: 'Post Party Debit / Bank Credit using the real bank reference.',
        action: 'accounting',
      },
      {
        permission: 'accounting.journal.read',
        label: 'Account and party ledger',
        description: 'Every ledger with date, particulars, debit, credit and balance.',
        action: 'account-ledger',
      },
      {
        permission: 'accounting.journal.read',
        label: 'Trial balance',
        description: 'Debit and credit totals of every account for a period.',
        action: 'trial-balance',
      },
      {
        permission: 'accounting.journal.read',
        label: 'Profit and loss',
        description: 'Income, expenses and net profit or loss for a period.',
        action: 'profit-and-loss',
      },
      {
        permission: 'accounting.journal.read',
        label: 'Balance sheet',
        description: 'Assets against liabilities and capital as at a date.',
        action: 'balance-sheet',
      },
    ],
  },
  {
    title: 'Identity & access',
    icon: 'identity',
    items: [
      {
        permission: 'identity.user.create',
        label: 'Create employee user',
        description: 'Create a user and assign profile and organization scope.',
        action: 'create-user',
      },
      {
        permission: 'identity.access-profile.manage',
        label: 'Manage permissions',
        description: 'Add or remove permissions from access profiles.',
        action: 'manage-permissions',
      },
    ],
  },
  {
    title: 'Organization',
    icon: 'organization',
    items: [
      {
        permission: 'organization.read',
        label: 'View organization',
        description: 'Browse enterprise, region, branch, plant and store hierarchy.',
      },
      {
        permission: 'organization.manage',
        label: 'Manage organization',
        description: 'Maintain organization units and reporting hierarchy.',
      },
    ],
  },
  {
    title: 'Procurement',
    icon: 'procurement',
    items: [
      {
        permission: 'procurement.material-request.create',
        label: 'Create material request',
        description: 'Raise a store or consumption-unit requirement.',
        action: 'create-material-request',
      },
      {
        permission: 'procurement.material-request.read',
        label: 'View material requests',
        description: 'Review requests available in your organization scope.',
        action: 'view-material-requests',
      },
      {
        permission: 'procurement.material-request.approve',
        label: 'Approve material requests',
        description: 'Approve or reject submitted requirements.',
        action: 'view-material-requests',
      },
      {
        permission: 'procurement.purchase-order.create',
        label: 'Create PO.',
        description: 'Create a supplier PO from an approved request.',
        action: 'view-material-requests',
      },
      {
        permission: 'procurement.purchase-order.read',
        label: 'View purchase orders',
        description: 'Track purchase orders in your organization scope.',
        action: 'purchase-orders',
      },
    ],
  },
  {
    title: 'Inventory',
    icon: 'inventory',
    items: [
      {
        permission: 'inventory.stock.read',
        label: 'View location stock',
        description: 'Check on-hand stock for authorized locations.',
      },
      {
        permission: 'inventory.stock.read',
        label: 'Stock ledger',
        description: 'Opening, receipts, issues and running balance by material.',
        action: 'stock-ledger',
      },
    ],
  },
  {
    title: 'Warehouse',
    icon: 'warehouse',
    items: [
      {
        permission: 'warehouse.goods-receipt.read',
        label: 'View goods receipts',
        description: 'Review material received against purchase orders.',
        action: 'view-material-requests',
      },
      {
        permission: 'warehouse.goods-receipt.post',
        label: 'Post goods receipt',
        description: 'Receive vendor material at an authorized location.',
        action: 'view-material-requests',
      },
      {
        permission: 'warehouse.quality-inspection.read',
        label: 'View quality inspections',
        description: 'Review received material and its quality-release status.',
        action: 'view-material-requests',
      },
      {
        permission: 'warehouse.quality-inspection.post',
        label: 'Complete quality test',
        description: 'Pass material into Inventory or reject it into quarantine.',
        action: 'view-material-requests',
      },
    ],
  },
]

function MenuIcon({ name }: { name: MenuIconName }) {
  const paths: Record<MenuIconName, ReactNode> = {
    identity: (
      <>
        <circle cx="12" cy="8" r="3" />
        <path d="M5.5 19c.7-3.4 3-5.2 6.5-5.2s5.8 1.8 6.5 5.2" />
      </>
    ),
    organization: (
      <>
        <rect x="9" y="3" width="6" height="5" rx="1" />
        <rect x="3" y="16" width="6" height="5" rx="1" />
        <rect x="15" y="16" width="6" height="5" rx="1" />
        <path d="M12 8v4M6 16v-4h12v4" />
      </>
    ),
    procurement: (
      <>
        <path d="M4 7h16l-1.4 9H6L4 4H2" />
        <circle cx="8" cy="20" r="1" />
        <circle cx="17" cy="20" r="1" />
      </>
    ),
    inventory: (
      <>
        <path d="m4 8 8-4 8 4-8 4-8-4Z" />
        <path d="m4 8 8 4 8-4v9l-8 4-8-4V8Z" />
        <path d="M12 12v9" />
      </>
    ),
    warehouse: (
      <>
        <path d="M3 10 12 4l9 6v11H3V10Z" />
        <path d="M7 21v-7h10v7M8 10h8" />
      </>
    ),
    accounting: (
      <>
        <path d="M4 7h16M6 7V5h12v2M6 11h12M7 15h3M14 15h3M7 19h3M14 19h3" />
        <path d="M5 7v14h14V7" />
      </>
    ),
  }

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      {paths[name]}
    </svg>
  )
}

export function DashboardPage({ session, onSignOut }: DashboardPageProps) {
  const [createUserOpen, setCreateUserOpen] = useState(false)
  const [permissionEditorOpen, setPermissionEditorOpen] = useState(false)
  const [moreOpen, setMoreOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const [activeView, setActiveView] = useState<DashboardView>('dashboard')
  const [createRequestSignal, setCreateRequestSignal] = useState(0)
  const [now, setNow] = useState(() => new Date())
  const moreMenuRef = useRef<HTMLDivElement>(null)
  const profileMenuRef = useRef<HTMLDivElement>(null)

  const visibleGroups = useMemo(
    () => moreMenuGroups
      .map((group) => ({
        ...group,
        items: group.items.filter((item) => hasPermission(session, item.permission)),
      }))
      .filter((group) => group.items.length > 0),
    [session],
  )

  useEffect(() => {
    const interval = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(interval)
  }, [])

  useEffect(() => {
    function closeMenus(event: MouseEvent) {
      const target = event.target as Node
      if (!moreMenuRef.current?.contains(target)) setMoreOpen(false)
      if (!profileMenuRef.current?.contains(target)) setProfileOpen(false)
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMoreOpen(false)
        setProfileOpen(false)
      }
    }

    document.addEventListener('mousedown', closeMenus)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeMenus)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [])

  const dateLabel = new Intl.DateTimeFormat('en-IN', {
    weekday: 'long',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(now)
  const timeLabel = new Intl.DateTimeFormat('en-IN', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(now)
  const displayName = session.userName.split('@')[0].replaceAll('.', ' ')

  function handleMenuAction(action?: MenuAction) {
    if (!action) return

    setMoreOpen(false)
    if (action === 'create-user') setCreateUserOpen(true)
    if (action === 'manage-permissions') setPermissionEditorOpen(true)
    if (action === 'create-material-request' || action === 'view-material-requests') {
      setActiveView('material-request')
    }
    if (action === 'create-material-request') setCreateRequestSignal((current) => current + 1)
    if (action === 'accounting') setActiveView('accounting')
    if (action === 'stock-ledger') setActiveView('stock-ledger')
    if (action === 'purchase-orders') setActiveView('purchase-order')
    if (action === 'account-ledger') setActiveView('account-ledger')
    if (action === 'trial-balance') setActiveView('trial-balance')
    if (action === 'profit-and-loss') setActiveView('profit-and-loss')
    if (action === 'balance-sheet') setActiveView('balance-sheet')
  }

  return (
    <div className="dashboard-page">
      <div className="utility-rail">
        <GridIcon />
        <span>GRD Supply Chain</span>
      </div>

      <header className="app-header">
        <Brand compact inverse />

        <nav className="main-nav" aria-label="Primary navigation">
          <button
            className={activeView === 'dashboard' ? 'is-active' : ''}
            type="button"
            onClick={() => {
              setActiveView('dashboard')
              setMoreOpen(false)
            }}
          >
            <span className="nav-symbol nav-symbol--dashboard" aria-hidden="true" />
            Dashboard
          </button>

          <div className="more-menu" ref={moreMenuRef}>
            <button
              className={moreOpen || activeView !== 'dashboard'
                ? 'more-menu__trigger is-open'
                : 'more-menu__trigger'}
              type="button"
              aria-haspopup="menu"
              aria-expanded={moreOpen}
              onClick={() => {
                setMoreOpen((open) => !open)
                setProfileOpen(false)
              }}
            >
              <span className="nav-symbol nav-symbol--more" aria-hidden="true" />
              More
              <span className="menu-chevron" aria-hidden="true">⌄</span>
            </button>

            {moreOpen && (
              <div className="more-menu__panel" role="menu" aria-label="Available application functions">
                <div className="more-menu__heading">
                  <div>
                    <strong>Application menu</strong>
                    <span>Functions assigned to {session.accessProfile}</span>
                  </div>
                  <span className="permission-count">{session.permissions.length} permissions</span>
                </div>

                {visibleGroups.length > 0 ? (
                  <div className="more-menu__groups">
                    {visibleGroups.map((group) => (
                      <section className="menu-group" key={group.title}>
                        <div className="menu-group__title">
                          <span className="menu-group__icon"><MenuIcon name={group.icon} /></span>
                          <strong>{group.title}</strong>
                        </div>
                        <div className="menu-group__items">
                          {group.items.map((item) => (
                            <button
                              key={`${item.permission}:${item.label}`}
                              className={item.action ? 'menu-action' : 'menu-action is-planned'}
                              type="button"
                              role="menuitem"
                              aria-disabled={!item.action}
                              onClick={() => handleMenuAction(item.action)}
                            >
                              <span>
                                <strong>{item.label}</strong>
                                <small>{item.description}</small>
                              </span>
                              {item.action ? (
                                <span className="menu-action__arrow" aria-hidden="true">→</span>
                              ) : (
                                <span className="menu-action__status">Later</span>
                              )}
                            </button>
                          ))}
                        </div>
                      </section>
                    ))}
                  </div>
                ) : (
                  <div className="more-menu__empty">No application functions are assigned to this profile.</div>
                )}
              </div>
            )}
          </div>
        </nav>

        <div className="header-actions">
          <span className="environment-badge">Local</span>
          <div className="profile-dropdown" ref={profileMenuRef}>
            <button
              className="profile-menu"
              type="button"
              aria-haspopup="menu"
              aria-expanded={profileOpen}
              onClick={() => {
                setProfileOpen((open) => !open)
                setMoreOpen(false)
              }}
            >
              <span>{session.userName.slice(0, 1).toUpperCase()}</span>
              <span className="profile-menu__copy">
                <strong>{session.role}</strong>
                <small>{session.userName}</small>
              </span>
              <span className="menu-chevron" aria-hidden="true">⌄</span>
            </button>

            {profileOpen && (
              <div className="profile-dropdown__panel" role="menu">
                <div>
                  <strong>{session.userName}</strong>
                  <span>{session.role} · {session.accessProfile}</span>
                </div>
                <button type="button" role="menuitem" onClick={onSignOut}>Sign out</button>
              </div>
            )}
          </div>
        </div>
      </header>

      <section className="dashboard-context" aria-label="Current user context">
        <div className="dashboard-greeting">
          <span className="greeting-icon" aria-hidden="true">◔</span>
          <span><strong>Good day, {displayName}.</strong> Your workspace is ready.</span>
        </div>
        <div className="dashboard-scope">
          <span><small>Role</small><strong>{session.role}</strong></span>
          <span><small>Access</small><strong>{session.accessProfile}</strong></span>
          <span className="dashboard-scope__organization"><small>Organization unit</small><strong>{session.organizationUnitId}</strong></span>
        </div>
        <div className="dashboard-clock">
          <span>▣ {dateLabel}</span>
          <span>◷ {timeLabel}</span>
        </div>
      </section>

      <main className="dashboard-canvas" aria-label="Dashboard content">
        {activeView === 'dashboard' ? (
          <div className="dashboard-home"><h1><ScreenName id={SCREEN.dashboard} /></h1></div>
        ) : activeView === 'material-request' ? (
          <MaterialRequestWorkspace
            session={session}
            createRequestSignal={createRequestSignal}
            onBack={() => setActiveView('dashboard')}
          />
        ) : activeView === 'purchase-order' ? (
          <PurchaseOrderWorkspace
            session={session}
            onBack={() => setActiveView('dashboard')}
          />
        ) : activeView === 'trial-balance' ? (
          <TrialBalanceWorkspace session={session} onBack={() => setActiveView('dashboard')} />
        ) : activeView === 'profit-and-loss' ? (
          <ProfitAndLossWorkspace session={session} onBack={() => setActiveView('dashboard')} />
        ) : activeView === 'balance-sheet' ? (
          <BalanceSheetWorkspace session={session} onBack={() => setActiveView('dashboard')} />
        ) : activeView === 'account-ledger' ? (
          <AccountLedgerWorkspace
            session={session}
            onBack={() => setActiveView('dashboard')}
          />
        ) : activeView === 'stock-ledger' ? (
          <StockLedgerWorkspace
            session={session}
            onBack={() => setActiveView('dashboard')}
          />
        ) : (
          <AccountingWorkspace
            session={session}
            onBack={() => setActiveView('dashboard')}
          />
        )}
      </main>

      {createUserOpen && (
        <CreateUserPanel accessToken={session.accessToken} onClose={() => setCreateUserOpen(false)} />
      )}
      {permissionEditorOpen && (
        <AccessProfilePermissionsPanel
          accessToken={session.accessToken}
          onClose={() => setPermissionEditorOpen(false)}
        />
      )}
    </div>
  )
}
