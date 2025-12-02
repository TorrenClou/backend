using TorreClou.Core.Enums;
using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Entities.Torrents;

namespace TorreClou.Core.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public string OAuthProvider { get; set; } = "Google";
        public string OAuthSubjectId { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsPhoneNumberVerified { get; set; } = false;
        public RegionCode Region { get; set; } = RegionCode.Global;
        public UserRole Role { get; set; } = UserRole.User;

        public ICollection<UserStorageProfile> StorageProfiles { get; set; } = [];

        public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];
        public ICollection<UserJob> Jobs { get; set; } = [];
        public ICollection<Compliance.UserStrike> Strikes { get; set; } = [];

        public decimal GetCurrentBalance() => WalletTransactions?.Sum(t => t.Amount) ?? 0;
    }
}