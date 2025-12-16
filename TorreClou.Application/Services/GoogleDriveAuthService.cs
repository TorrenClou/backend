using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;
using TorreClou.Core.Options;

namespace TorreClou.Application.Services
{
    public class GoogleDriveAuthService : IGoogleDriveAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GoogleDriveSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleDriveAuthService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private const string RedisKeyPrefix = "oauth:state:";

        public GoogleDriveAuthService(
            IUnitOfWork unitOfWork,
            IOptions<GoogleDriveSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleDriveAuthService> logger,
            IConnectionMultiplexer redis)
        {
            _unitOfWork = unitOfWork;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _redis = redis;
        }

        public async Task<Result<string>> GetAuthorizationUrlAsync(int userId)
        {
            try
            {
                // Generate state parameter (userId + nonce for security)
                var nonce = Guid.NewGuid().ToString("N");
                var state = $"{userId}:{nonce}";
                var stateHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(state)));

                // Store state in Redis with expiration (5 minutes)
                var oauthState = new OAuthState
                {
                    UserId = userId,
                    Nonce = nonce,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                };

                var redisKey = $"{RedisKeyPrefix}{stateHash}";
                var jsonValue = JsonSerializer.Serialize(oauthState);
                var db = _redis.GetDatabase();

                try
                {
                    await db.StringSetAsync(redisKey, jsonValue, TimeSpan.FromMinutes(5));
                }
                catch (Exception redisEx)
                {
                    _logger.LogError(redisEx, "Failed to store OAuth state in Redis for user {UserId}", userId);
                    return Result<string>.Failure("REDIS_ERROR", "Failed to store OAuth state");
                }

                // Build OAuth URL
                var scopes = string.Join(" ", _settings.Scopes);
                var redirectUri = HttpUtility.UrlEncode(_settings.RedirectUri);
                var encodedState = HttpUtility.UrlEncode(stateHash);

                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={_settings.ClientId}&" +
                    $"redirect_uri={redirectUri}&" +
                    $"response_type=code&" +
                    $"scope={HttpUtility.UrlEncode(scopes)}&" +
                    $"access_type=offline&" +
                    $"prompt=consent&" +
                    $"state={encodedState}";

                return Result.Success(authUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating authorization URL for user {UserId}", userId);
                return Result<string>.Failure("AUTH_URL_ERROR", "Failed to generate authorization URL");
            }
        }

        public async Task<Result<int>> HandleOAuthCallbackAsync(string code, string state)
        {
            try
            {
                // Retrieve and delete state from Redis (atomic operation ensures single-use)
                var redisKey = $"{RedisKeyPrefix}{state}";
                var db = _redis.GetDatabase();
                
                OAuthState? storedState;
                try
                {
                    // Atomic get-and-delete operation
                    var stateJson = await db.StringGetDeleteAsync(redisKey);
                    
                    if (!stateJson.HasValue)
                    {
                        return Result<int>.Failure("INVALID_STATE", "Invalid or expired OAuth state");
                    }

                    storedState = JsonSerializer.Deserialize<OAuthState>(stateJson!);
                    
                    if (storedState == null)
                    {
                        return Result<int>.Failure("INVALID_STATE", "Invalid OAuth state format");
                    }

                    // Validate expiration
                    if (storedState.ExpiresAt < DateTime.UtcNow)
                    {
                        return Result<int>.Failure("INVALID_STATE", "Expired OAuth state");
                    }
                }
                catch (Exception redisEx)
                {
                    _logger.LogError(redisEx, "Failed to retrieve OAuth state from Redis");
                    return Result<int>.Failure("REDIS_ERROR", "Failed to validate OAuth state");
                }

                // Extract userId from validated state
                var userId = storedState.UserId;

                // Exchange authorization code for tokens
                var tokenResponse = await ExchangeCodeForTokensAsync(code);
                if (tokenResponse == null)
                {
                    return Result<int>.Failure("TOKEN_EXCHANGE_FAILED", "Failed to exchange authorization code for tokens");
                }

                // Find or create storage profile
                var profileSpec = new BaseSpecification<UserStorageProfile>(
                    p => p.UserId == userId && p.ProviderType == StorageProviderType.GoogleDrive && p.IsActive
                );
                var existingProfile = await _unitOfWork.Repository<UserStorageProfile>().GetEntityWithSpec(profileSpec);

                UserStorageProfile profile;
                if (existingProfile != null)
                {
                    profile = existingProfile;
                }
                else
                {
                    profile = new UserStorageProfile
                    {
                        UserId = userId,
                        ProfileName = "My Google Drive",
                        ProviderType = StorageProviderType.GoogleDrive,
                        IsDefault = false,
                        IsActive = true
                    };
                    _unitOfWork.Repository<UserStorageProfile>().Add(profile);
                }

                // Store tokens in CredentialsJson
                var credentials = new
                {
                    access_token = tokenResponse.AccessToken,
                    refresh_token = tokenResponse.RefreshToken,
                    expires_at = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("O"),
                    token_type = tokenResponse.TokenType ?? "Bearer"
                };

                profile.CredentialsJson = JsonSerializer.Serialize(credentials);

                // If this is the first profile or user wants it as default, set it
                var defaultProfileSpec = new BaseSpecification<UserStorageProfile>(
                    p => p.UserId == userId && p.IsDefault && p.IsActive
                );
                var hasDefault = await _unitOfWork.Repository<UserStorageProfile>().GetEntityWithSpec(defaultProfileSpec) != null;

                if (!hasDefault)
                {
                    profile.IsDefault = true;
                }

                await _unitOfWork.Complete();

                _logger.LogInformation("Google Drive connected successfully for user {UserId}, profile {ProfileId}", userId, profile.Id);

                return Result.Success(profile.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling OAuth callback");
                return Result<int>.Failure("OAUTH_CALLBACK_ERROR", "Failed to complete OAuth flow");
            }
        }

        private async Task<TokenResponse?> ExchangeCodeForTokensAsync(string code)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "redirect_uri", _settings.RedirectUri },
                    { "grant_type", "authorization_code" }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TokenResponse>(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during token exchange");
                return null;
            }
        }

        private class OAuthState
        {
            public int UserId { get; set; }
            public string Nonce { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        private class TokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("token_type")]
            public string? TokenType { get; set; }
        }
    }
}

