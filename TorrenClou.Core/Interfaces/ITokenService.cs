using Google.Apis.Auth;
using TorrenClou.Core.Entities;

namespace TorrenClou.Core.Interfaces
{
    // خدمة التعامل مع التوكنات (إنشاء وتعميد)
    public interface ITokenService
    {
        // إنشاء توكن للسيستم بتاعنا
        string CreateToken(User user);

        // التحقق من توكن جوجل واستخراج البيانات منه
        Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(string idToken);
    }
}
