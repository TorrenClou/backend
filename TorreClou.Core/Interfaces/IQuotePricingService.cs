using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.Models.Pricing;
using TorreClou.Core.Shared;

namespace TorreClou.Core.Interfaces
{
    public interface IQuotePricingService
    {
        Task<Result<InvoicePaymentResult>> Pay(int InvoiceId);
        Task<Result<QuotePricingResult>> GenerateOrReuseInvoiceAsync(QuotePricingRequest request);
    }
}
