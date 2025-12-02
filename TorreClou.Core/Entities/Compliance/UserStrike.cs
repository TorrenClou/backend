using TorreClou.Core.Enums;

namespace TorreClou.Core.Entities.Compliance
{
    public class UserStrike : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ViolationType ViolationType { get; set; }
        public string Reason { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
