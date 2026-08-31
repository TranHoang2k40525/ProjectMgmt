#pragma warning disable CA1000 // Static factories are the intended Result<T> API.
#pragma warning disable CA1716 // Error is the contract name frozen for the project.

namespace ProjectMgmt.BuildingBlocks.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Failure,
    Unexpected
}

public class Error
{
    public Error(
        string code,
        string message,
        ErrorType type = ErrorType.Failure,
        IReadOnlyDictionary<string, string[]>? details = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Details = details;
    }

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, string[]>? Details { get; }

    public static readonly Error None = new Error(string.Empty, string.Empty);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, details);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}

public class ValidationError : Error
{
    public ValidationError(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> validationDetails)
        : base(code, message, ErrorType.Validation, validationDetails)
    {
        ValidationDetails = validationDetails;
    }

    public IReadOnlyDictionary<string, string[]> ValidationDetails { get; }
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && !ReferenceEquals(error, Error.None))
        {
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
        }

        if (!isSuccess && ReferenceEquals(error, Error.None))
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(true, Error.None)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(Error error) => new(error);
}
