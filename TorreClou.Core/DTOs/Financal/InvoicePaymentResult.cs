using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorreClou.Core.DTOs.Financal
{
    public class InvoicePaymentResult
    {
        public int WalletTransaction { get; set; }
        public int InvoiceId { get; set; }
        public decimal TotalAmountInNCurruncy { get; set; }
    }
}
