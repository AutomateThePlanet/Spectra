namespace Spectra.Core.Models;

/// <summary>
/// Base result type for operations that can fail.
/// Uses discriminated union pattern for error handling without exceptions.
/// </summary>
public abstract record ParseResult<T>
{
    /// <summary>
    /// Returns true if the operation succeeded.
    /// </summary>
    public bool IsSuccess => this is ParseSuccess<T>;

    /// <summary>
    /// Returns true if the operation failed.
    /// </summary>
    public bool IsFailure => this is ParseFailure<T>;

    /// <summary>
    /// Gets the value if successful, or throws if failed.
    /// </summary>
    public T Value => this is ParseSuccess<T> success
        ? success.Result
        : throw new InvalidOperationException("Cannot get value from failed result");

    /// <summary>
    /// Gets the errors if failed, or empty list if successful.
    /// </summary>
    public IReadOnlyList<ParseError> Errors => this is ParseFailure<T> failure
        ? failure.ParseErrors
        : [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ParseResult<T> Success(T value) => new ParseSuccess<T>(value);

    /// <summary>
    /// Creates a failed result with errors.
    /// </summary>
    public static ParseResult<T> Failure(IReadOnlyList<ParseError> errors) => new ParseFailure<T>(errors);

    /// <summary>
    /// Creates a failed result with a single error.
    /// </summary>
    public static ParseResult<T> Failure(ParseError error) => new ParseFailure<T>([error]);

    /// <summary>
    /// Maps the value to a new type if successful.
    /// </summary>
    public ParseResult<TNew> Map<TNew>(Func<T, TNew> mapper) => this switch
    {
        ParseSuccess<T> success => ParseResult<TNew>.Success(mapper(success.Result)),
        ParseFailure<T> failure => ParseResult<TNew>.Failure(failure.ParseErrors),
        _ => throw new InvalidOperationException("Unknown result type")
    };
}

/// <summary>
/// Represents a successful parse result.
/// </summary>
public sealed record ParseSuccess<T>(T Result) : ParseResult<T>;

/// <summary>
/// Represents a failed parse result with errors.
/// </summary>
public sealed record ParseFailure<T>(IReadOnlyList<ParseError> ParseErrors) : ParseResult<T>;
