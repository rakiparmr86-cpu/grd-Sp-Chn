-- Manual accounting vouchers (expense, asset purchase, payment, receipt, contra,
-- journal) and the chart of accounts they post to. Idempotent.
--
-- accounting_accounts replaces the account list that was hard-coded for reports.
-- System accounts are the codes the purchase-to-pay workflow posts automatically;
-- they cannot be deactivated from the screen.

CREATE TABLE IF NOT EXISTS accounting_accounts
(
    code VARCHAR(64) NOT NULL PRIMARY KEY,
    name VARCHAR(160) NOT NULL,
    account_type VARCHAR(16) NOT NULL,
    group_name VARCHAR(120) NOT NULL,
    is_cash_or_bank TINYINT(1) NOT NULL DEFAULT 0,
    is_system TINYINT(1) NOT NULL DEFAULT 0,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT chk_accounting_account_type
        CHECK (account_type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')),
    INDEX ix_accounting_account_type (account_type, is_active)
);

INSERT IGNORE INTO accounting_accounts
    (code, name, account_type, group_name, is_cash_or_bank, is_system, is_active, created_on_utc)
VALUES
    ('CASH', 'Cash', 'Asset', 'Cash-in-hand', 1, 1, 1, UTC_TIMESTAMP(6)),
    ('BANK', 'Bank', 'Asset', 'Bank accounts', 1, 1, 1, UTC_TIMESTAMP(6)),
    ('INVENTORY', 'Inventory (Stock)', 'Asset', 'Stock-in-hand', 0, 1, 1, UTC_TIMESTAMP(6)),
    ('INPUT_TAX', 'Input Tax', 'Asset', 'Duties and taxes (input credit)', 0, 1, 1, UTC_TIMESTAMP(6)),
    ('FA_FURNITURE', 'Furniture and Fixtures', 'Asset', 'Fixed assets', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('FA_COMPUTERS', 'Computers and IT Equipment', 'Asset', 'Fixed assets', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('FA_MACHINERY', 'Plant and Machinery', 'Asset', 'Fixed assets', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('FA_VEHICLES', 'Vehicles', 'Asset', 'Fixed assets', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('ADVANCES', 'Advances and Deposits', 'Asset', 'Loans and advances', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('GRNI', 'Goods Received Not Invoiced', 'Liability', 'Provisions (GRNI)', 0, 1, 1, UTC_TIMESTAMP(6)),
    ('VENDOR_PAYABLE', 'Vendor Payable (Sundry Creditors)', 'Liability', 'Sundry creditors', 0, 1, 1, UTC_TIMESTAMP(6)),
    ('OUTPUT_TAX', 'Output Tax', 'Liability', 'Duties and taxes (payable)', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('LOANS', 'Loans (Liability)', 'Liability', 'Loans', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('CAPITAL', 'Capital Account', 'Equity', 'Capital', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('DRAWINGS', 'Drawings', 'Equity', 'Capital', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('SALES', 'Sales', 'Income', 'Sales accounts', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('OTHER_INCOME', 'Other Income', 'Income', 'Indirect income', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_RENT', 'Rent', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_SALARY', 'Salaries and Wages', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_ELECTRICITY', 'Electricity and Power', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_OFFICE', 'Office Expenses', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_TRAVEL', 'Travelling and Conveyance', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_REPAIRS', 'Repairs and Maintenance', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_FREIGHT', 'Freight Inward', 'Expense', 'Direct expenses', 0, 0, 1, UTC_TIMESTAMP(6)),
    ('EXP_BANK_CHARGES', 'Bank Charges', 'Expense', 'Indirect expenses', 0, 0, 1, UTC_TIMESTAMP(6));

-- One row per manual voucher; its double entry lives in accounting_journal_entries
-- (source_type = 'ManualVoucher', source_id = this id).
CREATE TABLE IF NOT EXISTS accounting_vouchers
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    voucher_number VARCHAR(40) NOT NULL,
    voucher_type VARCHAR(24) NOT NULL,
    voucher_date_utc DATETIME(6) NOT NULL,
    party_name VARCHAR(160) NULL,
    reference VARCHAR(80) NULL,
    narration VARCHAR(500) NOT NULL,
    total_amount DECIMAL(18,2) NOT NULL,
    journal_entry_id CHAR(36) NOT NULL,
    created_by_user_id CHAR(36) NOT NULL,
    created_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_voucher_number UNIQUE (voucher_number),
    CONSTRAINT chk_accounting_voucher_type
        CHECK (voucher_type IN ('Expense', 'AssetPurchase', 'Payment', 'Receipt', 'Contra', 'Journal')),
    INDEX ix_accounting_voucher_date (voucher_date_utc)
);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active, created_on_utc, updated_on_utc)
VALUES
    ('accounting.voucher.create', 'Enter accounting vouchers', 'Accounting',
     'Post expense, asset purchase, payment, receipt, contra and journal vouchers.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('accounting.account.manage', 'Manage chart of accounts', 'Accounting',
     'Add ledger accounts such as expense heads, fixed assets and income accounts.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    module_name = VALUES(module_name),
    description = VALUES(description),
    updated_on_utc = VALUES(updated_on_utc);

INSERT IGNORE INTO identity_access_profile_permissions (access_profile_code, permission_code)
VALUES
    ('AccountsExecutive', 'accounting.voucher.create'),
    ('FinanceManager', 'accounting.voucher.create'),
    ('Director', 'accounting.voucher.create'),
    ('FinanceManager', 'accounting.account.manage'),
    ('Director', 'accounting.account.manage');
