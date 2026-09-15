namespace GRD.SpChn.Accounting.Domain;

public sealed record JournalLine(string AccountCode, decimal Debit, decimal Credit)
{
    public static JournalLine DebitLine(string accountCode, decimal amount) =>
        new(accountCode, decimal.Round(amount, 2), 0);

    public static JournalLine CreditLine(string accountCode, decimal amount) =>
        new(accountCode, 0, decimal.Round(amount, 2));
}

public sealed record JournalEntry(
    Guid Id,
    string EntryNumber,
    string EntryType,
    string SourceType,
    Guid SourceId,
    string Currency,
    DateTime PostedOnUtc,
    string Description,
    IReadOnlyCollection<JournalLine> Lines)
{
    public static JournalEntry Create(
        string entryType,
        string sourceType,
        Guid sourceId,
        string currency,
        string description,
        IReadOnlyCollection<JournalLine> lines,
        DateTime? utcNow = null)
    {
        if (lines.Count < 2)
            throw new ArgumentException("A journal entry requires at least two lines.", nameof(lines));
        var debit = lines.Sum(line => line.Debit);
        var credit = lines.Sum(line => line.Credit);
        if (debit <= 0 || debit != credit)
            throw new InvalidOperationException("Journal entry debit and credit totals must be positive and equal.");

        var id = Guid.NewGuid();
        var now = utcNow ?? DateTime.UtcNow;
        return new JournalEntry(
            id,
            $"JE-{now:yyyyMMddHHmmss}-{id:N}"[..30],
            entryType,
            sourceType,
            sourceId,
            currency,
            now,
            description,
            lines);
    }
}
