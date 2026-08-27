using GRD.SpChn.Identity.Domain;

namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(
        UserAccount user,
        CancellationToken cancellationToken = default);
}
