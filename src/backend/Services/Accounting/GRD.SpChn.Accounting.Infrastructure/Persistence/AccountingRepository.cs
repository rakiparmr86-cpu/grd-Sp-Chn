using Dapper;
using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Application.Payables;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Accounting.Infrastructure.Persistence;

internal sealed class AccountingRepository(AccountingUnitOfWork unitOfWork)
    : IAccountingRepository
{
    public async Task RecordPurchaseOrderAsync(
        PurchaseOrderIssuedIntegrationEvent message,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO accounting_purchase_orders
                (purchase_order_id, purchase_order_number, supplier_id,
                 destination_organization_unit_id, currency, issued_on_utc)
            VALUES
                (@PurchaseOrderId, @PurchaseOrderNumber, @SupplierId,
                 @DestinationOrganizationUnitId, @Currency, @IssuedOnUtc)
            ON DUPLICATE KEY UPDATE
                purchase_order_number = VALUES(purchase_order_number),
                supplier_id = VALUES(supplier_id),
                destination_organization_unit_id = VALUES(destination_organization_unit_id),
                currency = VALUES(currency),
                issued_on_utc = VALUES(issued_on_utc);
            """,
            new
            {
                message.PurchaseOrderId,
                message.PurchaseOrderNumber,
                message.SupplierId,
                message.DestinationOrganizationUnitId,
                message.Currency,
                IssuedOnUtc = message.OccurredOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        foreach (var item in message.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO accounting_purchase_order_items
                    (purchase_order_id, product_id, ordered_quantity, unit_of_measure, unit_price)
                VALUES (@PurchaseOrderId, @ProductId, @Quantity, @UnitOfMeasure, @UnitPrice)
                ON DUPLICATE KEY UPDATE
                    ordered_quantity = VALUES(ordered_quantity),
                    unit_of_measure = VALUES(unit_of_measure),
                    unit_price = VALUES(unit_price);
                """,
                new
                {
                    message.PurchaseOrderId,
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure,
                    item.UnitPrice
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task RecordAcceptedReceiptAsync(
        QualityInspectionApprovedIntegrationEvent message,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO accounting_accepted_receipts
                (goods_receipt_id, goods_receipt_number, quality_inspection_id,
                 purchase_order_id, destination_organization_unit_id, accepted_on_utc)
            VALUES
                (@GoodsReceiptId, @GoodsReceiptNumber, @QualityInspectionId,
                 @PurchaseOrderId, @DestinationOrganizationUnitId, @AcceptedOnUtc)
            ON DUPLICATE KEY UPDATE
                goods_receipt_number = VALUES(goods_receipt_number),
                quality_inspection_id = VALUES(quality_inspection_id),
                purchase_order_id = VALUES(purchase_order_id),
                destination_organization_unit_id = VALUES(destination_organization_unit_id),
                accepted_on_utc = VALUES(accepted_on_utc);
            """,
            new
            {
                message.GoodsReceiptId,
                message.GoodsReceiptNumber,
                message.QualityInspectionId,
                message.PurchaseOrderId,
                message.DestinationOrganizationUnitId,
                AcceptedOnUtc = message.OccurredOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        foreach (var item in message.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO accounting_accepted_receipt_items
                    (goods_receipt_id, product_id, accepted_quantity, unit_of_measure)
                VALUES (@GoodsReceiptId, @ProductId, @Quantity, @UnitOfMeasure)
                ON DUPLICATE KEY UPDATE
                    accepted_quantity = VALUES(accepted_quantity),
                    unit_of_measure = VALUES(unit_of_measure);
                """,
                new
                {
                    message.GoodsReceiptId,
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task ReconcileGoodsReceiptAccrualsAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var receipts = (await unitOfWork.Connection.QueryAsync<AccrualCandidateRow>(new CommandDefinition(
            """
            SELECT
                receipt.goods_receipt_id AS GoodsReceiptId,
                receipt.goods_receipt_number AS GoodsReceiptNumber,
                po.purchase_order_number AS PurchaseOrderNumber,
                po.currency AS Currency,
                receipt.accepted_on_utc AS AcceptedOnUtc
            FROM accounting_accepted_receipts receipt
            INNER JOIN accounting_purchase_orders po
                    ON po.purchase_order_id = receipt.purchase_order_id
            WHERE receipt.purchase_order_id = @PurchaseOrderId
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM accounting_journal_entries journal
                  WHERE journal.entry_type = 'GoodsReceiptAccrual'
                    AND journal.source_type = 'GoodsReceipt'
                    AND journal.source_id = receipt.goods_receipt_id
              );
            """,
            new { PurchaseOrderId = purchaseOrderId },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

        foreach (var receipt in receipts)
        {
            var lines = (await unitOfWork.Connection.QueryAsync<AccrualLineRow>(new CommandDefinition(
                """
                SELECT
                    accepted.product_id AS ProductId,
                    accepted.accepted_quantity AS Quantity,
                    po_item.unit_price AS UnitPrice
                FROM accounting_accepted_receipt_items accepted
                INNER JOIN accounting_accepted_receipts receipt
                        ON receipt.goods_receipt_id = accepted.goods_receipt_id
                INNER JOIN accounting_purchase_order_items po_item
                        ON po_item.purchase_order_id = receipt.purchase_order_id
                       AND po_item.product_id = accepted.product_id
                WHERE accepted.goods_receipt_id = @GoodsReceiptId;
                """,
                new { receipt.GoodsReceiptId },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken))).AsList();
            if (lines.Count == 0) continue;

            var amount = decimal.Round(lines.Sum(line => line.Quantity * line.UnitPrice), 2);
            if (amount <= 0) continue;

            await AddJournalEntryAsync(JournalEntry.Create(
                "GoodsReceiptAccrual",
                "GoodsReceipt",
                receipt.GoodsReceiptId,
                receipt.Currency,
                $"Accepted receipt {receipt.GoodsReceiptNumber} against {receipt.PurchaseOrderNumber}",
                [
                    JournalLine.DebitLine("INVENTORY", amount),
                    JournalLine.CreditLine("GRNI", amount)
                ],
                receipt.AcceptedOnUtc), cancellationToken);
        }
    }

    public async Task<InvoiceMatchContext?> GetInvoiceMatchContextAsync(
        Guid purchaseOrderId,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        var header = await unitOfWork.Connection.QuerySingleOrDefaultAsync<InvoiceMatchHeaderRow>(new CommandDefinition(
            """
            SELECT
                po.purchase_order_id AS PurchaseOrderId,
                po.purchase_order_number AS PurchaseOrderNumber,
                receipt.goods_receipt_id AS GoodsReceiptId,
                receipt.goods_receipt_number AS GoodsReceiptNumber,
                po.supplier_id AS SupplierId,
                po.currency AS Currency
            FROM accounting_purchase_orders po
            INNER JOIN accounting_accepted_receipts receipt
                    ON receipt.purchase_order_id = po.purchase_order_id
            WHERE po.purchase_order_id = @PurchaseOrderId
              AND receipt.goods_receipt_id = @GoodsReceiptId;
            """,
            new { PurchaseOrderId = purchaseOrderId, GoodsReceiptId = goodsReceiptId },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        if (header is null) return null;

        var items = (await unitOfWork.Connection.QueryAsync<InvoiceMatchItem>(new CommandDefinition(
            """
            SELECT
                po_item.product_id AS ProductId,
                po_item.ordered_quantity AS PurchaseOrderQuantity,
                accepted.accepted_quantity AS AcceptedQuantity,
                accepted.unit_of_measure AS UnitOfMeasure,
                po_item.unit_price AS PurchaseOrderUnitPrice
            FROM accounting_accepted_receipt_items accepted
            INNER JOIN accounting_accepted_receipts receipt
                    ON receipt.goods_receipt_id = accepted.goods_receipt_id
            INNER JOIN accounting_purchase_order_items po_item
                    ON po_item.purchase_order_id = receipt.purchase_order_id
                   AND po_item.product_id = accepted.product_id
            WHERE accepted.goods_receipt_id = @GoodsReceiptId;
            """,
            new { GoodsReceiptId = goodsReceiptId },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

        return new InvoiceMatchContext(
            header.PurchaseOrderId,
            header.PurchaseOrderNumber,
            header.GoodsReceiptId,
            header.GoodsReceiptNumber,
            header.SupplierId,
            header.Currency,
            items);
    }

    public Task<bool> SupplierInvoiceExistsAsync(
        Guid supplierId,
        string supplierInvoiceNumber,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(
                SELECT 1 FROM accounting_vendor_payables
                WHERE supplier_id = @SupplierId
                  AND supplier_invoice_number = @SupplierInvoiceNumber);
            """,
            new { SupplierId = supplierId, SupplierInvoiceNumber = supplierInvoiceNumber },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyCollection<InvoiceCandidateResponse>> ListInvoiceCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = (await unitOfWork.Connection.QueryAsync<InvoiceCandidateRow>(new CommandDefinition(
            """
            SELECT
                po.purchase_order_id AS PurchaseOrderId,
                po.purchase_order_number AS PurchaseOrderNumber,
                receipt.goods_receipt_id AS GoodsReceiptId,
                receipt.goods_receipt_number AS GoodsReceiptNumber,
                po.supplier_id AS SupplierId,
                po.currency AS Currency,
                receipt.accepted_on_utc AS AcceptedOnUtc,
                accepted.product_id AS ProductId,
                accepted.accepted_quantity AS AcceptedQuantity,
                COALESCE(invoiced.invoiced_quantity, 0) AS AlreadyInvoicedQuantity,
                accepted.accepted_quantity - COALESCE(invoiced.invoiced_quantity, 0) AS RemainingQuantity,
                accepted.unit_of_measure AS UnitOfMeasure,
                po_item.unit_price AS PurchaseOrderUnitPrice
            FROM accounting_accepted_receipts receipt
            INNER JOIN accounting_purchase_orders po
                    ON po.purchase_order_id = receipt.purchase_order_id
            INNER JOIN accounting_accepted_receipt_items accepted
                    ON accepted.goods_receipt_id = receipt.goods_receipt_id
            INNER JOIN accounting_purchase_order_items po_item
                    ON po_item.purchase_order_id = receipt.purchase_order_id
                   AND po_item.product_id = accepted.product_id
            LEFT JOIN
            (
                SELECT payable.goods_receipt_id, line.product_id, SUM(line.quantity) AS invoiced_quantity
                FROM accounting_vendor_payables payable
                INNER JOIN accounting_vendor_invoice_lines line ON line.payable_id = payable.id
                GROUP BY payable.goods_receipt_id, line.product_id
            ) invoiced ON invoiced.goods_receipt_id = receipt.goods_receipt_id
                      AND invoiced.product_id = accepted.product_id
            ORDER BY receipt.accepted_on_utc DESC, accepted.product_id;
            """,
            transaction: unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

        return rows
            .GroupBy(row => new
            {
                row.PurchaseOrderId,
                row.PurchaseOrderNumber,
                row.GoodsReceiptId,
                row.GoodsReceiptNumber,
                row.SupplierId,
                row.Currency,
                row.AcceptedOnUtc
            })
            .Select(group => new InvoiceCandidateResponse(
                group.Key.PurchaseOrderId,
                group.Key.PurchaseOrderNumber,
                group.Key.GoodsReceiptId,
                group.Key.GoodsReceiptNumber,
                group.Key.SupplierId,
                group.Key.Currency,
                group.Key.AcceptedOnUtc,
                group.Select(row => new InvoiceCandidateItem(
                    row.ProductId,
                    row.AcceptedQuantity,
                    row.AlreadyInvoicedQuantity,
                    row.RemainingQuantity,
                    row.UnitOfMeasure,
                    row.PurchaseOrderUnitPrice)).ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.QueryAsync<InvoicedQuantityRow>(new CommandDefinition(
            """
            SELECT line.product_id AS ProductId, SUM(line.quantity) AS Quantity
            FROM accounting_vendor_invoice_lines line
            INNER JOIN accounting_vendor_payables payable ON payable.id = line.payable_id
            WHERE payable.goods_receipt_id = @GoodsReceiptId
            GROUP BY line.product_id;
            """,
            new { GoodsReceiptId = goodsReceiptId },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.ProductId, row => row.Quantity);
    }

    public async Task AddPayableAsync(
        VendorPayable payable,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO accounting_vendor_payables
                (id, invoice_number, supplier_invoice_number, purchase_order_id,
                 goods_receipt_id, supplier_id, currency, subtotal, tax_amount,
                 total_amount, status, created_by_user_id, invoice_date_utc,
                 due_date_utc, created_on_utc)
            VALUES
                (@Id, @InvoiceNumber, @SupplierInvoiceNumber, @PurchaseOrderId,
                 @GoodsReceiptId, @SupplierId, @Currency, @Subtotal, @TaxAmount,
                 @TotalAmount, @Status, @CreatedByUserId, @InvoiceDateUtc,
                 @DueDateUtc, @CreatedOnUtc);
            """,
            new
            {
                payable.Id,
                payable.InvoiceNumber,
                payable.SupplierInvoiceNumber,
                payable.PurchaseOrderId,
                payable.GoodsReceiptId,
                payable.SupplierId,
                payable.Currency,
                payable.Subtotal,
                payable.TaxAmount,
                payable.TotalAmount,
                Status = payable.Status.ToString(),
                payable.CreatedByUserId,
                payable.InvoiceDateUtc,
                payable.DueDateUtc,
                payable.CreatedOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        foreach (var line in payable.Lines)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO accounting_vendor_invoice_lines
                    (payable_id, product_id, quantity, unit_of_measure, unit_price, line_amount)
                VALUES (@PayableId, @ProductId, @Quantity, @UnitOfMeasure, @UnitPrice, @LineAmount);
                """,
                new
                {
                    PayableId = payable.Id,
                    line.ProductId,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineAmount
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<VendorPayable?> GetPayableForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await unitOfWork.Connection.QuerySingleOrDefaultAsync<PayableRow>(new CommandDefinition(
            """
            SELECT
                id AS Id, invoice_number AS InvoiceNumber,
                supplier_invoice_number AS SupplierInvoiceNumber,
                purchase_order_id AS PurchaseOrderId, goods_receipt_id AS GoodsReceiptId,
                supplier_id AS SupplierId, currency AS Currency, tax_amount AS TaxAmount,
                status AS Status, created_by_user_id AS CreatedByUserId,
                approved_by_user_id AS ApprovedByUserId, paid_by_user_id AS PaidByUserId,
                bank_reference AS BankReference, invoice_date_utc AS InvoiceDateUtc,
                due_date_utc AS DueDateUtc, created_on_utc AS CreatedOnUtc,
                approved_on_utc AS ApprovedOnUtc, paid_on_utc AS PaidOnUtc
            FROM accounting_vendor_payables
            WHERE id = @Id
            FOR UPDATE;
            """,
            new { Id = id },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        if (row is null) return null;

        var lines = (await unitOfWork.Connection.QueryAsync<VendorInvoiceLine>(new CommandDefinition(
            """
            SELECT product_id AS ProductId, quantity AS Quantity,
                   unit_of_measure AS UnitOfMeasure, unit_price AS UnitPrice
            FROM accounting_vendor_invoice_lines
            WHERE payable_id = @Id
            ORDER BY product_id;
            """,
            new { Id = id },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

        return VendorPayable.Rehydrate(
            row.Id, row.InvoiceNumber, row.SupplierInvoiceNumber, row.PurchaseOrderId,
            row.GoodsReceiptId, row.SupplierId, row.Currency, row.TaxAmount,
            Enum.Parse<VendorPayableStatus>(row.Status), row.CreatedByUserId,
            row.ApprovedByUserId, row.PaidByUserId, row.BankReference,
            row.InvoiceDateUtc, row.DueDateUtc, row.CreatedOnUtc, row.ApprovedOnUtc,
            row.PaidOnUtc, lines);
    }

    public Task UpdatePayableAsync(
        VendorPayable payable,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE accounting_vendor_payables
            SET status = @Status,
                approved_by_user_id = @ApprovedByUserId,
                approved_on_utc = @ApprovedOnUtc,
                paid_by_user_id = @PaidByUserId,
                bank_reference = @BankReference,
                paid_on_utc = @PaidOnUtc
            WHERE id = @Id;
            """,
            new
            {
                payable.Id,
                Status = payable.Status.ToString(),
                payable.ApprovedByUserId,
                payable.ApprovedOnUtc,
                payable.PaidByUserId,
                payable.BankReference,
                payable.PaidOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

    public Task AddPaymentAsync(
        Guid paymentId,
        VendorPayable payable,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO accounting_payments
                (id, payable_id, amount, currency, bank_reference,
                 paid_by_user_id, paid_on_utc, recorded_on_utc)
            VALUES
                (@Id, @PayableId, @Amount, @Currency, @BankReference,
                 @PaidByUserId, @PaidOnUtc, @RecordedOnUtc);
            """,
            new
            {
                Id = paymentId,
                PayableId = payable.Id,
                Amount = payable.TotalAmount,
                payable.Currency,
                payable.BankReference,
                payable.PaidByUserId,
                payable.PaidOnUtc,
                RecordedOnUtc = DateTime.UtcNow
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

    public async Task AddJournalEntryAsync(
        JournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO accounting_journal_entries
                (id, entry_number, entry_type, source_type, source_id,
                 currency, description, posted_on_utc)
            VALUES
                (@Id, @EntryNumber, @EntryType, @SourceType, @SourceId,
                 @Currency, @Description, @PostedOnUtc);
            """,
            entry,
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var sequence = 0;
        foreach (var line in entry.Lines)
        {
            sequence++;
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO accounting_journal_lines
                    (journal_entry_id, line_number, account_code, debit_amount, credit_amount)
                VALUES (@JournalEntryId, @LineNumber, @AccountCode, @Debit, @Credit);
                """,
                new
                {
                    JournalEntryId = entry.Id,
                    LineNumber = sequence,
                    line.AccountCode,
                    line.Debit,
                    line.Credit
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyCollection<PayableResponse>> ListPayablesAsync(
        CancellationToken cancellationToken = default) =>
        (await unitOfWork.Connection.QueryAsync<PayableResponse>(new CommandDefinition(
            """
            SELECT
                id AS Id, invoice_number AS InvoiceNumber,
                supplier_invoice_number AS SupplierInvoiceNumber,
                purchase_order_id AS PurchaseOrderId, goods_receipt_id AS GoodsReceiptId,
                supplier_id AS SupplierId, currency AS Currency, subtotal AS Subtotal,
                tax_amount AS TaxAmount, total_amount AS TotalAmount, status AS Status,
                created_by_user_id AS CreatedByUserId, approved_by_user_id AS ApprovedByUserId,
                paid_by_user_id AS PaidByUserId, bank_reference AS BankReference,
                invoice_date_utc AS InvoiceDateUtc, due_date_utc AS DueDateUtc,
                created_on_utc AS CreatedOnUtc, approved_on_utc AS ApprovedOnUtc,
                paid_on_utc AS PaidOnUtc
            FROM accounting_vendor_payables
            ORDER BY created_on_utc DESC;
            """,
            transaction: unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

    public async Task<IReadOnlyCollection<JournalEntryResponse>> ListJournalEntriesAsync(
        CancellationToken cancellationToken = default) =>
        (await unitOfWork.Connection.QueryAsync<JournalEntryResponse>(new CommandDefinition(
            """
            SELECT
                journal.id AS Id, journal.entry_number AS EntryNumber,
                journal.entry_type AS EntryType, journal.source_type AS SourceType,
                journal.source_id AS SourceId, journal.currency AS Currency,
                SUM(line.debit_amount) AS DebitTotal,
                SUM(line.credit_amount) AS CreditTotal,
                journal.description AS Description, journal.posted_on_utc AS PostedOnUtc
            FROM accounting_journal_entries journal
            INNER JOIN accounting_journal_lines line ON line.journal_entry_id = journal.id
            GROUP BY journal.id, journal.entry_number, journal.entry_type,
                     journal.source_type, journal.source_id, journal.currency,
                     journal.description, journal.posted_on_utc
            ORDER BY journal.posted_on_utc DESC;
            """,
            transaction: unitOfWork.Transaction,
            cancellationToken: cancellationToken))).AsList();

    private sealed class AccrualCandidateRow
    {
        public Guid GoodsReceiptId { get; set; }
        public string GoodsReceiptNumber { get; set; } = string.Empty;
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateTime AcceptedOnUtc { get; set; }
    }

    private sealed class AccrualLineRow
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private sealed class InvoiceMatchHeaderRow
    {
        public Guid PurchaseOrderId { get; set; }
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public Guid GoodsReceiptId { get; set; }
        public string GoodsReceiptNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    private sealed class InvoicedQuantityRow
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    private sealed class InvoiceCandidateRow
    {
        public Guid PurchaseOrderId { get; set; }
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public Guid GoodsReceiptId { get; set; }
        public string GoodsReceiptNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime AcceptedOnUtc { get; set; }
        public Guid ProductId { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal AlreadyInvoicedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal PurchaseOrderUnitPrice { get; set; }
    }

    private sealed class PayableRow
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNumber { get; set; } = string.Empty;
        public Guid PurchaseOrderId { get; set; }
        public Guid GoodsReceiptId { get; set; }
        public Guid SupplierId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid CreatedByUserId { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public Guid? PaidByUserId { get; set; }
        public string? BankReference { get; set; }
        public DateTime InvoiceDateUtc { get; set; }
        public DateTime DueDateUtc { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public DateTime? ApprovedOnUtc { get; set; }
        public DateTime? PaidOnUtc { get; set; }
    }
}
