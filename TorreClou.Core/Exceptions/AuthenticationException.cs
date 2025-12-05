namespace TorreClou.Core.Exceptions;

/// <summary>
/// Exception thrown when there's an authentication or authorization issue
/// </summary>
public class AuthenticationException : BaseAppException
{
    public AuthenticationException(string message, string code = "AUTH_ERROR", int httpStatusCode = 401)
        : base(message, code, httpStatusCode)
    {
    }

    public AuthenticationException(string message, Exception innerException, string code = "AUTH_ERROR", int httpStatusCode = 401)
        : base(message, code, httpStatusCode, innerException)
    {
    }

    // Factory methods for common auth errors
    public static AuthenticationException InvalidToken()
        => new("Invalid or expired token", "AUTH_INVALID_TOKEN", 401);

    public static AuthenticationException UserNotFound()
        => new("User not found", "AUTH_USER_NOT_FOUND", 404);

    public static AuthenticationException InvalidCredentials()
        => new("Invalid credentials", "AUTH_INVALID_CREDENTIALS", 401);

    public static AuthenticationException AccountDisabled()
        => new("Account has been disabled", "AUTH_ACCOUNT_DISABLED", 403);

    public static AuthenticationException GoogleAuthFailed(string reason)
        => new($"Google authentication failed: {reason}", "AUTH_GOOGLE_FAILED", 401);

    public static AuthenticationException Forbidden(string resource)
        => new($"Access denied to {resource}", "AUTH_FORBIDDEN", 403);
}

