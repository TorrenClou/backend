namespace TorreClou.Core.Exceptions;

/// <summary>
/// Exception thrown when there's an issue with the payment provider (Coinremitter, etc.)
/// </summary>
public class PaymentProviderException : BaseAppException
{
    public PaymentProviderException(string message, string code = "PAYMENT_PROVIDER_ERROR", int httpStatusCode = 502)
        : base(message, code, httpStatusCode)
    {
    }

    public PaymentProviderException(string message, Exception innerException, string code = "PAYMENT_PROVIDER_ERROR", int httpStatusCode = 502)
        : base(message, code, httpStatusCode, innerException)
    {
    }

    // Factory methods for common payment provider errors
    public static PaymentProviderException ConnectionFailed(string providerName, Exception? inner = null)
        => inner != null
            ? new PaymentProviderException($"Failed to connect to {providerName}", inner, "PAYMENT_PROVIDER_CONNECTION_FAILED", 503)
            : new PaymentProviderException($"Failed to connect to {providerName}", "PAYMENT_PROVIDER_CONNECTION_FAILED", 503);

    public static PaymentProviderException InvalidResponse(string providerName)
        => new($"Invalid response from {providerName}", "PAYMENT_PROVIDER_INVALID_RESPONSE", 502);

    public static PaymentProviderException ApiError(string providerName, string errorMessage, int? providerStatusCode = null)
        => new($"{providerName} API error: {errorMessage}", "PAYMENT_PROVIDER_API_ERROR", 502);

    public static PaymentProviderException InvoiceCreationFailed(string reason)
        => new($"Failed to create invoice: {reason}", "INVOICE_CREATION_FAILED", 502);

    public static PaymentProviderException InvoiceVerificationFailed(string invoiceId)
        => new($"Failed to verify invoice: {invoiceId}", "INVOICE_VERIFICATION_FAILED", 502);
}

