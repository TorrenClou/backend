using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.DTOs.Auth;
using TorreClou.Core.Interfaces;

namespace TorreClou.API.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService) : BaseApiController
{
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto model)
    {
        var result = await authService.LoginWithGoogleAsync(model);
        return HandleResult(result);
    }
}
