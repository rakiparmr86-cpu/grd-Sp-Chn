using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Domain;
using GRD.SpChn.Security;

namespace GRD.SpChn.Identity.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer(IAccessTokenService tokenService)
    : IAccessTokenIssuer
{
    public AccessToken Issue(UserAccount user)
    {
        var token = tokenService.Create(new AccessTokenDescriptor(
            user.Id,
            user.UserName,
            user.Role,
            user.OrganizationUnitId,
            user.Permissions));
        return new AccessToken(token.Token, token.ExpiresOnUtc);
    }
}
