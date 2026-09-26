using Dapper;
using GRD.SpChn.Accounting.Application.Ledger;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Accounting.Infrastructure.Persistence;

/// <summary>
/// Read-only ledger over the immutable journal. Journal lines carry only an account
/// code, so the party (supplier) and voucher reference are resolved from the entry's
/// source: accepted GRN (via the PO projection), vendor invoice, payment or payment batch.
/// </summary>
internal sealed class AccountLedgerReader(IDbConnectionFactory connectionFactory) : IAccountLedgerReader
{
    private const string SourceJoins = """
        FROM accounting_journal_entries journal
        INNER JOIN accounting_journal_lines line ON line.journal_entry_id = journal.id
        LEFT JOIN accounting_accepted_receipts receipt
               ON journal.source_type = 'GoodsReceipt' AND receipt.goods_receipt_id = journal.source_id
        LEFT JOIN accounting_purchase_orders receipt_order
               ON receipt_order.purchase_order_id = receipt.purchase_order_id
        LEFT JOIN accounting_vendor_payables invoice
               ON journal.source_type = 'VendorPayable' AND invoice.id = journal.source_id
        LEFT JOIN accounting_payment_batches batch
               ON journal.source_type = 'AccountingPaymentBatch' AND batch.id = journal.source_id
        LEFT JOIN accounting_payments payment
               ON journal.source_type = 'AccountingPayment' AND payment.id = journal.source_id
        LEFT JOIN accounting_vendor_payables paid_invoice
               ON paid_invoice.id = payment.payable_id
        """;

    private const string SupplierColumn =
        "COALESCE(invoice.supplier_id, batch.supplier_id, paid_invoice.supplier_id, receipt_order.supplier_id)";

    public async Task<IReadOnlyList<LedgerOpeningBalance>> GetOpeningBalancesAsync(
        DateTime fromUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OpeningRow>(new CommandDefinition(
            $"""
            SELECT line.account_code AS AccountCode,
                   {SupplierColumn} AS SupplierId,
                   SUM(line.debit_amount) AS Debit,
                   SUM(line.credit_amount) AS Credit
            {SourceJoins}
            WHERE journal.posted_on_utc < @FromUtc
            GROUP BY line.account_code, {SupplierColumn};
            """,
            new { FromUtc = fromUtc },
            cancellationToken: cancellationToken));
        return rows
            .Select(row => new LedgerOpeningBalance(row.AccountCode, ParseSupplier(row.SupplierId), row.Debit, row.Credit))
            .ToList();
    }

    // COALESCE over CHAR(36) columns comes back as text rather than a Guid.
    private static Guid? ParseSupplier(string? value) =>
        Guid.TryParse(value, out var supplierId) ? supplierId : null;

    public async Task<IReadOnlyList<LedgerLine>> GetLinesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LedgerLineRow>(new CommandDefinition(
            $"""
            SELECT journal.id AS JournalEntryId,
                   journal.entry_number AS EntryNumber,
                   journal.entry_type AS EntryType,
                   journal.posted_on_utc AS PostedOnUtc,
                   line.line_number AS LineNumber,
                   line.account_code AS AccountCode,
                   line.debit_amount AS Debit,
                   line.credit_amount AS Credit,
                   journal.description AS Description,
                   {SupplierColumn} AS SupplierId,
                   COALESCE(receipt.goods_receipt_number, invoice.supplier_invoice_number,
                            batch.bank_reference, payment.bank_reference) AS VoucherReference,
                   (SELECT GROUP_CONCAT(DISTINCT other.account_code ORDER BY other.account_code SEPARATOR ',')
                    FROM accounting_journal_lines other
                    WHERE other.journal_entry_id = journal.id
                      AND other.account_code <> line.account_code) AS ContraAccounts
            {SourceJoins}
            WHERE journal.posted_on_utc >= @FromUtc
              AND journal.posted_on_utc < @ToUtc
            ORDER BY journal.posted_on_utc, journal.entry_number, line.line_number;
            """,
            new { FromUtc = fromUtc, ToUtc = toUtc },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new LedgerLine(
                row.JournalEntryId,
                row.EntryNumber,
                row.EntryType,
                DateTime.SpecifyKind(row.PostedOnUtc, DateTimeKind.Utc),
                row.LineNumber,
                row.AccountCode,
                row.Debit,
                row.Credit,
                row.Description,
                ParseSupplier(row.SupplierId),
                row.VoucherReference,
                string.IsNullOrEmpty(row.ContraAccounts) ? [] : row.ContraAccounts.Split(',')))
            .ToList();
    }

    private sealed class LedgerLineRow
    {
        public Guid JournalEntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public DateTime PostedOnUtc { get; set; }
        public int LineNumber { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? SupplierId { get; set; }
        public string? VoucherReference { get; set; }
        public string? ContraAccounts { get; set; }
    }

    private sealed class OpeningRow
    {
        public string AccountCode { get; set; } = string.Empty;
        public string? SupplierId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
