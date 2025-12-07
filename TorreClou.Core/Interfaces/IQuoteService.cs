using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.Shared;

namespace TorreClou.Application.Services
{
    public interface IQuoteService
    {
        Task<Result<QuoteResponseDto>> GenerateQuoteAsync(QuoteRequestDto request, int userId, Stream torrentFile); 
    }
}