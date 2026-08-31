using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.DTOs.Settings;
using TorreClou.Core.Interfaces;

namespace TorreClou.API.Controllers
{
    [Route("api/settings")]
    [Authorize]
    public class SettingsController(IUserSettingsService userSettingsService) : BaseApiController
    {
        /// <summary>Current preferences, created with defaults on first read.</summary>
        [HttpGet]
        public async Task<IActionResult> GetSettings()
            => Ok(await userSettingsService.GetSettingsAsync(UserId));

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequestDto request)
            => Ok(await userSettingsService.UpdateSettingsAsync(UserId, request));
    }
}
