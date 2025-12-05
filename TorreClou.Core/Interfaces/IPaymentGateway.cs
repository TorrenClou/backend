using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.DTOs.Payments;
using TorreClou.Core.Entities;
using TorreClou.Core.Entities.Financals;

namespace TorreClou.Core.Interfaces
{
    public interface IPaymentGateway
    {
        Task<DepositData> InitiatePaymentAsync(Deposit deposit, User user);
        Task<InvoiceData?> VerifyInvoiceAsync(string invoiceId, string coin);
        Task<Dictionary<string, decimal>> GetMinimumAmountsForStablecoinsAsync();
    }
}