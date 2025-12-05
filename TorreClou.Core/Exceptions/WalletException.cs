namespace TorreClou.Core.Exceptions;

/// <summary>
/// Exception thrown when there's an issue with wallet operations
/// </summary>
public class WalletException : BaseAppException
{
    public WalletException(string message, string code = "WALLET_ERROR", int httpStatusCode = 400)
        : base(message, code, httpStatusCode)
    {
    }

    public WalletException(string message, Exception innerException, string code = "WALLET_ERROR", int httpStatusCode = 400)
        : base(message, code, httpStatusCode, innerException)
    {
    }

    // Factory methods for common wallet errors
    public static WalletException InsufficientBalance(decimal requested, decimal available)
        => new($"Insufficient balance. Requested: {requested}, Available: {available}", "WALLET_INSUFFICIENT_BALANCE", 400);

    public static WalletException TransactionNotFound(int transactionId)
        => new($"Transaction with ID {transactionId} not found", "WALLET_TRANSACTION_NOT_FOUND", 404);

    public static WalletException InvalidTransactionAmount(decimal amount)
        => new($"Invalid transaction amount: {amount}", "WALLET_INVALID_AMOUNT", 400);

    public static WalletException WalletNotFound(int userId)
        => new($"Wallet not found for user {userId}", "WALLET_NOT_FOUND", 404);

    public static WalletException TransactionFailed(string reason)
        => new($"Transaction failed: {reason}", "WALLET_TRANSACTION_FAILED", 500);
}

