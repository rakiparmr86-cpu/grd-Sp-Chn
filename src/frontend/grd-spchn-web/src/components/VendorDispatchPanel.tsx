import { useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type PurchaseOrder,
} from '../api'

interface VendorDispatchPanelProps {
  accessToken: string
  purchaseOrder: PurchaseOrder
  supplierName: string
  onClose: () => void
  onRecorded: (purchaseOrder: PurchaseOrder) => void
}

const today = () => new Date().toISOString().slice(0, 10)

export function VendorDispatchPanel({
  accessToken,
  purchaseOrder,
  supplierName,
  onClose,
  onRecorded,
}: VendorDispatchPanelProps) {
  const [vendorReference, setVendorReference] = useState('')
  const [challanNumber, setChallanNumber] = useState('')
  const [transporterName, setTransporterName] = useState('')
  const [vehicleNumber, setVehicleNumber] = useState('')
  const [dispatchDate, setDispatchDate] = useState(today())
  const [expectedDeliveryDate, setExpectedDeliveryDate] = useState('')
  const [notes, setNotes] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setSubmitting(true)
    try {
      const updated = await api.recordVendorDispatch(
        accessToken,
        purchaseOrder.id,
        {
          vendorDispatchReference: vendorReference.trim(),
          deliveryChallanNumber: challanNumber.trim() || null,
          transporterName: transporterName.trim() || null,
          vehicleNumber: vehicleNumber.trim() || null,
          dispatchedOnUtc: new Date(`${dispatchDate}T00:00:00Z`).toISOString(),
          expectedDeliveryOnUtc: expectedDeliveryDate
            ? new Date(`${expectedDeliveryDate}T00:00:00Z`).toISOString()
            : null,
          notes: notes.trim() || null,
        },
      )
      onRecorded(updated)
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'Vendor dispatch could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <aside
        className="user-drawer vendor-dispatch-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="vendor-dispatch-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="drawer-header">
          <div>
            <span className="eyebrow">Procurement</span>
            <h2 id="vendor-dispatch-title">Record vendor dispatch.</h2>
            <p>The supplier needs no ERP login. Record the dispatch advice received by Purchase.</p>
          </div>
          <button className="close-button" type="button" onClick={onClose} aria-label="Close">×</button>
        </header>

        <form className="purchase-order-form" onSubmit={handleSubmit}>
          <div className="purchase-order-reference">
            <span>Purchase order</span>
            <strong>{purchaseOrder.purchaseOrderNumber}</strong>
            <small>{supplierName} · {purchaseOrder.currency} {purchaseOrder.totalAmount.toLocaleString('en-IN')}</small>
          </div>

          {error && <div className="form-alert" role="alert">{error}</div>}

          <div className="vendor-dispatch-grid">
            <label className="field">
              <span>Vendor dispatch reference *</span>
              <input value={vendorReference} onChange={(event) => setVendorReference(event.target.value)} maxLength={80} placeholder="Example: DSP-2026-1042" required />
            </label>
            <label className="field">
              <span>Delivery challan</span>
              <input value={challanNumber} onChange={(event) => setChallanNumber(event.target.value)} maxLength={80} placeholder="Challan number" />
            </label>
            <label className="field">
              <span>Transporter</span>
              <input value={transporterName} onChange={(event) => setTransporterName(event.target.value)} maxLength={160} placeholder="Transport company" />
            </label>
            <label className="field">
              <span>Vehicle number</span>
              <input value={vehicleNumber} onChange={(event) => setVehicleNumber(event.target.value.toUpperCase())} maxLength={40} placeholder="DL 01 AB 1234" />
            </label>
            <label className="field">
              <span>Dispatch date *</span>
              <input type="date" value={dispatchDate} onChange={(event) => setDispatchDate(event.target.value)} required />
            </label>
            <label className="field">
              <span>Expected delivery</span>
              <input type="date" min={dispatchDate} value={expectedDeliveryDate} onChange={(event) => setExpectedDeliveryDate(event.target.value)} />
            </label>
          </div>

          <label className="field">
            <span>Internal notes</span>
            <textarea value={notes} onChange={(event) => setNotes(event.target.value)} maxLength={500} rows={3} placeholder="Optional dispatch or receiving instructions" />
          </label>

          <div className="success-alert">
            You are recording information supplied by <strong>{supplierName}</strong>.
            Your signed-in user ID will be retained as the internal audit owner.
          </div>

          <div className="drawer-actions">
            <button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
            <button className="primary-button" type="submit" disabled={submitting}>
              {submitting ? <><span className="spinner" /> Recording…</> : 'Record dispatch'}
            </button>
          </div>
        </form>
      </aside>
    </div>
  )
}
