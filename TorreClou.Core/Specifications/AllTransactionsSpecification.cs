using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Specifications;

namespace TorreClou.Application.Services
{
    public class AllTransactionsSpecification : BaseSpecification<WalletTransaction>
    {
        public AllTransactionsSpecification(int pageNumber, int pageSize)
        {
            AddOrderByDescending(x => x.CreatedAt);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}