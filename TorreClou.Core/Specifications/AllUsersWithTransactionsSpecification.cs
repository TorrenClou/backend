using TorreClou.Core.Entities;
using TorreClou.Core.Specifications;

namespace TorreClou.Application.Services
{
    public class AllUsersWithTransactionsSpecification : BaseSpecification<User>
    {
        public AllUsersWithTransactionsSpecification(int pageNumber, int pageSize)
        {
            AddInclude(u => u.WalletTransactions);
            AddOrderByDescending(u => u.CreatedAt);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}