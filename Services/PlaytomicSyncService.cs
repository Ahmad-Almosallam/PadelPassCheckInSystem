using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadelPassCheckInSystem.Services;

public interface IPlaytomicSyncService
{
    Task<PlaytomicSyncResult> SyncActiveUsersToPlaytomicAsync(
        string accessToken);

    Task<PlaytomicSyncResponse> GetUserImportStatusAsync(
        string accessToken,
        string userImportId);

    Task<List<Models.Entities.EndUser>> GetActiveUsersAsync();
}

public class PlaytomicSyncService : IPlaytomicSyncService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlaytomicSyncService> _logger;
    private const string PLAYTOMIC_BASE_URL = "https://api.playtomic.io";

    public PlaytomicSyncService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<PlaytomicSyncService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PlaytomicSyncResult> SyncActiveUsersToPlaytomicAsync(
        string accessToken)
    {
        var result = new PlaytomicSyncResult();

        try
        {
            // Get all branches with PlaytomicTenantId
            var branchesWithTenantId = await _context.Branches
                .Where(b => b.PlaytomicTenantId.HasValue && b.IsActive)
                .ToListAsync();

            if (!branchesWithTenantId.Any())
            {
                result.ErrorMessage = "No branches found with Playtomic Tenant ID configured.";
                return result;
            }

            // Get all active users (both subscription active and not paused)
            var activeUsers = await GetActiveUsersAsync();

            if (!activeUsers.Any())
            {
                result.ErrorMessage = "No active users found to sync.";
                return result;
            }

            _logger.LogInformation(
                $"Found {activeUsers.Count} active users and {branchesWithTenantId.Count} branches to sync");
            PlaytomicSyncResponse playtomicSyncResponse = null;
            // Process each branch
            foreach (var branch in branchesWithTenantId)
            {
                var branchResult = new BranchSyncResult
                {
                    BranchName = branch.Name,
                    TenantId = branch.PlaytomicTenantId.Value
                };

                try
                {
                    // Create CSV content for this branch
                    var csvContent = CreateCsvContent(activeUsers);
                    var fileName = GenerateFileName();

                    // Upload to Playtomic
                    var (success, playtomicSyncResponse1) = await UploadToPlaytomicAsync(
                        accessToken,
                        branch.PlaytomicTenantId.Value,
                        fileName,
                        csvContent);

                    playtomicSyncResponse = playtomicSyncResponse1;

                    if (success)
                    {
                        branchResult.IsSuccess = true;
                        branchResult.UserCount = activeUsers.Count;
                        _logger.LogInformation(
                            $"Successfully synced {activeUsers.Count} users to branch {branch.Name}");
                    }
                    else
                    {
                        branchResult.IsSuccess = false;
                        branchResult.ErrorMessage = "Failed to upload to Playtomic API";
                        _logger.LogError($"Failed to sync users to branch {branch.Name}");
                    }
                }
                catch (Exception ex)
                {
                    branchResult.IsSuccess = false;
                    branchResult.ErrorMessage = ex.Message;
                    _logger.LogError(ex, $"Error syncing users to branch {branch.Name}: {ex.Message}");
                }

                result.BranchResults.Add(branchResult);
            }

            await Task.Delay(1000); // Wait for a second to avoid hitting API limits

            // call API to get the status of the sync
            if (playtomicSyncResponse is not null)
            {
                var userImportStatus = await GetUserImportStatusAsync(accessToken, playtomicSyncResponse.UserImportId);
                if (userImportStatus != null)
                {
                    if (userImportStatus.Status.ToUpper() == "FINISHED")
                    {
                        result.TotalBranches = branchesWithTenantId.Count;
                        result.TotalUsers = activeUsers.Count;
                        result.SuccessfulBranches = result.BranchResults.Count;
                        result.FailedBranches = 0;
                        result.IsSuccess = true;

                        return result;
                    }

                    if (userImportStatus.Status.ToUpper() == "FINISHED_WITH_ERRORS")
                    {
                        result.TotalBranches = branchesWithTenantId.Count;
                        result.SuccessfulBranches = 0;
                        result.FailedBranches = result.BranchResults.Count;
                        result.TotalUsers = activeUsers.Count;
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Total users: {userImportStatus.Result.Total}, " +
                                              $"Processed: {userImportStatus.Result.Processed}, " +
                                              $"Succeeded: {userImportStatus.Result.Succeeded}, " +
                                              $"Failed: {userImportStatus.Result.Failed}";
                        return result;
                    }
                }
            }

            // Calculate summary
            result.TotalBranches = branchesWithTenantId.Count;
            result.SuccessfulBranches = result.BranchResults.Count(r => r.IsSuccess);
            result.FailedBranches = result.BranchResults.Count(r => !r.IsSuccess);
            result.TotalUsers = activeUsers.Count;
            result.IsSuccess = result.SuccessfulBranches > 0;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"General error during sync: {ex.Message}";
            _logger.LogError(ex, $"General error during Playtomic sync: {ex.Message}");
        }

        return result;
    }

    public async Task<PlaytomicSyncResponse> GetUserImportStatusAsync(
        string accessToken,
        string userImportId)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            // Set headers
            httpClient.DefaultRequestHeaders.Add("accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br, zstd");
            httpClient.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
            httpClient.DefaultRequestHeaders.Add("origin", "https://manager.playtomic.io");
            httpClient.DefaultRequestHeaders.Add("referer", "https://manager.playtomic.io/");
            httpClient.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "com.playtomic.manager 1.207.0+ff400e4");

            // Add authorization header
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }

            // Build URL
            var url = $"{PLAYTOMIC_BASE_URL}/v1/user_imports/{userImportId}";

            _logger.LogInformation($"Getting user import status from Playtomic: {url}");

            // Make the GET request
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var playtomicResponse = JsonSerializer.Deserialize<PlaytomicSyncResponse>(responseContent);
                _logger.LogInformation(
                    $"Successfully retrieved user import status for ID {userImportId}: {responseContent}");
                return playtomicResponse;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    $"Failed to get user import status for ID {userImportId}. Status: {response.StatusCode}, Content: {errorContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception during getting user import status for ID {userImportId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Models.Entities.EndUser>> GetActiveUsersAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;

        var allUsers = await _context.EndUsers.ToListAsync();

        return allUsers.Where(user =>
            {
                // Check if subscription is active (using KSA dates)
                var startKSA = user.SubscriptionStartDate.AddDays(-1).ToKSATime()
                    .Date;
                var endKSA = user.SubscriptionEndDate.ToKSATime()
                    .Date;
                var isSubscriptionActive = startKSA <= todayKSA && endKSA >= todayKSA;

                // Check if not paused
                var isNotPaused = !user.IsPaused ||
                                  (user.CurrentPauseStartDate?.ToKSATime()
                                       .Date > todayKSA ||
                                   user.CurrentPauseEndDate?.ToKSATime()
                                       .Date < todayKSA);

                return isSubscriptionActive && isNotPaused;
            })
            .ToList();
    }

    private string CreateCsvContent(
        List<Models.Entities.EndUser> users)
    {
        var csv = new StringBuilder();

        // Add header
        csv.AppendLine("name,email,phone_number,gender,birthdate,category_name,category_expires");

        // Add user data
        foreach (var user in users)
        {
            if (user.IsStopped)
            {
                csv.AppendLine(
                    $"\"{user.Name}\",\"{user.Email ?? ""}\",\"{user.PhoneNumber}\",\"\",\"\",\"\",\"\"");
            }
            else
            {
                var categoryExpires = user.IsPaused
                    ? user.CurrentPauseStartDate!.Value.ToString("yyyy-MM-dd")
                    : user.SubscriptionEndDate
                        .ToString("yyyy-MM-dd");

                csv.AppendLine(
                    $"\"{user.Name}\",\"{user.Email ?? ""}\",\"{user.PhoneNumber}\",\"\",\"\",\"Padel Pass\",\"{categoryExpires}\"");
            }
        }

        return csv.ToString();
    }

    private string GenerateFileName()
    {
        var nowKSA = KSADateTimeExtensions.GetKSANow();
        return $"PadelPass Sync - {nowKSA:yyyy-MM-dd} - {nowKSA:HH:mm}.csv";
    }

    private async Task<(bool, PlaytomicSyncResponse)> UploadToPlaytomicAsync(
        string accessToken,
        Guid tenantId,
        string fileName,
        string csvContent)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            // Set headers
            httpClient.DefaultRequestHeaders.Add("accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br, zstd");
            httpClient.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
            httpClient.DefaultRequestHeaders.Add("origin", "https://manager.playtomic.io");
            httpClient.DefaultRequestHeaders.Add("referer", "https://manager.playtomic.io/");
            httpClient.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "com.playtomic.manager 1.207.0+ff400e4");

            // Add authorization header if needed (you might need to adjust this based on Playtomic's auth requirements)
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }

            // Create multipart form data
            using var formData = new MultipartFormDataContent();
            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var fileContent = new ByteArrayContent(csvBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            formData.Add(fileContent, "file", fileName);

            // Build URL
            var url =
                $"{PLAYTOMIC_BASE_URL}/v1/user_imports?tenant_id={tenantId}&name={Uri.EscapeDataString(fileName)}";

            _logger.LogInformation($"Uploading to Playtomic: {url}");

            // Make the request
            var response = await httpClient.PostAsync(url, formData);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var playtomicResponse = JsonSerializer.Deserialize<PlaytomicSyncResponse>(responseContent);
                _logger.LogInformation($"Playtomic upload successful for tenant {tenantId}: {responseContent}");
                return (true, playtomicResponse);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    $"Playtomic upload failed for tenant {tenantId}. Status: {response.StatusCode}, Content: {errorContent}");
                return (false, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception during Playtomic upload for tenant {tenantId}: {ex.Message}");
            return (false, null);
        }
    }
}

public class PlaytomicSyncResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public int TotalBranches { get; set; }
    public int SuccessfulBranches { get; set; }
    public int FailedBranches { get; set; }
    public int TotalUsers { get; set; }
    public List<BranchSyncResult> BranchResults { get; set; } = new List<BranchSyncResult>();
}

public class BranchSyncResult
{
    public string BranchName { get; set; }
    public Guid TenantId { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public int UserCount { get; set; }
}

public class PlaytomicSyncResponse
{
    [JsonPropertyName("user_import_id")] public string UserImportId { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("organization_id")] public object OrganizationId { get; set; }

    [JsonPropertyName("tenant_id")] public string TenantId { get; set; }

    [JsonPropertyName("status")] public string Status { get; set; }

    [JsonPropertyName("error_details")] public object[] ErrorDetails { get; set; }

    [JsonPropertyName("result")] public Result Result { get; set; }

    [JsonPropertyName("created_at")] public string CreatedAt { get; set; }
}

public class Result
{
    [JsonPropertyName("total")] public int Total { get; set; }

    [JsonPropertyName("processed")] public int Processed { get; set; }

    [JsonPropertyName("succeeded")] public int Succeeded { get; set; }

    [JsonPropertyName("failed")] public int Failed { get; set; }
}