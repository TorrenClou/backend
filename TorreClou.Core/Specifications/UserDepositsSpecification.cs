using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Specifications;

namespace TorreClou.Application.Services
{
    // Specification classes for deposit queries
    public class UserDepositsSpecification : BaseSpecification<Deposit>
    {
        public UserDepositsSpecification(int userId, int pageNumber, int pageSize)
            : base(x => x.UserId == userId)
        {
            AddOrderByDescending(x => x.CreatedAt);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}
