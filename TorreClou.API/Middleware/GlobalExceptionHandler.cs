using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.Exceptions;

namespace TorreClou.API.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, code) = exception switch
        {
            BaseAppException appEx => (appEx.HttpStatusCode, GetTitleForStatusCode(appEx.HttpStatusCode), appEx.Code),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "INVALID_ARGUMENT"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "NOT_FOUND"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error", "INTERNAL_ERROR")
        };

        // Log based on severity
        if (statusCode >= 500)
        {
            logger.LogError(exception, "Server error occurred: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning(exception, "Client error occurred: {Code} - {Message}", code, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Extensions =
            {
                ["code"] = code
            }
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => statusCode >= 500 ? "Server Error" : "Client Error"
    };
}
