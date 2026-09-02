using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.Users.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUserAccountRepository repository,
    IAccessProfileRepository accessProfileRepository,
    IPasswordHasher passwordHasher)
    : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    public async Task<Result<CreateUserResponse>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (userName.Length is < 3 or > 160 || !userName.Contains('@'))
        {
            return Validation(
                "Identity.InvalidUserName",
                "Enter a valid user name in email format.",
                nameof(request.UserName));
        }

        if (password.Length < 7)
        {
            return Validation(
                "Identity.WeakPassword",
                "The initial password must contain at least 7 characters.",
                nameof(request.Password));
        }

        if (request.OrganizationUnitId == Guid.Empty)
        {
            return Validation(
                "Identity.OrganizationUnitRequired",
                "Select an organization unit for the user.",
                nameof(request.OrganizationUnitId));
        }

        var profile = await accessProfileRepository.GetByCodeAsync(
            request.AccessProfile,
            cancellationToken);
        if (profile is null || !profile.CanBeAssignedByHr)
        {
            return Validation(
                "Identity.InvalidAccessProfile",
                "Select one of the supported operational access profiles.",
                nameof(request.AccessProfile));
        }

        var user = new UserAccount(
            Guid.NewGuid(),
            userName,
            CreateNotificationEmail(userName),
            passwordHasher.Hash(password),
            profile.Role,
            profile.Code,
            request.OrganizationUnitId,
            isActive: true,
            profile.Permissions);

        var added = await repository.TryAddAsync(user, cancellationToken);
        if (!added)
        {
            return Result<CreateUserResponse>.Failure(Error.Conflict(
                "Identity.UserNameAlreadyExists",
                "A user with this user name already exists."));
        }

        return Result<CreateUserResponse>.Success(new CreateUserResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.Role,
            profile.Code,
            user.OrganizationUnitId,
            user.Permissions,
            user.IsActive));
    }

    private static string CreateNotificationEmail(string userName) =>
        $"{userName[..userName.IndexOf('@')].ToLowerInvariant()}@yopmail.com";

    private static Result<CreateUserResponse> Validation(
        string code,
        string description,
        string target) =>
        Result<CreateUserResponse>.Failure(
            Error.Validation(code, description, target));
}
