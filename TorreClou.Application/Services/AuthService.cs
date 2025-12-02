using TorreClou.Core.DTOs.Auth;
using TorreClou.Core.Entities;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Specifications;
using TorreClou.Core.Shared;

namespace TorreClou.Application.Services
{
    public class AuthService(IUnitOfWork unitOfWork, ITokenService tokenService) : IAuthService
    {
        public async Task<Result<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginDto model)
        {
            // 1. Verify Google Token (Infrastructure Layer)
            var googlePayload = await tokenService.VerifyGoogleTokenAsync(model.IdToken);

            // 2. Check if user exists using Specification
            var spec = new BaseSpecification<User>(u => u.Email == googlePayload.Email);
            // بنعمل Include عشان نجيب المحفظة معانا وإحنا بنبحث
            spec.AddInclude(u => u.WalletTransactions);

            var existingUser = await unitOfWork.Repository<User>().GetEntityWithSpec(spec);

            User user;

            if (existingUser == null)
            {
                // === CASE A: NEW USER (REGISTER) ===
                user = new User
                {
                    Email = googlePayload.Email,
                    FullName = googlePayload.Name,
                    OAuthProvider = "Google",
                    OAuthSubjectId = googlePayload.Subject, // Google Unique ID
                    IsPhoneNumberVerified = googlePayload.EmailVerified, // مبدئيا نعتبره Verified لو جوجل قال كده
                    Role = UserRole.User
                };

                // Add Default Storage Profile (مهم جدا للـ Coupling اللي لغيناه)
                user.StorageProfiles.Add(new UserStorageProfile
                {
                    ProfileName = "My Google Drive",
                    ProviderType = StorageProviderType.GoogleDrive,
                    CredentialsJson = "{}", // لسه فاضي لحد ما ناخد صلاحية الـ Drive
                    IsDefault = true,
                    IsActive = true
                });

                unitOfWork.Repository<User>().Add(user);
                await unitOfWork.Complete(); // Save to generate ID
            }
            else
            {
                user = existingUser;

            }

            // 3. Generate Our JWT
            var token = tokenService.CreateToken(user);

            // 4. Return DTO
            return Result.Success(new AuthResponseDto
            {
                AccessToken = token,
                Email = user.Email,
                FullName = user.FullName,
                CurrentBalance = user.GetCurrentBalance(),
                Role = user.Role.ToString()
            });
        }
    }
}
