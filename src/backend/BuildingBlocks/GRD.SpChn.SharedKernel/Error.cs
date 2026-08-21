namespace GRD.SpChn.SharedKernel;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict
}

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type = ErrorType.Failure,
    string? Target = null)
{
    public static Error Validation(string code, string description, string? target = null) =>
        new(code, description, ErrorType.Validation, target);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);
}
