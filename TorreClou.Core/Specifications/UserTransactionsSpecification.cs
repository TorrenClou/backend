using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Enums;

namespace TorreClou.Core.Specifications
{
    // Specification classes for wallet queries
    public class UserTransactionsSpecification : BaseSpecification<WalletTransaction>
    {
        public UserTransactionsSpecification(int userId, int pageNumber, int pageSize, TransactionType? transactionType = null)
            : base(x => x.UserId == userId && (transactionType == null || x.Type == transactionType))
        {
            AddOrderByDescending(x => x.CreatedAt);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}