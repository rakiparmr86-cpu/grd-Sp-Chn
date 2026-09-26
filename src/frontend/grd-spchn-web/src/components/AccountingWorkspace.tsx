import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import {
  ApiError,
  api,
  type AccountingInvoiceCandidate,
  type AccountingJournalEntry,
  type AccountingPayable,
  type AccountingPayableDetail,
  type CatalogItem,
  type LoginResponse,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { ScreenName } from './ScreenName'
import { ScreenTabs } from './ScreenTabs'
import { SCREEN } from '../config/screens'

interface AccountingWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

type PayableFilter = 'all' | 'outstanding' | 'PendingApproval' | 'Approved' | 'Paid'

const filterLabels: Record<PayableFilter, string> = {
  all: 'All payables',
  outstanding: 'Outstanding',
  PendingApproval: 'Awaiting approval',
  Approved: 'Ready for payment',
  Paid: 'Paid',
}

const today = () => new Date().toISOString().slice(0, 10)
const thirtyDaysFromNow = () => {
  const date = new Date()
  date.setDate(date.getDate() + 30)
  return date.toISOString().slice(0, 10)
}

const toUtc = (value: string) => new Date(`${value}T00:00:00.000Z`).toISOString()
const money = (value: number, currency = 'INR') =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency }).format(value)
const shortDate = (value: string) => new Intl.DateTimeFormat('en-IN', {
  day: '2-digit', month: 'short', year: 'numeric',
}).format(new Date(value))
const statusLabel = (status: AccountingPayable['status']) =>
  status === 'PendingApproval' ? 'Pending approval' : status

export function AccountingWorkspace({ session, onBack }: AccountingWorkspaceProps) {
  const canCreateInvoice = hasPermission(session, 'accounting.invoice.create')
  const canApprove = hasPermission(session, 'accounting.payable.approve')
  const canReleasePayment = hasPermission(session, 'accounting.payment.release')
  const [candidates, setCandidates] = useState<AccountingInvoiceCandidate[]>([])
  const [payables, setPayables] = useState<AccountingPayable[]>([])
  const [journals, setJournals] = useState<AccountingJournalEntry[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [invoiceCandidate, setInvoiceCandidate] = useState<AccountingInvoiceCandidate | null>(null)
  const [paymentPayables, setPaymentPayables] = useState<AccountingPayable[] | null>(null)
  const [detailPayableId, setDetailPayableId] = useState<string | null>(null)
  const [filter, setFilter] = useState<PayableFilter>('outstanding')
  const [activeSection, setActiveSection] = useState<'receipts' | 'payables' | 'ledger'>(
    canCreateInvoice ? 'receipts' : 'payables')
  const [supplierFilter, setSupplierFilter] = useState('all')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set())

  const supplierById = useMemo(
    () => new Map(suppliers.map((supplier) => [supplier.id, supplier])),
    [suppliers],
  )
  const productById = useMemo(
    () => new Map(catalog.map((product) => [product.id, product])),
    [catalog],
  )
  const supplierName = useCallback(
    (id: string) => supplierById.get(id)?.displayName ?? 'Supplier',
    [supplierById],
  )

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [candidateRows, payableRows, journalRows, supplierRows, productRows] = await Promise.all([
        canCreateInvoice ? api.getAccountingInvoiceCandidates(session.accessToken) : Promise.resolve([]),
        api.getAccountingPayables(session.accessToken),
        api.getAccountingJournalEntries(session.accessToken),
        api.getSuppliers(session.accessToken),
        api.getProcurementItems(session.accessToken),
      ])
      setCandidates(candidateRows)
      setPayables(payableRows)
      setJournals(journalRows)
      setSuppliers(supplierRows)
      setCatalog(productRows)
      setSelectedIds(new Set())
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load the Accounting workspace.')
    } finally {
      setLoading(false)
    }
  }, [canCreateInvoice, session.accessToken])

  useEffect(() => { void load() }, [load])

  const outstanding = payables
    .filter((payable) => payable.status !== 'Paid')
    .reduce((sum, payable) => sum + payable.totalAmount, 0)

  // A payable is selectable only when this user can act on it right now.
  const canActOn = useCallback((payable: AccountingPayable) =>
    (payable.status === 'PendingApproval' && canApprove && payable.createdByUserId !== session.userId) ||
    (payable.status === 'Approved' && canReleasePayment),
  [canApprove, canReleasePayment, session.userId])

  const visiblePayables = useMemo(() => payables.filter((payable) => {
    if (supplierFilter !== 'all' && payable.supplierId !== supplierFilter) return false
    if (filter === 'all') return true
    if (filter === 'outstanding') return payable.status !== 'Paid'
    return payable.status === filter
  }), [filter, payables, supplierFilter])

  const selected = useMemo(
    () => payables.filter((payable) => selectedIds.has(payable.id)),
    [payables, selectedIds],
  )
  const selectedTotal = selected.reduce((sum, payable) => sum + payable.totalAmount, 0)
  const selectionStatus = selected.length > 0 && selected.every((row) => row.status === selected[0].status)
    ? selected[0].status
    : null
  const selectionSupplierIds = new Set(selected.map((row) => row.supplierId))
  const selectionCurrencies = new Set(selected.map((row) => row.currency))
  const canApproveSelection = selectionStatus === 'PendingApproval' && canApprove
  const canPaySelection = selectionStatus === 'Approved' && canReleasePayment &&
    selectionSupplierIds.size === 1 && selectionCurrencies.size === 1
  const selectableVisible = visiblePayables.filter(canActOn)
  const allVisibleSelected = selectableVisible.length > 0 &&
    selectableVisible.every((payable) => selectedIds.has(payable.id))

  function toggle(payable: AccountingPayable) {
    setSelectedIds((current) => {
      const next = new Set(current)
      if (next.has(payable.id)) next.delete(payable.id)
      else next.add(payable.id)
      return next
    })
  }

  function toggleAllVisible() {
    setSelectedIds(allVisibleSelected ? new Set() : new Set(selectableVisible.map((payable) => payable.id)))
  }

  function applyFilter(next: PayableFilter) {
    setFilter(next)
    setSelectedIds(new Set())
  }

  async function approve(rows: AccountingPayable[]) {
    setBusy(true)
    setError(null)
    setNotice(null)
    try {
      const approved = await api.approveVendorPayables(session.accessToken, rows.map((row) => row.id))
      setNotice(approved.length === 1
        ? `${approved[0].supplierInvoiceNumber} approved and ready for payment.`
        : `${approved.length} payables approved and ready for payment.`)
      setDetailPayableId(null)
      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not approve the payable.')
    } finally {
      setBusy(false)
    }
  }

  const summaryCards: { label: string; value: string; note: string; tone: string; target: PayableFilter | 'receipts' }[] = [
    { label: 'Accepted GRNs', value: String(candidates.filter(hasRemaining).length), note: canCreateInvoice ? 'Click to enter invoices' : 'Available for invoice entry', tone: 'blue', target: 'receipts' },
    { label: 'Awaiting approval', value: String(payables.filter((row) => row.status === 'PendingApproval').length), note: canApprove ? 'Click to approve in batch' : 'Creator cannot self-approve', tone: 'amber', target: 'PendingApproval' },
    { label: 'Ready for payment', value: String(payables.filter((row) => row.status === 'Approved').length), note: canReleasePayment ? 'Click to pay in batch' : 'Record bank reference after execution', tone: 'green', target: 'Approved' },
    { label: 'Outstanding payable', value: money(outstanding), note: 'Approved + pending invoices', tone: 'navy', target: 'outstanding' },
  ]

  // A summary card opens the matching tab (and status filter) instead of scrolling.
  function openSummary(target: PayableFilter | 'receipts') {
    if (target === 'receipts') {
      setActiveSection('receipts')
      return
    }
    applyFilter(target)
    setActiveSection('payables')
  }

  return (
    <section className="accounting-workspace">
      <div className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1><ScreenName id={SCREEN.purchasePayment} /></h1>
          <p>Match accepted material to the PO, approve the party payable, and record bank payment.</p>
        </div>
        <button className="workspace-refresh" type="button" onClick={() => void load()} disabled={loading}>
          ↻ Refresh
        </button>
      </div>

      {error && <div className="form-alert form-alert--error accounting-alert">{error}</div>}
      {notice && <div className="success-alert accounting-alert" role="status">{notice}</div>}

      <div className="accounting-summary">
        {summaryCards.map((card) => (
          <SummaryCard
            key={card.label}
            {...card}
            active={card.target === filter}
            disabled={card.target === 'receipts' && !canCreateInvoice}
            onClick={() => openSummary(card.target)}
          />
        ))}
      </div>

      <ScreenTabs
        idPrefix="accounting"
        label="Purchase payment sections"
        active={activeSection}
        onChange={setActiveSection}
        tabs={[
          ...(canCreateInvoice
            ? [{ id: 'receipts' as const, label: 'Quality-approved receipts', count: candidates.filter(hasRemaining).length }]
            : []),
          { id: 'payables', label: 'Vendor payable register', count: payables.length },
          { id: 'ledger', label: 'Stock and vendor ledger', count: journals.length },
        ]}
      />

      <div id="accounting-panel" role="tabpanel" aria-labelledby={`accounting-tab-${activeSection}`}>
      {canCreateInvoice && activeSection === 'receipts' && (
        <section className="accounting-card" id="accounting-receipts">
          <CardHeading title="Quality-approved receipts" subtitle="Only accepted quantity can become a supplier invoice." />
          {loading ? <EmptyState text="Loading accepted receipts…" /> : candidates.length === 0 ? (
            <EmptyState text="No quality-approved GRN has reached Accounting yet." />
          ) : (
            <div className="accounting-table-scroll">
              <table className="accounting-table">
                <thead><tr><th>PO / GRN</th><th>Supplier</th><th>Accepted material</th><th>Accrual entry</th><th>Action</th></tr></thead>
                <tbody>
                  {candidates.map((candidate) => {
                    const available = hasRemaining(candidate)
                    return (
                      <tr key={candidate.goodsReceiptId}>
                        <td><strong>{candidate.purchaseOrderNumber}</strong><small>{candidate.goodsReceiptNumber} · {shortDate(candidate.acceptedOnUtc)}</small></td>
                        <td><strong>{supplierName(candidate.supplierId)}</strong><small>{candidate.supplierId}</small></td>
                        <td className="accounting-lines-cell">{candidate.items.map((item) => (
                          <span key={item.productId}>
                            <strong>{productById.get(item.productId)?.name ?? item.productId}</strong>
                            <small>{item.remainingQuantity} / {item.acceptedQuantity} {item.unitOfMeasure} remaining · PO rate {money(item.purchaseOrderUnitPrice, candidate.currency)}</small>
                          </span>
                        ))}</td>
                        <td><span className="journal-chip"><b>Dr</b> Inventory<br /><b>Cr</b> GRNI</span></td>
                        <td><button className="table-action-button table-action-button--primary" type="button" disabled={!available} onClick={() => setInvoiceCandidate(candidate)}>{available ? 'Enter invoice' : 'Fully invoiced'}</button></td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {activeSection === 'payables' && (
      <section className="accounting-card" id="accounting-payables">
        <div className="accounting-card__heading">
          <div><h2>Vendor payable register</h2><p>Click a row for full detail. Tick rows to approve or pay them together.</p></div>
          <div className="payable-filters">
            <div className="payable-filter-tabs" role="tablist" aria-label="Payable status">
              {(Object.keys(filterLabels) as PayableFilter[]).map((key) => (
                <button key={key} type="button" role="tab" aria-selected={filter === key}
                  className={filter === key ? 'is-active' : ''} onClick={() => applyFilter(key)}>
                  {filterLabels[key]}
                </button>
              ))}
            </div>
            <select value={supplierFilter} aria-label="Supplier"
              onChange={(event) => { setSupplierFilter(event.target.value); setSelectedIds(new Set()) }}>
              <option value="all">All suppliers</option>
              {[...new Set(payables.map((payable) => payable.supplierId))].map((id) => (
                <option key={id} value={id}>{supplierName(id)}</option>
              ))}
            </select>
          </div>
        </div>

        {selected.length > 0 && (
          <div className="batch-bar" role="region" aria-label="Selected payables">
            <div>
              <strong>{selected.length} selected · {money(selectedTotal, selected[0].currency)}</strong>
              <small>
                {selectionStatus === null
                  ? 'Select payables with the same status to act on them together.'
                  : selectionStatus === 'Approved' && selectionSupplierIds.size > 1
                    ? 'A payment batch must be for one supplier. Filter by supplier first.'
                    : `${[...selectionSupplierIds].map(supplierName).join(', ')}`}
              </small>
            </div>
            <div className="batch-bar__actions">
              <button className="secondary-button" type="button" onClick={() => setSelectedIds(new Set())}>Clear</button>
              {canApproveSelection && (
                <button className="primary-button" type="button" disabled={busy} onClick={() => void approve(selected)}>
                  {busy ? 'Approving…' : `Approve ${selected.length}`}
                </button>
              )}
              {selectionStatus === 'Approved' && canReleasePayment && (
                <button className="primary-button" type="button" disabled={!canPaySelection} onClick={() => setPaymentPayables(selected)}>
                  Pay {selected.length} in one batch
                </button>
              )}
            </div>
          </div>
        )}

        {loading ? <EmptyState text="Loading vendor payables…" /> : visiblePayables.length === 0 ? (
          <EmptyState text={payables.length === 0 ? 'No vendor invoice has been recorded.' : `No payables in “${filterLabels[filter]}”.`} />
        ) : (
          <div className="accounting-table-scroll">
            <table className="accounting-table accounting-table--payables">
              <thead><tr>
                <th className="select-cell">
                  <input type="checkbox" aria-label="Select all actionable payables" checked={allVisibleSelected}
                    disabled={selectableVisible.length === 0} onChange={toggleAllVisible} />
                </th>
                <th>Invoice</th><th>Supplier / source</th><th>Amount</th><th>Status</th><th>Accounting entry</th><th>Action</th>
              </tr></thead>
              <tbody>
                {visiblePayables.map((payable) => {
                  const actionable = canActOn(payable)
                  return (
                    <tr key={payable.id} className={`is-clickable${selectedIds.has(payable.id) ? ' is-selected' : ''}`}
                      onClick={() => setDetailPayableId(payable.id)}>
                      <td className="select-cell" onClick={(event) => event.stopPropagation()}>
                        <input type="checkbox" aria-label={`Select ${payable.supplierInvoiceNumber}`}
                          checked={selectedIds.has(payable.id)} disabled={!actionable} onChange={() => toggle(payable)} />
                      </td>
                      <td><strong>{payable.supplierInvoiceNumber}</strong><small>{payable.invoiceNumber} · due {shortDate(payable.dueDateUtc)}</small></td>
                      <td><strong>{supplierName(payable.supplierId)}</strong><small>PO {payable.purchaseOrderId.slice(0, 8)} · GRN {payable.goodsReceiptId.slice(0, 8)}</small></td>
                      <td><strong>{money(payable.totalAmount, payable.currency)}</strong><small>Tax {money(payable.taxAmount, payable.currency)}</small></td>
                      <td><span className={`accounting-status accounting-status--${payable.status.toLowerCase()}`}>{statusLabel(payable.status)}</span>{payable.bankReference && <small>{payable.bankReference}</small>}</td>
                      <td><span className="journal-chip">{payable.status === 'Paid' ? <><b>Dr</b> Vendor payable<br /><b>Cr</b> Bank</> : <><b>Dr</b> GRNI / Tax<br /><b>Cr</b> Vendor payable</>}</span></td>
                      <td className="accounting-actions" onClick={(event) => event.stopPropagation()}>
                        {payable.status === 'PendingApproval' && actionable && <button type="button" disabled={busy} onClick={() => void approve([payable])}>Approve</button>}
                        {payable.status === 'Approved' && actionable && <button className="is-primary" type="button" onClick={() => setPaymentPayables([payable])}>Record payment</button>}
                        {payable.status === 'Paid' && <span>Complete</span>}
                        {payable.status === 'PendingApproval' && !actionable && <span>{canApprove ? 'Another approver needed' : 'Waiting for Finance'}</span>}
                        {payable.status === 'Approved' && !actionable && <span>Approved</span>}
                        <button className="is-link" type="button" onClick={() => setDetailPayableId(payable.id)}>Details</button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
      )}

      {activeSection === 'ledger' && (
      <section className="accounting-card">
        <CardHeading title="Stock and vendor ledger" subtitle="System-generated balanced entries; users cannot edit debit or credit lines." />
        {journals.length === 0 ? <EmptyState text="No accounting journal entry is available." /> : (
          <div className="journal-list">
            {journals.slice(0, 12).map((journal) => <JournalRow key={journal.id} journal={journal} />)}
          </div>
        )}
      </section>
      )}
      </div>

      {invoiceCandidate && (
        <InvoiceDrawer
          candidate={invoiceCandidate}
          supplierName={supplierName(invoiceCandidate.supplierId)}
          productName={(id) => productById.get(id)?.name ?? id}
          accessToken={session.accessToken}
          onClose={() => setInvoiceCandidate(null)}
          onSaved={async () => { setInvoiceCandidate(null); await load() }}
        />
      )}
      {detailPayableId && (
        <PayableDetailDrawer
          payableId={detailPayableId}
          accessToken={session.accessToken}
          supplierName={supplierName}
          productName={(id) => productById.get(id)?.name ?? id}
          canAct={canActOn}
          busy={busy}
          onApprove={(payable) => void approve([payable])}
          onPay={(payable) => { setDetailPayableId(null); setPaymentPayables([payable]) }}
          onClose={() => setDetailPayableId(null)}
        />
      )}
      {paymentPayables && (
        <PaymentDrawer
          payables={paymentPayables}
          supplierName={supplierName(paymentPayables[0].supplierId)}
          accessToken={session.accessToken}
          onClose={() => setPaymentPayables(null)}
          onSaved={async (message) => { setPaymentPayables(null); setNotice(message); await load() }}
        />
      )}
    </section>
  )
}

function hasRemaining(candidate: AccountingInvoiceCandidate) {
  return candidate.items.some((item) => item.remainingQuantity > 0)
}

function SummaryCard({ label, value, note, tone, active, disabled, onClick }: {
  label: string
  value: string
  note: string
  tone: string
  active: boolean
  disabled: boolean
  onClick: () => void
}) {
  return (
    // Compact strip: the hint is a tooltip so each card stays one line high.
    <button type="button" disabled={disabled} onClick={onClick} aria-pressed={active}
      title={note} aria-label={`${label}: ${value}. ${note}`}
      className={`accounting-summary__card accounting-summary__card--${tone}${active ? ' is-active' : ''}`}>
      <span>{label}</span><strong>{value}</strong>
    </button>
  )
}

function CardHeading({ title, subtitle }: { title: string; subtitle: string }) {
  return <div className="accounting-card__heading"><div><h2>{title}</h2><p>{subtitle}</p></div></div>
}

function EmptyState({ text }: { text: string }) {
  return <div className="accounting-empty">{text}</div>
}

function JournalRow({ journal }: { journal: AccountingJournalEntry }) {
  return (
    <article>
      <span className="journal-list__icon">JE</span>
      <div><strong>{journal.entryNumber}</strong><small>{journal.description}</small></div>
      <span><small>{journal.entryType}</small><strong>{money(journal.debitTotal, journal.currency)}</strong></span>
      <time>{shortDate(journal.postedOnUtc)}</time>
    </article>
  )
}

function PayableDetailDrawer({ payableId, accessToken, supplierName, productName, canAct, busy, onApprove, onPay, onClose }: {
  payableId: string
  accessToken: string
  supplierName: (id: string) => string
  productName: (id: string) => string
  canAct: (payable: AccountingPayable) => boolean
  busy: boolean
  onApprove: (payable: AccountingPayable) => void
  onPay: (payable: AccountingPayable) => void
  onClose: () => void
}) {
  const [detail, setDetail] = useState<AccountingPayableDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    setDetail(null)
    setError(null)
    api.getAccountingPayableDetail(accessToken, payableId)
      .then((result) => { if (active) setDetail(result) })
      .catch((caught: unknown) => {
        if (active) setError(caught instanceof ApiError ? caught.message : 'Could not load the payable detail.')
      })
    return () => { active = false }
  }, [accessToken, payableId])

  const payable = detail?.payable
  const currency = payable?.currency ?? 'INR'

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside className="user-drawer accounting-drawer payable-detail-drawer" role="dialog" aria-modal="true"
        aria-label="Payable detail" onMouseDown={(event) => event.stopPropagation()}>
        <div className="drawer-header">
          <div>
            <span className="eyebrow">Vendor payable</span>
            <h2><ScreenName id={SCREEN.outstandingPayable} /></h2>
            <p>{payable ? `${payable.supplierInvoiceNumber} · ${supplierName(payable.supplierId)} · ${payable.invoiceNumber}` : 'Loading…'}</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </div>

        {error && <div className="form-alert form-alert--error">{error}</div>}
        {!detail && !error && <div className="drawer-loading"><span className="spinner spinner--dark" /> Loading payable…</div>}

        {detail && payable && (
          <div className="payable-detail">
            <div className="payable-detail__facts">
              <Fact label="Status" value={<span className={`accounting-status accounting-status--${payable.status.toLowerCase()}`}>{statusLabel(payable.status)}</span>} />
              <Fact label="Total payable" value={money(payable.totalAmount, currency)} note={`Subtotal ${money(payable.subtotal, currency)} + tax ${money(payable.taxAmount, currency)}`} />
              <Fact label="Purchase order" value={detail.purchaseOrderNumber ?? payable.purchaseOrderId.slice(0, 8)} />
              <Fact label="Goods receipt" value={detail.goodsReceiptNumber ?? payable.goodsReceiptId.slice(0, 8)}
                note={detail.acceptedOnUtc ? `Quality accepted ${shortDate(detail.acceptedOnUtc)}` : undefined} />
              <Fact label="Invoice date" value={shortDate(payable.invoiceDateUtc)} />
              <Fact label="Due date" value={shortDate(payable.dueDateUtc)} />
            </div>

            <div className="payable-detail__section">
              <h3>Three-way match</h3>
              <div className="accounting-table-scroll">
                <table className="accounting-table payable-detail__lines">
                  <thead><tr><th>Material</th><th>PO qty</th><th>Accepted</th><th>Invoiced</th><th>PO rate</th><th>Invoice rate</th><th>Amount</th></tr></thead>
                  <tbody>
                    {detail.lines.map((line) => (
                      <tr key={line.productId}>
                        <td><strong>{productName(line.productId)}</strong></td>
                        <td>{line.purchaseOrderQuantity ?? '—'} {line.unitOfMeasure}</td>
                        <td>{line.acceptedQuantity ?? '—'} {line.unitOfMeasure}</td>
                        <td><strong>{line.invoicedQuantity} {line.unitOfMeasure}</strong></td>
                        <td>{line.purchaseOrderUnitPrice === null ? '—' : money(line.purchaseOrderUnitPrice, currency)}</td>
                        <td>{money(line.invoiceUnitPrice, currency)}</td>
                        <td><strong>{money(line.lineAmount, currency)}</strong></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="payable-detail__section">
              <h3>Approval and payment</h3>
              <ol className="payable-timeline">
                <li className="is-done"><strong>Invoice recorded</strong><small>{shortDate(payable.createdOnUtc)}</small></li>
                <li className={payable.approvedOnUtc ? 'is-done' : ''}><strong>Approved</strong><small>{payable.approvedOnUtc ? shortDate(payable.approvedOnUtc) : 'Waiting for an approver other than the invoice creator'}</small></li>
                <li className={payable.paidOnUtc ? 'is-done' : ''}>
                  <strong>Paid</strong>
                  <small>{detail.paymentBatch
                    ? `${detail.paymentBatch.bankReference} · ${shortDate(detail.paymentBatch.paidOnUtc)} · batch ${detail.paymentBatch.batchNumber} (${detail.paymentBatch.payableCount} invoice${detail.paymentBatch.payableCount === 1 ? '' : 's'}, ${money(detail.paymentBatch.totalAmount, currency)})`
                    : payable.paidOnUtc ? `${payable.bankReference ?? ''} · ${shortDate(payable.paidOnUtc)}` : 'Not released'}</small>
                </li>
              </ol>
            </div>

            <div className="payable-detail__section">
              <h3>Ledger entries</h3>
              {detail.journalEntries.length === 0 ? <EmptyState text="No journal entry is linked yet." /> : (
                <div className="journal-list">{detail.journalEntries.map((journal) => <JournalRow key={journal.id} journal={journal} />)}</div>
              )}
            </div>

            {canAct(payable) && (
              <div className="drawer-actions">
                <button className="secondary-button" type="button" onClick={onClose}>Close</button>
                {payable.status === 'PendingApproval'
                  ? <button className="primary-button" type="button" disabled={busy} onClick={() => onApprove(payable)}>{busy ? 'Approving…' : 'Approve payable'}</button>
                  : <button className="primary-button" type="button" onClick={() => onPay(payable)}>Record payment</button>}
              </div>
            )}
          </div>
        )}
      </aside>
    </div>
  )
}

function Fact({ label, value, note }: { label: string; value: ReactNode; note?: string }) {
  return <div className="payable-fact"><span>{label}</span><strong>{value}</strong>{note && <small>{note}</small>}</div>
}

function InvoiceDrawer({ candidate, supplierName, productName, accessToken, onClose, onSaved }: {
  candidate: AccountingInvoiceCandidate
  supplierName: string
  productName: (id: string) => string
  accessToken: string
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(today())
  const [dueDate, setDueDate] = useState(thirtyDaysFromNow())
  const [taxAmount, setTaxAmount] = useState('0')
  const [quantities, setQuantities] = useState<Record<string, string>>(() =>
    Object.fromEntries(candidate.items.map((item) => [item.productId, String(item.remainingQuantity)])),
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const subtotal = candidate.items.reduce(
    (sum, item) => sum + (Number(quantities[item.productId]) || 0) * item.purchaseOrderUnitPrice,
    0,
  )
  const total = subtotal + (Number(taxAmount) || 0)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createVendorInvoice(accessToken, {
        purchaseOrderId: candidate.purchaseOrderId,
        goodsReceiptId: candidate.goodsReceiptId,
        supplierInvoiceNumber,
        invoiceDateUtc: toUtc(invoiceDate),
        dueDateUtc: toUtc(dueDate),
        taxAmount: Number(taxAmount),
        lines: candidate.items
          .map((item) => ({ productId: item.productId, quantity: Number(quantities[item.productId]), unitPrice: item.purchaseOrderUnitPrice }))
          .filter((line) => line.quantity > 0),
      })
      await onSaved()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not record the supplier invoice.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation">
      <aside className="user-drawer accounting-drawer" role="dialog" aria-modal="true" aria-label="Enter supplier invoice">
        <div className="drawer-header"><div><span className="eyebrow">Accounting</span><h2><ScreenName id={SCREEN.purchaseInvoice} /></h2><p>PO + accepted GRN + supplier invoice three-way match.</p></div><button className="close-button" type="button" onClick={onClose}>×</button></div>
        <form className="accounting-form" onSubmit={(event) => void submit(event)}>
          <div className="accounting-source"><span>Matched source</span><strong>{candidate.purchaseOrderNumber} · {candidate.goodsReceiptNumber}</strong><small>{supplierName}</small></div>
          <div className="accounting-form-grid">
            <label className="field"><span>Supplier invoice number *</span><input value={supplierInvoiceNumber} onChange={(event) => setSupplierInvoiceNumber(event.target.value)} required /></label>
            <label className="field"><span>Invoice date *</span><input type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} required /></label>
            <label className="field"><span>Due date *</span><input type="date" value={dueDate} min={invoiceDate} onChange={(event) => setDueDate(event.target.value)} required /></label>
            <label className="field"><span>Tax amount ({candidate.currency})</span><input type="number" min="0" step="0.01" value={taxAmount} onChange={(event) => setTaxAmount(event.target.value)} required /></label>
          </div>
          <div className="invoice-match-lines">
            <div className="invoice-match-lines__header"><strong>Accepted material</strong><span>Invoice cannot exceed quality-approved quantity or PO rate.</span></div>
            {candidate.items.map((item) => <div className="invoice-match-line" key={item.productId}>
              <div><strong>{productName(item.productId)}</strong><small>Accepted {item.acceptedQuantity} · already invoiced {item.alreadyInvoicedQuantity} {item.unitOfMeasure}</small></div>
              <label><span>Invoice quantity</span><input type="number" min="0" max={item.remainingQuantity} step="0.001" value={quantities[item.productId]} onChange={(event) => setQuantities((current) => ({ ...current, [item.productId]: event.target.value }))} /></label>
              <div><span>PO rate</span><strong>{money(item.purchaseOrderUnitPrice, candidate.currency)}</strong></div>
            </div>)}
          </div>
          <div className="accounting-entry-preview"><span>Automatic invoice posting</span><div><strong>GRNI / Input Tax</strong><b>Debit {money(total, candidate.currency)}</b></div><div><strong>Vendor payable ({supplierName})</strong><b>Credit {money(total, candidate.currency)}</b></div></div>
          {error && <div className="form-alert form-alert--error">{error}</div>}
          <div className="drawer-actions"><button className="secondary-button" type="button" onClick={onClose}>Cancel</button><button className="primary-button" type="submit" disabled={saving || subtotal <= 0}>{saving ? 'Recording…' : 'Record invoice'}</button></div>
        </form>
      </aside>
    </div>
  )
}

function PaymentDrawer({ payables, supplierName, accessToken, onClose, onSaved }: {
  payables: AccountingPayable[]
  supplierName: string
  accessToken: string
  onClose: () => void
  onSaved: (message: string) => Promise<void>
}) {
  const [bankReference, setBankReference] = useState('')
  const [paidOn, setPaidOn] = useState(today())
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const currency = payables[0].currency
  const total = payables.reduce((sum, payable) => sum + payable.totalAmount, 0)
  const isBatch = payables.length > 1

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const batch = await api.recordVendorPaymentBatch(
        accessToken,
        payables.map((payable) => payable.id),
        bankReference,
        toUtc(paidOn),
      )
      await onSaved(isBatch
        ? `Payment batch ${batch.batchNumber} recorded: ${batch.payables.length} invoices, ${money(batch.totalAmount, batch.currency)} (${batch.bankReference}).`
        : `Payment ${batch.bankReference} recorded for ${payables[0].supplierInvoiceNumber}.`)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not record the vendor payment.')
    } finally {
      setSaving(false)
    }
  }

  return <div className="drawer-backdrop" role="presentation">
    <aside className="user-drawer accounting-drawer" role="dialog" aria-modal="true" aria-label="Record vendor payment">
      <div className="drawer-header"><div><span className="eyebrow">Accounts payable</span><h2><ScreenName id={SCREEN.paymentEntry} /></h2><p>Use the reference returned by the bank after payment is actually executed.</p></div><button className="close-button" type="button" onClick={onClose}>×</button></div>
      <form className="accounting-form" onSubmit={(event) => void submit(event)}>
        <div className="payment-party"><span>Party account</span><strong>{supplierName}</strong><small>{isBatch ? `${payables.length} approved invoices · ${money(total, currency)}` : `${payables[0].supplierInvoiceNumber} · ${money(total, currency)}`}</small></div>
        {isBatch && (
          <div className="batch-invoice-list">
            {payables.map((payable) => (
              <div key={payable.id}>
                <span><strong>{payable.supplierInvoiceNumber}</strong><small>{payable.invoiceNumber} · due {shortDate(payable.dueDateUtc)}</small></span>
                <b>{money(payable.totalAmount, payable.currency)}</b>
              </div>
            ))}
            <div className="batch-invoice-list__total"><span>Batch total</span><b>{money(total, currency)}</b></div>
          </div>
        )}
        <label className="field"><span>Bank transaction / UTR reference *</span><input value={bankReference} onChange={(event) => setBankReference(event.target.value)} placeholder="Example: UTR-HDFC-20260918-001" required /></label>
        <label className="field"><span>Payment date *</span><input type="date" max={today()} value={paidOn} onChange={(event) => setPaidOn(event.target.value)} required /></label>
        <div className="accounting-entry-preview accounting-entry-preview--payment"><span>Payment journal preview</span><div><strong>Vendor payable — {supplierName}</strong><b>Debit {money(total, currency)}</b></div><div><strong>Bank</strong><b>Credit {money(total, currency)}</b></div><small>{isBatch ? 'One bank transfer settles every invoice above and posts one balanced entry. ' : ''}This screen records a completed external payment; it does not transfer money from the bank.</small></div>
        {error && <div className="form-alert form-alert--error">{error}</div>}
        <div className="drawer-actions"><button className="secondary-button" type="button" onClick={onClose}>Cancel</button><button className="primary-button" type="submit" disabled={saving}>{saving ? 'Recording…' : isBatch ? `Record batch payment (${payables.length})` : 'Record payment'}</button></div>
      </form>
    </aside>
  </div>
}
