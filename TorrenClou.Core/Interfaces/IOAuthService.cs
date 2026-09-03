using TorrenClou.Core.Entities;

namespace TorrenClou.Core.Interfaces
{
    public interface IOAuthService
    {
        Task<UserOAuthCredential?> GetUserOAuthCredentialByClientId(string clientId, int userId);
        Task<UserOAuthCredential?> GetUserOAuthCredentialById(int credId, int userId);
        Task Update(UserOAuthCredential credential);
        Task<UserOAuthCredential> Add(UserOAuthCredential credential);
        Task<IReadOnlyCollection<UserOAuthCredential>> GetAll(int userId);
    }
}