export interface LoginResponse {
  accessToken: string
  expiresOnUtc: string
  userId: string
  userName: string
  email: string
  role: string
  accessProfile: string
  organizationUnitId: string
  permissions: string[]
}

export interface OrganizationUnit {
  id: string
  parentId: string | null
  code: string
  name: string
  type: string
  isActive: boolean
}

export interface AccessProfile {
  code: string
  displayName: string
  role: string
}

export interface ManagedAccessProfile extends AccessProfile {
  isHrAssignable: boolean
  isActive: boolean
  permissions: string[]
}

export interface PermissionDefinition {
  code: string
  displayName: string
  module: string
  description: string
  isActive: boolean
}

export interface CreateUserRequest {
  userName: string
  password: string
  accessProfile: string
  organizationUnitId: string
}

export interface CreatedUser {
  userId: string
  userName: string
  email: string
  role: string
  accessProfile: string
  organizationUnitId: string
  permissions: string[]
  isActive: boolean
}

export interface MaterialRequestItemInput {
  productId: string
  quantity: number
  unitOfMeasure: string
}

export interface CreateMaterialRequestRequest {
  purpose: string
  items: MaterialRequestItemInput[]
}

export type MaterialRequestItem = MaterialRequestItemInput

export interface MaterialRequest {
  id: string
  requestNumber: string
  requestingOrganizationUnitId: string
  destinationOrganizationUnitId: string
  requestedByUserId: string
  purpose: string
  status: string
  items: MaterialRequestItem[]
  approvedByUserId: string | null
  purchaseOrderId: string | null
  createdOnUtc: string
  updatedOnUtc: string
}

export interface MaterialRequestListItem {
  id: string
  requestNumber: string
  purpose: string
  status: string
  itemCount: number
  requestedByUserId: string
  createdOnUtc: string
  purchaseOrderId: string | null
  purchaseOrderNumber: string | null
  purchaseOrderStatus: string | null
  purchaseOrderCreated: boolean
  materialDispatched: boolean
  dispatchedOnUtc: string | null
}

export interface PurchaseOrderPriceInput {
  productId: string
  unitPrice: number
}

export interface IssuePurchaseOrderRequest {
  supplierId: string
  currency: string
  prices: PurchaseOrderPriceInput[]
}

export interface PurchaseOrderItem {
  productId: string
  quantity: number
  unitOfMeasure: string
  unitPrice: number
  lineAmount: number
}

export interface PurchaseOrder {
  id: string
  purchaseOrderNumber: string
  materialRequestId: string
  supplierId: string
  destinationOrganizationUnitId: string
  currency: string
  status: string
  items: PurchaseOrderItem[]
  totalAmount: number
  issuedOnUtc: string
  dispatchedOnUtc: string | null
  updatedOnUtc: string
}

export interface CatalogItem {
  id: string
  code: string
  name: string
  description: string | null
  categoryCode: string
  categoryName: string
  baseUnitOfMeasure: string
  unitOfMeasureName: string
  inventoryTracked: boolean
}

export interface RecordVendorDispatchRequest {
  vendorDispatchReference: string
  deliveryChallanNumber: string | null
  transporterName: string | null
  vehicleNumber: string | null
  dispatchedOnUtc: string
  expectedDeliveryOnUtc: string | null
  notes: string | null
}

export interface ExpectedPurchaseOrderItem {
  productId: string
  quantity: number
  unitOfMeasure: string
}

export interface ExpectedPurchaseOrder {
  purchaseOrderId: string
  purchaseOrderNumber: string
  supplierId: string
  destinationOrganizationUnitId: string
  status: string
  items: ExpectedPurchaseOrderItem[]
}

export interface GoodsReceipt {
  id: string
  goodsReceiptNumber: string
  purchaseOrderId: string
  destinationOrganizationUnitId: string
  receivedByUserId: string
  receivedOnUtc: string
  items: ExpectedPurchaseOrderItem[]
}

export interface QualityInspection {
  id: string
  goodsReceiptId: string
  purchaseOrderId: string
  destinationOrganizationUnitId: string
  inspectedByUserId: string
  result: 'Passed' | 'Rejected'
  notes: string | null
  inspectedOnUtc: string
}

export interface QualityInspectionContext {
  goodsReceipt: GoodsReceipt
  inspection: QualityInspection | null
}

export interface Supplier {
  id: string
  code: string
  legalName: string
  displayName: string
  taxIdentificationNumber: string | null
  email: string | null
  phone: string | null
  addressLine1: string | null
  city: string | null
  state: string | null
  postalCode: string | null
  countryCode: string
  paymentTermsDays: number
  defaultCurrency: string
  status: string
}

interface ProblemDetails {
  title?: string
  detail?: string
  message?: string
}

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()
const apiBaseUrl = configuredBaseUrl?.replace(/\/$/, '') ?? ''

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
  }
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  accessToken?: string,
): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')
  if (options.body) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers,
  })

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // The status text remains a useful fallback for non-JSON proxy failures.
    }

    throw new ApiError(
      problem.detail ?? problem.message ?? problem.title ?? response.statusText,
      response.status,
    )
  }

  return (await response.json()) as T
}

export const api = {
  login: (userName: string, password: string) =>
    request<LoginResponse>('/api/identity/auth/login', {
      method: 'POST',
      body: JSON.stringify({ userName, password }),
    }),

  getOrganizationUnits: (accessToken: string) =>
    request<OrganizationUnit[]>('/api/organization/units', {}, accessToken),

  getAccessProfiles: (accessToken: string) =>
    request<AccessProfile[]>(
      '/api/identity/users/access-profiles',
      {},
      accessToken,
    ),

  getManagedAccessProfiles: (accessToken: string) =>
    request<ManagedAccessProfile[]>(
      '/api/identity/access-profiles',
      {},
      accessToken,
    ),

  getPermissionCatalog: (accessToken: string) =>
    request<PermissionDefinition[]>(
      '/api/identity/access-profiles/permissions',
      {},
      accessToken,
    ),

  replaceAccessProfilePermissions: (
    accessToken: string,
    accessProfileCode: string,
    permissionCodes: string[],
  ) =>
    request<ManagedAccessProfile>(
      `/api/identity/access-profiles/${encodeURIComponent(accessProfileCode)}/permissions`,
      { method: 'PUT', body: JSON.stringify({ permissionCodes }) },
      accessToken,
    ),

  createUser: (accessToken: string, payload: CreateUserRequest) =>
    request<CreatedUser>(
      '/api/identity/users',
      { method: 'POST', body: JSON.stringify(payload) },
      accessToken,
    ),

  createMaterialRequest: (
    accessToken: string,
    payload: CreateMaterialRequestRequest,
  ) =>
    request<MaterialRequest>(
      '/api/procurement/material-requests',
      { method: 'POST', body: JSON.stringify(payload) },
      accessToken,
    ),

  listMaterialRequests: (accessToken: string) =>
    request<MaterialRequestListItem[]>(
      '/api/procurement/material-requests',
      {},
      accessToken,
    ),

  getMaterialRequest: (accessToken: string, materialRequestId: string) =>
    request<MaterialRequest>(
      `/api/procurement/material-requests/${encodeURIComponent(materialRequestId)}`,
      {},
      accessToken,
    ),

  approveMaterialRequest: (accessToken: string, materialRequestId: string) =>
    request<MaterialRequest>(
      `/api/procurement/material-requests/${encodeURIComponent(materialRequestId)}/approve`,
      { method: 'POST' },
      accessToken,
    ),

  issuePurchaseOrder: (
    accessToken: string,
    materialRequestId: string,
    payload: IssuePurchaseOrderRequest,
  ) =>
    request<PurchaseOrder>(
      `/api/procurement/material-requests/${encodeURIComponent(materialRequestId)}/purchase-orders`,
      { method: 'POST', body: JSON.stringify(payload) },
      accessToken,
    ),

  listPurchaseOrders: (accessToken: string) =>
    request<PurchaseOrder[]>(
      '/api/procurement/purchase-orders',
      {},
      accessToken,
    ),

  recordVendorDispatch: (
    accessToken: string,
    purchaseOrderId: string,
    payload: RecordVendorDispatchRequest,
  ) =>
    request<PurchaseOrder>(
      `/api/procurement/purchase-orders/${encodeURIComponent(purchaseOrderId)}/dispatch`,
      { method: 'POST', body: JSON.stringify(payload) },
      accessToken,
    ),

  getExpectedPurchaseOrder: (accessToken: string, purchaseOrderId: string) =>
    request<ExpectedPurchaseOrder>(
      `/api/warehouses/purchase-orders/${encodeURIComponent(purchaseOrderId)}`,
      {},
      accessToken,
    ),

  postGoodsReceipt: (
    accessToken: string,
    purchaseOrderId: string,
    items: ExpectedPurchaseOrderItem[],
  ) =>
    request<GoodsReceipt>(
      `/api/warehouses/purchase-orders/${encodeURIComponent(purchaseOrderId)}/goods-receipts`,
      { method: 'POST', body: JSON.stringify({ items }) },
      accessToken,
    ),

  getQualityInspection: (accessToken: string, purchaseOrderId: string) =>
    request<QualityInspectionContext>(
      `/api/warehouses/purchase-orders/${encodeURIComponent(purchaseOrderId)}/quality-inspection`,
      {},
      accessToken,
    ),

  completeQualityInspection: (
    accessToken: string,
    purchaseOrderId: string,
    result: 'Passed' | 'Rejected',
    notes: string | null,
  ) =>
    request<QualityInspection>(
      `/api/warehouses/purchase-orders/${encodeURIComponent(purchaseOrderId)}/quality-inspection`,
      { method: 'POST', body: JSON.stringify({ result, notes }) },
      accessToken,
    ),

  getSuppliers: (accessToken: string) =>
    request<Supplier[]>(
      '/api/suppliers/catalog',
      {},
      accessToken,
    ),

  getProcurementItems: (accessToken: string) =>
    request<CatalogItem[]>(
      '/api/products/items',
      {},
      accessToken,
    ),
}
