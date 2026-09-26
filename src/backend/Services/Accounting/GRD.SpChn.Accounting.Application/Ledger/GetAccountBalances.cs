using MediatR;

namespace GRD.SpChn.Accounting.Application.Ledger;

// Per-account opening (before FromUtc), period (FromUtc..ToUtc) and closing totals.
// Trial Balance, Profit and Loss and Balance Sheet are all built from this one read.
public sealed record GetAccountBalancesQuery(DateTime FromUtc, DateTime ToUtc)
    : IRequest<AccountBalancesResponse>;

public sealed record AccountBalancesResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<AccountBalance> Accounts);

public sealed record AccountBalance(
    string Code,
    string Name,
    string Type,
    string Group,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit)
{
    // Positive is a debit balance, negative a credit balance.
    public decimal Closing => OpeningDebit - OpeningCredit + PeriodDebit - PeriodCredit;
}

public sealed record AccountTotalsRow(
    string AccountCode,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit);

public interface IAccountBalancesReader
{
    Task<IReadOnlyList<AccountTotalsRow>> GetTotalsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

internal sealed class GetAccountBalancesQueryHandler(IAccountBalancesReader reader)
    : IRequestHandler<GetAccountBalancesQuery, AccountBalancesResponse>
{
    public async Task<AccountBalancesResponse> Handle(
        GetAccountBalancesQuery request,
        CancellationToken cancellationToken)
    {
        var totals = await reader.GetTotalsAsync(request.FromUtc, request.ToUtc, cancellationToken);
        // Every account in the chart is reported, with zeros when nothing was posted, so
        // Cash, Bank and Stock always show their opening and closing balances.
        var posted = totals.Select(row => row.AccountCode).ToHashSet();
        var unposted = LedgerAccounts.AllCodes
            .Where(code => !posted.Contains(code))
            .Select(code => new AccountTotalsRow(code, 0, 0, 0, 0));
        var accounts = totals
            .Concat(unposted)
            .Select(row =>
            {
                var definition = LedgerAccounts.DefinitionOf(row.AccountCode);
                return new AccountBalance(
                    row.AccountCode,
                    definition.Name,
                    definition.Type.ToString(),
                    definition.Group,
                    row.OpeningDebit,
                    row.OpeningCredit,
                    row.PeriodDebit,
                    row.PeriodCredit);
            })
            .OrderBy(account => account.Type)
            .ThenBy(account => account.Name)
            .ToList();
        return new AccountBalancesResponse(request.FromUtc, request.ToUtc, accounts);
    }
}
