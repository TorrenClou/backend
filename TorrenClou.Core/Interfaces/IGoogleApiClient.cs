using TorrenClou.Core.DTOs.Storage.Google_Drive;

namespace TorrenClou.Core.Interfaces
{
    public interface IGoogleApiClient
    {
        Task<TokenResponse> ExchangeCodeForTokensAsync(string code, string clientId, string clientSecret, string redirectUri);
        Task<UserInfoResponse> GetUserInfoAsync(string accessToken);
    }
}
