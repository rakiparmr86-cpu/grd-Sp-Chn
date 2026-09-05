import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type ExpectedPurchaseOrder,
  type GoodsReceipt,
} from '../api'

interface GoodsReceiptPanelProps {
  accessToken: string
  purchaseOrderId: string
  purchaseOrderNumber: string | null
  catalogItems: CatalogItem[]
  onClose: () => void
  onRecorded: (receipt: GoodsReceipt) => void
}

export function GoodsReceiptPanel({
  accessToken,
  purchaseOrderId,
  purchaseOrderNumber,
  catalogItems,
  onClose,
  onRecorded,
}: GoodsReceiptPanelProps) {
  const [expectedOrder, setExpectedOrder] = useState<ExpectedPurchaseOrder | null>(null)
  const [confirmed, setConfirmed] = useState(false)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const loadExpectedOrder = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setExpectedOrder(await api.getExpectedPurchaseOrder(accessToken, purchaseOrderId))
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The expected purchase order could not be loaded from Warehouse.')
    } finally {
      setLoading(false)
    }
  }, [accessToken, purchaseOrderId])

  useEffect(() => {
    void loadExpectedOrder()
  }, [loadExpectedOrder])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!expectedOrder || !confirmed) return

    setSubmitting(true)
    setError('')
    try {
      const receipt = await api.postGoodsReceipt(
        accessToken,
        purchaseOrderId,
        expectedOrder.items,
      )
      onRecorded(receipt)
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The goods receipt could not be posted.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside
        className="user-drawer goods-receipt-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="goods-receipt-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="drawer-header">
          <div>
            <span className="eyebrow">Warehouse / Store</span>
            <h2 id="goods-receipt-title">Receive material.</h2>
            <p>Physically verify the supplier delivery, then post the Goods Receipt Note.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        <form className="purchase-order-form" onSubmit={handleSubmit}>
          <div className="purchase-order-reference">
            <span>Expected purchase order</span>
            <strong>{expectedOrder?.purchaseOrderNumber ?? purchaseOrderNumber ?? purchaseOrderId}</strong>
            <small>The receiving location is taken from your authenticated organization scope.</small>
          </div>

          {error && (
            <div className="form-alert" role="alert">
              {error}
              <button className="inline-retry-button" type="button" onClick={() => void loadExpectedOrder()}>
                Retry
              </button>
            </div>
          )}

          {loading ? (
            <div className="requisition-list-empty">
              <span className="spinner spinner--dark" /> Loading expected delivery…
            </div>
          ) : expectedOrder ? (
            <>
              <div className="goods-receipt-lines" aria-label="Materials to receive">
                {expectedOrder.items.map((item) => {
                  const material = catalogItems.find((entry) => entry.id === item.productId)
                  return (
                    <div className="goods-receipt-line" key={item.productId}>
                      <div>
                        <strong>{material?.name ?? item.productId}</strong>
                        <small>{material?.code ?? 'Catalog item'}</small>
                      </div>
                      <strong>{item.quantity.toLocaleString('en-IN')} {item.unitOfMeasure}</strong>
                    </div>
                  )
                })}
              </div>

              {expectedOrder.status === 'Received' ? (
                <div className="success-alert">This purchase order has already been received.</div>
              ) : (
                <label className="receipt-confirmation">
                  <input
                    type="checkbox"
                    checked={confirmed}
                    onChange={(event) => setConfirmed(event.target.checked)}
                  />
                  <span>
                    I physically checked the delivered materials and confirm every quantity and UOM above.
                    Version 1 accepts only a complete receipt.
                  </span>
                </label>
              )}
            </>
          ) : null}

          <div className="drawer-actions">
            <button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
            <button
              className="primary-button"
              type="submit"
              disabled={loading || submitting || !confirmed || expectedOrder?.status !== 'Expected'}
            >
              {submitting ? <><span className="spinner" /> Posting GRN…</> : 'Post goods receipt'}
            </button>
          </div>
        </form>
      </aside>
    </div>
  )
}
