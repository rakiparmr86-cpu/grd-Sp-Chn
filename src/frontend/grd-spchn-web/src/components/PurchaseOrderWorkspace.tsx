import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type LoginResponse,
  type MaterialRequestListItem,
  type PurchaseOrder,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { SCREEN } from '../config/screens'
import { dateRangeError, toDateRangeFilter } from '../dateRange'
import { DateRangeSearch } from './DateRangeSearch'
import { ScreenName } from './ScreenName'
import { VendorDispatchPanel } from './VendorDispatchPanel'

interface PurchaseOrderWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

const formatAmount = (value: number) => value.toLocaleString('en-IN', { minimumFractionDigits: 2 })

export function PurchaseOrderWorkspace({ session, onBack }: PurchaseOrderWorkspaceProps) {
  const [orders, setOrders] = useState<PurchaseOrder[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [catalogItems, setCatalogItems] = useState<CatalogItem[]>([])
  const [requests, setRequests] = useState<MaterialRequestListItem[]>([])
  const [dispatchOrder, setDispatchOrder] = useState<PurchaseOrder | null>(null)
  // Dates are filtered by the server when Search is pressed; number, status and supplier filter instantly.
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [numberFilter, setNumberFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [supplierFilter, setSupplierFilter] = useState('')
  const canRecordDispatch = hasPermission(session, 'procurement.purchase-order.dispatch')
  const canReadRequests = hasPermission(session, 'procurement.material-request.read')

  useEffect(() => {
    let active = true
    // Masters are display-only; the list still renders with ids if one cannot be read.
    void Promise.allSettled([
      api.getSuppliers(session.accessToken),
      api.getProcurementItems(session.accessToken),
      canReadRequests ? api.listMaterialRequests(session.accessToken) : Promise.resolve([]),
    ]).then(([supplierItems, items, requestItems]) => {
      if (!active) return
      if (supplierItems.status === 'fulfilled') setSuppliers(supplierItems.value)
      if (items.status === 'fulfilled') setCatalogItems(items.value)
      if (requestItems.status === 'fulfilled') setRequests(requestItems.value)
    })
    return () => {
      active = false
    }
  }, [canReadRequests, session.accessToken])

  const refreshOrders = useCallback(async () => {
    if (dateRangeError(fromDate, toDate)) return
    setLoading(true)
    setError('')
    try {
      setOrders(await api.listPurchaseOrders(session.accessToken, toDateRangeFilter(fromDate, toDate)))
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not load purchase orders.')
    } finally {
      setLoading(false)
    }
  }, [fromDate, session.accessToken, toDate])

  useEffect(() => {
    void refreshOrders()
  }, [refreshOrders])

  // Searching the same range again simply reloads it.
  function applyDates(nextFrom: string, nextTo: string) {
    if (nextFrom === fromDate && nextTo === toDate) {
      void refreshOrders()
      return
    }
    setFromDate(nextFrom)
    setToDate(nextTo)
  }

  const requestNumberOf = (order: PurchaseOrder) =>
    requests.find((request) => request.id === order.materialRequestId)?.requestNumber

  const statusOptions = useMemo(() => [...new Set(orders.map((order) => order.status))].sort(), [orders])
  const supplierOptions = useMemo(
    () => [...new Set(orders.map((order) => order.supplierId))]
      .map((id) => ({ id, name: suppliers.find((supplier) => supplier.id === id)?.displayName ?? id }))
      .sort((left, right) => left.name.localeCompare(right.name)),
    [orders, suppliers],
  )
  const numberText = numberFilter.trim().toLowerCase()
  const visibleOrders = orders.filter((order) =>
    (!statusFilter || order.status === statusFilter) &&
    (!supplierFilter || order.supplierId === supplierFilter) &&
    (!numberText ||
      order.purchaseOrderNumber.toLowerCase().includes(numberText) ||
      (requestNumberOf(order)?.toLowerCase().includes(numberText) ?? false)))
  const filtersActive = !!(fromDate || toDate || numberFilter || statusFilter || supplierFilter)

  function clearFilters() {
    setFromDate('')
    setToDate('')
    setNumberFilter('')
    setStatusFilter('')
    setSupplierFilter('')
  }

  return (
    <section className="requisition-workspace" aria-labelledby="purchase-order-title">
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1 id="purchase-order-title"><ScreenName id={SCREEN.purchaseOrder} /></h1>
          <p>Review supplier, ordered quantity, negotiated rate, amount, and dispatch state.</p>
        </div>
        <span className="workspace-status"><i /> Purchase orders</span>
      </header>

      <div className="list-filters" role="search" aria-label="Purchase order filters">
        <DateRangeSearch fromDate={fromDate} toDate={toDate} onSearch={applyDates} busy={loading} />
        <label className="list-filters__search">
          <span>PO / requisition no.</span>
          <input
            type="search"
            value={numberFilter}
            onChange={(event) => setNumberFilter(event.target.value)}
            placeholder="PO-… or MR-…"
          />
        </label>
        <label>
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">All statuses</option>
            {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
          </select>
        </label>
        <label>
          <span>Supplier</span>
          <select value={supplierFilter} onChange={(event) => setSupplierFilter(event.target.value)}>
            <option value="">All suppliers</option>
            {supplierOptions.map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}
          </select>
        </label>
        <div className="list-filters__actions">
          <button type="button" className="workspace-refresh" onClick={clearFilters} disabled={!filtersActive}>
            Clear filters
          </button>
        </div>
      </div>

      <section className="requisition-list-card" aria-labelledby="purchase-order-list-title">
        <div className="requisition-list-heading">
          <div>
            <strong id="purchase-order-list-title">Purchase order list</strong>
            <span>
              {visibleOrders.length} of {orders.length} purchase order{orders.length === 1 ? '' : 's'}
            </span>
          </div>
          <button type="button" onClick={() => void refreshOrders()} disabled={loading}>
            {loading ? 'Refreshing…' : '↻ Refresh'}
          </button>
        </div>

        {error && <div className="form-alert requisition-list-alert" role="alert">{error}</div>}
        {message && <div className="success-alert requisition-list-alert" role="status">{message}</div>}
        {loading && orders.length === 0 ? (
          <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading purchase orders…</div>
        ) : visibleOrders.length === 0 ? (
          <div className="requisition-list-empty">
            {filtersActive ? 'No purchase orders match these filters.' : 'No purchase orders have been created yet.'}
          </div>
        ) : (
          <div className="requisition-table-scroll">
            <table className="requisition-table purchase-order-table">
              <thead>
                <tr>
                  <th>Purchase order</th>
                  <th>Supplier</th>
                  <th>Items and rates</th>
                  <th>Total</th>
                  <th>Status</th>
                  <th>Issued</th>
                  {canRecordDispatch && <th>Action</th>}
                </tr>
              </thead>
              <tbody>
                {visibleOrders.map((order) => {
                  const supplier = suppliers.find((item) => item.id === order.supplierId)
                  return (
                    <tr key={order.id}>
                      <td>
                        <strong>{order.purchaseOrderNumber}</strong>
                        <small>{requestNumberOf(order) ?? `Requisition ${order.materialRequestId.slice(0, 8)}…`}</small>
                      </td>
                      <td>
                        <strong>{supplier?.displayName ?? 'Supplier'}</strong>
                        <small>{supplier?.code ?? order.supplierId}</small>
                      </td>
                      <td className="po-rate-lines">
                        {order.items.map((item) => {
                          const catalogItem = catalogItems.find((entry) => entry.id === item.productId)
                          const lineAmount = item.lineAmount ?? item.quantity * item.unitPrice
                          return (
                            <div key={item.productId}>
                              <strong>{catalogItem?.name ?? item.productId}</strong>
                              <small>
                                {item.quantity.toLocaleString('en-IN')} {item.unitOfMeasure}
                                {' × '}{order.currency} {formatAmount(item.unitPrice)}
                                {' = '}{order.currency} {formatAmount(lineAmount)}
                              </small>
                            </div>
                          )
                        })}
                      </td>
                      <td className="po-total-cell">
                        <strong>{order.currency} {formatAmount(order.totalAmount ?? order.items.reduce(
                          (total, item) => total + item.quantity * item.unitPrice,
                          0,
                        ))}</strong>
                      </td>
                      <td><span className={`tracking-status tracking-status--${order.status.toLowerCase()}`}>{order.status}</span></td>
                      <td>{new Date(order.issuedOnUtc).toLocaleDateString('en-IN')}</td>
                      {canRecordDispatch && (
                        <td className="requisition-action-cell">
                          {order.status === 'Issued' ? (
                            <button
                              className="table-action-button table-action-button--primary"
                              type="button"
                              onClick={() => {
                                setMessage('')
                                setDispatchOrder(order)
                              }}
                            >
                              Record dispatch
                            </button>
                          ) : (
                            <span className="table-action-complete">
                              {order.status === 'Received' ? 'Received' : 'Dispatch recorded'}
                            </span>
                          )}
                        </td>
                      )}
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {dispatchOrder && (
        <VendorDispatchPanel
          accessToken={session.accessToken}
          purchaseOrder={dispatchOrder}
          supplierName={suppliers.find((supplier) => supplier.id === dispatchOrder.supplierId)?.displayName ?? 'Selected supplier'}
          onClose={() => setDispatchOrder(null)}
          onRecorded={(purchaseOrder) => {
            setDispatchOrder(null)
            setMessage(`${purchaseOrder.purchaseOrderNumber} vendor dispatch was recorded and Warehouse was notified.`)
            void refreshOrders()
          }}
        />
      )}
    </section>
  )
}
