using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorreClou.Core.DTOs.Storage;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;
using TorreClou.Core.Entities.Jobs;

namespace TorreClou.API.Controllers
{
    [Route("api/storage")]
    [Authorize]
    public class StorageProfilesController : BaseApiController
    {
        private readonly IGoogleDriveAuthService _googleDriveAuthService;
        private readonly IUnitOfWork _unitOfWork;

        public StorageProfilesController(
            IGoogleDriveAuthService googleDriveAuthService,
            IUnitOfWork unitOfWork)
        {
            _googleDriveAuthService = googleDriveAuthService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("google-drive/connect")]
        public async Task<IActionResult> ConnectGoogleDrive()
        {
            var result = await _googleDriveAuthService.GetAuthorizationUrlAsync(UserId);
            if (result.IsFailure)
            {
                return HandleResult(result);
            }

            return Ok(new GoogleDriveAuthResponse
            {
                AuthorizationUrl = result.Value
            });
        }

        [HttpGet("google-drive/callback")]
        public async Task<IActionResult> GoogleDriveCallback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return BadRequest(new { error = "Missing code or state parameter" });
            }

            var result = await _googleDriveAuthService.HandleOAuthCallbackAsync(code, state, UserId);
            return HandleResult(result);
        }

        [HttpGet("profiles")]
        public async Task<IActionResult> GetStorageProfiles()
        {
            var spec = new BaseSpecification<UserStorageProfile>(
                p => p.UserId == UserId && p.IsActive
            );

            var profiles = await _unitOfWork.Repository<UserStorageProfile>().ListAsync(spec);

            var dtos = profiles
                .OrderBy(p => p.IsDefault ? 0 : 1)
                .ThenBy(p => p.CreatedAt)
                .Select(p => new StorageProfileDto
                {
                    Id = p.Id,
                    ProfileName = p.ProfileName,
                    ProviderType = p.ProviderType.ToString(),
                    IsDefault = p.IsDefault,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).ToList();

            return Ok(dtos);
        }

        [HttpPost("profiles/{id}/set-default")]
        public async Task<IActionResult> SetDefaultProfile(int id)
        {
            var spec = new BaseSpecification<UserStorageProfile>(
                p => p.Id == id && p.UserId == UserId && p.IsActive
            );
            var profile = await _unitOfWork.Repository<UserStorageProfile>().GetEntityWithSpec(spec);

            if (profile == null)
            {
                return HandleResult(Result.Failure("PROFILE_NOT_FOUND", "Storage profile not found"));
            }

            // Unset all other default profiles for this user
            var allProfilesSpec = new BaseSpecification<UserStorageProfile>(
                p => p.UserId == UserId && p.IsActive
            );
            var allProfiles = await _unitOfWork.Repository<UserStorageProfile>().ListAsync(allProfilesSpec);

            foreach (var p in allProfiles)
            {
                p.IsDefault = false;
            }

            // Set this profile as default
            profile.IsDefault = true;
            await _unitOfWork.Complete();

            return HandleResult(Result.Success());
        }
    }
}

