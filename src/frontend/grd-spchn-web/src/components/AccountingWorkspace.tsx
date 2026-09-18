import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  ApiError,
  api,
  type AccountingInvoiceCandidate,
  type AccountingJournalEntry,
  type AccountingPayable,
  type CatalogItem,
  type LoginResponse,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'

interface AccountingWorkspaceProps {
  session: LoginResponse
  onBack: () => void
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
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [invoiceCandidate, setInvoiceCandidate] = useState<AccountingInvoiceCandidate | null>(null)
  const [paymentPayable, setPaymentPayable] = useState<AccountingPayable | null>(null)

  const supplierById = useMemo(
    () => new Map(suppliers.map((supplier) => [supplier.id, supplier])),
    [suppliers],
  )
  const productById = useMemo(
    () => new Map(catalog.map((product) => [product.id, product])),
    [catalog],
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

  async function approve(payable: AccountingPayable) {
    setBusyId(payable.id)
    setError(null)
    try {
      await api.approveVendorPayable(session.accessToken, payable.id)
      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not approve the payable.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="accounting-workspace">
      <div className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1>GRD Vdr ac</h1>
          <p>Match accepted material to the PO, approve the party payable, and record bank payment.</p>
        </div>
        <button className="workspace-refresh" type="button" onClick={() => void load()} disabled={loading}>
          ↻ Refresh
        </button>
      </div>

      {error && <div className="form-alert form-alert--error accounting-alert">{error}</div>}

      <div className="accounting-summary">
        <SummaryCard label="Accepted GRNs" value={String(candidates.filter(hasRemaining).length)} note="Available for invoice entry" tone="blue" />
        <SummaryCard label="Awaiting approval" value={String(payables.filter((row) => row.status === 'PendingApproval').length)} note="Creator cannot self-approve" tone="amber" />
        <SummaryCard label="Ready for payment" value={String(payables.filter((row) => row.status === 'Approved').length)} note="Record bank reference after execution" tone="green" />
        <SummaryCard label="Outstanding payable" value={money(outstanding)} note="Approved + pending invoices" tone="navy" />
      </div>

      {canCreateInvoice && (
        <section className="accounting-card">
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
                        <td><strong>{supplierById.get(candidate.supplierId)?.displayName ?? 'Supplier'}</strong><small>{candidate.supplierId}</small></td>
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

      <section className="accounting-card">
        <CardHeading title="Vendor payable register" subtitle="Three-way matched invoices, approval, and payment status." />
        {loading ? <EmptyState text="Loading vendor payables…" /> : payables.length === 0 ? (
          <EmptyState text="No vendor invoice has been recorded." />
        ) : (
          <div className="accounting-table-scroll">
            <table className="accounting-table accounting-table--payables">
              <thead><tr><th>Invoice</th><th>Supplier / source</th><th>Amount</th><th>Status</th><th>Accounting entry</th><th>Action</th></tr></thead>
              <tbody>
                {payables.map((payable) => (
                  <tr key={payable.id}>
                    <td><strong>{payable.supplierInvoiceNumber}</strong><small>{payable.invoiceNumber} · due {shortDate(payable.dueDateUtc)}</small></td>
                    <td><strong>{supplierById.get(payable.supplierId)?.displayName ?? 'Supplier'}</strong><small>PO {payable.purchaseOrderId.slice(0, 8)} · GRN {payable.goodsReceiptId.slice(0, 8)}</small></td>
                    <td><strong>{money(payable.totalAmount, payable.currency)}</strong><small>Tax {money(payable.taxAmount, payable.currency)}</small></td>
                    <td><span className={`accounting-status accounting-status--${payable.status.toLowerCase()}`}>{payable.status.replace('PendingApproval', 'Pending approval')}</span>{payable.bankReference && <small>{payable.bankReference}</small>}</td>
                    <td><span className="journal-chip">{payable.status === 'Paid' ? <><b>Dr</b> Vendor payable<br /><b>Cr</b> Bank</> : <><b>Dr</b> GRNI / Tax<br /><b>Cr</b> Vendor payable</>}</span></td>
                    <td className="accounting-actions">
                      {payable.status === 'PendingApproval' && canApprove && <button type="button" disabled={busyId === payable.id} onClick={() => void approve(payable)}>Approve</button>}
                      {payable.status === 'Approved' && canReleasePayment && <button className="is-primary" type="button" onClick={() => setPaymentPayable(payable)}>Record payment</button>}
                      {payable.status === 'Paid' && <span>Complete</span>}
                      {payable.status === 'PendingApproval' && !canApprove && <span>Waiting for Finance</span>}
                      {payable.status === 'Approved' && !canReleasePayment && <span>Approved</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="accounting-card">
        <CardHeading title="Stock and vendor ledger" subtitle="System-generated balanced entries; users cannot edit debit or credit lines." />
        {journals.length === 0 ? <EmptyState text="No accounting journal entry is available." /> : (
          <div className="journal-list">
            {journals.slice(0, 12).map((journal) => (
              <article key={journal.id}>
                <span className="journal-list__icon">JE</span>
                <div><strong>{journal.entryNumber}</strong><small>{journal.description}</small></div>
                <span><small>{journal.entryType}</small><strong>{money(journal.debitTotal, journal.currency)}</strong></span>
                <time>{shortDate(journal.postedOnUtc)}</time>
              </article>
            ))}
          </div>
        )}
      </section>

      {invoiceCandidate && (
        <InvoiceDrawer
          candidate={invoiceCandidate}
          supplierName={supplierById.get(invoiceCandidate.supplierId)?.displayName ?? 'Supplier'}
          productName={(id) => productById.get(id)?.name ?? id}
          accessToken={session.accessToken}
          onClose={() => setInvoiceCandidate(null)}
          onSaved={async () => { setInvoiceCandidate(null); await load() }}
        />
      )}
      {paymentPayable && (
        <PaymentDrawer
          payable={paymentPayable}
          supplierName={supplierById.get(paymentPayable.supplierId)?.displayName ?? 'Supplier'}
          accessToken={session.accessToken}
          onClose={() => setPaymentPayable(null)}
          onSaved={async () => { setPaymentPayable(null); await load() }}
        />
      )}
    </section>
  )
}

function hasRemaining(candidate: AccountingInvoiceCandidate) {
  return candidate.items.some((item) => item.remainingQuantity > 0)
}

function SummaryCard({ label, value, note, tone }: { label: string; value: string; note: string; tone: string }) {
  return <article className={`accounting-summary__card accounting-summary__card--${tone}`}><span>{label}</span><strong>{value}</strong><small>{note}</small></article>
}

function CardHeading({ title, subtitle }: { title: string; subtitle: string }) {
  return <div className="accounting-card__heading"><div><h2>{title}</h2><p>{subtitle}</p></div></div>
}

function EmptyState({ text }: { text: string }) {
  return <div className="accounting-empty">{text}</div>
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
        <div className="drawer-header"><div><span className="eyebrow">Accounting</span><h2>Enter supplier invoice.</h2><p>PO + accepted GRN + supplier invoice three-way match.</p></div><button className="close-button" type="button" onClick={onClose}>×</button></div>
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

function PaymentDrawer({ payable, supplierName, accessToken, onClose, onSaved }: {
  payable: AccountingPayable
  supplierName: string
  accessToken: string
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [bankReference, setBankReference] = useState('')
  const [paidOn, setPaidOn] = useState(today())
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.recordVendorPayment(accessToken, payable.id, bankReference, toUtc(paidOn))
      await onSaved()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not record the vendor payment.')
    } finally {
      setSaving(false)
    }
  }

  return <div className="drawer-backdrop" role="presentation">
    <aside className="user-drawer accounting-drawer" role="dialog" aria-modal="true" aria-label="Record vendor payment">
      <div className="drawer-header"><div><span className="eyebrow">Accounts payable</span><h2>Record vendor payment.</h2><p>Use the reference returned by the bank after payment is actually executed.</p></div><button className="close-button" type="button" onClick={onClose}>×</button></div>
      <form className="accounting-form" onSubmit={(event) => void submit(event)}>
        <div className="payment-party"><span>Party account</span><strong>{supplierName}</strong><small>{payable.supplierInvoiceNumber} · {money(payable.totalAmount, payable.currency)}</small></div>
        <label className="field"><span>Bank transaction / UTR reference *</span><input value={bankReference} onChange={(event) => setBankReference(event.target.value)} placeholder="Example: UTR-HDFC-20260918-001" required /></label>
        <label className="field"><span>Payment date *</span><input type="date" max={today()} value={paidOn} onChange={(event) => setPaidOn(event.target.value)} required /></label>
        <div className="accounting-entry-preview accounting-entry-preview--payment"><span>Payment journal preview</span><div><strong>Vendor payable — {supplierName}</strong><b>Debit {money(payable.totalAmount, payable.currency)}</b></div><div><strong>Bank</strong><b>Credit {money(payable.totalAmount, payable.currency)}</b></div><small>This screen records a completed external payment; it does not transfer money from the bank.</small></div>
        {error && <div className="form-alert form-alert--error">{error}</div>}
        <div className="drawer-actions"><button className="secondary-button" type="button" onClick={onClose}>Cancel</button><button className="primary-button" type="submit" disabled={saving}>{saving ? 'Recording…' : 'Record payment'}</button></div>
      </form>
    </aside>
  </div>
}
