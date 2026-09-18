using GRD.SpChn.Accounting.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

public sealed record ListInvoiceCandidatesQuery
    : IRequest<IReadOnlyCollection<InvoiceCandidateResponse>>, IAccountingTransactionalRequest;

internal sealed class ListInvoiceCandidatesQueryHandler(IAccountingRepository repository)
    : IRequestHandler<ListInvoiceCandidatesQuery, IReadOnlyCollection<InvoiceCandidateResponse>>
{
    public Task<IReadOnlyCollection<InvoiceCandidateResponse>> Handle(
        ListInvoiceCandidatesQuery request,
        CancellationToken cancellationToken) => repository.ListInvoiceCandidatesAsync(cancellationToken);
}

public sealed record ListPayablesQuery
    : IRequest<IReadOnlyCollection<PayableResponse>>, IAccountingTransactionalRequest;

internal sealed class ListPayablesQueryHandler(IAccountingRepository repository)
    : IRequestHandler<ListPayablesQuery, IReadOnlyCollection<PayableResponse>>
{
    public Task<IReadOnlyCollection<PayableResponse>> Handle(
        ListPayablesQuery request,
        CancellationToken cancellationToken) => repository.ListPayablesAsync(cancellationToken);
}

public sealed record ListJournalEntriesQuery
    : IRequest<IReadOnlyCollection<JournalEntryResponse>>, IAccountingTransactionalRequest;

internal sealed class ListJournalEntriesQueryHandler(IAccountingRepository repository)
    : IRequestHandler<ListJournalEntriesQuery, IReadOnlyCollection<JournalEntryResponse>>
{
    public Task<IReadOnlyCollection<JournalEntryResponse>> Handle(
        ListJournalEntriesQuery request,
        CancellationToken cancellationToken) => repository.ListJournalEntriesAsync(cancellationToken);
}
