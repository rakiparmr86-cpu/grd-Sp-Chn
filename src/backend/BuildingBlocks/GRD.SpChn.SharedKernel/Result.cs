namespace GRD.SpChn.SharedKernel;

public interface IValidationResult<TSelf>
    where TSelf : IValidationResult<TSelf>
{
    static abstract TSelf ValidationFailure(IReadOnlyCollection<Error> errors);
}

public sealed class Result<T> : IValidationResult<Result<T>>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        Errors = [];
    }

    private Result(IReadOnlyCollection<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));
        }

        IsSuccess = false;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyCollection<Error> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public Error FirstError => IsFailure
        ? Errors.First()
        : throw new InvalidOperationException("A successful result has no error.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new([error]);

    public static Result<T> Failure(IReadOnlyCollection<Error> errors) => new(errors);

    public static Result<T> ValidationFailure(IReadOnlyCollection<Error> errors) =>
        Failure(errors);
}
