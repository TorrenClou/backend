using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.DTOs.Auth;
using TorreClou.Core.Interfaces;

namespace TorreClou.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : BaseApiController
{
    /// <summary>
    /// Login with email and password. Credentials are set by the first-run setup
    /// wizard and stored hashed on the user row.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequestDto request)
    {
        var response = await authService.LoginAsync(request.Email, request.Password);
        return Ok(response);
    }
}
