using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.Authentication.Login;

internal sealed class LoginCommandHandler(
    IUserAccountRepository repository,
    IPasswordVerifier passwordVerifier,
    IAccessTokenIssuer tokenIssuer)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidCredentials();
        }

        var user = await repository.GetByUserNameAsync(
            request.UserName.Trim(),
            cancellationToken);
        if (user is null || !user.IsActive ||
            !passwordVerifier.Verify(request.Password, user.PasswordHash))
        {
            return InvalidCredentials();
        }

        var token = tokenIssuer.Issue(user);
        return Result<LoginResponse>.Success(new LoginResponse(
            token.Value,
            token.ExpiresOnUtc,
            user.Id,
            user.UserName,
            user.Email,
            user.Role,
            user.AccessProfileCode,
            user.OrganizationUnitId,
            user.Permissions));
    }

    private static Result<LoginResponse> InvalidCredentials() =>
        Result<LoginResponse>.Failure(new Error(
            "Identity.InvalidCredentials",
            "The user name or password is invalid."));
}
