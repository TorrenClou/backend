using Microsoft.Extensions.Options;
using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.DTOs.Payments;
using TorreClou.Core.Entities;
using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Exceptions;
using TorreClou.Core.Interfaces;
using TorreClou.Infrastructure.Helpers;
using TorreClou.Infrastructure.Settings;

namespace TorreClou.Infrastructure.Services;

public class CoinremitterService : IPaymentGateway
{
    private readonly CoinremitterSettings _settings;
    private readonly HttpClientHelper _http;

    public CoinremitterService(IOptions<CoinremitterSettings> settings, HttpClient httpClient)
    {
        _settings = settings.Value;
        _http = new HttpClientHelper(httpClient);

        // Set default headers for authentication
        _http.SetHeader("x-api-key", _settings.ApiKey);
        _http.SetHeader("x-api-password", _settings.ApiPassword);
    }

    public async Task<DepositData> InitiatePaymentAsync(Deposit deposit, User user)
    {
        var request = new CreateInvoiceRequest
        {
            Amount = deposit.Amount.ToString("F2"),
            NotifyUrl = _settings.WebhookUrl,
            Description = $"Deposit for user {user.Id}",
            CustomData1 = deposit.Id.ToString(),
            CustomData2 = user.Id.ToString()
        };

        var url = $"{_settings.ApiUrl}/invoice/create";
        var response = await _http.PostAsync<CoinremitterResponse<CreateInvoiceData>>(url, request);

        if (!response.Success)
        {
            throw PaymentProviderException.ApiError("Coinremitter", response.Error ?? "Unknown error");
        }

        if (response.Data == null || !response.Data.Success || response.Data.Data == null)
        {
            throw PaymentProviderException.InvoiceCreationFailed("Invalid response from Coinremitter");
        }

        var invoiceData = response.Data.Data;
        return new DepositData
        {
            InvoiceId = invoiceData.InvoiceId ?? throw PaymentProviderException.InvoiceCreationFailed("Missing invoice ID"),
            PaymentUrl = invoiceData.Url ?? throw PaymentProviderException.InvoiceCreationFailed("Missing payment URL")
        };
    }

    public async Task<InvoiceData?> VerifyInvoiceAsync(string invoiceId, string coin)
    {
        var request = new GetInvoiceRequest { InvoiceId = invoiceId };
        var url = $"{_settings.ApiUrl}/invoice/get";

        var response = await _http.PostAsync<CoinremitterResponse<GetInvoiceData>>(url, request);

        if (!response.Success || response.Data == null || !response.Data.Success || response.Data.Data == null)
        {
            return null;
        }

        var data = response.Data.Data;
        return new InvoiceData
        {
            id = data.Id,
            invoice_id = data.InvoiceId,
            url = data.Url,
            status = data.Status,
            status_code = data.StatusCode,
            coin = data.CoinSymbol ?? coin,
            total_amount = data.TotalAmount
        };
    }

    public Task<Dictionary<string, decimal>> GetMinimumAmountsForStablecoinsAsync()
    {
        var minAmounts = new Dictionary<string, decimal>
        {
            { _settings.CoinSymbol, _settings.MinimumDepositAmount }
        };

        return Task.FromResult(minAmounts);
    }
}
