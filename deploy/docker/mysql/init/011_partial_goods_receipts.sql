-- Allow controlled partial deliveries against one purchase order.
-- A separate GRN is created for each delivery; only quality-approved quantities
-- are released into inventory by the existing Inventory consumer.

SET @has_completes_column = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'warehouse_goods_receipts'
      AND column_name = 'completes_purchase_order'
);
SET @add_completes_column_sql = IF(
    @has_completes_column = 0,
    'ALTER TABLE warehouse_goods_receipts ADD COLUMN completes_purchase_order BOOLEAN NOT NULL DEFAULT FALSE AFTER received_by_user_id',
    'SELECT 1'
);
PREPARE add_completes_column_statement FROM @add_completes_column_sql;
EXECUTE add_completes_column_statement;
DEALLOCATE PREPARE add_completes_column_statement;

-- Existing data came from the old exact/full-receipt flow.
UPDATE warehouse_goods_receipts receipt
SET receipt.completes_purchase_order = TRUE
WHERE receipt.completes_purchase_order = FALSE
  AND NOT EXISTS (
      SELECT 1
      FROM warehouse_expected_purchase_order_items expected
      LEFT JOIN warehouse_goods_receipt_items received
             ON received.goods_receipt_id = receipt.id
            AND received.product_id = expected.product_id
      WHERE expected.purchase_order_id = receipt.purchase_order_id
        AND COALESCE(received.quantity, 0) < expected.quantity
  );

SET @has_single_grn_index = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'warehouse_goods_receipts'
      AND index_name = 'uq_warehouse_grn_po'
);
SET @drop_single_grn_index_sql = IF(
    @has_single_grn_index > 0,
    'ALTER TABLE warehouse_goods_receipts DROP INDEX uq_warehouse_grn_po',
    'SELECT 1'
);
PREPARE drop_single_grn_index_statement FROM @drop_single_grn_index_sql;
EXECUTE drop_single_grn_index_statement;
DEALLOCATE PREPARE drop_single_grn_index_statement;

SET @has_receipt_lookup_index = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'warehouse_goods_receipts'
      AND index_name = 'ix_warehouse_grn_po_received'
);
SET @add_receipt_lookup_index_sql = IF(
    @has_receipt_lookup_index = 0,
    'CREATE INDEX ix_warehouse_grn_po_received ON warehouse_goods_receipts (purchase_order_id, received_on_utc)',
    'SELECT 1'
);
PREPARE add_receipt_lookup_index_statement FROM @add_receipt_lookup_index_sql;
EXECUTE add_receipt_lookup_index_statement;
DEALLOCATE PREPARE add_receipt_lookup_index_statement;
