import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  api,
  ApiError,
  type AccountLedger,
  type LedgerLine,
  type LoginResponse,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { SCREEN } from '../config/screens'
import { startOfLocalDay, toInputDate } from '../dateRange'
import { DateRangeSearch } from './DateRangeSearch'
import { ScreenName } from './ScreenName'
import { ScreenTabs } from './ScreenTabs'

interface AccountLedgerWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

// The party (supplier) ledger is the Vendor Payable account seen per supplier.
const PARTY_ACCOUNT = 'VENDOR_PAYABLE'

const voucherTypes: Record<string, string> = {
  GoodsReceiptAccrual: 'Receipt (GRN)',
  VendorInvoice: 'Purchase invoice',
  VendorPayment: 'Payment',
  VendorPaymentBatch: 'Payment',
}

const moneyFormat = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const money = (value: number) => (value ? `₹ ${moneyFormat.format(value)}` : '')
// Balances read as "₹ 1,200.00 Dr" / "₹ 800.00 Cr" from the debit-minus-credit total.
const balance = (value: number) => (value === 0 ? '₹ 0.00' : `₹ ${moneyFormat.format(Math.abs(value))} ${value > 0 ? 'Dr' : 'Cr'}`)
const formatDateTime = (value: string) => new Intl.DateTimeFormat('en-IN', {
  day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
}).format(new Date(value))

function csvCell(value: string | number): string {
  const text = String(value)
  return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}

interface LedgerTotals {
  opening: number
  debit: number
  credit: number
  closing: number
}

export function AccountLedgerWorkspace({ session, onBack }: AccountLedgerWorkspaceProps) {
  const today = useMemo(() => new Date(), [])
  const [fromDate, setFromDate] = useState(() => toInputDate(new Date(today.getFullYear(), today.getMonth(), 1)))
  const [toDate, setToDate] = useState(() => toInputDate(today))
  const [ledger, setLedger] = useState<AccountLedger | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [accountFilter, setAccountFilter] = useState('')
  const [partyFilter, setPartyFilter] = useState('')
  const [search, setSearch] = useState('')
  const [activeTab, setActiveTab] = useState<'all' | 'detail'>('all')
  const canReadSuppliers = hasPermission(session, 'supplier.read')

  useEffect(() => {
    if (!canReadSuppliers) return
    let active = true
    // Supplier names are display-only; ids are shown if the master cannot be read.
    api.getSuppliers(session.accessToken)
      .then((items) => { if (active) setSuppliers(items) })
      .catch(() => undefined)
    return () => { active = false }
  }, [canReadSuppliers, session.accessToken])

  const loadLedger = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setLedger(await api.getAccountLedger(
        session.accessToken,
        startOfLocalDay(fromDate).toISOString(),
        startOfLocalDay(toDate, 1).toISOString(),
      ))
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not load the ledger.')
    } finally {
      setLoading(false)
    }
  }, [fromDate, session.accessToken, toDate])

  useEffect(() => {
    void loadLedger()
  }, [loadLedger])

  function applyDates(nextFrom: string, nextTo: string) {
    if (nextFrom === fromDate && nextTo === toDate) {
      void loadLedger()
      return
    }
    setFromDate(nextFrom)
    setToDate(nextTo)
  }

  const accountName = (code: string) => ledger?.accounts.find((account) => account.code === code)?.name ?? code
  const supplierName = (id: string | null) => {
    if (!id) return ''
    return suppliers.find((supplier) => supplier.id === id)?.displayName ?? `Party ${id.slice(0, 8)}…`
  }
  const particulars = (line: LedgerLine) =>
    `${line.debit > 0 ? 'To' : 'By'} ${line.contraAccounts.map(accountName).join(' / ') || 'Journal'}`

  const totalsFor = (accountCode: string, supplierId: string | null) => {
    const matches = (account: string, supplier: string | null) =>
      account === accountCode && (supplierId === null || supplier === supplierId)
    const opening = (ledger?.openingBalances ?? [])
      .filter((row) => matches(row.accountCode, row.supplierId))
      .reduce((total, row) => total + row.debit - row.credit, 0)
    const lines = (ledger?.lines ?? []).filter((line) => matches(line.accountCode, line.supplierId))
    const debit = lines.reduce((total, line) => total + line.debit, 0)
    const credit = lines.reduce((total, line) => total + line.credit, 0)
    return { opening, debit, credit, closing: opening + debit - credit } satisfies LedgerTotals
  }

  // "All ledgers" tab: every account, then every party on the Vendor Payable account.
  const accountRows = (ledger?.accounts ?? [])
    .map((account) => ({ account, totals: totalsFor(account.code, null) }))
    .filter(({ totals }) => totals.opening || totals.debit || totals.credit)
  const partyIds = [...new Set([
    ...(ledger?.openingBalances ?? []).filter((row) => row.accountCode === PARTY_ACCOUNT).map((row) => row.supplierId),
    ...(ledger?.lines ?? []).filter((line) => line.accountCode === PARTY_ACCOUNT).map((line) => line.supplierId),
  ].filter((id): id is string => !!id))]
  const partyRows = partyIds
    .map((id) => ({ id, totals: totalsFor(PARTY_ACCOUNT, id) }))
    .sort((left, right) => supplierName(left.id).localeCompare(supplierName(right.id)))
  const grandDebit = accountRows.reduce((total, row) => total + row.totals.debit, 0)
  const grandCredit = accountRows.reduce((total, row) => total + row.totals.credit, 0)

  // "Ledger detail" tab: one block per account with opening, entries, running balance, closing.
  const searchText = search.trim().toLowerCase()
  const detailGroups = (ledger?.accounts ?? [])
    .filter((account) => !accountFilter || account.code === accountFilter)
    .map((account) => {
      const totals = totalsFor(account.code, partyFilter || null)
      let running = totals.opening
      const entries = (ledger?.lines ?? [])
        .filter((line) => line.accountCode === account.code && (!partyFilter || line.supplierId === partyFilter))
        .map((line) => {
          running += line.debit - line.credit
          return { line, runningBalance: running }
        })
        .filter(({ line }) => !searchText || [
          line.entryNumber, line.voucherReference, line.description, supplierName(line.supplierId), particulars(line),
        ].some((value) => value?.toLowerCase().includes(searchText)))
      return { account, totals, entries }
    })
    .filter(({ totals, entries }) => entries.length > 0 || (!searchText && (totals.opening || accountFilter)))
  const detailEntryCount = detailGroups.reduce((total, group) => total + group.entries.length, 0)

  function openLedger(accountCode: string, supplierId = '') {
    setAccountFilter(accountCode)
    setPartyFilter(supplierId)
    setSearch('')
    setActiveTab('detail')
  }

  function exportCsv() {
    const rows: (string | number)[][] = [[
      'Account', 'Date', 'Voucher No.', 'Voucher type', 'Reference', 'Particulars', 'Party', 'Narration', 'Debit', 'Credit', 'Balance',
    ]]
    for (const { account, totals, entries } of detailGroups) {
      rows.push([account.name, fromDate, '', '', '', 'Opening balance', '', '', '', '', balance(totals.opening)])
      for (const { line, runningBalance } of entries) {
        rows.push([
          account.name, formatDateTime(line.postedOnUtc), line.entryNumber, voucherTypes[line.entryType] ?? line.entryType,
          line.voucherReference ?? '', particulars(line), supplierName(line.supplierId), line.description,
          line.debit ? line.debit.toFixed(2) : '', line.credit ? line.credit.toFixed(2) : '', balance(runningBalance),
        ])
      }
      rows.push([account.name, toDate, '', '', '', 'Closing balance', '', '', totals.debit.toFixed(2), totals.credit.toFixed(2), balance(totals.closing)])
    }
    const blob = new Blob([rows.map((row) => row.map(csvCell).join(',')).join('\n')], { type: 'text/csv' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `ledger_${accountFilter || 'all-accounts'}_${fromDate}_${toDate}.csv`
    link.click()
    URL.revokeObjectURL(link.href)
  }

  return (
    <section className="requisition-workspace" aria-labelledby="account-ledger-title">
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1 id="account-ledger-title"><ScreenName id={SCREEN.ledger} /></h1>
          <p>Every account and party ledger with date, voucher, particulars, debit, credit and running balance.</p>
        </div>
        <span className="workspace-status"><i /> Accounts</span>
      </header>

      <div className="list-filters" role="search" aria-label="Ledger filters">
        <DateRangeSearch fromDate={fromDate} toDate={toDate} onSearch={applyDates} busy={loading} required />
        <label>
          <span>Account</span>
          <select value={accountFilter} onChange={(event) => setAccountFilter(event.target.value)}>
            <option value="">All accounts</option>
            {(ledger?.accounts ?? []).map((account) => (
              <option key={account.code} value={account.code}>{account.name}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Party</span>
          <select value={partyFilter} onChange={(event) => setPartyFilter(event.target.value)}>
            <option value="">All parties</option>
            {[...new Set((ledger?.lines ?? []).map((line) => line.supplierId).filter((id): id is string => !!id))]
              .sort((left, right) => supplierName(left).localeCompare(supplierName(right)))
              .map((id) => <option key={id} value={id}>{supplierName(id)}</option>)}
          </select>
        </label>
        <label className="list-filters__search">
          <span>Voucher, reference or narration</span>
          <input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="JE-…, GRN-…, invoice no." />
        </label>
        <div className="list-filters__actions">
          <button type="button" className="workspace-refresh" onClick={exportCsv} disabled={detailEntryCount === 0}>
            ⤓ Export CSV
          </button>
        </div>
      </div>

      {error && <div className="form-alert requisition-list-alert" role="alert">{error}</div>}

      <ScreenTabs
        idPrefix="account-ledger"
        label="Ledger view"
        active={activeTab}
        onChange={setActiveTab}
        tabs={[
          { id: 'all', label: 'All ledgers', count: accountRows.length + partyRows.length },
          { id: 'detail', label: 'Ledger detail', count: detailEntryCount },
        ]}
      />

      <div id="account-ledger-panel" role="tabpanel" aria-labelledby={`account-ledger-tab-${activeTab}`}>
        {loading && !ledger ? (
          <section className="requisition-list-card">
            <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading ledger…</div>
          </section>
        ) : activeTab === 'all' ? (
          <section className="requisition-list-card" aria-labelledby="all-ledgers-title">
            <div className="requisition-list-heading">
              <div>
                <strong id="all-ledgers-title">Account and party balances</strong>
                <span>
                  {fromDate} to {toDate} · Total Dr {money(grandDebit) || '₹ 0.00'} · Total Cr {money(grandCredit) || '₹ 0.00'}
                  {grandDebit === grandCredit ? ' · ✓ Balanced' : ' · ⚠ Not balanced'}
                </span>
              </div>
            </div>
            {accountRows.length === 0 ? (
              <div className="requisition-list-empty">No journal entries in this period.</div>
            ) : (
              <div className="requisition-table-scroll">
                <table className="requisition-table stock-ledger-table account-ledger-table">
                  <thead>
                    <tr>
                      <th>Ledger</th>
                      <th className="is-number">Opening</th>
                      <th className="is-number">Debit</th>
                      <th className="is-number">Credit</th>
                      <th className="is-number">Closing</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    <tr className="stock-ledger-group"><td colSpan={6}><strong>Accounts</strong></td></tr>
                    {accountRows.map(({ account, totals }) => (
                      <tr key={account.code}>
                        <td><strong>{account.name}</strong><small>{account.code}</small></td>
                        <td className="is-number">{balance(totals.opening)}</td>
                        <td className="is-number">{money(totals.debit)}</td>
                        <td className="is-number">{money(totals.credit)}</td>
                        <td className="is-number"><strong>{balance(totals.closing)}</strong></td>
                        <td><button type="button" className="stock-ledger-drill" onClick={() => openLedger(account.code)}>View ledger →</button></td>
                      </tr>
                    ))}
                    {partyRows.length > 0 && (
                      <tr className="stock-ledger-group"><td colSpan={6}><strong>Parties (Vendor payable)</strong></td></tr>
                    )}
                    {partyRows.map(({ id, totals }) => (
                      <tr key={id}>
                        <td><strong>{supplierName(id)}</strong><small>Sundry creditor</small></td>
                        <td className="is-number">{balance(totals.opening)}</td>
                        <td className="is-number">{money(totals.debit)}</td>
                        <td className="is-number">{money(totals.credit)}</td>
                        <td className="is-number"><strong>{balance(totals.closing)}</strong></td>
                        <td><button type="button" className="stock-ledger-drill" onClick={() => openLedger(PARTY_ACCOUNT, id)}>View ledger →</button></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ) : (
          <section className="requisition-list-card" aria-labelledby="ledger-detail-title">
            <div className="requisition-list-heading">
              <div>
                <strong id="ledger-detail-title">
                  {accountFilter ? accountName(accountFilter) : 'All accounts'}
                  {partyFilter ? ` · ${supplierName(partyFilter)}` : ''}
                </strong>
                <span>{detailEntryCount} entr{detailEntryCount === 1 ? 'y' : 'ies'} · running balance per account</span>
              </div>
            </div>
            {detailGroups.length === 0 ? (
              <div className="requisition-list-empty">No entries match these filters.</div>
            ) : (
              <div className="requisition-table-scroll">
                <table className="requisition-table stock-ledger-table account-ledger-table">
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Voucher No.</th>
                      <th>Particulars</th>
                      <th className="is-number">Debit</th>
                      <th className="is-number">Credit</th>
                      <th className="is-number">Balance</th>
                    </tr>
                  </thead>
                  {detailGroups.map(({ account, totals, entries }) => (
                    <tbody key={account.code}>
                      <tr className="stock-ledger-group">
                        <td colSpan={6}><strong>{account.name}</strong><small>{account.code}</small></td>
                      </tr>
                      <tr className="stock-ledger-balance-row">
                        <td>{startOfLocalDay(fromDate).toLocaleDateString('en-IN')}</td>
                        <td>—</td>
                        <td>Opening balance</td>
                        <td />
                        <td />
                        <td className="is-number">{balance(totals.opening)}</td>
                      </tr>
                      {entries.map(({ line, runningBalance }) => (
                        <tr key={`${line.journalEntryId}-${line.lineNumber}`}>
                          <td>{formatDateTime(line.postedOnUtc)}</td>
                          <td>
                            <strong>{line.entryNumber}</strong>
                            <small>{voucherTypes[line.entryType] ?? line.entryType}{line.voucherReference ? ` · ${line.voucherReference}` : ''}</small>
                          </td>
                          <td className="account-ledger-particulars">
                            <strong>{particulars(line)}</strong>
                            <small>{[supplierName(line.supplierId), line.description].filter(Boolean).join(' · ')}</small>
                          </td>
                          <td className="is-number is-in">{money(line.debit)}</td>
                          <td className="is-number is-out">{money(line.credit)}</td>
                          <td className="is-number"><strong>{balance(runningBalance)}</strong></td>
                        </tr>
                      ))}
                      <tr className="stock-ledger-balance-row">
                        <td>{startOfLocalDay(toDate).toLocaleDateString('en-IN')}</td>
                        <td>—</td>
                        <td>Closing balance</td>
                        <td className="is-number is-in">{money(totals.debit) || '₹ 0.00'}</td>
                        <td className="is-number is-out">{money(totals.credit) || '₹ 0.00'}</td>
                        <td className="is-number">{balance(totals.closing)}</td>
                      </tr>
                    </tbody>
                  ))}
                </table>
              </div>
            )}
          </section>
        )}
      </div>
    </section>
  )
}
