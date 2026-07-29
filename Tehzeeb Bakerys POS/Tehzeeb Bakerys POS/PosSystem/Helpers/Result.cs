namespace PosSystem.Helpers;

/// <summary>
/// Generics: a reusable, type-safe wrapper for "it worked / it didn't, and here's why".
/// Used for catalog writes, JSON I/O and payment authorisation.
/// </summary>
/// <typeparam name="T">Type of the value carried on success.</typeparam>
public sealed class Result<T>
{
    // Private constructor: callers must go through Ok() / Fail().
    private Result(bool isSuccess, T? value, string message)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public string Message { get; }

    public static Result<T> Ok(T value, string message = "OK")
    {
        return new Result<T>(true, value, message);
    }

    public static Result<T> Fail(string message)
    {
        return new Result<T>(false, default, message);
    }

    public T ValueOr(T fallback)
    {
        return IsSuccess && Value is not null ? Value : fallback;
    }

    public override string ToString()
    {
        return IsSuccess ? "[OK]   " + Message : "[FAIL] " + Message;
    }
}
