export interface LoginResponse {
  accessToken: string
  expiresOnUtc: string
  userId: string
  userName: string
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
  role: string
  accessProfile: string
  organizationUnitId: string
  permissions: string[]
  isActive: boolean
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
}
