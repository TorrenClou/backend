using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Options;
using TorreClou.Core.Shared;
using TorreClou.Infrastructure.Helpers;

namespace TorreClou.Infrastructure.Services
{
    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly GoogleDriveSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleDriveService> _logger;

        public GoogleDriveService(
            IOptions<GoogleDriveSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleDriveService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Result<string>> GetAccessTokenAsync(string credentialsJson, CancellationToken cancellationToken = default)
        {
            try
            {
                var credentials = JsonSerializer.Deserialize<GoogleDriveCredentials>(credentialsJson);
                if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
                {
                    return Result<string>.Failure("INVALID_CREDENTIALS", "Invalid credentials JSON");
                }

                // Check if token is expired
                if (!string.IsNullOrEmpty(credentials.ExpiresAt))
                {
                    if (DateTime.TryParse(credentials.ExpiresAt, out var expiresAt) && expiresAt <= DateTime.UtcNow.AddMinutes(5))
                    {
                        // Token expired or about to expire, refresh it
                        if (string.IsNullOrEmpty(credentials.RefreshToken))
                        {
                            return Result<string>.Failure("NO_REFRESH_TOKEN", "Access token expired and no refresh token available");
                        }

                        var refreshResult = await RefreshAccessTokenAsync(credentials.RefreshToken, cancellationToken);
                        if (refreshResult.IsFailure)
                        {
                            return refreshResult;
                        }

                        return Result.Success(refreshResult.Value);
                    }
                }

                return Result.Success(credentials.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access token from credentials");
                return Result<string>.Failure("TOKEN_ERROR", "Failed to get access token");
            }
        }

        public async Task<Result<string>> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "refresh_token", refreshToken },
                    { "grant_type", "refresh_token" }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Token refresh failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return Result<string>.Failure("REFRESH_FAILED", "Failed to refresh access token");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(jsonResponse);

                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    return Result<string>.Failure("INVALID_RESPONSE", "Invalid token refresh response");
                }

                return Result.Success(tokenResponse.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during token refresh");
                return Result<string>.Failure("REFRESH_ERROR", "Error refreshing access token");
            }
        }

        public async Task<Result<string>> CreateFolderAsync(string folderName, string? parentFolderId, string accessToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var folderMetadata = new
                {
                    name = folderName,
                    mimeType = "application/vnd.google-apps.folder",
                    parents = !string.IsNullOrEmpty(parentFolderId) ? new[] { parentFolderId } : Array.Empty<string>()
                };

                var json = JsonSerializer.Serialize(folderMetadata);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    "https://www.googleapis.com/drive/v3/files?fields=id,name",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Folder creation failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return Result<string>.Failure("FOLDER_CREATE_FAILED", $"Failed to create folder: {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var folderResponse = JsonSerializer.Deserialize<FolderResponse>(responseJson);

                if (folderResponse == null || string.IsNullOrEmpty(folderResponse.Id))
                {
                    return Result<string>.Failure("INVALID_RESPONSE", "Invalid folder creation response");
                }

                return Result.Success(folderResponse.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating folder: {FolderName}", folderName);
                return Result<string>.Failure("FOLDER_CREATE_ERROR", "Error creating folder");
            }
        }

        // Update signature to accept 'progress'
        public async Task<Result<string>> UploadFileAsync(
            string filePath,
            string fileName,
            string folderId,
            string accessToken,
            IProgress<double>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(filePath))
                    return Result<string>.Failure("FILE_NOT_FOUND", $"File not found: {filePath}");

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromHours(2);

                // ---------------------------------------------------------
                // PHASE 1: Initiate Resumable Upload (Metadata only)
                // ---------------------------------------------------------
                var initiateUrl = "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&fields=id,name";
                var request = new HttpRequestMessage(HttpMethod.Post, initiateUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var fileInfo = new FileInfo(filePath);
                string contentType = GetMimeType(fileName);

                request.Headers.Add("X-Upload-Content-Type", contentType);
                request.Headers.Add("X-Upload-Content-Length", fileInfo.Length.ToString());

                var metadata = new { name = fileName, parents = new[] { folderId } };
                var jsonMetadata = JsonSerializer.Serialize(metadata);
                request.Content = new StringContent(jsonMetadata, Encoding.UTF8, "application/json");

                var initiateResponse = await httpClient.SendAsync(request, cancellationToken);

                if (!initiateResponse.IsSuccessStatusCode)
                {
                    // ... (Error handling same as before)
                    return Result<string>.Failure("INIT_FAILED", "Failed to initiate upload");
                }

                var uploadUrl = initiateResponse.Headers.Location;

                // ---------------------------------------------------------
                // PHASE 2: Upload Content with Progress
                // ---------------------------------------------------------

                // Open the file stream
                var fileStream = File.OpenRead(filePath);

                // Use our custom wrapper instead of standard StreamContent
                var progressContent = new ProgressableStreamContent(fileStream, (sent, total) =>
                {
                    if (progress != null)
                    {
                        // Calculate percentage (0 to 100)
                        double percent = (double)sent / total * 100;
                        progress.Report(percent);
                    }
                });

                // Ensure headers are set on the content wrapper
                progressContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                // Send the PUT request using the wrapped content
                var uploadResponse = await httpClient.PutAsync(uploadUrl, progressContent, cancellationToken);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    // ... (Error handling same as before)
                    return Result<string>.Failure("UPLOAD_FAILED", "Bytes transfer failed");
                }

                var responseJson = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
                var driveFile = JsonSerializer.Deserialize<FileUploadResponse>(responseJson);

                return Result.Success(driveFile?.Id ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception uploading file");
                return Result<string>.Failure("UPLOAD_ERROR", ex.Message);
            }
        }

        // ---------------------------------------------------------
        // HELPER: Dynamic Mime Type Detection
        // ---------------------------------------------------------
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                ".bin" => "application/octet-stream",
                _ => "application/octet-stream" // Default fallback for unknown types
            };
        }

        private class GoogleDriveCredentials
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
            public string? ExpiresAt { get; set; }
        }

        private class TokenRefreshResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private class FolderResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private class FileUploadResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}

