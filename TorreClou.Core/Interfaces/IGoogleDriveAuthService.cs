using TorreClou.Core.Shared;

namespace TorreClou.Core.Interfaces
{
    public interface IGoogleDriveAuthService
    {
        Task<Result<string>> GetAuthorizationUrlAsync(int userId);
        Task<Result<int>> HandleOAuthCallbackAsync(string code, string state, int userId);
    }
}

