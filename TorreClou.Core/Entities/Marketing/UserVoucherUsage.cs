namespace TorreClou.Core.Entities.Marketing
{
    public class UserVoucherUsage : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int VoucherId { get; set; }
        public Voucher Voucher { get; set; } = null!;

        public int? JobId { get; set; }
    }
}