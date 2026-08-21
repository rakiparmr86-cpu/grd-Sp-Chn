using FluentValidation;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IValidationResult<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var errors = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage,
                failure.PropertyName))
            .ToArray();

        return errors.Length == 0
            ? await next()
            : TResponse.ValidationFailure(errors);
    }
}
