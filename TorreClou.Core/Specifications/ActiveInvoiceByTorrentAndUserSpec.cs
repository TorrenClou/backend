using TorreClou.Core.Entities.Financals;

namespace TorreClou.Core.Specifications
{
    public class ActiveInvoiceByTorrentAndUserSpec : BaseSpecification<Invoice>
    {
        public ActiveInvoiceByTorrentAndUserSpec(string infoHash, int userId)
            : base(i =>
                i.TorrentFile.InfoHash == infoHash &&
                i.UserId == userId &&
                i.CancelledAt == null &&  // Not cancelled
                i.RefundedAt == null      // Not refunded
            )
        {
            AddOrderByDescending(i => i.CreatedAt);
            AddInclude(i => i.TorrentFile);
        }
    }
}