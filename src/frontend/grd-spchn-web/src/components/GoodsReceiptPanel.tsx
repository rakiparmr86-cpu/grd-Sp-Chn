import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type ExpectedPurchaseOrder,
  type GoodsReceiptItem,
  type GoodsReceipt,
  type QualityInspection,
} from '../api'
import { ScreenName } from './ScreenName'
import { SCREEN } from '../config/screens'

interface GoodsReceiptPanelProps {
  accessToken: string
  purchaseOrderId: string
  purchaseOrderNumber: string | null
  catalogItems: CatalogItem[]
  canInspectQuality: boolean
  onClose: () => void
  onQualityCompleted: (inspection: QualityInspection, completesPurchaseOrder: boolean) => void
}

export function GoodsReceiptPanel({
  accessToken,
  purchaseOrderId,
  purchaseOrderNumber,
  catalogItems,
  canInspectQuality,
  onClose,
  onQualityCompleted,
}: GoodsReceiptPanelProps) {
  const [expectedOrder, setExpectedOrder] = useState<ExpectedPurchaseOrder | null>(null)
  const [goodsReceipt, setGoodsReceipt] = useState<GoodsReceipt | null>(null)
  const [inspection, setInspection] = useState<QualityInspection | null>(null)
  const [receivedQuantities, setReceivedQuantities] = useState<Record<string, string>>({})
  const [confirmed, setConfirmed] = useState(false)
  const [qualityResult, setQualityResult] = useState<'Passed' | 'Rejected'>('Passed')
  const [qualityNotes, setQualityNotes] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [qualitySubmitting, setQualitySubmitting] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const loadExpectedOrder = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const order = await api.getExpectedPurchaseOrder(accessToken, purchaseOrderId)
      setExpectedOrder(order)
      setReceivedQuantities(Object.fromEntries(order.items.map((item) => [
        item.productId,
        String(item.remainingQuantity ?? Math.max(item.quantity - (item.receivedQuantity ?? 0), 0)),
      ])))
      if (order.status !== 'Expected' && canInspectQuality) {
        const context = await api.getQualityInspection(accessToken, purchaseOrderId)
        setGoodsReceipt(context.goodsReceipt)
        setInspection(context.inspection)
      }
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The receiving record could not be loaded from Warehouse.')
    } finally {
      setLoading(false)
    }
  }, [accessToken, purchaseOrderId, canInspectQuality])

  useEffect(() => {
    void loadExpectedOrder()
  }, [loadExpectedOrder])

  async function handleReceipt(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!expectedOrder || !confirmed) return

    const items: GoodsReceiptItem[] = expectedOrder.items
      .map((item) => ({
        productId: item.productId,
        quantity: Number(receivedQuantities[item.productId] ?? 0),
        unitOfMeasure: item.unitOfMeasure,
      }))
      .filter((item) => item.quantity > 0)
    if (items.length === 0) {
      setError('Enter a received quantity greater than zero for at least one material.')
      return
    }
    const invalidItem = items.find((received) => {
      const expected = expectedOrder.items.find((item) => item.productId === received.productId)
      const remaining = expected?.remainingQuantity ?? expected?.quantity ?? 0
      return !Number.isFinite(received.quantity) || received.quantity > remaining
    })
    if (invalidItem) {
      setError('Received quantity cannot be greater than the remaining purchase-order quantity.')
      return
    }

    setSubmitting(true)
    setError('')
    try {
      const receipt = await api.postGoodsReceipt(
        accessToken,
        purchaseOrderId,
        items,
      )
      setGoodsReceipt(receipt)
      setInspection(null)
      const refreshedOrder = await api.getExpectedPurchaseOrder(accessToken, purchaseOrderId)
      setExpectedOrder(refreshedOrder)
      setReceivedQuantities(Object.fromEntries(refreshedOrder.items.map((item) => [
        item.productId,
        String(item.remainingQuantity ?? Math.max(item.quantity - (item.receivedQuantity ?? 0), 0)),
      ])))
      setConfirmed(false)
      setNotice(
        `${receipt.goodsReceiptNumber} was posted for the actual delivered quantity. ` +
        `Material is quarantined until Quality passes it.${receipt.completesPurchaseOrder ? '' : ' The PO remains open for the balance.'}`,
      )
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The goods receipt could not be posted.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleQuality(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!goodsReceipt) return
    if (qualityResult === 'Rejected' && !qualityNotes.trim()) {
      setError('Enter a rejection reason before failing the quality inspection.')
      return
    }

    setQualitySubmitting(true)
    setError('')
    try {
      const completed = await api.completeQualityInspection(
        accessToken,
        purchaseOrderId,
        qualityResult,
        qualityNotes.trim() || null,
      )
      setInspection(completed)
      onQualityCompleted(completed, goodsReceipt.completesPurchaseOrder)
      if (expectedOrder?.status !== 'Received') {
        setNotice(`Quality is ${completed.result}. You can post the next delivery for the remaining PO quantity.`)
      }
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The quality inspection could not be completed.')
    } finally {
      setQualitySubmitting(false)
    }
  }

  const showReceiptForm = Boolean(
    expectedOrder &&
    expectedOrder.status !== 'Received' &&
    (!goodsReceipt || inspection !== null),
  )
  const itemLines = showReceiptForm
    ? expectedOrder?.items ?? []
    : goodsReceipt?.items ?? expectedOrder?.items ?? []

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
            <h2 id="goods-receipt-title"><ScreenName id={SCREEN.goodsReceipt} /></h2>
            <p>Physical receipt creates quarantine stock. Quality approval releases usable inventory.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        <div className="purchase-order-form">
          <div className="purchase-order-reference">
            <span>Expected purchase order</span>
            <strong>{expectedOrder?.purchaseOrderNumber ?? purchaseOrderNumber ?? purchaseOrderId}</strong>
            <small>The receiver and location are taken from the authenticated user token.</small>
          </div>

          {error && (
            <div className="form-alert" role="alert">
              {error}
              <button className="inline-retry-button" type="button" onClick={() => void loadExpectedOrder()}>
                Retry
              </button>
            </div>
          )}
          {notice && <div className="success-alert" role="status">{notice}</div>}

          {loading ? (
            <div className="requisition-list-empty">
              <span className="spinner spinner--dark" /> Loading receiving status…
            </div>
          ) : expectedOrder ? (
            <>
              <div className="goods-receipt-lines" aria-label={showReceiptForm ? 'Purchase order quantities' : 'Materials received'}>
                {itemLines.map((item) => {
                  const material = catalogItems.find((entry) => entry.id === item.productId)
                  const expectedItem = expectedOrder.items.find((entry) => entry.productId === item.productId)
                  const receivedToDate = expectedItem?.receivedQuantity ?? 0
                  const remaining = expectedItem?.remainingQuantity ?? Math.max((expectedItem?.quantity ?? item.quantity) - receivedToDate, 0)
                  return (
                    <div className="goods-receipt-line" key={item.productId}>
                      <div>
                        <strong>{material?.name ?? item.productId}</strong>
                        <small>
                          {material?.code ?? 'Catalog item'}
                          {showReceiptForm && ` · Ordered ${expectedItem?.quantity.toLocaleString('en-IN')} ${item.unitOfMeasure} · Received ${receivedToDate.toLocaleString('en-IN')} · Balance ${remaining.toLocaleString('en-IN')}`}
                        </small>
                      </div>
                      {showReceiptForm ? (
                        <label className="received-quantity-field">
                          <span>Receive now</span>
                          <div>
                            <input
                              type="number"
                              min="0"
                              max={remaining}
                              step="0.001"
                              inputMode="decimal"
                              value={receivedQuantities[item.productId] ?? ''}
                              onChange={(event) => {
                                setReceivedQuantities((current) => ({
                                  ...current,
                                  [item.productId]: event.target.value,
                                }))
                                setConfirmed(false)
                              }}
                              aria-label={`Quantity received for ${material?.name ?? item.productId}`}
                            />
                            <strong>{item.unitOfMeasure}</strong>
                          </div>
                        </label>
                      ) : (
                        <strong>{item.quantity.toLocaleString('en-IN')} {item.unitOfMeasure}</strong>
                      )}
                    </div>
                  )
                })}
              </div>

              {showReceiptForm ? (
                <form className="receipt-stage" onSubmit={handleReceipt}>
                  <div className="workflow-stage-heading">
                    <span>Step 1</span>
                    <div><strong>Post physical receipt</strong><small>Creates a GRN; inventory remains quarantined.</small></div>
                  </div>
                  <label className="receipt-confirmation">
                    <input
                      type="checkbox"
                      checked={confirmed}
                      onChange={(event) => setConfirmed(event.target.checked)}
                    />
                    <span>I physically checked the delivery and confirm the manually entered quantities and UOM above.</span>
                  </label>
                  <div className="drawer-actions">
                    <button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
                    <button className="primary-button" type="submit" disabled={submitting || !confirmed}>
                      {submitting ? <><span className="spinner" /> Posting GRN…</> : 'Post goods receipt'}
                    </button>
                  </div>
                </form>
              ) : inspection ? (
                <div className={inspection.result === 'Passed' ? 'success-alert' : 'form-alert'}>
                  Quality inspection is <strong>{inspection.result}</strong>.
                  {inspection.notes && <> {inspection.notes}</>}
                </div>
              ) : canInspectQuality && goodsReceipt ? (
                <form className="quality-stage" onSubmit={handleQuality}>
                  <div className="workflow-stage-heading">
                    <span>Step 2</span>
                    <div><strong>Complete quality test</strong><small>Only Passed material is released into Inventory.</small></div>
                  </div>
                  <div className="quality-result-options">
                    <label className={qualityResult === 'Passed' ? 'is-selected' : ''}>
                      <input type="radio" name="quality-result" checked={qualityResult === 'Passed'} onChange={() => setQualityResult('Passed')} />
                      <span><strong>Pass</strong><small>Release received quantity to usable stock.</small></span>
                    </label>
                    <label className={qualityResult === 'Rejected' ? 'is-selected is-rejected' : ''}>
                      <input type="radio" name="quality-result" checked={qualityResult === 'Rejected'} onChange={() => setQualityResult('Rejected')} />
                      <span><strong>Reject</strong><small>Keep it out of usable inventory.</small></span>
                    </label>
                  </div>
                  <label className="field">
                    <span>Quality notes {qualityResult === 'Rejected' ? '*' : ''}</span>
                    <textarea
                      rows={4}
                      maxLength={1000}
                      value={qualityNotes}
                      onChange={(event) => setQualityNotes(event.target.value)}
                      placeholder={qualityResult === 'Rejected' ? 'Describe the quality failure' : 'Optional test observations'}
                      required={qualityResult === 'Rejected'}
                    />
                  </label>
                  <div className="drawer-actions">
                    <button className="secondary-button" type="button" onClick={onClose}>Do later</button>
                    <button className="primary-button" type="submit" disabled={qualitySubmitting}>
                      {qualitySubmitting ? <><span className="spinner" /> Saving test…</> : `Confirm ${qualityResult}`}
                    </button>
                  </div>
                </form>
              ) : (
                <div className="permission-token-note">
                  GRN {goodsReceipt?.goodsReceiptNumber ?? ''} is awaiting a user with
                  `warehouse.quality-inspection.post` permission.
                </div>
              )}
            </>
          ) : null}
        </div>
      </aside>
    </div>
  )
}
