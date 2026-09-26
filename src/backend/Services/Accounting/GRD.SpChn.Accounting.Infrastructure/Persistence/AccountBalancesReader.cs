using Dapper;
using GRD.SpChn.Accounting.Application.Ledger;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Accounting.Infrastructure.Persistence;

internal sealed class AccountBalancesReader(IDbConnectionFactory connectionFactory) : IAccountBalancesReader
{
    public async Task<IReadOnlyList<AccountTotalsRow>> GetTotalsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountTotalsRow>(new CommandDefinition(
            """
            SELECT line.account_code AS AccountCode,
                   SUM(CASE WHEN journal.posted_on_utc < @FromUtc THEN line.debit_amount ELSE 0 END) AS OpeningDebit,
                   SUM(CASE WHEN journal.posted_on_utc < @FromUtc THEN line.credit_amount ELSE 0 END) AS OpeningCredit,
                   SUM(CASE WHEN journal.posted_on_utc >= @FromUtc THEN line.debit_amount ELSE 0 END) AS PeriodDebit,
                   SUM(CASE WHEN journal.posted_on_utc >= @FromUtc THEN line.credit_amount ELSE 0 END) AS PeriodCredit
            FROM accounting_journal_entries journal
            INNER JOIN accounting_journal_lines line ON line.journal_entry_id = journal.id
            WHERE journal.posted_on_utc < @ToUtc
            GROUP BY line.account_code;
            """,
            new { FromUtc = fromUtc, ToUtc = toUtc },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
