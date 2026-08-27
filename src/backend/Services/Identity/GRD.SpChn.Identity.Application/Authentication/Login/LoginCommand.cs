using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string UserName, string Password)
    : IRequest<Result<LoginResponse>>;
