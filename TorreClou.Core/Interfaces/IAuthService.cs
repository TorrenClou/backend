using TorreClou.Core.DTOs.Auth;
using TorreClou.Core.Shared;

namespace TorreClou.Core.Interfaces
{
    // خدمة البيزنس لوجيك (تسجيل الدخول)
    public interface IAuthService
    {
        Task<Result<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginDto model);
    }
}
