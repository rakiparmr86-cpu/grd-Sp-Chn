# Accounting: receive-to-pay

This document explains the implemented Accounting vertical slice and why Accounting
is a separate service from Inventory Management.

## Business flow

```text
Vendor dispatches material
  -> Store posts the actual received quantity (GRN)
  -> Quality passes accepted quantity
  -> Inventory increases usable stock
  -> Accounting receives QualityInspectionApproved through RabbitMQ
  -> automatic accrual: Inventory Dr / GRNI Cr
  -> Accounts Executive enters the supplier invoice
  -> system three-way matches PO rate + accepted GRN quantity + invoice
  -> automatic invoice journal: GRNI Dr + Input Tax Dr / Vendor Payable Cr
  -> Finance Manager approves the payable
  -> payment is executed outside GRD through the bank
  -> Finance records the real UTR/bank reference
  -> automatic payment journal: Vendor Payable (Party) Dr / Bank Cr
  -> VendorPaymentReleased Integration Event is published through the Outbox
```

Accounts does **not** enter the PO again. The Accounting service owns an immutable
projection of PO price and quality-approved GRN quantity received through Integration
Events. An invoice cannot use a different PO rate or exceed the remaining accepted
quantity. Partial GRNs therefore allow only partial invoicing and payment.

## Roles and separation of duties

| Profile | Local login | Responsibility |
|---|---|---|
| Accounts Executive | `executive.accounts@grd.local` | Enter and three-way match supplier invoices; cannot approve own invoice. |
| Finance Manager | `manager.finance@grd.local` | Approve vendor payables and record the bank payment reference. |
| Director | `director@grd.local` | All Accounting permissions for controlled administration/testing. |

Local demonstration password: `1223456`.

The invoice creator cannot approve the same payable. V1 does not send money to a bank;
it records a completed external payment and its unique bank reference. A later bank
adapter can replace this manual recording step without changing the payable aggregate.

## API and port

- Direct service: `http://localhost:5310`
- Public Gateway prefix: `http://localhost:7000/api/accounting`
- Swagger through Gateway: select **Accounting v1** at `http://localhost:7000`

| Method | Public path | Permission | Purpose |
|---|---|---|---|
| GET | `/api/accounting/payables/invoice-candidates` | `accounting.invoice.create` | List quality-approved GRNs and remaining invoiceable quantity. |
| POST | `/api/accounting/payables/vendor-invoices` | `accounting.invoice.create` | Record a supplier invoice after three-way matching. |
| GET | `/api/accounting/payables` | `accounting.payable.read` | View payable, approval, and payment states. |
| POST | `/api/accounting/payables/{id}/approve` | `accounting.payable.approve` | Approve a payable under separation of duties. |
| POST | `/api/accounting/payables/{id}/payments` | `accounting.payment.release` | Record external bank reference and payment date. |
| GET | `/api/accounting/journal-entries` | `accounting.journal.read` | View immutable balanced journal summaries. |

## Service-owned data

Migration [`012_accounting_payables.sql`](../deploy/docker/mysql/init/012_accounting_payables.sql)
creates Accounting-owned PO/GRN projections, vendor payables and invoice lines, payment
records, journal headers/lines, Inbox, and Outbox. The local upgrade portion copies old
demo rows once so existing accepted GRNs remain usable. Runtime Accounting code never
reads or writes Procurement, Warehouse, Inventory, or Supplier tables.

## Reliability and transaction boundaries

- PO and quality events are idempotent through `accounting_inbox`.
- Events may arrive in either order. GRNI accrual is posted only after both projections
  exist and is protected by a unique journal source.
- Payable, invoice lines, journal entry, and notification Outbox row share one local
  MySQL transaction.
- Payment, payment journal, and outgoing payment event share one local transaction.
- `GRD.SpChn.OutboxPublisher` polls `accounting_outbox`; API handlers never publish
  directly to RabbitMQ.

## Local startup

F5 profile **GRD: Start ALL backend services with debugger** includes Accounting.
To apply only the idempotent local migrations from the repository root:

```powershell
.\scripts\apply-local-identity-seed.ps1
```

Docker Desktop and the GRD MySQL container must be running first.
