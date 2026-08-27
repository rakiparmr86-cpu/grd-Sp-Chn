using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GRD.SpChn.Security;

public static class ErpClaimTypes
{
    public const string OrganizationUnitId = "organization_unit_id";
    public const string Permission = "permission";
}

public static class ErpRoles
{
    public const string Director = "Director";
    public const string GeneralManager = "GeneralManager";
    public const string Manager = "Manager";
    public const string Supervisor = "Supervisor";
    public const string Executive = "Executive";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Director, GeneralManager, Manager, Supervisor, Executive],
        StringComparer.OrdinalIgnoreCase);
}

public static class ErpPermissions
{
    public const string IdentityUserCreate = "identity.user.create";
    public const string IdentityAccessProfileManage = "identity.access-profile.manage";
    public const string OrganizationRead = "organization.read";
    public const string OrganizationManage = "organization.manage";
    public const string MaterialRequestCreate = "procurement.material-request.create";
    public const string MaterialRequestRead = "procurement.material-request.read";
    public const string MaterialRequestApprove = "procurement.material-request.approve";
    public const string PurchaseOrderCreate = "procurement.purchase-order.create";
    public const string PurchaseOrderRead = "procurement.purchase-order.read";
    public const string InventoryStockRead = "inventory.stock.read";
    public const string GoodsReceiptRead = "warehouse.goods-receipt.read";
    public const string GoodsReceiptPost = "warehouse.goods-receipt.post";
}

public static class ErpPolicies
{
    public const string IdentityUserCreate = nameof(IdentityUserCreate);
    public const string IdentityAccessProfileManage = nameof(IdentityAccessProfileManage);
    public const string OrganizationRead = nameof(OrganizationRead);
    public const string OrganizationManage = nameof(OrganizationManage);
    public const string MaterialRequestCreate = nameof(MaterialRequestCreate);
    public const string MaterialRequestRead = nameof(MaterialRequestRead);
    public const string MaterialRequestApprove = nameof(MaterialRequestApprove);
    public const string PurchaseOrderCreate = nameof(PurchaseOrderCreate);
    public const string PurchaseOrderRead = nameof(PurchaseOrderRead);
    public const string InventoryStockRead = nameof(InventoryStockRead);
    public const string GoodsReceiptRead = nameof(GoodsReceiptRead);
    public const string GoodsReceiptPost = nameof(GoodsReceiptPost);
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "GRD.SpChn.Identity";
    public string Audience { get; init; } = "GRD.SpChn.Api";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 60;
}

public sealed record AccessTokenDescriptor(
    Guid UserId,
    string UserName,
    string Role,
    Guid OrganizationUnitId,
    IReadOnlyCollection<string> Permissions);

public interface IAccessTokenService
{
    (string Token, DateTime ExpiresOnUtc) Create(AccessTokenDescriptor descriptor);
}

internal sealed class JwtAccessTokenService(IOptions<JwtOptions> options)
    : IAccessTokenService
{
    private readonly JwtOptions _options = Validate(options.Value);

    public (string Token, DateTime ExpiresOnUtc) Create(AccessTokenDescriptor descriptor)
    {
        var now = DateTime.UtcNow;
        var expiresOnUtc = now.AddMinutes(Math.Max(5, _options.AccessTokenMinutes));
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = descriptor.UserId.ToString(),
            [JwtRegisteredClaimNames.UniqueName] = descriptor.UserName,
            ["role"] = descriptor.Role,
            [ErpClaimTypes.OrganizationUnitId] = descriptor.OrganizationUnitId.ToString(),
            [ErpClaimTypes.Permission] = descriptor.Permissions
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

        var credentials = new SigningCredentials(
            CreateSecurityKey(_options.SigningKey),
            SecurityAlgorithms.HmacSha256);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = now,
            Expires = expiresOnUtc,
            SigningCredentials = credentials,
            Claims = claims
        });

        return (token, expiresOnUtc);
    }

    private static JwtOptions Validate(JwtOptions options)
    {
        _ = CreateSecurityKey(options.SigningKey);
        return options;
    }

    private static SymmetricSecurityKey CreateSecurityKey(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must contain at least 32 UTF-8 bytes. " +
                "Supply it through configuration or a secret store.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }
}

public static class SecurityDependencyInjection
{
    public static IServiceCollection AddErpTokenIssuer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        return services;
    }

    public static IServiceCollection AddErpAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(options.SigningKey) ||
            Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must contain at least 32 UTF-8 bytes. " +
                "Use the same secret for Identity and protected APIs.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(authorization =>
        {
            AddPermissionPolicy(authorization, ErpPolicies.IdentityUserCreate, ErpPermissions.IdentityUserCreate);
            AddPermissionPolicy(authorization, ErpPolicies.IdentityAccessProfileManage, ErpPermissions.IdentityAccessProfileManage);
            AddPermissionPolicy(authorization, ErpPolicies.OrganizationRead, ErpPermissions.OrganizationRead);
            AddPermissionPolicy(authorization, ErpPolicies.OrganizationManage, ErpPermissions.OrganizationManage);
            AddPermissionPolicy(authorization, ErpPolicies.MaterialRequestCreate, ErpPermissions.MaterialRequestCreate);
            AddPermissionPolicy(authorization, ErpPolicies.MaterialRequestRead, ErpPermissions.MaterialRequestRead);
            AddPermissionPolicy(authorization, ErpPolicies.MaterialRequestApprove, ErpPermissions.MaterialRequestApprove);
            AddPermissionPolicy(authorization, ErpPolicies.PurchaseOrderCreate, ErpPermissions.PurchaseOrderCreate);
            AddPermissionPolicy(authorization, ErpPolicies.PurchaseOrderRead, ErpPermissions.PurchaseOrderRead);
            AddPermissionPolicy(authorization, ErpPolicies.InventoryStockRead, ErpPermissions.InventoryStockRead);
            AddPermissionPolicy(authorization, ErpPolicies.GoodsReceiptRead, ErpPermissions.GoodsReceiptRead);
            AddPermissionPolicy(authorization, ErpPolicies.GoodsReceiptPost, ErpPermissions.GoodsReceiptPost);
        });

        return services;
    }

    private static void AddPermissionPolicy(
        AuthorizationOptions options,
        string policy,
        string permission) =>
        options.AddPolicy(
            policy,
            builder => builder
                .RequireAuthenticatedUser()
                .RequireClaim(ErpClaimTypes.Permission, permission));
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal) =>
        GetRequiredGuid(principal, JwtRegisteredClaimNames.Sub, "user id");

    public static Guid GetRequiredOrganizationUnitId(this ClaimsPrincipal principal) =>
        GetRequiredGuid(principal, ErpClaimTypes.OrganizationUnitId, "organization unit id");

    private static Guid GetRequiredGuid(
        ClaimsPrincipal principal,
        string claimType,
        string displayName)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"The authenticated token does not contain a valid {displayName} claim.");
    }
}
