using MediatR;

namespace GRD.SpChn.Accounting.Application.Ledger;

// Every posted journal line in a period, tagged with its party (supplier) and voucher
// reference, plus opening balances per account and party. The screen builds account and
// party ledgers with running Dr/Cr balances from this one read.
public sealed record GetAccountLedgerQuery(DateTime FromUtc, DateTime ToUtc)
    : IRequest<AccountLedgerResponse>;

public sealed record AccountLedgerResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<LedgerAccount> Accounts,
    IReadOnlyList<LedgerOpeningBalance> OpeningBalances,
    IReadOnlyList<LedgerLine> Lines);

public sealed record LedgerAccount(string Code, string Name);

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,
    Unclassified,
}

public sealed record AccountDefinition(string Code, string Name, AccountType Type, string Group);

public sealed record LedgerOpeningBalance(
    string AccountCode,
    Guid? SupplierId,
    decimal Debit,
    decimal Credit);

public sealed record LedgerLine(
    Guid JournalEntryId,
    string EntryNumber,
    string EntryType,
    DateTime PostedOnUtc,
    int LineNumber,
    string AccountCode,
    decimal Debit,
    decimal Credit,
    string Description,
    Guid? SupplierId,
    string? VoucherReference,
    IReadOnlyList<string> ContraAccounts);

public interface IAccountLedgerReader
{
    Task<IReadOnlyList<LedgerOpeningBalance>> GetOpeningBalancesAsync(
        DateTime fromUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerLine>> GetLinesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

public static class LedgerAccounts
{
    // Account codes are posted by the payable workflow; names are for display only.
    // Chart of accounts for the codes the workflows post. Type drives the financial
    // statements; a code missing here is reported as Unclassified rather than guessed.
    private static readonly IReadOnlyDictionary<string, AccountDefinition> Definitions =
        new AccountDefinition[]
        {
            new("INVENTORY", "Inventory (Stock)", AccountType.Asset, "Stock-in-hand"),
            new("INPUT_TAX", "Input Tax", AccountType.Asset, "Duties and taxes (input credit)"),
            new("CASH", "Cash", AccountType.Asset, "Cash-in-hand"),
            new("BANK", "Bank", AccountType.Asset, "Bank accounts"),
            new("GRNI", "Goods Received Not Invoiced", AccountType.Liability, "Provisions (GRNI)"),
            new("VENDOR_PAYABLE", "Vendor Payable (Sundry Creditors)", AccountType.Liability, "Sundry creditors"),
        }.ToDictionary(definition => definition.Code);

    public static AccountDefinition DefinitionOf(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : new AccountDefinition(code, code, AccountType.Unclassified, "Unclassified");

    public static string NameOf(string code) => DefinitionOf(code).Name;

    public static IEnumerable<string> AllCodes => Definitions.Keys;

    public static IReadOnlyList<LedgerAccount> From(IEnumerable<string> codes) =>
        Definitions.Keys
            .Union(codes)
            .Select(code => new LedgerAccount(code, NameOf(code)))
            .OrderBy(account => account.Name)
            .ToList();
}

internal sealed class GetAccountLedgerQueryHandler(IAccountLedgerReader reader)
    : IRequestHandler<GetAccountLedgerQuery, AccountLedgerResponse>
{
    public async Task<AccountLedgerResponse> Handle(
        GetAccountLedgerQuery request,
        CancellationToken cancellationToken)
    {
        var openings = await reader.GetOpeningBalancesAsync(request.FromUtc, cancellationToken);
        var lines = await reader.GetLinesAsync(request.FromUtc, request.ToUtc, cancellationToken);
        var accounts = LedgerAccounts.From(
            openings.Select(opening => opening.AccountCode).Concat(lines.Select(line => line.AccountCode)));
        return new AccountLedgerResponse(request.FromUtc, request.ToUtc, accounts, openings, lines);
    }
}
