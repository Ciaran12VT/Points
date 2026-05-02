using Points.Global;
using Points.Services.Time;
using Microsoft.Maui.Authentication;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Points.Services.Backup
{
    public static class GoogleDriveOAuthDefaults
    {
        public const string CallbackScheme = "com.companyname.points";
        public const string CallbackPath = "/oauth2redirect";
        public const string RedirectUri = CallbackScheme + ":" + CallbackPath;
        public const string PackageConfigFileName = "google_drive_oauth.json";
        public const string BackupFolderName = "Points Backups";
    }

    public sealed class GoogleDriveConnectionResult
    {
        public string CredentialKey { get; init; } = "";
        public string? AccountEmail { get; init; }
        public string FolderId { get; init; } = "";
        public string FolderName { get; init; } = "";
    }

    public interface IGoogleDriveBackupConnector
    {
        Task<GoogleDriveConnectionResult> ConnectAsync(CancellationToken cancellationToken = default);

        Task DisconnectAsync(string credentialKey, CancellationToken cancellationToken = default);
    }

    public sealed class GoogleDriveOAuthClientConfig
    {
        public string ClientId { get; init; } = "";
        public string RedirectUri { get; init; } = GoogleDriveOAuthDefaults.RedirectUri;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
    }

    public interface IGoogleDriveOAuthClientConfigProvider
    {
        Task<GoogleDriveOAuthClientConfig> GetAsync(CancellationToken cancellationToken = default);
    }

    public sealed class JsonGoogleDriveOAuthClientConfigProvider : IGoogleDriveOAuthClientConfigProvider
    {
        private readonly string _configPath;

        public JsonGoogleDriveOAuthClientConfigProvider()
            : this(AppPaths.GoogleDriveOAuthClientConfigPath)
        {
        }

        public JsonGoogleDriveOAuthClientConfigProvider(string configPath)
        {
            _configPath = string.IsNullOrWhiteSpace(configPath)
                ? throw new ArgumentException("Config path is required.", nameof(configPath))
                : configPath;
        }

        public async Task<GoogleDriveOAuthClientConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            var envClientId = Environment.GetEnvironmentVariable("POINTS_GOOGLE_DRIVE_CLIENT_ID");
            var envRedirectUri = Environment.GetEnvironmentVariable("POINTS_GOOGLE_DRIVE_REDIRECT_URI");
            if (!string.IsNullOrWhiteSpace(envClientId))
            {
                return new GoogleDriveOAuthClientConfig
                {
                    ClientId = envClientId,
                    RedirectUri = string.IsNullOrWhiteSpace(envRedirectUri)
                        ? GoogleDriveOAuthDefaults.RedirectUri
                        : envRedirectUri
                };
            }

            if (File.Exists(_configPath))
                return Normalize(await ReadConfigFileAsync(_configPath, cancellationToken));

            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(
                    GoogleDriveOAuthDefaults.PackageConfigFileName);
                var config = await JsonSerializer.DeserializeAsync<GoogleDriveOAuthClientConfig>(
                    stream,
                    GoogleDriveJson.Options,
                    cancellationToken);

                return Normalize(config);
            }
            catch (FileNotFoundException)
            {
                return new GoogleDriveOAuthClientConfig();
            }
            catch (DirectoryNotFoundException)
            {
                return new GoogleDriveOAuthClientConfig();
            }
        }

        private static async Task<GoogleDriveOAuthClientConfig?> ReadConfigFileAsync(
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<GoogleDriveOAuthClientConfig>(
                stream,
                GoogleDriveJson.Options,
                cancellationToken);
        }

        private static GoogleDriveOAuthClientConfig Normalize(GoogleDriveOAuthClientConfig? config)
        {
            if (config == null)
                return new GoogleDriveOAuthClientConfig();

            return new GoogleDriveOAuthClientConfig
            {
                ClientId = config.ClientId?.Trim() ?? "",
                RedirectUri = string.IsNullOrWhiteSpace(config.RedirectUri)
                    ? GoogleDriveOAuthDefaults.RedirectUri
                    : config.RedirectUri.Trim()
            };
        }
    }

    public sealed class GoogleDriveOAuthToken
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public string Scope { get; set; } = "";
        public string? AccountEmail { get; set; }
    }

    public interface IGoogleDriveTokenStore
    {
        Task<GoogleDriveOAuthToken?> GetAsync(string credentialKey, CancellationToken cancellationToken = default);
        Task SaveAsync(string credentialKey, GoogleDriveOAuthToken token, CancellationToken cancellationToken = default);
        Task DeleteAsync(string credentialKey, CancellationToken cancellationToken = default);
    }

    public sealed class SecureStorageGoogleDriveTokenStore : IGoogleDriveTokenStore
    {
        private const string KeyPrefix = "points_google_drive_oauth_";

        public async Task<GoogleDriveOAuthToken?> GetAsync(
            string credentialKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await SecureStorage.Default.GetAsync(ToStorageKey(credentialKey));
                return string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<GoogleDriveOAuthToken>(json, GoogleDriveJson.Options);
            }
            catch (Exception ex)
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveTokenStoreUnavailable",
                    $"Google Drive tokens could not be read securely: {ex.Message}");
            }
        }

        public async Task SaveAsync(
            string credentialKey,
            GoogleDriveOAuthToken token,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = JsonSerializer.Serialize(token, GoogleDriveJson.Options);
                await SecureStorage.Default.SetAsync(ToStorageKey(credentialKey), json);
            }
            catch (Exception ex)
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveTokenStoreUnavailable",
                    $"Google Drive tokens could not be saved securely: {ex.Message}");
            }
        }

        public Task DeleteAsync(string credentialKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecureStorage.Default.Remove(ToStorageKey(credentialKey));
            return Task.CompletedTask;
        }

        private static string ToStorageKey(string credentialKey)
        {
            return $"{KeyPrefix}{credentialKey}";
        }
    }

    public sealed class GoogleDriveBackupService : IGoogleDriveBackupConnector, IScheduledBackupRemoteStorage
    {
        public const string DefaultCredentialKey = "scheduled-backup";
        private const string DriveScope = "https://www.googleapis.com/auth/drive.file";
        private const string EmailScope = "email";
        private const string ProfileScope = "profile";
        private const string FolderMimeType = "application/vnd.google-apps.folder";
        private const string BackupMimeType = "application/zip";
        private const string FileNamePrefix = "points_scheduled_backup_";
        private const string FileExtension = ".zip";
        private static readonly Uri TokenEndpoint = new("https://oauth2.googleapis.com/token");
        private static readonly Uri UserInfoEndpoint = new("https://www.googleapis.com/oauth2/v2/userinfo");
        private static readonly Uri DriveApiRoot = new("https://www.googleapis.com/drive/v3/");
        private static readonly Uri DriveUploadRoot = new("https://www.googleapis.com/upload/drive/v3/");

        private readonly HttpClient _http;
        private readonly IGoogleDriveOAuthClientConfigProvider _clientConfigProvider;
        private readonly IGoogleDriveTokenStore _tokenStore;
        private readonly IClock _clock;

        public GoogleDriveBackupService(
            HttpClient http,
            IGoogleDriveOAuthClientConfigProvider clientConfigProvider,
            IGoogleDriveTokenStore tokenStore,
            IClock clock)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _clientConfigProvider = clientConfigProvider ?? throw new ArgumentNullException(nameof(clientConfigProvider));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<GoogleDriveConnectionResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            var clientConfig = await GetConfiguredClientAsync(cancellationToken);
            var token = await AuthorizeAsync(clientConfig, cancellationToken);
            var accountEmail = await GetAccountEmailAsync(token.AccessToken, cancellationToken);
            token.AccountEmail = accountEmail;

            var folder = await EnsureFolderAsync(
                token.AccessToken,
                null,
                GoogleDriveOAuthDefaults.BackupFolderName,
                cancellationToken);
            await _tokenStore.SaveAsync(DefaultCredentialKey, token, cancellationToken);

            return new GoogleDriveConnectionResult
            {
                CredentialKey = DefaultCredentialKey,
                AccountEmail = accountEmail,
                FolderId = folder.Id,
                FolderName = folder.Name
            };
        }

        public Task DisconnectAsync(string credentialKey, CancellationToken cancellationToken = default)
        {
            return _tokenStore.DeleteAsync(credentialKey, cancellationToken);
        }

        public async Task<ScheduledBackupStoredFile> StoreAsync(
            string packagePath,
            ScheduledBackupConfig config,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Package path is required.", nameof(packagePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("The scheduled backup package could not be found.", packagePath);

            if (string.IsNullOrWhiteSpace(config.Destination.GoogleDriveCredentialKey))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveReconnectRequired",
                    "Reconnect Google Drive to run scheduled exports.");
            }

            var token = await GetUsableTokenAsync(config.Destination.GoogleDriveCredentialKey, cancellationToken);
            var folder = await EnsureFolderAsync(
                token.AccessToken,
                config.Destination.GoogleDriveFolderId,
                config.Destination.GoogleDriveFolderName,
                cancellationToken);

            config.Destination.GoogleDriveFolderId = folder.Id;
            config.Destination.GoogleDriveFolderName = folder.Name;
            config.Destination.GoogleDriveAccountEmail = token.AccountEmail ?? config.Destination.GoogleDriveAccountEmail;

            var fileName = $"{FileNamePrefix}{_clock.LocalNow:yyyyMMdd_HHmmss}{FileExtension}";
            var uploaded = await UploadResumableAsync(
                token.AccessToken,
                packagePath,
                fileName,
                folder.Id,
                cancellationToken);

            return new ScheduledBackupStoredFile
            {
                FileName = uploaded.Name,
                FilePath = uploaded.WebViewLink ?? $"https://drive.google.com/file/d/{uploaded.Id}/view",
                Bytes = uploaded.Size ?? new FileInfo(packagePath).Length
            };
        }

        public async Task PruneAsync(
            ScheduledBackupConfig config,
            int retentionCount,
            CancellationToken cancellationToken = default)
        {
            if (retentionCount < 1)
                retentionCount = 1;

            if (string.IsNullOrWhiteSpace(config.Destination.GoogleDriveCredentialKey) ||
                string.IsNullOrWhiteSpace(config.Destination.GoogleDriveFolderId))
            {
                return;
            }

            try
            {
                var token = await GetUsableTokenAsync(config.Destination.GoogleDriveCredentialKey, cancellationToken);
                var files = await ListScheduledBackupFilesAsync(
                    token.AccessToken,
                    config.Destination.GoogleDriveFolderId,
                    cancellationToken);

                foreach (var file in files
                    .OrderByDescending(file => file.CreatedTimeUtc ?? DateTime.MinValue)
                    .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                    .Skip(retentionCount))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await DeleteFileAsync(token.AccessToken, file.Id, cancellationToken);
                }
            }
            catch (ScheduledBackupRequiresUserActionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Google Drive backup retention cleanup failed: {ex}");
            }
        }

        private async Task<GoogleDriveOAuthClientConfig> GetConfiguredClientAsync(CancellationToken cancellationToken)
        {
            var clientConfig = await _clientConfigProvider.GetAsync(cancellationToken);
            if (clientConfig.IsConfigured)
                return clientConfig;

            throw new ScheduledBackupRequiresUserActionException(
                "GoogleDriveOAuthClientMissing",
                "Google Drive sign-in is not configured for this build of Points.");
        }

        private async Task<GoogleDriveOAuthToken> AuthorizeAsync(
            GoogleDriveOAuthClientConfig clientConfig,
            CancellationToken cancellationToken)
        {
            var codeVerifier = CreateCodeVerifier();
            var state = CreateCodeVerifier();
            var authorizationUri = BuildAuthorizationUri(clientConfig, codeVerifier, state);

            WebAuthenticatorResult result;
            try
            {
                result = await WebAuthenticator.Default.AuthenticateAsync(
                    authorizationUri,
                    new Uri(clientConfig.RedirectUri));
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException("Google Drive sign-in was cancelled.", cancellationToken);
            }

            var returnedState = GetAuthResultValue(result, "state");
            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveOAuthStateMismatch",
                    "Google Drive sign-in could not be verified. Try connecting again.");
            }

            var error = GetAuthResultValue(result, "error");
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    $"GoogleDrive_{error}",
                    GetAuthResultValue(result, "error_description") ??
                    "Google Drive access was not granted.");
            }

            var code = GetAuthResultValue(result, "code");
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveOAuthCodeMissing",
                    "Google Drive did not return a sign-in code. Try connecting again.");
            }

            return await ExchangeAuthorizationCodeAsync(
                clientConfig,
                code,
                codeVerifier,
                cancellationToken);
        }

        private static Uri BuildAuthorizationUri(
            GoogleDriveOAuthClientConfig clientConfig,
            string codeVerifier,
            string state)
        {
            var scope = $"{DriveScope} {EmailScope} {ProfileScope}";
            var codeChallenge = Base64UrlEncode(
                SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

            var query = new Dictionary<string, string>
            {
                ["client_id"] = clientConfig.ClientId,
                ["redirect_uri"] = clientConfig.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = scope,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256"
            };

            return new Uri($"https://accounts.google.com/o/oauth2/v2/auth?{ToFormEncodedQuery(query)}");
        }

        private async Task<GoogleDriveOAuthToken> ExchangeAuthorizationCodeAsync(
            GoogleDriveOAuthClientConfig clientConfig,
            string authorizationCode,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientConfig.ClientId,
                ["code"] = authorizationCode,
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = clientConfig.RedirectUri,
                ["grant_type"] = "authorization_code"
            });

            using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                ThrowGoogleUserActionError("GoogleDriveOAuthTokenFailed", responseContent);

            var token = ToToken(responseContent);
            if (string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveRefreshTokenMissing",
                    "Google Drive did not return permission for background backups. Try connecting again.");
            }

            return token;
        }

        private async Task<GoogleDriveOAuthToken> GetUsableTokenAsync(
            string credentialKey,
            CancellationToken cancellationToken)
        {
            var token = await _tokenStore.GetAsync(credentialKey, cancellationToken);
            if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                throw new ScheduledBackupRequiresUserActionException(
                    "GoogleDriveReconnectRequired",
                    "Reconnect Google Drive to run scheduled exports.");
            }

            if (token.ExpiresAtUtc > _clock.UtcNow.AddMinutes(5) &&
                !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return token;
            }

            var refreshed = await RefreshAccessTokenAsync(credentialKey, token, cancellationToken);
            return refreshed;
        }

        private async Task<GoogleDriveOAuthToken> RefreshAccessTokenAsync(
            string credentialKey,
            GoogleDriveOAuthToken currentToken,
            CancellationToken cancellationToken)
        {
            var clientConfig = await GetConfiguredClientAsync(cancellationToken);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientConfig.ClientId,
                ["refresh_token"] = currentToken.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

            using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await _tokenStore.DeleteAsync(credentialKey, cancellationToken);
                ThrowGoogleUserActionError("GoogleDriveReconnectRequired", responseContent);
            }

            var refreshed = ToToken(responseContent);
            refreshed.RefreshToken = currentToken.RefreshToken;
            refreshed.AccountEmail = currentToken.AccountEmail;
            await _tokenStore.SaveAsync(credentialKey, refreshed, cancellationToken);
            return refreshed;
        }

        private async Task<string?> GetAccountEmailAsync(
            string accessToken,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(responseContent, GoogleDriveJson.Options);
            return userInfo?.Email;
        }

        private async Task<DriveFolderInfo> EnsureFolderAsync(
            string accessToken,
            string? folderId,
            string? folderName,
            CancellationToken cancellationToken)
        {
            folderName = string.IsNullOrWhiteSpace(folderName)
                ? GoogleDriveOAuthDefaults.BackupFolderName
                : folderName.Trim();

            if (!string.IsNullOrWhiteSpace(folderId))
            {
                var existing = await TryGetFolderAsync(accessToken, folderId, cancellationToken);
                if (existing != null)
                    return existing;
            }

            var matching = await FindFolderByNameAsync(accessToken, folderName, cancellationToken);
            return matching ?? await CreateFolderAsync(accessToken, folderName, cancellationToken);
        }

        private async Task<DriveFolderInfo?> TryGetFolderAsync(
            string accessToken,
            string folderId,
            CancellationToken cancellationToken)
        {
            var uri = new Uri(DriveApiRoot, $"files/{Uri.EscapeDataString(folderId)}?fields=id,name,mimeType,trashed");
            using var request = AuthorizedRequest(HttpMethod.Get, uri, accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureDriveSuccessAsync(response, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var file = JsonSerializer.Deserialize<DriveFileResponse>(responseContent, GoogleDriveJson.Options);
            if (file == null || file.Trashed || file.MimeType != FolderMimeType)
                return null;

            return new DriveFolderInfo(file.Id, file.Name);
        }

        private async Task<DriveFolderInfo?> FindFolderByNameAsync(
            string accessToken,
            string folderName,
            CancellationToken cancellationToken)
        {
            var query = $"mimeType='{FolderMimeType}' and name='{EscapeQueryLiteral(folderName)}' and trashed=false";
            var uri = DriveListUri(query, "files(id,name)", pageSize: 1);
            using var request = AuthorizedRequest(HttpMethod.Get, uri, accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            await EnsureDriveSuccessAsync(response, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var list = JsonSerializer.Deserialize<DriveFileListResponse>(responseContent, GoogleDriveJson.Options);
            var file = list?.Files?.FirstOrDefault();
            return file == null ? null : new DriveFolderInfo(file.Id, file.Name);
        }

        private async Task<DriveFolderInfo> CreateFolderAsync(
            string accessToken,
            string folderName,
            CancellationToken cancellationToken)
        {
            var uri = new Uri(DriveApiRoot, "files?fields=id,name");
            using var request = AuthorizedRequest(HttpMethod.Post, uri, accessToken);
            request.Content = JsonContent(new
            {
                name = folderName,
                mimeType = FolderMimeType
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            await EnsureDriveSuccessAsync(response, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var file = JsonSerializer.Deserialize<DriveFileResponse>(responseContent, GoogleDriveJson.Options);
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
                throw new InvalidDataException("Google Drive did not return the created backup folder id.");

            return new DriveFolderInfo(file.Id, file.Name);
        }

        private async Task<DriveFileResponse> UploadResumableAsync(
            string accessToken,
            string packagePath,
            string fileName,
            string folderId,
            CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(packagePath);
            var startUri = new Uri(DriveUploadRoot, "files?uploadType=resumable&fields=id,name,size,webViewLink");
            using var startRequest = AuthorizedRequest(HttpMethod.Post, startUri, accessToken);
            startRequest.Headers.Add("X-Upload-Content-Type", BackupMimeType);
            startRequest.Headers.Add("X-Upload-Content-Length", fileInfo.Length.ToString());
            startRequest.Content = JsonContent(new
            {
                name = fileName,
                mimeType = BackupMimeType,
                parents = new[] { folderId }
            });

            using var startResponse = await _http.SendAsync(startRequest, cancellationToken);
            await EnsureDriveSuccessAsync(startResponse, cancellationToken);

            var uploadUri = startResponse.Headers.Location;
            if (uploadUri == null)
                throw new InvalidDataException("Google Drive did not return an upload session URI.");

            await using var stream = File.OpenRead(packagePath);
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUri);
            uploadRequest.Content = new StreamContent(stream);
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(BackupMimeType);
            uploadRequest.Content.Headers.ContentLength = fileInfo.Length;

            using var uploadResponse = await _http.SendAsync(uploadRequest, cancellationToken);
            await EnsureDriveSuccessAsync(uploadResponse, cancellationToken);

            var responseContent = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
            var uploaded = JsonSerializer.Deserialize<DriveFileResponse>(responseContent, GoogleDriveJson.Options);
            if (uploaded == null || string.IsNullOrWhiteSpace(uploaded.Id))
                throw new InvalidDataException("Google Drive did not return the uploaded backup file id.");

            return uploaded;
        }

        private async Task<List<DriveFileResponse>> ListScheduledBackupFilesAsync(
            string accessToken,
            string folderId,
            CancellationToken cancellationToken)
        {
            var files = new List<DriveFileResponse>();
            string? pageToken = null;

            do
            {
                var query = $"'{EscapeQueryLiteral(folderId)}' in parents and name contains '{FileNamePrefix}' and trashed=false";
                var uri = DriveListUri(query, "nextPageToken,files(id,name,createdTime)", pageSize: 100, pageToken);
                using var request = AuthorizedRequest(HttpMethod.Get, uri, accessToken);
                using var response = await _http.SendAsync(request, cancellationToken);
                await EnsureDriveSuccessAsync(response, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var list = JsonSerializer.Deserialize<DriveFileListResponse>(responseContent, GoogleDriveJson.Options);
                if (list?.Files != null)
                    files.AddRange(list.Files.Where(file => file.Name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)));

                pageToken = list?.NextPageToken;
            }
            while (!string.IsNullOrWhiteSpace(pageToken));

            return files;
        }

        private async Task DeleteFileAsync(
            string accessToken,
            string fileId,
            CancellationToken cancellationToken)
        {
            var uri = new Uri(DriveApiRoot, $"files/{Uri.EscapeDataString(fileId)}");
            using var request = AuthorizedRequest(HttpMethod.Delete, uri, accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return;

            await EnsureDriveSuccessAsync(response, cancellationToken);
        }

        private static HttpRequestMessage AuthorizedRequest(HttpMethod method, Uri uri, string accessToken)
        {
            var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }

        private static StringContent JsonContent<T>(T value)
        {
            return new StringContent(
                JsonSerializer.Serialize(value, GoogleDriveJson.Options),
                Encoding.UTF8,
                "application/json");
        }

        private static Uri DriveListUri(
            string query,
            string fields,
            int pageSize,
            string? pageToken = null)
        {
            var builder = new StringBuilder("files?");
            builder.Append("q=").Append(Uri.EscapeDataString(query));
            builder.Append("&spaces=drive");
            builder.Append("&fields=").Append(Uri.EscapeDataString(fields));
            builder.Append("&pageSize=").Append(pageSize);

            if (!string.IsNullOrWhiteSpace(pageToken))
                builder.Append("&pageToken=").Append(Uri.EscapeDataString(pageToken));

            return new Uri(DriveApiRoot, builder.ToString());
        }

        private async Task EnsureDriveSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                ThrowGoogleUserActionError("GoogleDriveReconnectRequired", responseContent);

            throw new HttpRequestException(
                $"Google Drive API request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseContent}");
        }

        private GoogleDriveOAuthToken ToToken(string responseContent)
        {
            var token = JsonSerializer.Deserialize<TokenResponse>(responseContent, GoogleDriveJson.Options);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
                throw new InvalidDataException("Google did not return a usable OAuth access token.");

            return new GoogleDriveOAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken ?? "",
                ExpiresAtUtc = _clock.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)),
                Scope = token.Scope ?? "",
                TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType
            };
        }

        private static void ThrowGoogleUserActionError(string fallbackCode, string responseContent)
        {
            var error = TryReadOAuthError(responseContent);
            var message = TryReadOAuthErrorDescription(responseContent);

            throw new ScheduledBackupRequiresUserActionException(
                string.IsNullOrWhiteSpace(error) ? fallbackCode : $"GoogleDrive_{error}",
                string.IsNullOrWhiteSpace(message)
                    ? "Google Drive authorization failed. Reconnect Google Drive and try again."
                    : message);
        }

        private static string? TryReadOAuthError(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                return document.RootElement.TryGetProperty("error", out var error)
                    ? error.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? TryReadOAuthErrorDescription(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("error_description", out var description))
                    return description.GetString();

                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string EscapeQueryLiteral(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private static string? GetAuthResultValue(WebAuthenticatorResult result, string key)
        {
            return result.Properties.TryGetValue(key, out var value)
                ? value
                : null;
        }

        private static string ToFormEncodedQuery(IEnumerable<KeyValuePair<string, string>> values)
        {
            return string.Join(
                "&",
                values.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        private static string CreateCodeVerifier()
        {
            Span<byte> bytes = stackalloc byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        {
            return Convert
                .ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private sealed record DriveFolderInfo(string Id, string Name);

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = "";

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }

        private sealed class UserInfoResponse
        {
            [JsonPropertyName("email")]
            public string? Email { get; set; }
        }

        private sealed class DriveFileListResponse
        {
            [JsonPropertyName("nextPageToken")]
            public string? NextPageToken { get; set; }

            [JsonPropertyName("files")]
            public List<DriveFileResponse>? Files { get; set; }
        }

        private sealed class DriveFileResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";

            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("mimeType")]
            public string? MimeType { get; set; }

            [JsonPropertyName("trashed")]
            public bool Trashed { get; set; }

            [JsonPropertyName("size")]
            public long? Size { get; set; }

            [JsonPropertyName("webViewLink")]
            public string? WebViewLink { get; set; }

            [JsonPropertyName("createdTime")]
            public DateTime? CreatedTimeUtc { get; set; }
        }
    }

    internal static class GoogleDriveJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
