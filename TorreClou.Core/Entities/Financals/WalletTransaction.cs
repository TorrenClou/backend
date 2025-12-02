using TorreClou.Core.Enums;

namespace TorreClou.Core.Entities.Financals
{
    public class WalletTransaction : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public decimal Amount { get; set; }

        public TransactionType Type { get; set; }

        public string? ReferenceId { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}