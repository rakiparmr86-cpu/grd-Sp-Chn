import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type CatalogItem,
  type LoginResponse,
  type MaterialRequestListItem,
  type OrganizationUnit,
  type PurchaseOrder,
  type Supplier,
} from '../api'
import { hasPermission } from '../auth'
import { PurchaseOrderPanel } from './PurchaseOrderPanel'
import { VendorDispatchPanel } from './VendorDispatchPanel'
import { GoodsReceiptPanel } from './GoodsReceiptPanel'
import { DateRangeSearch } from './DateRangeSearch'
import { ScreenName } from './ScreenName'
import { SCREEN } from '../config/screens'
import { dateRangeError, toDateRangeFilter } from '../dateRange'

interface MaterialRequestWorkspaceProps {
  session: LoginResponse
  onBack: () => void
  // Incremented by the dashboard menu to open the create popup on top of the list.
  createRequestSignal?: number
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
  createRequestSignal = 0,
}: MaterialRequestWorkspaceProps) {
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnit[]>([])
  const [catalogItems, setCatalogItems] = useState<CatalogItem[]>([])
  const [catalogLoading, setCatalogLoading] = useState(true)
  const [catalogError, setCatalogError] = useState('')
  const [purpose, setPurpose] = useState('')
  const [lines, setLines] = useState<MaterialLineForm[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [requests, setRequests] = useState<MaterialRequestListItem[]>([])
  const [listLoading, setListLoading] = useState(true)
  const [listError, setListError] = useState('')
  // Dates are filtered by the server when Search is pressed; number and status filter instantly.
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [requestNumberFilter, setRequestNumberFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
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
  const [dispatchOrder, setDispatchOrder] = useState<PurchaseOrder | null>(null)
  const [receiptRequest, setReceiptRequest] = useState<MaterialRequestListItem | null>(null)
  const [createOpen, setCreateOpen] = useState(canCreateRequest && createRequestSignal > 0)

  useEffect(() => {
    if (canCreateRequest && createRequestSignal > 0) setCreateOpen(true)
  }, [canCreateRequest, createRequestSignal])

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
          setListError(reason instanceof ApiError
            ? reason.message
            : 'Could not load purchase orders for the Action column.')
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
  }

  function removeLine(key: string) {
    setLines((current) => current.filter((line) => line.key !== key))
  }

  function resetForm() {
    setPurpose('')
    setLines(catalogItems.length > 0 ? [createLine(catalogItems)] : [])
    setError('')

    if (lines.length === 0) {
      setError('Select at least one active item from Product Catalog.')
      return
    }
  }

  function openCreate() {
    setWorkflowMessage('')
    setCreateOpen(true)
  }

  function closeCreate() {
    if (submitting) return
    setCreateOpen(false)
    setError('')
  }

  const refreshRequests = useCallback(async () => {
    if (dateRangeError(fromDate, toDate)) return
    setListLoading(true)
    setListError('')
    try {
      setRequests(await api.listMaterialRequests(
        session.accessToken,
        toDateRangeFilter(fromDate, toDate),
      ))
    } catch (reason) {
      setListError(reason instanceof ApiError
        ? reason.message
        : 'Could not load requisitions.')
    } finally {
      setListLoading(false)
    }
  }, [fromDate, session.accessToken, toDate])

  useEffect(() => {
    void refreshRequests()
  }, [refreshRequests])

  // Searching the same range again simply reloads it.
  function applyDates(nextFrom: string, nextTo: string) {
    if (nextFrom === fromDate && nextTo === toDate) {
      void refreshRequests()
      return
    }
    setFromDate(nextFrom)
    setToDate(nextTo)
  }

  const statusOptions = useMemo(
    () => [...new Set(requests.map((request) => request.status))].sort(),
    [requests],
  )
  const requestNumberText = requestNumberFilter.trim().toLowerCase()
  const visibleRequests = requests.filter((request) =>
    (!statusFilter || request.status === statusFilter) &&
    (!requestNumberText || request.requestNumber.toLowerCase().includes(requestNumberText)))
  const filtersActive = !!(fromDate || toDate || requestNumberFilter || statusFilter)

  function clearFilters() {
    setFromDate('')
    setToDate('')
    setRequestNumberFilter('')
    setStatusFilter('')
  }

  // Purchase orders are listed on their own screen; here they only drive the Action column.
  async function refreshPurchaseOrders() {
    if (!canReadPurchaseOrders) return
    try {
      const [orders, supplierItems] = await Promise.all([
        api.listPurchaseOrders(session.accessToken),
        api.getSuppliers(session.accessToken),
      ])
      setPurchaseOrders(orders)
      setSuppliers(supplierItems)
    } catch (reason) {
      setListError(reason instanceof ApiError
        ? reason.message
        : 'Could not load purchase orders for the Action column.')
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

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
      setCreateOpen(false)
      setPurpose('')
      setLines(catalogItems.length > 0 ? [createLine(catalogItems)] : [])
      setWorkflowMessage(
        `${result.requestNumber} was submitted to the Purchase Department. Current status: ${result.status}.`,
      )
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
          <h1 id="material-request-title"><ScreenName id={SCREEN.purchaseRequisition} /></h1>
          <p>
            {canCreateRequest
              ? 'Raise a plant requirement for review by the Purchase Department.'
              : 'Review plant and branch requirements sent to the Purchase Department.'}
          </p>
        </div>
        {canCreateRequest ? (
          <button className="workspace-create-button" type="button" onClick={openCreate}>
            + New requisition
          </button>
        ) : (
          <span className="workspace-status"><i /> Purchase review</span>
        )}
      </header>

      {canCreateRequest && createOpen && (
      <div className="drawer-backdrop" role="presentation" onMouseDown={closeCreate}>
      <aside
        className="user-drawer requisition-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="requisition-drawer-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
      <header className="drawer-header">
        <div>
          <span className="eyebrow">Request details</span>
          <h2 id="requisition-drawer-title"><ScreenName id={SCREEN.purchaseRequisition} /></h2>
          <p>Required fields are marked *</p>
        </div>
        <button className="close-button" type="button" onClick={closeCreate} aria-label="Close">×</button>
      </header>
      <form className="requisition-form" onSubmit={handleSubmit}>

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

        <div className="requisition-actions">
          <button className="secondary-button" type="button" onClick={resetForm}>Clear</button>
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? <><span className="spinner" /> Sending…</> : 'Send requisition'}
          </button>
        </div>
      </form>
      </aside>
      </div>
      )}

      <div className="list-filters" role="search" aria-label="Requisition filters">
        <DateRangeSearch fromDate={fromDate} toDate={toDate} onSearch={applyDates} busy={listLoading} />
        <label className="list-filters__search">
          <span>Requisition no.</span>
          <input
            type="search"
            value={requestNumberFilter}
            onChange={(event) => setRequestNumberFilter(event.target.value)}
            placeholder="MR-…"
          />
        </label>
        <label>
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">All statuses</option>
            {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
          </select>
        </label>
        <div className="list-filters__actions">
          <button type="button" className="workspace-refresh" onClick={clearFilters} disabled={!filtersActive}>
            Clear filters
          </button>
        </div>
      </div>

      <section className="requisition-list-card" aria-labelledby="requisition-list-title">
        <div className="requisition-list-heading">
          <div>
            <strong id="requisition-list-title">Requisition list</strong>
            <span>
              {visibleRequests.length} of {requests.length} requisition{requests.length === 1 ? '' : 's'}
              {' · '}Track approval, purchase order, and material dispatch.
            </span>
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
        ) : visibleRequests.length === 0 ? (
          <div className="requisition-list-empty">
            {filtersActive
              ? 'No requisitions match these filters.'
              : canCreateRequest
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
                {visibleRequests.map((request) => {
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
          onQualityCompleted={(inspection, completesPurchaseOrder) => {
            const completedRequestId = receiptRequest.id
            setReceiptRequest(null)
            if (inspection.result === 'Passed') {
              setWorkflowMessage(completesPurchaseOrder
                ? 'Final receipt passed Quality. Approved material is being added to Inventory and the PO will close through RabbitMQ.'
                : 'Partial receipt passed Quality. Its quantity is being added to Inventory; the PO remains open for the balance.')
              if (completesPurchaseOrder) {
                setRequests((current) => current.map((request) => request.id === completedRequestId
                  ? { ...request, status: 'Received', purchaseOrderStatus: 'Received' }
                  : request))
              }
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
