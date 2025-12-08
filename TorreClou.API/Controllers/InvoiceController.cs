using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.Interfaces;

namespace TorreClou.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController(IQuotePricingService pricingService) : BaseApiController
    {
        [HttpPost("pay")]
        public async Task<IActionResult> PayInvoice([FromQuery] int invoiceId)
        {
           var paymentResult = await pricingService.Pay(invoiceId);
            return HandleResult(paymentResult);
        }
    }
}
