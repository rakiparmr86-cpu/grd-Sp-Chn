using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string UserName,
    string Password,
    string AccessProfile,
    Guid OrganizationUnitId)
    : IRequest<Result<CreateUserResponse>>;

public sealed record CreateUserResponse(
    Guid UserId,
    string UserName,
    string Role,
    string AccessProfile,
    Guid OrganizationUnitId,
    IReadOnlyCollection<string> Permissions,
    bool IsActive);
