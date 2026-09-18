-- Accounting bounded context: PO/accepted-receipt projections, three-way matched
-- vendor payables, immutable double-entry journals, payment records, Inbox/Outbox,
-- and local demonstration access profiles.

CREATE TABLE IF NOT EXISTS accounting_purchase_orders
(
    purchase_order_id CHAR(36) NOT NULL PRIMARY KEY,
    purchase_order_number VARCHAR(64) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    currency CHAR(3) NOT NULL,
    issued_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_po_number UNIQUE (purchase_order_number),
    INDEX ix_accounting_po_supplier (supplier_id, issued_on_utc)
);

CREATE TABLE IF NOT EXISTS accounting_purchase_order_items
(
    purchase_order_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    ordered_quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    unit_price DECIMAL(18, 4) NOT NULL,
    PRIMARY KEY (purchase_order_id, product_id),
    CONSTRAINT fk_accounting_po_item
        FOREIGN KEY (purchase_order_id) REFERENCES accounting_purchase_orders (purchase_order_id),
    CONSTRAINT chk_accounting_po_item_quantity CHECK (ordered_quantity > 0),
    CONSTRAINT chk_accounting_po_item_price CHECK (unit_price > 0)
);

CREATE TABLE IF NOT EXISTS accounting_accepted_receipts
(
    goods_receipt_id CHAR(36) NOT NULL PRIMARY KEY,
    goods_receipt_number VARCHAR(64) NOT NULL,
    quality_inspection_id CHAR(36) NOT NULL,
    purchase_order_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    accepted_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_receipt_number UNIQUE (goods_receipt_number),
    CONSTRAINT uq_accounting_quality_inspection UNIQUE (quality_inspection_id),
    INDEX ix_accounting_receipt_po (purchase_order_id, accepted_on_utc)
);

CREATE TABLE IF NOT EXISTS accounting_accepted_receipt_items
(
    goods_receipt_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    accepted_quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    PRIMARY KEY (goods_receipt_id, product_id),
    CONSTRAINT fk_accounting_receipt_item
        FOREIGN KEY (goods_receipt_id) REFERENCES accounting_accepted_receipts (goods_receipt_id),
    CONSTRAINT chk_accounting_accepted_quantity CHECK (accepted_quantity > 0)
);

CREATE TABLE IF NOT EXISTS accounting_vendor_payables
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    invoice_number VARCHAR(64) NOT NULL,
    supplier_invoice_number VARCHAR(100) NOT NULL,
    purchase_order_id CHAR(36) NOT NULL,
    goods_receipt_id CHAR(36) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    currency CHAR(3) NOT NULL,
    subtotal DECIMAL(18, 2) NOT NULL,
    tax_amount DECIMAL(18, 2) NOT NULL,
    total_amount DECIMAL(18, 2) NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_by_user_id CHAR(36) NOT NULL,
    approved_by_user_id CHAR(36) NULL,
    paid_by_user_id CHAR(36) NULL,
    bank_reference VARCHAR(160) NULL,
    invoice_date_utc DATETIME(6) NOT NULL,
    due_date_utc DATETIME(6) NOT NULL,
    created_on_utc DATETIME(6) NOT NULL,
    approved_on_utc DATETIME(6) NULL,
    paid_on_utc DATETIME(6) NULL,
    CONSTRAINT uq_accounting_invoice_number UNIQUE (invoice_number),
    CONSTRAINT uq_accounting_supplier_invoice UNIQUE (supplier_id, supplier_invoice_number),
    CONSTRAINT chk_accounting_payable_amounts
        CHECK (subtotal > 0 AND tax_amount >= 0 AND total_amount = subtotal + tax_amount),
    CONSTRAINT chk_accounting_payable_status
        CHECK (status IN ('PendingApproval', 'Approved', 'Paid')),
    INDEX ix_accounting_payable_status_due (status, due_date_utc),
    INDEX ix_accounting_payable_receipt (goods_receipt_id)
);

CREATE TABLE IF NOT EXISTS accounting_vendor_invoice_lines
(
    payable_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    unit_price DECIMAL(18, 4) NOT NULL,
    line_amount DECIMAL(18, 2) NOT NULL,
    PRIMARY KEY (payable_id, product_id),
    CONSTRAINT fk_accounting_invoice_line
        FOREIGN KEY (payable_id) REFERENCES accounting_vendor_payables (id),
    CONSTRAINT chk_accounting_invoice_line_quantity CHECK (quantity > 0),
    CONSTRAINT chk_accounting_invoice_line_price CHECK (unit_price > 0)
);

CREATE TABLE IF NOT EXISTS accounting_payments
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    payable_id CHAR(36) NOT NULL,
    amount DECIMAL(18, 2) NOT NULL,
    currency CHAR(3) NOT NULL,
    bank_reference VARCHAR(160) NOT NULL,
    paid_by_user_id CHAR(36) NOT NULL,
    paid_on_utc DATETIME(6) NOT NULL,
    recorded_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_payment_payable UNIQUE (payable_id),
    CONSTRAINT uq_accounting_payment_bank_reference UNIQUE (bank_reference),
    CONSTRAINT fk_accounting_payment_payable
        FOREIGN KEY (payable_id) REFERENCES accounting_vendor_payables (id),
    CONSTRAINT chk_accounting_payment_amount CHECK (amount > 0)
);

CREATE TABLE IF NOT EXISTS accounting_journal_entries
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    entry_number VARCHAR(64) NOT NULL,
    entry_type VARCHAR(64) NOT NULL,
    source_type VARCHAR(64) NOT NULL,
    source_id CHAR(36) NOT NULL,
    currency CHAR(3) NOT NULL,
    description VARCHAR(500) NOT NULL,
    posted_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_journal_number UNIQUE (entry_number),
    CONSTRAINT uq_accounting_journal_source UNIQUE (entry_type, source_type, source_id),
    INDEX ix_accounting_journal_posted (posted_on_utc)
);

CREATE TABLE IF NOT EXISTS accounting_journal_lines
(
    journal_entry_id CHAR(36) NOT NULL,
    line_number INT NOT NULL,
    account_code VARCHAR(64) NOT NULL,
    debit_amount DECIMAL(18, 2) NOT NULL DEFAULT 0,
    credit_amount DECIMAL(18, 2) NOT NULL DEFAULT 0,
    PRIMARY KEY (journal_entry_id, line_number),
    CONSTRAINT fk_accounting_journal_line
        FOREIGN KEY (journal_entry_id) REFERENCES accounting_journal_entries (id),
    CONSTRAINT chk_accounting_journal_side
        CHECK ((debit_amount > 0 AND credit_amount = 0) OR
               (credit_amount > 0 AND debit_amount = 0)),
    INDEX ix_accounting_journal_account (account_code)
);

CREATE TABLE IF NOT EXISTS accounting_inbox
(
    event_id CHAR(36) NOT NULL PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    processed_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS accounting_outbox
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id CHAR(36) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    exchange_name VARCHAR(255) NOT NULL,
    routing_key VARCHAR(255) NOT NULL,
    payload JSON NOT NULL,
    occurred_on_utc DATETIME(6) NOT NULL,
    available_on_utc DATETIME(6) NOT NULL,
    processed_on_utc DATETIME(6) NULL,
    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    CONSTRAINT uq_accounting_outbox_event UNIQUE (event_id),
    INDEX ix_accounting_outbox_pending
        (processed_on_utc, available_on_utc, occurred_on_utc)
);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('accounting.payable.read', 'View vendor payables', 'Accounting',
     'View matched supplier invoices, approval state, and payment state.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('accounting.invoice.create', 'Record vendor invoices', 'Accounting',
     'Record a supplier invoice after PO, accepted GRN, and rate matching.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('accounting.payable.approve', 'Approve vendor payables', 'Accounting',
     'Approve a matched payable under separation-of-duties controls.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('accounting.payment.release', 'Record released vendor payments', 'Accounting',
     'Record the real external bank transaction reference for an approved payable.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('accounting.journal.read', 'View accounting journal', 'Accounting',
     'View immutable double-entry accounting postings.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    module_name = VALUES(module_name),
    description = VALUES(description),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO identity_access_profiles
    (code, display_name, role_name, is_hr_assignable, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('AccountsExecutive', 'Accounts Executive', 'Executive', TRUE, TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('FinanceManager', 'Finance Manager', 'Manager', TRUE, TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    role_name = VALUES(role_name),
    is_hr_assignable = VALUES(is_hr_assignable),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT IGNORE INTO identity_access_profile_permissions
    (access_profile_code, permission_code)
VALUES
    ('AccountsExecutive', 'accounting.payable.read'),
    ('AccountsExecutive', 'accounting.invoice.create'),
    ('AccountsExecutive', 'accounting.journal.read'),
    ('AccountsExecutive', 'supplier.read'),
    ('AccountsExecutive', 'catalog.item.read'),
    ('FinanceManager', 'accounting.payable.read'),
    ('FinanceManager', 'accounting.payable.approve'),
    ('FinanceManager', 'accounting.payment.release'),
    ('FinanceManager', 'accounting.journal.read'),
    ('FinanceManager', 'supplier.read'),
    ('FinanceManager', 'catalog.item.read'),
    ('Director', 'accounting.payable.read'),
    ('Director', 'accounting.invoice.create'),
    ('Director', 'accounting.payable.approve'),
    ('Director', 'accounting.payment.release'),
    ('Director', 'accounting.journal.read');

INSERT INTO identity_users
    (id, user_name, email, normalized_user_name, password_hash, role_name,
     access_profile_code, organization_unit_id, is_active, created_on_utc)
VALUES
    ('10000000-0000-0000-0000-000000000007', 'executive.accounts@grd.local',
     'executive.accounts@yopmail.com', 'EXECUTIVE.ACCOUNTS@GRD.LOCAL',
     'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=',
     'Executive', 'AccountsExecutive', '00000000-0000-0000-0000-000000000002',
     TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000008', 'manager.finance@grd.local',
     'manager.finance@yopmail.com', 'MANAGER.FINANCE@GRD.LOCAL',
     'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=',
     'Manager', 'FinanceManager', '00000000-0000-0000-0000-000000000002',
     TRUE, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    email = VALUES(email),
    password_hash = VALUES(password_hash),
    role_name = VALUES(role_name),
    access_profile_code = VALUES(access_profile_code),
    organization_unit_id = VALUES(organization_unit_id),
    is_active = TRUE;

-- Local upgrade bootstrap only: replay current source rows into Accounting-owned
-- projections. Runtime code never reads another service's tables.
INSERT IGNORE INTO accounting_purchase_orders
    (purchase_order_id, purchase_order_number, supplier_id,
     destination_organization_unit_id, currency, issued_on_utc)
SELECT id, purchase_order_number, supplier_id,
       destination_organization_unit_id, currency, issued_on_utc
FROM procurement_purchase_orders;

INSERT IGNORE INTO accounting_purchase_order_items
    (purchase_order_id, product_id, ordered_quantity, unit_of_measure, unit_price)
SELECT purchase_order_id, product_id, quantity, unit_of_measure, unit_price
FROM procurement_purchase_order_items;

INSERT IGNORE INTO accounting_accepted_receipts
    (goods_receipt_id, goods_receipt_number, quality_inspection_id,
     purchase_order_id, destination_organization_unit_id, accepted_on_utc)
SELECT receipt.id, receipt.goods_receipt_number, inspection.id,
       receipt.purchase_order_id, receipt.destination_organization_unit_id,
       inspection.inspected_on_utc
FROM warehouse_goods_receipts receipt
INNER JOIN warehouse_quality_inspections inspection
        ON inspection.goods_receipt_id = receipt.id
WHERE inspection.result = 'Passed';

INSERT IGNORE INTO accounting_accepted_receipt_items
    (goods_receipt_id, product_id, accepted_quantity, unit_of_measure)
SELECT item.goods_receipt_id, item.product_id, item.quantity, item.unit_of_measure
FROM warehouse_goods_receipt_items item
INNER JOIN accounting_accepted_receipts receipt
        ON receipt.goods_receipt_id = item.goods_receipt_id;

INSERT IGNORE INTO accounting_journal_entries
    (id, entry_number, entry_type, source_type, source_id,
     currency, description, posted_on_utc)
SELECT UUID(), CONCAT('JE-MIG-', LEFT(REPLACE(UUID(), '-', ''), 23)),
       'GoodsReceiptAccrual', 'GoodsReceipt', receipt.goods_receipt_id,
       po.currency,
       CONCAT('Accepted receipt ', receipt.goods_receipt_number,
              ' against ', po.purchase_order_number),
       receipt.accepted_on_utc
FROM accounting_accepted_receipts receipt
INNER JOIN accounting_purchase_orders po
        ON po.purchase_order_id = receipt.purchase_order_id;

INSERT IGNORE INTO accounting_journal_lines
    (journal_entry_id, line_number, account_code, debit_amount, credit_amount)
SELECT journal.id, 1, 'INVENTORY',
       ROUND(SUM(item.accepted_quantity * po_item.unit_price), 2), 0
FROM accounting_journal_entries journal
INNER JOIN accounting_accepted_receipts receipt ON receipt.goods_receipt_id = journal.source_id
INNER JOIN accounting_accepted_receipt_items item ON item.goods_receipt_id = receipt.goods_receipt_id
INNER JOIN accounting_purchase_order_items po_item
        ON po_item.purchase_order_id = receipt.purchase_order_id
       AND po_item.product_id = item.product_id
WHERE journal.entry_type = 'GoodsReceiptAccrual'
  AND journal.source_type = 'GoodsReceipt'
GROUP BY journal.id;

INSERT IGNORE INTO accounting_journal_lines
    (journal_entry_id, line_number, account_code, debit_amount, credit_amount)
SELECT journal.id, 2, 'GRNI', 0,
       ROUND(SUM(item.accepted_quantity * po_item.unit_price), 2)
FROM accounting_journal_entries journal
INNER JOIN accounting_accepted_receipts receipt ON receipt.goods_receipt_id = journal.source_id
INNER JOIN accounting_accepted_receipt_items item ON item.goods_receipt_id = receipt.goods_receipt_id
INNER JOIN accounting_purchase_order_items po_item
        ON po_item.purchase_order_id = receipt.purchase_order_id
       AND po_item.product_id = item.product_id
WHERE journal.entry_type = 'GoodsReceiptAccrual'
  AND journal.source_type = 'GoodsReceipt'
GROUP BY journal.id;
