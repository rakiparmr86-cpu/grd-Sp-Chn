-- Requisition tracking, material dispatch, and queued email notifications.
-- Safe to run repeatedly against an existing grd_local database.

SET @identity_email_column_exists =
(
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'identity_users'
      AND column_name = 'email'
);
SET @identity_email_column_sql = IF(
    @identity_email_column_exists = 0,
    'ALTER TABLE identity_users ADD COLUMN email VARCHAR(254) NULL AFTER user_name',
    'SELECT 1');
PREPARE identity_email_column_statement FROM @identity_email_column_sql;
EXECUTE identity_email_column_statement;
DEALLOCATE PREPARE identity_email_column_statement;

UPDATE identity_users
SET email = CONCAT(LOWER(SUBSTRING_INDEX(user_name, '@', 1)), '@yopmail.com')
WHERE email IS NULL OR TRIM(email) = '';

SET @identity_email_index_exists =
(
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'identity_users'
      AND index_name = 'uq_identity_user_email'
);
SET @identity_email_index_sql = IF(
    @identity_email_index_exists = 0,
    'ALTER TABLE identity_users ADD UNIQUE INDEX uq_identity_user_email (email)',
    'SELECT 1');
PREPARE identity_email_index_statement FROM @identity_email_index_sql;
EXECUTE identity_email_index_statement;
DEALLOCATE PREPARE identity_email_index_statement;

SET @procurement_dispatch_column_exists =
(
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'procurement_purchase_orders'
      AND column_name = 'dispatched_on_utc'
);
SET @procurement_dispatch_column_sql = IF(
    @procurement_dispatch_column_exists = 0,
    'ALTER TABLE procurement_purchase_orders ADD COLUMN dispatched_on_utc DATETIME(6) NULL AFTER issued_on_utc',
    'SELECT 1');
PREPARE procurement_dispatch_column_statement FROM @procurement_dispatch_column_sql;
EXECUTE procurement_dispatch_column_statement;
DEALLOCATE PREPARE procurement_dispatch_column_statement;

CREATE TABLE IF NOT EXISTS notification_email_deliveries
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id CHAR(36) NOT NULL,
    activity_code VARCHAR(128) NOT NULL,
    reference_type VARCHAR(64) NOT NULL,
    reference_id CHAR(36) NOT NULL,
    recipient_user_id CHAR(36) NOT NULL,
    recipient_email VARCHAR(254) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    attempt_count INT NOT NULL DEFAULT 0,
    available_on_utc DATETIME(6) NOT NULL,
    sent_on_utc DATETIME(6) NULL,
    last_error VARCHAR(2000) NULL,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_notification_event_recipient UNIQUE (event_id, recipient_user_id),
    INDEX ix_notification_delivery_pending (status, available_on_utc),
    INDEX ix_notification_delivery_reference (reference_type, reference_id)
);
