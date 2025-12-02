using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.DTOs.Auth;
using TorreClou.Core.Interfaces;

namespace TorreClou.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("google-login")]
        public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] GoogleLoginDto model)
        {
            var result = await authService.LoginWithGoogleAsync(model);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return BadRequest(result.Error);
        }
    }
}