namespace TorreClou.Core.Exceptions;

/// <summary>
/// Base exception for all application-specific exceptions.
/// Provides a standardized way to include error codes and HTTP status codes.
/// </summary>
public abstract class BaseAppException : Exception
{
    /// <summary>
    /// A unique error code identifying the type of error (e.g., "PAYMENT_FAILED", "DEPOSIT_NOT_FOUND")
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// The HTTP status code to return to the client (default: 400 Bad Request)
    /// </summary>
    public int HttpStatusCode { get; }

    protected BaseAppException(string message, string code, int httpStatusCode = 400)
        : base(message)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }

    protected BaseAppException(string message, string code, int httpStatusCode, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }
}

