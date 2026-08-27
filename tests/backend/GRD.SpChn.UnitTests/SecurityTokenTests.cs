using System.Security.Claims;
using System.Text;
using GRD.SpChn.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GRD.SpChn.UnitTests;

public sealed class SecurityTokenTests
{
    [Fact]
    public async Task Issued_token_contains_deterministic_user_org_role_and_permissions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:SigningKey"] = "test-signing-key-with-more-than-thirty-two-bytes"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddErpTokenIssuer(configuration);
        using var provider = services.BuildServiceProvider();
        var issuer = provider.GetRequiredService<IAccessTokenService>();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var issued = issuer.Create(new AccessTokenDescriptor(
            userId,
            "supervisor@grd.local",
            ErpRoles.Supervisor,
            organizationId,
            [ErpPermissions.GoodsReceiptRead, ErpPermissions.GoodsReceiptPost]));
        var handler = new JsonWebTokenHandler();
        var validation = await handler.ValidateTokenAsync(
            issued.Token,
            new TokenValidationParameters
            {
                ValidIssuer = "test-issuer",
                ValidAudience = "test-audience",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    "test-signing-key-with-more-than-thirty-two-bytes")),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                NameClaimType = "unique_name",
                RoleClaimType = "role"
            });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        var principal = new ClaimsPrincipal(validation.ClaimsIdentity);

        Assert.Equal(userId, principal.GetRequiredUserId());
        Assert.Equal(organizationId, principal.GetRequiredOrganizationUnitId());
        Assert.True(principal.IsInRole(ErpRoles.Supervisor));
        var permissions = principal.Claims
            .Where(claim => claim.Type == ErpClaimTypes.Permission)
            .Select(claim => claim.Value)
            .ToArray();
        Assert.Contains(ErpPermissions.GoodsReceiptRead, permissions);
        Assert.Contains(ErpPermissions.GoodsReceiptPost, permissions);
    }
}
