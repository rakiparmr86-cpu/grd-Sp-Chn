namespace GRD.SpChn.Identity.Application.Authentication.Login;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresOnUtc,
    Guid UserId,
    string UserName,
    string Email,
    string Role,
    string AccessProfile,
    Guid OrganizationUnitId,
    IReadOnlyCollection<string> Permissions);
