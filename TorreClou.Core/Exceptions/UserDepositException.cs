namespace TorreClou.Core.Exceptions;

/// <summary>
/// Exception thrown when there's an issue with user deposit operations
/// </summary>
public class UserDepositException : BaseAppException
{
    public UserDepositException(string message, string code = "DEPOSIT_ERROR", int httpStatusCode = 400)
        : base(message, code, httpStatusCode)
    {
    }

    public UserDepositException(string message, Exception innerException, string code = "DEPOSIT_ERROR", int httpStatusCode = 400)
        : base(message, code, httpStatusCode, innerException)
    {
    }

    // Factory methods for common deposit errors
    public static UserDepositException NotFound(int depositId)
        => new($"Deposit with ID {depositId} not found", "DEPOSIT_NOT_FOUND", 404);

    public static UserDepositException NotFoundByInvoice(string invoiceId)
        => new($"Deposit with invoice ID {invoiceId} not found", "DEPOSIT_NOT_FOUND", 404);

    public static UserDepositException InvalidAmount(decimal amount, decimal minimumAmount)
        => new($"Deposit amount {amount} is below minimum required amount of {minimumAmount}", "DEPOSIT_INVALID_AMOUNT", 400);

    public static UserDepositException InvalidCurrency(string currency)
        => new($"Currency '{currency}' is not supported for deposits", "DEPOSIT_INVALID_CURRENCY", 400);

    public static UserDepositException AlreadyProcessed(int depositId)
        => new($"Deposit {depositId} has already been processed", "DEPOSIT_ALREADY_PROCESSED", 409);

    public static UserDepositException ProcessingFailed(int depositId, string reason)
        => new($"Failed to process deposit {depositId}: {reason}", "DEPOSIT_PROCESSING_FAILED", 500);

    public static UserDepositException Unauthorized(int depositId)
        => new($"You are not authorized to access deposit {depositId}", "DEPOSIT_UNAUTHORIZED", 403);
}

