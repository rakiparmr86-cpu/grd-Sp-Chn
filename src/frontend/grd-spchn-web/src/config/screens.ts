import screenRegistry from './screens.json'

export interface ScreenDefinition {
  Id: number
  title: string
  code: string
}

// What every screen heading shows. This is the single switch:
//   'code'  -> GRD-033-po   (default)
//   'title' -> Purchase Order
//   'both'  -> GRD-033-po · Purchase Order
// Change it with VITE_SCREEN_LABEL_MODE in .env (restart Vite), or change the default below.
export type ScreenLabelMode = 'code' | 'title' | 'both'

const DEFAULT_SCREEN_LABEL_MODE: ScreenLabelMode = 'code'

function readMode(): ScreenLabelMode {
  const configured = import.meta.env.VITE_SCREEN_LABEL_MODE?.trim().toLowerCase()
  return configured === 'code' || configured === 'title' || configured === 'both'
    ? configured
    : DEFAULT_SCREEN_LABEL_MODE
}

export const screenLabelMode = readMode()

// Screen Ids used by the UI. Values are Ids in screens.json.
export const SCREEN = {
  dashboard: 1,
  userManagement: 2,
  permissionManagement: 4,
  ledger: 9,
  paymentEntry: 11,
  trialBalance: 15,
  profitAndLoss: 16,
  balanceSheet: 17,
  stockLedger: 25,
  purchaseRequisition: 32,
  purchaseOrder: 33,
  goodsReceipt: 34,
  purchaseInvoice: 35,
  purchasePayment: 37,
  outstandingPayable: 56,
  vendorDispatch: 61,
} as const

export type ScreenId = (typeof SCREEN)[keyof typeof SCREEN]

const screensById = new Map<number, ScreenDefinition>(
  (screenRegistry as ScreenDefinition[]).map((screen) => [screen.Id, screen]),
)

export function getScreen(id: ScreenId): ScreenDefinition {
  return screensById.get(id) ?? { Id: id, title: `Screen ${id}`, code: `GRD-${id}` }
}

// The registry code keeps its module segment (GRD-PUR-032-pr); the UI shows only
// prefix-number-suffix (GRD-032-pr) so the module is not exposed on screen.
export function displayCode(code: string): string {
  const parts = code.split('-')
  return parts.length === 4 ? `${parts[0]}-${parts[2]}-${parts[3]}` : code
}

export function screenLabel(id: ScreenId, mode: ScreenLabelMode = screenLabelMode): string {
  const screen = getScreen(id)
  if (mode === 'title') return screen.title
  if (mode === 'both') return `${displayCode(screen.code)} · ${screen.title}`
  return displayCode(screen.code)
}
