# ERP foundation: Store request to purchase and material receipt

## Scope

This is the first ERP-oriented vertical slice for a multi-company, multi-region,
multi-branch manufacturing organization. It proves this business path:

```text
Production Store Supervisor logs in
  -> creates Material Request for the supervisor's organization unit
Purchase Manager logs in
  -> approves Material Request
  -> issues Purchase Order to a supplier
Procurement transaction
  -> PO + Procurement Outbox row
Outbox Publisher -> RabbitMQ procurement.events
Warehouse consumer at the production location
  -> creates Expected Purchase Order through Inbox deduplication
Outside supplier (no GRD login)
  -> physically dispatches material and sends challan/dispatch advice
Purchase Department user
  -> records the supplier dispatch against the issued PO
  -> system stores vendor details + authenticated internal audit owner
Outside supplier/carrier
  -> delivers material to the production location
Production Store Supervisor
  -> verifies delivered items against the expected PO
  -> posts Goods Receipt / GRN
Warehouse transaction
  -> Expected PO Received + GRN + Warehouse Outbox row
  -> material remains in quality quarantine; usable inventory is unchanged
Quality Inspector at the receiving location
  ├── Rejects -> Quality row + Purchase notification; no inventory movement
  └── Passes  -> Quality row + QualityApproved Outbox row in one transaction
Outbox Publisher -> RabbitMQ warehouse.events
  ├── Inventory consumer writes stock movement + increases location stock
  └── Procurement consumer closes PO and Material Request
```

Version 1 deliberately supports one complete receipt per PO. Partial receipt,
over/under-delivery tolerance, partial quality acceptance, tax, payment, supplier
validation and production consumption are future slices. Quality currently records
one final Pass or Rejected result for the complete GRN.

## Bounded-context ownership

| Service | Owns in this slice | Does not own |
| --- | --- | --- |
| Identity | Login, PBKDF2 password verification, JWT, role/permission/org claims | Organization hierarchy and business approvals |
| Organization | Enterprise-to-facility hierarchy | Users, stock and procurement transactions |
| Procurement | Material Request, approval, Purchase Order and procurement status | Physical receipt and stock balance |
| Product Catalog | Material, category and UOM masters used by requisitions and POs | Stock balances, purchasing and receiving |
| Supplier | Supplier master and supplier lifecycle | Purchase Orders and vendor-user authentication |
| Warehouse | Expected PO, physical receipt/GRN, quarantine and quality result | Supplier negotiation and usable stock balance |
| Inventory | Usable on-hand balance and immutable quality-release movements | PO/GRN and quality-test decisions |
| Outbox Publisher | Reliable publishing from service-owned Outbox tables | Business decisions |
| API Gateway | Public HTTP routing and header forwarding | Authentication decisions and business rules |

Services never read or write another service's tables. RabbitMQ contracts contain
only the identifiers and line details needed by their consumers.

## Organization hierarchy

The seed data in
[`002_erp_procure_to_receive.sql`](../deploy/docker/mysql/init/002_erp_procure_to_receive.sql)
creates:

```text
GRD Enterprise
└── Head Office
    └── Regional Office North
        └── Delhi Head Branch
            └── Delhi Branch
                ├── Delhi Manufacturing Plant
                ├── Delhi Warehouse
                ├── Delhi Boiler Consumption Unit
                └── Delhi Sales Branch
```

`OrganizationUnit` is a management/facility hierarchy. Procurement, Warehouse and
Inventory store its identifier; they do not copy the hierarchy. In the current
slice a user has one assigned organization unit, and Warehouse requires the token's
organization unit to exactly equal the PO destination. Descendant/ancestor access
grants for regional and head-office users must be implemented before multi-region
production rollout.

## Roles and permissions

A role is the person's grade; permissions control actions. Do not infer every
permission only from seniority.

| Local account | Role | Organization | Main actions |
| --- | --- | --- | --- |
| `director@grd.local` | Director | Head Office | All first-slice actions and hierarchy management |
| `gm.north@grd.local` | General Manager | North Region | Approve/read requests and issue/read POs |
| `manager.purchase@grd.local` | Manager | Delhi Head Branch | Approve/read requests and issue/read POs |
| `supervisor.plant@grd.local` | Supervisor | Delhi Manufacturing Plant | Create/read requests, receive goods, read location stock |
| `executive.boiler@grd.local` | Executive | Delhi Boiler Unit | Create/read requests |

The local password for every seeded account is `1223456`. These users and the
development JWT key are demonstration values only. Production must use unique user
passwords, a secret manager, key rotation, refresh/revocation and administrative
user/permission APIs.

## HTTP APIs through the Gateway

| User action | Gateway request | Required permission |
| --- | --- | --- |
| Login | `POST /api/identity/auth/login` | Anonymous |
| Read hierarchy | `GET /api/organization/units` | `organization.read` |
| Create hierarchy node | `POST /api/organization/units` | `organization.manage` |
| Create request | `POST /api/procurement/material-requests` | `procurement.material-request.create` |
| List requests and PO/dispatch progress | `GET /api/procurement/material-requests` | `procurement.material-request.read` |
| Read request | `GET /api/procurement/material-requests/{id}` | `procurement.material-request.read` |
| Approve request | `POST /api/procurement/material-requests/{id}/approve` | `procurement.material-request.approve` |
| Issue PO | `POST /api/procurement/material-requests/{id}/purchase-orders` | `procurement.purchase-order.create` |
| List POs with item rates/totals | `GET /api/procurement/purchase-orders` | `procurement.purchase-order.read` |
| Record vendor dispatch | `POST /api/procurement/purchase-orders/{id}/dispatch` | `procurement.purchase-order.dispatch` |
| Read PO | `GET /api/procurement/purchase-orders/{id}` | `procurement.purchase-order.read` |
| List active suppliers | `GET /api/suppliers/catalog` | `supplier.read` |
| List active material/UOM master | `GET /api/products/items` | `catalog.item.read` |
| View expected PO | `GET /api/warehouses/purchase-orders/{id}` | `warehouse.goods-receipt.read` |
| Post GRN | `POST /api/warehouses/purchase-orders/{id}/goods-receipts` | `warehouse.goods-receipt.post` |
| View GRN/quality state | `GET /api/warehouses/purchase-orders/{id}/quality-inspection` | `warehouse.quality-inspection.read` |
| Pass or reject quality | `POST /api/warehouses/purchase-orders/{id}/quality-inspection` | `warehouse.quality-inspection.post` |
| Read location stock | `GET /api/inventory/stock/locations/{organizationUnitId}/{productId}` | `inventory.stock.read` |

The API derives the current `userId` and `organizationUnitId` from the validated JWT.
The browser is never allowed to submit those security-sensitive identities.

## Vendor dispatch without a vendor login

The supplier is an external business party, not an Identity user. The GRD system
therefore does not create a password, role, JWT or menu for the supplier. After an
issued PO is sent outside GRD, the supplier dispatches the goods using its own
process and shares a dispatch reference or delivery challan by email, portal,
telephone, EDI or a future secure supplier portal.

An authenticated Purchase Department employee opens the PO list and selects
**Record dispatch**. The employee records the supplier reference, challan,
transporter, vehicle, dispatch date and expected delivery date. The server takes
`recorded_by_user_id` from the employee's JWT; it never trusts a user ID submitted
by the browser. This creates two separate audit identities:

- `supplier_id`: the external supplier that physically sent the material;
- `recorded_by_user_id`: the internal employee who entered the advice in GRD.

The record is stored in `procurement_purchase_order_dispatches`, while the PO moves
from `Issued` to `Dispatched`. A notification is written to the Procurement Outbox
in the same transaction. The branch/store can then see that material is in transit;
only an authorized Warehouse/Store user posts the Goods Receipt after physically
checking the delivery.

For the local vertical slice, `supervisor.plant@grd.local` opens **More → Post goods
receipt**. A dispatched requisition shows **Receive material** in its Action column.
The form loads the Warehouse-owned expected PO through
`GET /api/warehouses/purchase-orders/{purchaseOrderId}`, displays the ordered
materials and requires an explicit physical-verification confirmation. Posting the
form calls `POST /api/warehouses/purchase-orders/{purchaseOrderId}/goods-receipts`.
The Warehouse service derives both receiver user ID and location from the JWT and
rejects receipt at a different organization unit.

Posting a GRN does not create usable stock. The same drawer moves to **Step 2 –
Complete quality test**. A Passed result publishes
`QualityInspectionApprovedIntegrationEvent`; Inventory then writes an immutable
`inventory_stock_movements` row and increments `inventory_location_stock` in the
same Inventory transaction. A Rejected result requires a reason, stays outside
usable inventory and notifies Purchase. Plant Supervisor has quality permission for
the local demo; production can assign the separate `QualityInspector` access profile
to enforce separation of duties.

Version 1 supports one complete dispatch per PO. Partial shipments require a future
dispatch-header/dispatch-line model and must not be simulated by overwriting this
audit row.

Example Gateway request (the bearer token belongs to the Purchase employee):

```http
POST /api/procurement/purchase-orders/{purchaseOrderId}/dispatch
Authorization: Bearer <purchase-employee-token>
Content-Type: application/json

{
  "vendorDispatchReference": "ABACUS-DSP-10027",
  "deliveryChallanNumber": "DC-10027",
  "transporterName": "North Freight",
  "vehicleNumber": "DL01AB1234",
  "dispatchedOnUtc": "2026-09-05T10:30:00Z",
  "expectedDeliveryOnUtc": "2026-09-07T10:30:00Z",
  "notes": "Deliver to Delhi Manufacturing Plant store gate"
}
```

## Supplier master

The Supplier service owns `supplier_master`; Procurement stores only the selected
supplier ID on a purchase order. The master is designed for later maintenance APIs
and contains stable code, legal/display name, tax identifier, email, phone, postal
address, country, payment terms, default currency, lifecycle status, active flag,
and audit timestamps.

Migration `007_supplier_master.sql` seeds four fictional local-development suppliers:
Abacus, GRD, AU, and IDFC. They are test records, not verified real-world vendor
identities. Purchase Manager and Regional General Manager receive `supplier.read`;
Director receives `supplier.read` and `supplier.manage`. The PO screen loads only
active suppliers through the Gateway instead of keeping supplier IDs in React.

## Material and UOM master

Migration `008_product_catalog_master.sql` creates the Product Catalog-owned
`catalog_categories`, `catalog_units_of_measure` and `catalog_items` tables. It
seeds store/procurement test materials including Packing Bag, Coal, Furnace Oil,
Maize, Rice and Edible Oil. A requisition stores the stable catalog item ID and the
requested quantity/UOM; the material name is loaded from Product Catalog rather
than hard-coded in React.

Migration `009_purchase_order_vendor_dispatch.sql` creates the vendor dispatch
audit table and dynamically assigns `procurement.purchase-order.dispatch` to the
Purchase Manager, Regional General Manager and Director access profiles.

Migration `010_quality_release_to_inventory.sql` creates
`warehouse_quality_inspections` and the Inventory-owned
`inventory_stock_movements` ledger. It adds dynamic quality permissions and the
HR-assignable `QualityInspector` access profile. The existing
`inventory_location_stock` table remains the authoritative usable balance; no
duplicate inventory-balance table is introduced.

## Local setup

From `D:\newdata\grd-Sp-Chn`, start MySQL and RabbitMQ as described in the root
README. A fresh MySQL volume applies both initialization scripts automatically.
For an existing volume, apply all idempotent migrations without deleting data:

```powershell
.\scripts\apply-local-identity-seed.ps1
```

Start these processes:

```text
API Gateway       7000
Identity          7001
Product Catalog   5006
Inventory         5018
Organization      5218
Procurement       5112
Supplier          5141
Warehouse         5276
Outbox Publisher  background worker
```

The VS Code task can start all enabled services. To run only this slice, open one
terminal per process and use, for example:

```powershell
.\scripts\start-local-services.ps1 -Service Identity -SkipInfrastructure
```

Replace `Identity` with each process name above. The first terminal may start the
containers; use `-SkipInfrastructure` in the remaining terminals.

After all processes are running, execute:

```powershell
.\scripts\smoke-procure-to-receive.ps1
```

The script calls only Gateway port `7000` and verifies the final Procurement status
and production-location stock.

## Reliability and scaling behavior

- Procurement writes PO and Outbox together.
- Warehouse uses Inbox before creating its expected PO.
- Warehouse writes GRN and receipt status together; the goods remain quarantined.
- Warehouse writes the quality decision and quality-approved Outbox event together.
- Inventory uses Inbox, writes a quality-release movement and updates location stock
  in one transaction.
- Procurement uses Inbox and completes PO/request only after quality approval.
- Every API is stateless and can be replicated behind the Gateway/load balancer.
- Service-owned indexes include organization/status or location/product keys.
- Reporting should consume events into read models instead of joining service tables.

Before thousands-user production usage, add hierarchical access-scope grants,
approval delegation, optimistic version columns, audit history, distributed tracing,
rate limiting, production secret/key management, database-per-service deployment,
RabbitMQ high availability and realistic concurrency/load tests.

## Requisition activity email notifications

The requisition screen lists the request status, whether a purchase order exists,
and whether material has been dispatched. These values come from Procurement through
`GET /api/procurement/material-requests`; the browser does not manufacture workflow
status locally.

Every local Identity user has a durable `email` value ending in `@yopmail.com`. The
address is assigned by Identity and is not selected on the login screen. Procurement
writes an `ActivityNotificationRequestedIntegrationEvent` to its Outbox in the same
transaction as each supported workflow action:

- material request created;
- material request approved;
- purchase order issued;
- material dispatched; and
- goods receipt processed.

The Outbox Publisher sends the event to RabbitMQ. Notifications resolves the direct
users and permission-based recipients, then inserts idempotent rows into
`notification_email_deliveries`. Its background worker sends pending rows and records
`Pending`, `Sending`, `Sent`, or `Failed` status and retry details.

Yopmail is the recipient inbox domain; it is not the application's SMTP relay. To
send real email, copy the SMTP settings from `deploy/docker/.env.example` into the
untracked `deploy/docker/.env`, supply credentials for an SMTP provider, and set:

```dotenv
SMTP_ENABLED=true
SMTP_HOST=your-smtp-host
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_USERNAME=your-smtp-user
SMTP_PASSWORD=your-smtp-password
SMTP_FROM_ADDRESS=notifications@your-domain.example
SMTP_FROM_NAME=GRD Supply Chain
```

When SMTP is disabled, activity rows remain safely queued; the application does not
pretend they were delivered. Restart Notifications after changing these values.
