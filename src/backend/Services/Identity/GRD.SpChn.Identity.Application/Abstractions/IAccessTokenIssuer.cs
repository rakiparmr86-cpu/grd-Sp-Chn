using GRD.SpChn.Identity.Domain;

namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount user);
}

public sealed record AccessToken(string Value, DateTime ExpiresOnUtc);
