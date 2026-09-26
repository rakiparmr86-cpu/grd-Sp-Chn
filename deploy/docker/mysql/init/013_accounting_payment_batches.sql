-- Vendor payment batches.
-- One bank transfer can settle several approved payables of the same supplier.
-- The batch owns the bank reference (unique); each payable still gets its own
-- accounting_payments row, now linked to the batch. Re-runnable.

CREATE TABLE IF NOT EXISTS accounting_payment_batches
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    batch_number VARCHAR(64) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    currency CHAR(3) NOT NULL,
    total_amount DECIMAL(18, 2) NOT NULL,
    payable_count INT NOT NULL,
    bank_reference VARCHAR(160) NOT NULL,
    paid_by_user_id CHAR(36) NOT NULL,
    paid_on_utc DATETIME(6) NOT NULL,
    recorded_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_accounting_payment_batch_number UNIQUE (batch_number),
    CONSTRAINT uq_accounting_payment_batch_bank_reference UNIQUE (bank_reference),
    CONSTRAINT chk_accounting_payment_batch_amount CHECK (total_amount > 0 AND payable_count > 0),
    INDEX ix_accounting_payment_batch_supplier (supplier_id, paid_on_utc)
);

SET @has_batch_column = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'accounting_payments'
      AND column_name = 'payment_batch_id'
);
SET @add_batch_column_sql = IF(
    @has_batch_column = 0,
    'ALTER TABLE accounting_payments ADD COLUMN payment_batch_id CHAR(36) NULL AFTER payable_id, ADD INDEX ix_accounting_payment_batch (payment_batch_id)',
    'SELECT 1'
);
PREPARE add_batch_column_statement FROM @add_batch_column_sql;
EXECUTE add_batch_column_statement;
DEALLOCATE PREPARE add_batch_column_statement;

-- Payments made before batches existed become single-payable batches.
INSERT IGNORE INTO accounting_payment_batches
    (id, batch_number, supplier_id, currency, total_amount, payable_count,
     bank_reference, paid_by_user_id, paid_on_utc, recorded_on_utc)
SELECT payment.id,
       CONCAT('PB-LEGACY-', LEFT(REPLACE(payment.id, '-', ''), 20)),
       payable.supplier_id, payment.currency, payment.amount, 1,
       payment.bank_reference, payment.paid_by_user_id, payment.paid_on_utc, payment.recorded_on_utc
FROM accounting_payments payment
INNER JOIN accounting_vendor_payables payable ON payable.id = payment.payable_id
WHERE payment.payment_batch_id IS NULL;

UPDATE accounting_payments
SET payment_batch_id = id
WHERE payment_batch_id IS NULL;

-- The bank reference is now unique per batch, not per payment row.
SET @has_payment_reference_unique = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'accounting_payments'
      AND index_name = 'uq_accounting_payment_bank_reference'
);
SET @drop_payment_reference_unique_sql = IF(
    @has_payment_reference_unique > 0,
    'ALTER TABLE accounting_payments DROP INDEX uq_accounting_payment_bank_reference, ADD INDEX ix_accounting_payment_bank_reference (bank_reference)',
    'SELECT 1'
);
PREPARE drop_payment_reference_unique_statement FROM @drop_payment_reference_unique_sql;
EXECUTE drop_payment_reference_unique_statement;
DEALLOCATE PREPARE drop_payment_reference_unique_statement;
