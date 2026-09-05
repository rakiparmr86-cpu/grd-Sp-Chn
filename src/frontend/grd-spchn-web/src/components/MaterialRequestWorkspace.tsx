import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type LoginResponse,
  type MaterialRequest,
  type MaterialRequestListItem,
  type OrganizationUnit,
  type PurchaseOrder,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { PurchaseOrderPanel } from './PurchaseOrderPanel'
import { VendorDispatchPanel } from './VendorDispatchPanel'
import { GoodsReceiptPanel } from './GoodsReceiptPanel'

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

function createLine(catalog: CatalogItem[], catalogIndex = 0): MaterialLineForm {
  const material = catalog[catalogIndex] ?? catalog[0]
  return {
    key: crypto.randomUUID(),
    productId: material?.id ?? '',
    quantity: '',
    unitOfMeasure: material?.baseUnitOfMeasure ?? '',
  }
}

export function MaterialRequestWorkspace({
  session,
  onBack,
}: MaterialRequestWorkspaceProps) {
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnit[]>([])
  const [catalogItems, setCatalogItems] = useState<CatalogItem[]>([])
  const [catalogLoading, setCatalogLoading] = useState(true)
  const [catalogError, setCatalogError] = useState('')
  const [purpose, setPurpose] = useState('')
  const [lines, setLines] = useState<MaterialLineForm[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState<MaterialRequest | null>(null)
  const [requests, setRequests] = useState<MaterialRequestListItem[]>([])
  const [listLoading, setListLoading] = useState(true)
  const [listError, setListError] = useState('')
  const canCreateRequest = hasPermission(session, 'procurement.material-request.create')
  const canApproveRequest = hasPermission(session, 'procurement.material-request.approve')
  const canCreatePurchaseOrder = hasPermission(session, 'procurement.purchase-order.create')
  const canReadPurchaseOrders = hasPermission(session, 'procurement.purchase-order.read')
  const canRecordDispatch = hasPermission(session, 'procurement.purchase-order.dispatch')
  const canPostGoodsReceipt = hasPermission(session, 'warehouse.goods-receipt.post')
  const canInspectQuality = hasPermission(session, 'warehouse.quality-inspection.post')
  const canHandleReceiving = canPostGoodsReceipt || canInspectQuality
  const [actingRequestId, setActingRequestId] = useState<string | null>(null)
  const [purchaseOrderRequestId, setPurchaseOrderRequestId] = useState<string | null>(null)
  const [workflowMessage, setWorkflowMessage] = useState('')
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrder[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [purchaseOrdersLoading, setPurchaseOrdersLoading] = useState(canReadPurchaseOrders)
  const [purchaseOrdersError, setPurchaseOrdersError] = useState('')
  const [dispatchOrder, setDispatchOrder] = useState<PurchaseOrder | null>(null)
  const [receiptRequest, setReceiptRequest] = useState<MaterialRequestListItem | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationUnits(session.accessToken)
      .then((units) => {
        if (active) setOrganizationUnits(units)
      })
    api.getProcurementItems(session.accessToken)
      .then((items) => {
        if (!active) return
        setCatalogItems(items)
        setLines((current) => current.length > 0 ? current : [createLine(items)])
      })
      .catch((reason: unknown) => {
        if (!active) return
        setCatalogError(reason instanceof ApiError
          ? reason.message
          : 'Could not load the Product Catalog item master.')
      })
      .finally(() => {
        if (active) setCatalogLoading(false)
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

    if (canReadPurchaseOrders) {
      Promise.all([
        api.listPurchaseOrders(session.accessToken),
        api.getSuppliers(session.accessToken),
      ])
        .then(([orders, supplierItems]) => {
          if (!active) return
          setPurchaseOrders(orders)
          setSuppliers(supplierItems)
        })
        .catch((reason: unknown) => {
          if (!active) return
          setPurchaseOrdersError(reason instanceof ApiError
            ? reason.message
            : 'Could not load purchase orders.')
        })
        .finally(() => {
          if (active) setPurchaseOrdersLoading(false)
        })
    }

    return () => {
      active = false
    }
  }, [canReadPurchaseOrders, session.accessToken])

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
    const material = catalogItems.find((entry) => entry.id === productId)
    updateLine(key, {
      productId,
      unitOfMeasure: material?.baseUnitOfMeasure ?? 'EA',
    })
  }

  function addLine() {
    const unusedIndex = catalogItems.findIndex(
      (material) => !lines.some((line) => line.productId === material.id),
    )
    setLines((current) => [...current, createLine(catalogItems, unusedIndex >= 0 ? unusedIndex : 0)])
    setCreated(null)
  }

  function removeLine(key: string) {
    setLines((current) => current.filter((line) => line.key !== key))
    setCreated(null)
  }

  function resetForm() {
    setPurpose('')
    setLines(catalogItems.length > 0 ? [createLine(catalogItems)] : [])
    setError('')
    setCreated(null)

    if (lines.length === 0) {
      setError('Select at least one active item from Product Catalog.')
      return
    }
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

  async function refreshPurchaseOrders() {
    if (!canReadPurchaseOrders) return
    setPurchaseOrdersLoading(true)
    setPurchaseOrdersError('')
    try {
      const [orders, supplierItems] = await Promise.all([
        api.listPurchaseOrders(session.accessToken),
        api.getSuppliers(session.accessToken),
      ])
      setPurchaseOrders(orders)
      setSuppliers(supplierItems)
    } catch (reason) {
      setPurchaseOrdersError(reason instanceof ApiError
        ? reason.message
        : 'Could not load purchase orders.')
    } finally {
      setPurchaseOrdersLoading(false)
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
            {canCreateRequest ? 'GRD M. Requisition' : 'Material R'}
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
            disabled={catalogLoading || catalogItems.length === 0 || lines.length >= catalogItems.length}
          >
            + Add material
          </button>
        </div>

        {catalogError && <div className="form-alert requisition-message" role="alert">Product Catalog: {catalogError}</div>}
        {catalogLoading && <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading item master…</div>}

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
                  {catalogItems.map((material) => (
                    <option key={material.id} value={material.id}>
                      {material.name} · {material.code}
                    </option>
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
                  {(canApproveRequest || canCreatePurchaseOrder || canRecordDispatch || canHandleReceiving) && <th>Action</th>}
                </tr>
              </thead>
              <tbody>
                {requests.map((request) => {
                  const linkedPurchaseOrder = request.purchaseOrderId
                    ? purchaseOrders.find((order) => order.id === request.purchaseOrderId)
                    : undefined

                  return (
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
                    {(canApproveRequest || canCreatePurchaseOrder || canRecordDispatch || canHandleReceiving) && (
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
                        ) : request.purchaseOrderCreated &&
                            !request.materialDispatched &&
                            linkedPurchaseOrder?.status === 'Issued' &&
                            canRecordDispatch ? (
                          <button
                            className="table-action-button table-action-button--primary"
                            type="button"
                            onClick={() => setDispatchOrder(linkedPurchaseOrder)}
                          >
                            Record dispatch
                          </button>
                        ) : request.purchaseOrderId &&
                            request.materialDispatched &&
                            request.purchaseOrderStatus !== 'Received' &&
                            canHandleReceiving ? (
                          <button
                            className="table-action-button table-action-button--primary"
                            type="button"
                            onClick={() => setReceiptRequest(request)}
                          >
                            Receive / quality
                          </button>
                        ) : (
                          <span className="table-action-complete">
                            {request.purchaseOrderStatus === 'Received'
                              ? 'GRN posted'
                              : request.purchaseOrderCreated ? 'PO created' : 'No action'}
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

      {canReadPurchaseOrders && (
        <section className="requisition-list-card purchase-order-list-card" aria-labelledby="purchase-order-list-title">
          <div className="requisition-list-heading">
            <div>
              <strong id="purchase-order-list-title">Purchase order list</strong>
              <span>Review supplier, ordered quantity, negotiated rate, amount, and dispatch state.</span>
            </div>
            <button type="button" onClick={refreshPurchaseOrders} disabled={purchaseOrdersLoading}>
              {purchaseOrdersLoading ? 'Refreshing…' : '↻ Refresh'}
            </button>
          </div>

          {purchaseOrdersError && <div className="form-alert requisition-list-alert" role="alert">{purchaseOrdersError}</div>}
          {purchaseOrdersLoading && purchaseOrders.length === 0 ? (
            <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading purchase orders…</div>
          ) : purchaseOrders.length === 0 ? (
            <div className="requisition-list-empty">No purchase orders have been created yet.</div>
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
                  {purchaseOrders.map((order) => {
                    const supplier = suppliers.find((item) => item.id === order.supplierId)
                    return (
                      <tr key={order.id}>
                        <td>
                          <strong>{order.purchaseOrderNumber}</strong>
                          <small>Requisition {order.materialRequestId.slice(0, 8)}…</small>
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
                                  {' × '}{order.currency} {item.unitPrice.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                                  {' = '}{order.currency} {lineAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                                </small>
                              </div>
                            )
                          })}
                        </td>
                        <td className="po-total-cell">
                          <strong>{order.currency} {(order.totalAmount ?? order.items.reduce(
                            (total, item) => total + item.quantity * item.unitPrice,
                            0,
                          )).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong>
                        </td>
                        <td><span className={`tracking-status tracking-status--${order.status.toLowerCase()}`}>{order.status}</span></td>
                        <td>{new Date(order.issuedOnUtc).toLocaleDateString('en-IN')}</td>
                        {canRecordDispatch && (
                          <td className="requisition-action-cell">
                            {order.status === 'Issued' ? (
                              <button
                                className="table-action-button table-action-button--primary"
                                type="button"
                                onClick={() => setDispatchOrder(order)}
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
      )}

      {purchaseOrderRequestId && (
        <PurchaseOrderPanel
          accessToken={session.accessToken}
          materialRequestId={purchaseOrderRequestId}
          catalogItems={catalogItems}
          onClose={() => setPurchaseOrderRequestId(null)}
          onCreated={(purchaseOrder) => {
            setPurchaseOrderRequestId(null)
            setWorkflowMessage(
              `${purchaseOrder.purchaseOrderNumber} was created and sent to the receiving workflow.`,
            )
            void refreshRequests()
            void refreshPurchaseOrders()
          }}
        />
      )}

      {dispatchOrder && (
        <VendorDispatchPanel
          accessToken={session.accessToken}
          purchaseOrder={dispatchOrder}
          supplierName={suppliers.find((supplier) => supplier.id === dispatchOrder.supplierId)?.displayName ?? 'Selected supplier'}
          onClose={() => setDispatchOrder(null)}
          onRecorded={(purchaseOrder) => {
            setDispatchOrder(null)
            setWorkflowMessage(`${purchaseOrder.purchaseOrderNumber} vendor dispatch was recorded and Warehouse was notified.`)
            void refreshRequests()
            void refreshPurchaseOrders()
          }}
        />
      )}

      {receiptRequest?.purchaseOrderId && (
        <GoodsReceiptPanel
          accessToken={session.accessToken}
          purchaseOrderId={receiptRequest.purchaseOrderId}
          purchaseOrderNumber={receiptRequest.purchaseOrderNumber}
          catalogItems={catalogItems}
          canInspectQuality={canInspectQuality}
          onClose={() => setReceiptRequest(null)}
          onQualityCompleted={(inspection) => {
            const completedRequestId = receiptRequest.id
            setReceiptRequest(null)
            if (inspection.result === 'Passed') {
              setWorkflowMessage('Quality passed. Approved material is being added to Inventory through RabbitMQ.')
              setRequests((current) => current.map((request) => request.id === completedRequestId
                ? { ...request, status: 'Received', purchaseOrderStatus: 'Received' }
                : request))
              window.setTimeout(() => void refreshRequests(), 1500)
            } else {
              setWorkflowMessage('Quality rejected the material. No usable Inventory was added; Purchase was notified.')
            }
          }}
        />
      )}
    </section>
  )
}
