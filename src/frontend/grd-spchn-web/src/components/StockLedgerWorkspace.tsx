import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type LoginResponse,
  type StockLocation,
  type PurchaseOrder,
  type StockLedger,
  type StockLedgerEntry,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { SCREEN } from '../config/screens'
import { startOfLocalDay, toInputDate } from '../dateRange'
import { DateRangeSearch } from './DateRangeSearch'
import { ScreenName } from './ScreenName'
import { ScreenTabs } from './ScreenTabs'

interface StockLedgerWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

const movementLabels: Record<string, string> = {
  QualityRelease: 'Receipt (quality passed)',
}

const quantityFormat = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 3 })
const formatQuantity = (value: number) => quantityFormat.format(value)
const moneyFormat = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const formatMoney = (value: number | null) => value === null ? '—' : `₹ ${moneyFormat.format(value)}`
const formatDateTime = (value: string) => new Intl.DateTimeFormat('en-IN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
}).format(new Date(value))

function csvCell(value: string | number): string {
  const text = String(value)
  return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}

export function StockLedgerWorkspace({ session, onBack }: StockLedgerWorkspaceProps) {
  const today = useMemo(() => new Date(), [])
  const [fromDate, setFromDate] = useState(() => toInputDate(new Date(today.getFullYear(), today.getMonth(), 1)))
  const [toDate, setToDate] = useState(() => toInputDate(today))
  const [productId, setProductId] = useState('')
  const [search, setSearch] = useState('')
  const [activeTab, setActiveTab] = useState<'summary' | 'entries'>('summary')
  const [ledger, setLedger] = useState<StockLedger | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  // The user's own unit and every unit below it; a parent unit gives a consolidated ledger.
  const [locations, setLocations] = useState<StockLocation[]>([])
  const [locationId, setLocationId] = useState(session.organizationUnitId)
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrder[]>([])
  const canReadSuppliers = hasPermission(session, 'supplier.read')
  // Values use the PO rate, so they are shown only to users allowed to see purchase orders.
  const canReadPurchaseOrders = hasPermission(session, 'procurement.purchase-order.read')

  useEffect(() => {
    let active = true
    // Names and rates are display-only; the ledger still renders quantities if a master cannot be read.
    void Promise.allSettled([
      api.getProcurementItems(session.accessToken),
      canReadSuppliers ? api.getSuppliers(session.accessToken) : Promise.resolve([]),
      api.getStockLedgerLocations(session.accessToken),
      canReadPurchaseOrders ? api.listPurchaseOrders(session.accessToken) : Promise.resolve([]),
    ]).then(([items, supplierItems, units, orders]) => {
      if (!active) return
      if (items.status === 'fulfilled') setCatalog(items.value)
      if (supplierItems.status === 'fulfilled') setSuppliers(supplierItems.value)
      if (units.status === 'fulfilled') setLocations(units.value)
      if (orders.status === 'fulfilled') setPurchaseOrders(orders.value)
    })
    return () => {
      active = false
    }
  }, [canReadPurchaseOrders, canReadSuppliers, session.accessToken])

  const locationOptions = useMemo(() => {
    const byId = new Map(locations.map((unit) => [unit.id, unit]))
    const depth = (unit: StockLocation): number => {
      let level = 0
      for (let parent = unit.parentId && byId.get(unit.parentId); parent; parent = parent.parentId && byId.get(parent.parentId)) {
        level += 1
      }
      return level
    }
    // Depth-first order so each unit is listed under its parent.
    const ordered: { unit: StockLocation; level: number }[] = []
    const visit = (parentId: string | null) => locations
      .filter((unit) => (parentId === null ? !unit.parentId || !byId.has(unit.parentId) : unit.parentId === parentId))
      .sort((left, right) => left.name.localeCompare(right.name))
      .forEach((unit) => {
        ordered.push({ unit, level: depth(unit) })
        visit(unit.id)
      })
    visit(null)
    return ordered
  }, [locations])
  const location = locations.find((unit) => unit.id === locationId)
  const locationName = (id: string) => locations.find((unit) => unit.id === id)?.name ?? `${id.slice(0, 8)}…`
  const consolidated = locations.some((unit) => unit.parentId === locationId)

  const loadLedger = useCallback(async () => {
    if (!fromDate || !toDate || fromDate > toDate) return
    setLoading(true)
    setError('')
    try {
      setLedger(await api.getStockLedger(
        session.accessToken,
        startOfLocalDay(fromDate).toISOString(),
        startOfLocalDay(toDate, 1).toISOString(),
        locationId,
        productId || undefined,
      ))
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not load the stock ledger.')
    } finally {
      setLoading(false)
    }
  }, [fromDate, locationId, productId, session.accessToken, toDate])

  useEffect(() => {
    void loadLedger()
  }, [loadLedger])

  // Dates apply when Search is pressed; searching the same range again reloads it.
  function applyDates(nextFrom: string, nextTo: string) {
    if (nextFrom === fromDate && nextTo === toDate) {
      void loadLedger()
      return
    }
    setFromDate(nextFrom)
    setToDate(nextTo)
  }

  const material = useCallback(
    (id: string) => catalog.find((item) => item.id === id),
    [catalog],
  )
  const supplierName = (id: string | null) => {
    if (!id) return ''
    return suppliers.find((supplier) => supplier.id === id)?.displayName ?? `Supplier ${id.slice(0, 8)}…`
  }

  const rateOf = (entry: StockLedgerEntry): number | null => {
    const order = purchaseOrders.find((candidate) => candidate.id === entry.purchaseOrderId)
    return order?.items.find((line) => line.productId === entry.productId)?.unitPrice ?? null
  }
  const valueOf = (quantity: number, entry: StockLedgerEntry): number | null => {
    const rate = rateOf(entry)
    return quantity && rate !== null ? quantity * rate : null
  }
  const sumValues = (entries: StockLedgerEntry[], pick: (entry: StockLedgerEntry) => number) => {
    if (!canReadPurchaseOrders) return null
    const values = entries.filter((entry) => pick(entry) !== 0).map((entry) => valueOf(pick(entry), entry))
    return values.some((value) => value === null) ? null : values.reduce<number>((total, value) => total + (value ?? 0), 0)
  }
  const describe = (entry: StockLedgerEntry) => {
    const label = movementLabels[entry.movementType] ?? entry.movementType
    return entry.supplierId ? `${label} · ${supplierName(entry.supplierId)}` : label
  }

  // On-hand is today's balance, so it can only be compared when the period runs to today.
  const periodReachesToday = toDate >= toInputDate(new Date())

  const searchText = search.trim().toLowerCase()
  const matchesSearch = (entry: StockLedgerEntry) => !searchText || [
    entry.goodsReceiptNumber,
    entry.purchaseOrderNumber,
    supplierName(entry.supplierId),
    movementLabels[entry.movementType] ?? entry.movementType,
  ].some((value) => value?.toLowerCase().includes(searchText))

  const groups = (ledger?.products ?? [])
    .map((summary) => ({
      summary,
      item: material(summary.productId),
      entries: ledger!.entries.filter((entry) => entry.productId === summary.productId && matchesSearch(entry)),
    }))
    .filter((group) => searchText
      ? group.entries.length > 0
      : group.entries.length > 0 ||
        group.summary.openingQuantity !== 0 ||
        (periodReachesToday && group.summary.onHandQuantity !== 0))
    .sort((left, right) => (left.item?.name ?? left.summary.productId)
      .localeCompare(right.item?.name ?? right.summary.productId))

  const visibleEntryCount = groups.reduce((total, group) => total + group.entries.length, 0)
  const unreconciled = periodReachesToday
    ? (ledger?.products ?? []).filter((summary) => summary.closingQuantity !== summary.onHandQuantity)
    : []

  function exportCsv() {
    if (!ledger) return
    const rows: (string | number)[][] = [[
      'Material code', 'Material', 'UOM', 'Date', 'Location', 'Ref / Voucher No.', 'Purchase order', 'Description / Party',
      'Receipts (Inward) Qty', 'Receipts (Inward) Value', 'Issues (Outward) Qty', 'Issues (Outward) Value', 'Balance',
    ]]
    const csvMoney = (value: number | null) => value === null ? '' : value.toFixed(2)
    for (const { summary, item, entries } of groups) {
      const code = item?.code ?? summary.productId
      const name = item?.name ?? ''
      const uom = item?.baseUnitOfMeasure ?? ''
      rows.push([code, name, uom, fromDate, '', '', '', 'Opening balance', '', '', '', '', summary.openingQuantity])
      for (const entry of entries) {
        rows.push([
          code, name, uom, formatDateTime(entry.occurredOnUtc), locationName(entry.organizationUnitId),
          entry.goodsReceiptNumber ?? '', entry.purchaseOrderNumber ?? '', describe(entry),
          entry.inQuantity || '', csvMoney(valueOf(entry.inQuantity, entry)),
          entry.outQuantity || '', csvMoney(valueOf(entry.outQuantity, entry)),
          entry.balanceQuantity,
        ])
      }
      rows.push([
        code, name, uom, toDate, '', '', '', 'Closing balance',
        summary.inQuantity, csvMoney(sumValues(entries, (entry) => entry.inQuantity)),
        summary.outQuantity, csvMoney(sumValues(entries, (entry) => entry.outQuantity)),
        summary.closingQuantity,
      ])
    }
    const blob = new Blob([rows.map((row) => row.map(csvCell).join(',')).join('\n')], { type: 'text/csv' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `stock-ledger_${location?.code ?? 'location'}_${fromDate}_${toDate}.csv`
    link.click()
    URL.revokeObjectURL(link.href)
  }

  return (
    <section className="requisition-workspace stock-ledger" aria-labelledby="stock-ledger-title">
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1 id="stock-ledger-title"><ScreenName id={SCREEN.stockLedger} /></h1>
          <p>
            Opening balance, quality-approved receipts, issues and running balance for {location?.name ?? 'your location'}
            {consolidated ? ' and every location under it.' : '.'}
          </p>
        </div>
        <span className="workspace-status"><i /> {location?.code ?? session.organizationUnitId.slice(0, 8)}</span>
      </header>

      <div className="list-filters" role="search" aria-label="Stock ledger filters">
        <label>
          <span>Location</span>
          <select value={locationId} onChange={(event) => setLocationId(event.target.value)} disabled={locationOptions.length <= 1}>
            {locationOptions.length === 0 && <option value={locationId}>My location</option>}
            {locationOptions.map(({ unit, level }) => (
              <option key={unit.id} value={unit.id}>
                {'  '.repeat(level)}{unit.name}{locations.some((child) => child.parentId === unit.id) ? ' (all below)' : ''}
              </option>
            ))}
          </select>
        </label>
        <DateRangeSearch fromDate={fromDate} toDate={toDate} onSearch={applyDates} busy={loading} required />
        <label>
          <span>Material</span>
          <select value={productId} onChange={(event) => setProductId(event.target.value)}>
            <option value="">All materials</option>
            {catalog.map((item) => (
              <option key={item.id} value={item.id}>{item.name} · {item.code}</option>
            ))}
          </select>
        </label>
        <label className="list-filters__search">
          <span>GRN, PO or supplier</span>
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search reference"
          />
        </label>
        <div className="list-filters__actions">
          <button type="button" className="workspace-refresh" onClick={exportCsv} disabled={!ledger || groups.length === 0}>
            ⤓ Export CSV
          </button>
        </div>
      </div>

      {error && <div className="form-alert requisition-list-alert" role="alert">{error}</div>}
      {unreconciled.length > 0 && (
        <div className="form-alert requisition-list-alert" role="alert">
          Closing balance does not match current on-hand stock for {unreconciled
            .map((summary) => material(summary.productId)?.name ?? summary.productId)
            .join(', ')}. Report this to the Inventory administrator.
        </div>
      )}

      <ScreenTabs
        idPrefix="stock-ledger"
        label="Stock ledger view"
        active={activeTab}
        onChange={setActiveTab}
        tabs={[
          { id: 'summary', label: 'Material summary', count: groups.length },
          { id: 'entries', label: 'Ledger entries', count: visibleEntryCount },
        ]}
      />

      <div id="stock-ledger-panel" role="tabpanel" aria-labelledby={`stock-ledger-tab-${activeTab}`}>
      {activeTab === 'summary' && (
      <section className="requisition-list-card" aria-labelledby="stock-summary-title">
        <div className="requisition-list-heading">
          <div>
            <strong id="stock-summary-title">Material summary</strong>
            <span>{fromDate} to {toDate} · quantities in each material's base unit</span>
          </div>
        </div>
        {loading && !ledger ? (
          <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading stock ledger…</div>
        ) : groups.length === 0 ? (
          <div className="requisition-list-empty">No stock has been recorded for this location and filter.</div>
        ) : (
          <div className="requisition-table-scroll">
            <table className="requisition-table stock-ledger-table">
              <thead>
                <tr>
                  <th>Material</th>
                  <th className="is-number">Opening</th>
                  <th className="is-number">In</th>
                  <th className="is-number">Out</th>
                  <th className="is-number">Closing</th>
                  <th>On-hand check</th>
                </tr>
              </thead>
              <tbody>
                {groups.map(({ summary, item }) => (
                  <tr key={summary.productId}>
                    <td>
                      <strong>{item?.name ?? summary.productId}</strong>
                      <small>{item?.code ?? 'Unknown material'}</small>
                      <button
                        type="button"
                        className="stock-ledger-drill"
                        onClick={() => {
                          setProductId(summary.productId)
                          setActiveTab('entries')
                        }}
                      >
                        View entries →
                      </button>
                    </td>
                    <td className="is-number">{formatQuantity(summary.openingQuantity)} {item?.baseUnitOfMeasure}</td>
                    <td className="is-number is-in">{formatQuantity(summary.inQuantity)}</td>
                    <td className="is-number is-out">{formatQuantity(summary.outQuantity)}</td>
                    <td className="is-number"><strong>{formatQuantity(summary.closingQuantity)} {item?.baseUnitOfMeasure}</strong></td>
                    <td>
                      {!periodReachesToday ? (
                        <span className="table-action-complete">Past period</span>
                      ) : summary.closingQuantity === summary.onHandQuantity ? (
                        <span className="tracking-status tracking-status--received">✓ Matches on-hand</span>
                      ) : (
                        <span className="tracking-status tracking-status--submitted">
                          On-hand {formatQuantity(summary.onHandQuantity)}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      )}

      {activeTab === 'entries' && (
      <section className="requisition-list-card" aria-labelledby="stock-ledger-list-title">
        <div className="requisition-list-heading">
          <div>
            <strong id="stock-ledger-list-title">Ledger entries</strong>
            <span>
              {visibleEntryCount} movement{visibleEntryCount === 1 ? '' : 's'}
              {searchText ? ` matching "${search.trim()}"` : ''} · running balance per material
              {!canReadPurchaseOrders && ' · values need purchase-order access'}
            </span>
          </div>
        </div>
        {groups.length === 0 ? (
          <div className="requisition-list-empty">
            {loading ? 'Loading…' : 'No movements in this period.'}
          </div>
        ) : (
          <div className="requisition-table-scroll">
            <table className="requisition-table stock-ledger-table stock-ledger-register">
              <thead>
                <tr>
                  <th rowSpan={2}>Date</th>
                  <th rowSpan={2}>Ref / Voucher No.</th>
                  <th rowSpan={2}>Description / Party</th>
                  <th colSpan={2} className="is-group">Receipts (Inward)</th>
                  <th colSpan={2} className="is-group">Issues (Outward)</th>
                  <th rowSpan={2} className="is-number">Balance</th>
                </tr>
                <tr>
                  <th className="is-number">Qty</th>
                  <th className="is-number">Value</th>
                  <th className="is-number">Qty</th>
                  <th className="is-number">Value</th>
                </tr>
              </thead>
              {groups.map(({ summary, item, entries }) => (
                <tbody key={summary.productId}>
                  <tr className="stock-ledger-group">
                    <td colSpan={8}>
                      <strong>{item?.name ?? summary.productId}</strong>
                      <small>{item?.code} · {item?.baseUnitOfMeasure}</small>
                    </td>
                  </tr>
                  <tr className="stock-ledger-balance-row">
                    <td>{startOfLocalDay(fromDate).toLocaleDateString('en-IN')}</td>
                    <td>—</td>
                    <td colSpan={5}>Opening balance</td>
                    <td className="is-number">{formatQuantity(summary.openingQuantity)}</td>
                  </tr>
                  {entries.map((entry) => (
                    <tr key={entry.movementId}>
                      <td>
                        {formatDateTime(entry.occurredOnUtc)}
                        {consolidated && <small>{locationName(entry.organizationUnitId)}</small>}
                      </td>
                      <td>
                        <strong>{entry.goodsReceiptNumber ?? '—'}</strong>
                        {entry.purchaseOrderNumber && <small>{entry.purchaseOrderNumber}</small>}
                      </td>
                      <td>
                        <strong>{movementLabels[entry.movementType] ?? entry.movementType}</strong>
                        {entry.supplierId && <small>{supplierName(entry.supplierId)}</small>}
                      </td>
                      <td className="is-number is-in">{entry.inQuantity ? formatQuantity(entry.inQuantity) : ''}</td>
                      <td className="is-number is-in">{entry.inQuantity ? formatMoney(valueOf(entry.inQuantity, entry)) : ''}</td>
                      <td className="is-number is-out">{entry.outQuantity ? formatQuantity(entry.outQuantity) : ''}</td>
                      <td className="is-number is-out">{entry.outQuantity ? formatMoney(valueOf(entry.outQuantity, entry)) : ''}</td>
                      <td className="is-number"><strong>{formatQuantity(entry.balanceQuantity)} {item?.baseUnitOfMeasure}</strong></td>
                    </tr>
                  ))}
                  <tr className="stock-ledger-balance-row">
                    <td>{startOfLocalDay(toDate).toLocaleDateString('en-IN')}</td>
                    <td>—</td>
                    <td>Closing balance</td>
                    <td className="is-number is-in">{formatQuantity(summary.inQuantity)}</td>
                    <td className="is-number is-in">{formatMoney(sumValues(entries, (entry) => entry.inQuantity))}</td>
                    <td className="is-number is-out">{formatQuantity(summary.outQuantity)}</td>
                    <td className="is-number is-out">{formatMoney(sumValues(entries, (entry) => entry.outQuantity))}</td>
                    <td className="is-number">{formatQuantity(summary.closingQuantity)} {item?.baseUnitOfMeasure}</td>
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
