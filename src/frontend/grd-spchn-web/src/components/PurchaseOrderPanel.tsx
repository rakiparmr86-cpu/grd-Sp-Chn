import { useEffect, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type MaterialRequest,
  type PurchaseOrder,
  type Supplier,
} from '../api'

interface PurchaseOrderPanelProps {
  accessToken: string
  materialRequestId: string
  catalogItems: CatalogItem[]
  onClose: () => void
  onCreated: (purchaseOrder: PurchaseOrder) => void
}

export function PurchaseOrderPanel({
  accessToken,
  materialRequestId,
  catalogItems,
  onClose,
  onCreated,
}: PurchaseOrderPanelProps) {
  const [request, setRequest] = useState<MaterialRequest | null>(null)
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [supplierId, setSupplierId] = useState('')
  const [prices, setPrices] = useState<Record<string, string>>({})
  const [currency, setCurrency] = useState('INR')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true

    async function loadPurchaseOrderData() {
      const [materialRequestResult, supplierResult] = await Promise.allSettled([
        api.getMaterialRequest(accessToken, materialRequestId),
        api.getSuppliers(accessToken),
      ])
      if (!active) return

      if (materialRequestResult.status === 'rejected') {
        const reason = materialRequestResult.reason as unknown
        setError(reason instanceof ApiError
          ? `Could not load the requisition: ${reason.message}`
          : 'Could not load the selected requisition.')
        setLoading(false)
        return
      }

      const materialRequest = materialRequestResult.value
      setRequest(materialRequest)
      setPrices(Object.fromEntries(
        materialRequest.items.map((item) => [item.productId, '']),
      ))

      if (supplierResult.status === 'fulfilled') {
        const availableSuppliers = supplierResult.value
        setSuppliers(availableSuppliers)
        const defaultSupplier = availableSuppliers[0]
        setSupplierId(defaultSupplier?.id ?? '')
        setCurrency(defaultSupplier?.defaultCurrency ?? 'INR')
        if (availableSuppliers.length === 0) {
          setError('No active suppliers exist in Supplier master.')
        }
      } else {
        const reason = supplierResult.reason as unknown
        setError(reason instanceof ApiError && reason.status === 404
          ? 'Supplier catalog endpoint was not found. Restart the Supplier API so it loads the new /catalog endpoint.'
          : reason instanceof ApiError
            ? `Could not load Supplier master: ${reason.message}`
            : 'Could not load Supplier master.')
      }

      setLoading(false)
    }

    void loadPurchaseOrderData()

    return () => {
      active = false
    }
  }, [accessToken, materialRequestId])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!request) return

    const purchasePrices = request.items.map((item) => ({
      productId: item.productId,
      unitPrice: Number(prices[item.productId]),
    }))
    if (purchasePrices.some((price) => !Number.isFinite(price.unitPrice) || price.unitPrice <= 0)) {
      setError('Enter a unit price greater than zero for every material.')
      return
    }
    if (!supplierId) {
      setError('Select an active supplier before creating the purchase order.')
      return
    }

    setSubmitting(true)
    setError('')
    try {
      const purchaseOrder = await api.issuePurchaseOrder(
        accessToken,
        request.id,
        {
          supplierId,
          currency,
          prices: purchasePrices,
        },
      )
      onCreated(purchaseOrder)
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The purchase order could not be created.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside
        className="user-drawer purchase-order-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="purchase-order-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="drawer-header">
          <div>
            <span className="eyebrow">Procurement</span>
            <h2 id="purchase-order-title">Create PO.</h2>
            <p>Convert an approved requisition into a supplier commitment.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        {loading ? (
          <div className="drawer-loading"><span className="spinner spinner--dark" /> Loading requisition…</div>
        ) : request ? (
          <form className="purchase-order-form" onSubmit={handleSubmit}>
            <div className="purchase-order-reference">
              <span>Approved requisition</span>
              <strong>{request.requestNumber}</strong>
              <small>{request.purpose}</small>
            </div>

            {error && <div className="form-alert" role="alert">{error}</div>}

            <div className="purchase-order-fields">
              <label className="field">
                <span>Supplier *</span>
                <select
                  value={supplierId}
                  onChange={(event) => {
                    const nextSupplierId = event.target.value
                    setSupplierId(nextSupplierId)
                    const supplier = suppliers.find((item) => item.id === nextSupplierId)
                    if (supplier) setCurrency(supplier.defaultCurrency)
                  }}
                  required
                >
                  <option value="" disabled>Select supplier</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>
                      {supplier.displayName} · {supplier.code}
                    </option>
                  ))}
                </select>
                <small>
                  {supplierId
                    ? `${suppliers.find((item) => item.id === supplierId)?.paymentTermsDays ?? 0}-day payment terms`
                    : 'Only active suppliers from Supplier master are available.'}
                </small>
              </label>
              <label className="field">
                <span>Currency *</span>
                <select value={currency} onChange={(event) => setCurrency(event.target.value)}>
                  <option value="INR">INR</option>
                  <option value="USD">USD</option>
                </select>
              </label>
            </div>

            <div className="purchase-order-lines-heading">
              <strong>Requisition pricing</strong>
              <span>Enter the agreed unit price for every requested material.</span>
            </div>
            <div className="purchase-order-lines">
              {request.items.map((item) => (
                <div className="purchase-order-line" key={item.productId}>
                  <div>
                    <strong>{catalogItems.find((catalogItem) => catalogItem.id === item.productId)?.name ?? item.productId}</strong>
                    <small>{item.quantity} {item.unitOfMeasure} requested</small>
                  </div>
                  <label>
                    <span>Unit price ({currency})</span>
                    <input
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={prices[item.productId] ?? ''}
                      onChange={(event) => setPrices((current) => ({
                        ...current,
                        [item.productId]: event.target.value,
                      }))}
                      placeholder="0.00"
                      required
                    />
                  </label>
                </div>
              ))}
            </div>

            <div className="drawer-actions">
              <button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
              <button className="primary-button" type="submit" disabled={submitting}>
                {submitting ? <><span className="spinner" /> Creating…</> : 'Create PO'}
              </button>
            </div>
          </form>
        ) : (
          <div className="drawer-loading">{error || 'The requisition is unavailable.'}</div>
        )}
      </aside>
    </div>
  )
}
