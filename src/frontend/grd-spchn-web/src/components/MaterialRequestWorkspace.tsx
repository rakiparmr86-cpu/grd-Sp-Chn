import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type LoginResponse,
  type MaterialRequest,
  type MaterialRequestListItem,
  type OrganizationUnit,
} from '../api'
import { hasPermission } from '../auth'
import { PurchaseOrderPanel } from './PurchaseOrderPanel'

interface MaterialRequestWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

interface MaterialLineForm {
  key: string
  productId: string
  quantity: string
  unitOfMeasure: string
}

const materialCatalog = [
  {
    id: '30000000-0000-0000-0000-000000000001',
    name: 'Packing Bag · 70 kg capacity',
    unitOfMeasure: 'BAG',
  },
  {
    id: '30000000-0000-0000-0000-000000000002',
    name: 'Production Coal',
    unitOfMeasure: 'MT',
  },
  {
    id: '30000000-0000-0000-0000-000000000003',
    name: 'Furnace Oil',
    unitOfMeasure: 'LTR',
  },
] as const

function createLine(catalogIndex = 0): MaterialLineForm {
  const material = materialCatalog[catalogIndex] ?? materialCatalog[0]
  return {
    key: crypto.randomUUID(),
    productId: material.id,
    quantity: '',
    unitOfMeasure: material.unitOfMeasure,
  }
}

export function MaterialRequestWorkspace({
  session,
  onBack,
}: MaterialRequestWorkspaceProps) {
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnit[]>([])
  const [purpose, setPurpose] = useState('')
  const [lines, setLines] = useState<MaterialLineForm[]>([createLine()])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState<MaterialRequest | null>(null)
  const [requests, setRequests] = useState<MaterialRequestListItem[]>([])
  const [listLoading, setListLoading] = useState(true)
  const [listError, setListError] = useState('')
  const canCreateRequest = hasPermission(session, 'procurement.material-request.create')
  const canApproveRequest = hasPermission(session, 'procurement.material-request.approve')
  const canCreatePurchaseOrder = hasPermission(session, 'procurement.purchase-order.create')
  const [actingRequestId, setActingRequestId] = useState<string | null>(null)
  const [purchaseOrderRequestId, setPurchaseOrderRequestId] = useState<string | null>(null)
  const [workflowMessage, setWorkflowMessage] = useState('')

  useEffect(() => {
    let active = true
    api.getOrganizationUnits(session.accessToken)
      .then((units) => {
        if (active) setOrganizationUnits(units)
      })
      .catch(() => {
        // The authenticated organization id remains authoritative if names cannot be loaded.
      })
    api.listMaterialRequests(session.accessToken)
      .then((items) => {
        if (active) setRequests(items)
      })
      .catch((reason: unknown) => {
        if (!active) return
        setListError(reason instanceof ApiError
          ? reason.message
          : 'Could not load requisitions.')
      })
      .finally(() => {
        if (active) setListLoading(false)
      })

    return () => {
      active = false
    }
  }, [session.accessToken])

  const requestingUnit = useMemo(
    () => organizationUnits.find((unit) => unit.id === session.organizationUnitId),
    [organizationUnits, session.organizationUnitId],
  )

  function updateLine(key: string, updates: Partial<MaterialLineForm>) {
    setLines((current) => current.map((line) => (
      line.key === key ? { ...line, ...updates } : line
    )))
    setCreated(null)
  }

  function selectMaterial(key: string, productId: string) {
    const material = materialCatalog.find((entry) => entry.id === productId)
    updateLine(key, {
      productId,
      unitOfMeasure: material?.unitOfMeasure ?? 'EA',
    })
  }

  function addLine() {
    const unusedIndex = materialCatalog.findIndex(
      (material) => !lines.some((line) => line.productId === material.id),
    )
    setLines((current) => [...current, createLine(unusedIndex >= 0 ? unusedIndex : 0)])
    setCreated(null)
  }

  function removeLine(key: string) {
    setLines((current) => current.filter((line) => line.key !== key))
    setCreated(null)
  }

  function resetForm() {
    setPurpose('')
    setLines([createLine()])
    setError('')
    setCreated(null)
  }

  async function refreshRequests() {
    setListLoading(true)
    setListError('')
    try {
      setRequests(await api.listMaterialRequests(session.accessToken))
    } catch (reason) {
      setListError(reason instanceof ApiError
        ? reason.message
        : 'Could not load requisitions.')
    } finally {
      setListLoading(false)
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setCreated(null)

    const duplicateProducts = new Set(lines.map((line) => line.productId)).size !== lines.length
    if (duplicateProducts) {
      setError('Each material can appear only once in a requisition.')
      return
    }

    if (lines.some((line) => Number(line.quantity) <= 0)) {
      setError('Enter a quantity greater than zero for every material.')
      return
    }

    setSubmitting(true)
    try {
      const result = await api.createMaterialRequest(session.accessToken, {
        purpose: purpose.trim(),
        items: lines.map((line) => ({
          productId: line.productId,
          quantity: Number(line.quantity),
          unitOfMeasure: line.unitOfMeasure,
        })),
      })
      setCreated(result)
      await refreshRequests()
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The requisition could not be sent to the Purchase Department.')
    } finally {
      setSubmitting(false)
    }
  }

  async function approveRequest(request: MaterialRequestListItem) {
    setActingRequestId(request.id)
    setListError('')
    setWorkflowMessage('')
    try {
      const approved = await api.approveMaterialRequest(session.accessToken, request.id)
      setWorkflowMessage(`${approved.requestNumber} was approved and is ready for a purchase order.`)
      await refreshRequests()
    } catch (reason) {
      setListError(reason instanceof ApiError
        ? reason.message
        : 'The requisition could not be approved.')
    } finally {
      setActingRequestId(null)
    }
  }

  return (
    <section className="requisition-workspace" aria-labelledby="material-request-title">
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1 id="material-request-title">
            {canCreateRequest ? 'GRD M. Requisition' : 'Material requisitions'}
          </h1>
          <p>
            {canCreateRequest
              ? 'Raise a plant requirement for review by the Purchase Department.'
              : 'Review plant and branch requirements sent to the Purchase Department.'}
          </p>
        </div>
        <span className="workspace-status">
          <i /> {canCreateRequest ? 'New request' : 'Purchase review'}
        </span>
      </header>

      {canCreateRequest && (
      <form className="requisition-card" onSubmit={handleSubmit}>
        <div className="requisition-card__heading">
          <div>
            <span className="eyebrow">Request details</span>
            <h2>Plant requirement</h2>
          </div>
          <span>Required fields are marked *</span>
        </div>

        <div className="requisition-routing-grid">
          <div className="read-only-field">
            <span>Requesting location</span>
            <strong>{requestingUnit?.name ?? 'Delhi Manufacturing Plant'}</strong>
            <small>{requestingUnit?.code ?? session.organizationUnitId}</small>
          </div>
          <div className="read-only-field">
            <span>Send to</span>
            <strong>Purchase Department</strong>
            <small>Approval and supplier sourcing queue</small>
          </div>
          <div className="read-only-field">
            <span>Requested by</span>
            <strong>{session.userName}</strong>
            <small>{session.role} · {session.accessProfile}</small>
          </div>
        </div>

        <label className="field requisition-purpose">
          <span>Business purpose *</span>
          <input
            value={purpose}
            onChange={(event) => {
              setPurpose(event.target.value)
              setCreated(null)
            }}
            maxLength={500}
            placeholder="Example: Packing bags required for September production"
            required
          />
        </label>

        <div className="request-lines-heading">
          <div>
            <h3>Requested materials</h3>
            <span>Add the item, required quantity, and unit of measure.</span>
          </div>
          <button
            className="add-line-button"
            type="button"
            onClick={addLine}
            disabled={lines.length >= materialCatalog.length}
          >
            + Add material
          </button>
        </div>

        <div className="request-lines" role="group" aria-label="Requested material lines">
          <div className="request-line request-line--header" aria-hidden="true">
            <span>Material</span>
            <span>Quantity</span>
            <span>UOM</span>
            <span />
          </div>
          {lines.map((line, index) => (
            <div className="request-line" key={line.key}>
              <label>
                <span className="mobile-field-label">Material</span>
                <select
                  value={line.productId}
                  onChange={(event) => selectMaterial(line.key, event.target.value)}
                  aria-label={`Material ${index + 1}`}
                  required
                >
                  {materialCatalog.map((material) => (
                    <option key={material.id} value={material.id}>{material.name}</option>
                  ))}
                </select>
              </label>
              <label>
                <span className="mobile-field-label">Quantity</span>
                <input
                  type="number"
                  min="0.001"
                  step="0.001"
                  value={line.quantity}
                  onChange={(event) => updateLine(line.key, { quantity: event.target.value })}
                  placeholder="0.000"
                  aria-label={`Quantity ${index + 1}`}
                  required
                />
              </label>
              <label>
                <span className="mobile-field-label">UOM</span>
                <select
                  value={line.unitOfMeasure}
                  onChange={(event) => updateLine(line.key, { unitOfMeasure: event.target.value })}
                  aria-label={`Unit of measure ${index + 1}`}
                  required
                >
                  <option value="BAG">BAG</option>
                  <option value="EA">EA</option>
                  <option value="KG">KG</option>
                  <option value="MT">MT</option>
                  <option value="LTR">LTR</option>
                </select>
              </label>
              <button
                className="remove-line-button"
                type="button"
                onClick={() => removeLine(line.key)}
                disabled={lines.length === 1}
                aria-label={`Remove material ${index + 1}`}
              >
                ×
              </button>
            </div>
          ))}
        </div>

        {error && <div className="form-alert requisition-message" role="alert">{error}</div>}
        {created && (
          <div className="success-alert requisition-message" role="status">
            <strong>{created.requestNumber}</strong> was submitted to the Purchase Department.
            Current status: <strong>{created.status}</strong>.
          </div>
        )}

        <div className="requisition-actions">
          <button className="secondary-button" type="button" onClick={resetForm}>Clear</button>
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? <><span className="spinner" /> Sending…</> : 'Send requisition'}
          </button>
        </div>
      </form>
      )}

      <section className="requisition-list-card" aria-labelledby="requisition-list-title">
        <div className="requisition-list-heading">
          <div>
            <strong id="requisition-list-title">Requisition list</strong>
            <span>Track approval, purchase order, and material dispatch.</span>
          </div>
          <button type="button" onClick={refreshRequests} disabled={listLoading}>
            {listLoading ? 'Refreshing…' : '↻ Refresh'}
          </button>
        </div>

        {listError && <div className="form-alert requisition-list-alert" role="alert">{listError}</div>}
        {workflowMessage && (
          <div className="success-alert requisition-list-alert" role="status">{workflowMessage}</div>
        )}
        {listLoading && requests.length === 0 ? (
          <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading requisitions…</div>
        ) : requests.length === 0 ? (
          <div className="requisition-list-empty">
            {canCreateRequest
              ? 'No requisitions have been submitted from this location.'
              : 'No requisitions are available in your organization scope.'}
          </div>
        ) : (
          <div className="requisition-table-scroll">
            <table className="requisition-table">
              <thead>
                <tr>
                  <th>Requisition</th>
                  <th>Purpose</th>
                  <th>Request status</th>
                  <th>Purchase order</th>
                  <th>Material dispatch</th>
                  <th>Created</th>
                  {(canApproveRequest || canCreatePurchaseOrder) && <th>Action</th>}
                </tr>
              </thead>
              <tbody>
                {requests.map((request) => (
                  <tr key={request.id}>
                    <td>
                      <strong>{request.requestNumber}</strong>
                      <small>{request.itemCount} material{request.itemCount === 1 ? '' : 's'}</small>
                    </td>
                    <td className="requisition-purpose-cell" title={request.purpose}>{request.purpose}</td>
                    <td><span className={`tracking-status tracking-status--${request.status.toLowerCase()}`}>{request.status}</span></td>
                    <td>
                      {request.purchaseOrderCreated ? (
                        <span className="tracking-answer is-yes">
                          <i /> Yes<small>{request.purchaseOrderNumber}</small>
                        </span>
                      ) : (
                        <span className="tracking-answer is-no"><i /> Not created</span>
                      )}
                    </td>
                    <td>
                      {request.materialDispatched ? (
                        <span className="tracking-answer is-yes">
                          <i /> Dispatched
                          {request.dispatchedOnUtc && <small>{new Date(request.dispatchedOnUtc).toLocaleDateString('en-IN')}</small>}
                        </span>
                      ) : (
                        <span className="tracking-answer is-waiting"><i /> Not dispatched</span>
                      )}
                    </td>
                    <td>{new Date(request.createdOnUtc).toLocaleDateString('en-IN')}</td>
                    {(canApproveRequest || canCreatePurchaseOrder) && (
                      <td className="requisition-action-cell">
                        {request.status === 'Submitted' && canApproveRequest ? (
                          <button
                            className="table-action-button"
                            type="button"
                            onClick={() => approveRequest(request)}
                            disabled={actingRequestId === request.id}
                          >
                            {actingRequestId === request.id ? 'Approving…' : 'Approve'}
                          </button>
                        ) : request.status === 'Approved' && canCreatePurchaseOrder ? (
                          <button
                            className="table-action-button table-action-button--primary"
                            type="button"
                            onClick={() => {
                              setWorkflowMessage('')
                              setPurchaseOrderRequestId(request.id)
                            }}
                          >
                            Create PO
                          </button>
                        ) : (
                          <span className="table-action-complete">
                            {request.purchaseOrderCreated ? 'PO created' : 'No action'}
                          </span>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {purchaseOrderRequestId && (
        <PurchaseOrderPanel
          accessToken={session.accessToken}
          materialRequestId={purchaseOrderRequestId}
          onClose={() => setPurchaseOrderRequestId(null)}
          onCreated={(purchaseOrder) => {
            setPurchaseOrderRequestId(null)
            setWorkflowMessage(
              `${purchaseOrder.purchaseOrderNumber} was created and sent to the receiving workflow.`,
            )
            void refreshRequests()
          }}
        />
      )}
    </section>
  )
}
