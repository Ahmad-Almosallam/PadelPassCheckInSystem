using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using System.Text;

namespace PadelPassCheckInSystem.Services;

public interface IPlaytomicSyncService
{
    Task<PlaytomicSyncResult> SyncActiveUsersToPlaytomicAsync(string accessToken);
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

    public async Task<PlaytomicSyncResult> SyncActiveUsersToPlaytomicAsync(string accessToken)
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

            _logger.LogInformation($"Found {activeUsers.Count} active users and {branchesWithTenantId.Count} branches to sync");

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
                    var success = await UploadToPlaytomicAsync(
                        accessToken,
                        branch.PlaytomicTenantId.Value,
                        fileName,
                        csvContent);

                    if (success)
                    {
                        branchResult.IsSuccess = true;
                        branchResult.UserCount = activeUsers.Count;
                        _logger.LogInformation($"Successfully synced {activeUsers.Count} users to branch {branch.Name}");
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

    private async Task<List<Models.Entities.EndUser>> GetActiveUsersAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow().Date;

        var allUsers = await _context.EndUsers.ToListAsync();

        return allUsers.Where(user =>
        {
            // Check if subscription is active (using KSA dates)
            var startKSA = user.SubscriptionStartDate.ToKSATime().Date;
            var endKSA = user.SubscriptionEndDate.ToKSATime().Date;
            var isSubscriptionActive = startKSA <= todayKSA && endKSA >= todayKSA;

            // Check if not paused
            var isNotPaused = !user.IsPaused || 
                              (user.CurrentPauseStartDate?.ToKSATime().Date > todayKSA || 
                               user.CurrentPauseEndDate?.ToKSATime().Date < todayKSA);

            return isSubscriptionActive && isNotPaused;
        }).ToList();
    }

    private string CreateCsvContent(List<Models.Entities.EndUser> users)
    {
        var csv = new StringBuilder();
            
        // Add header
        csv.AppendLine("name,email,phone_number,gender,birthdate,category_name,category_expires");

        // Add user data
        foreach (var user in users)
        {
            var categoryExpires = user.SubscriptionEndDate.ToKSATime().ToString("yyyy-MM-dd");
                
            csv.AppendLine($"\"{user.Name}\",\"{user.Email ?? ""}\",\"{user.PhoneNumber}\",\"\",\"\",\"Padel Pass\",\"{categoryExpires}\"");
        }

        return csv.ToString();
    }

    private string GenerateFileName()
    {
        var nowKSA = KSADateTimeExtensions.GetKSANow();
        return $"PadelPass Sync - {nowKSA:yyyy-MM-dd} - {nowKSA:HH:mm}.csv";
    }

    private async Task<bool> UploadToPlaytomicAsync(string accessToken, Guid tenantId, string fileName, string csvContent)
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
            httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");
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
            var url = $"{PLAYTOMIC_BASE_URL}/v1/user_imports?tenant_id={tenantId}&name={Uri.EscapeDataString(fileName)}";

            _logger.LogInformation($"Uploading to Playtomic: {url}");

            // Make the request
            var response = await httpClient.PostAsync(url, formData);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Playtomic upload successful for tenant {tenantId}: {responseContent}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Playtomic upload failed for tenant {tenantId}. Status: {response.StatusCode}, Content: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception during Playtomic upload for tenant {tenantId}: {ex.Message}");
            return false;
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