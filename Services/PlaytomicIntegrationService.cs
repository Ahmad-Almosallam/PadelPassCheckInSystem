using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadelPassCheckInSystem.Services
{
    public interface IPlaytomicIntegrationService
    {
        Task<PlaytomicIntegration?> GetActiveIntegrationAsync();
        Task<PlaytomicIntegration> SaveIntegrationAsync(PlaytomicIntegrationViewModel model);
        Task<PlaytomicIntegration> RefreshAccessTokenAsync(PlaytomicIntegration integration);
        Task<string> GetValidAccessTokenAsync();
        Task<int> SyncCategoryMembersPlaytomicUserIdsAsync(CancellationToken cancellationToken = default);
    }

    public class PlaytomicIntegrationService : IPlaytomicIntegrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PlaytomicIntegrationService> _logger;
        private const string PLAYTOMIC_BASE_URL = "https://api.playtomic.io";
        private const string CATEGORY_ID = "d401a97e-cad8-4211-94dd-bf4f536f7892";

        public PlaytomicIntegrationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<PlaytomicIntegrationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<PlaytomicIntegration?> GetActiveIntegrationAsync()
        {
            return await _context.PlaytomicIntegrations
                .OrderByDescending(pi => pi.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<PlaytomicIntegration> SaveIntegrationAsync(PlaytomicIntegrationViewModel model)
        {
            
            // get first integration
            var existingIntegration = await _context.PlaytomicIntegrations.FirstOrDefaultAsync();
            
            if (existingIntegration != null)
            {
                // Update existing integration
                existingIntegration.AccessToken = model.AccessToken;
                existingIntegration.AccessTokenExpiration = model.AccessTokenExpiration;
                existingIntegration.RefreshToken = model.RefreshToken;
                existingIntegration.RefreshTokenExpiration = model.RefreshTokenExpiration;
                existingIntegration.UpdatedAt = DateTime.UtcNow;

                _context.PlaytomicIntegrations.Update(existingIntegration);
                await _context.SaveChangesAsync();

                return existingIntegration;
            }
            
            // Create new integration
            var integration = new PlaytomicIntegration
            {
                AccessToken = model.AccessToken,
                AccessTokenExpiration = model.AccessTokenExpiration,
                RefreshToken = model.RefreshToken,
                RefreshTokenExpiration = model.RefreshTokenExpiration,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PlaytomicIntegrations.Add(integration);
            await _context.SaveChangesAsync();

            return integration;
        }

        public async Task<PlaytomicIntegration> RefreshAccessTokenAsync(PlaytomicIntegration integration)
        {
            if (integration.RefreshTokenExpiration <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Refresh token has expired. Please update the integration with new tokens.");
            }

            var httpClient = _httpClientFactory.CreateClient();
            
            var refreshRequest = new PlaytomicTokenRefreshRequest
            {
                RefreshToken = integration.RefreshToken
            };

            var json = JsonSerializer.Serialize(refreshRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"{PLAYTOMIC_BASE_URL}/v3/auth/token", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to refresh Playtomic access token. Status: {StatusCode}, Content: {Content}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Failed to refresh access token: {response.StatusCode}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<PlaytomicTokenRefreshResponse>(responseJson);

                // Parse the expiration dates
                if (DateTime.TryParse(tokenResponse.AccessTokenExpiration, out var accessExpiration))
                {
                    integration.AccessTokenExpiration = accessExpiration;
                }
                else
                {
                    // Fallback: assume 1 hour expiration if parsing fails
                    integration.AccessTokenExpiration = DateTime.UtcNow.AddHours(1);
                }

                if (DateTime.TryParse(tokenResponse.RefreshTokenExpiration, out var refreshExpiration))
                {
                    integration.RefreshTokenExpiration = refreshExpiration;
                }

                integration.AccessToken = tokenResponse.AccessToken;
                integration.RefreshToken = tokenResponse.RefreshToken;
                integration.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully refreshed Playtomic access token");
                return integration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing Playtomic access token");
                throw;
            }
        }

        public async Task<string> GetValidAccessTokenAsync()
        {
            var integration = await GetActiveIntegrationAsync();
            
            if (integration == null)
            {
                throw new InvalidOperationException("No active Playtomic integration found. Please configure the integration first.");
            }

            // Check if access token is expiring within 3 minutes
            if (integration.AccessTokenExpiration <= DateTime.UtcNow.AddMinutes(3))
            {
                _logger.LogInformation("Access token is expiring soon, refreshing...");
                integration = await RefreshAccessTokenAsync(integration);
            }

            return integration.AccessToken;
        }

        public async Task<int> SyncCategoryMembersPlaytomicUserIdsAsync(CancellationToken cancellationToken = default)
        {
            // Load users without PlaytomicUserId
            var usersWithoutId = await _context.EndUsers
                .Where(u => u.PlaytomicUserId == null)
                .ToListAsync(cancellationToken);

            if (usersWithoutId.Count == 0)
            {
                return 0;
            }

            // Build lookup dictionaries for matching
            string NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
            string NormalizePhone(string? phone)
            {
                if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
                var sb = new System.Text.StringBuilder(phone.Length);
                foreach (var ch in phone)
                {
                    if (char.IsDigit(ch)) sb.Append(ch);
                }
                return sb.ToString();
            }

            var byEmail = usersWithoutId
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .GroupBy(u => NormalizeEmail(u.Email))
                .ToDictionary(g => g.Key, g => g.ToList());

            var httpClient = _httpClientFactory.CreateClient();
            var accessToken = await GetValidAccessTokenAsync();

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var updatedCount = 0;
            var page = 0;
            var size = 100;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url = $"{PLAYTOMIC_BASE_URL}/v2/users/suggestions/category_members?category_id={CATEGORY_ID}&filter=&page={page}&size={size}";
                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling Playtomic category members endpoint");
                    throw;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to fetch category members. Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Failed to fetch category members: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                List<CategoryMemberDto>? members;
                try
                {
                    members = JsonSerializer.Deserialize<List<CategoryMemberDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize category members response");
                    throw;
                }

                if (members == null || members.Count == 0)
                {
                    break; // No more data
                }

                foreach (var m in members)
                {
                    if (string.IsNullOrWhiteSpace(m.UserId)) continue;

                    long parsedId;
                    if (!long.TryParse(m.UserId, out parsedId)) continue;

                    // Try email first
                    if (!string.IsNullOrWhiteSpace(m.Email))
                    {
                        var key = NormalizeEmail(m.Email);
                        if (byEmail.TryGetValue(key, out var candidates))
                        {
                            foreach (var user in candidates)
                            {
                                if (user.PlaytomicUserId == null)
                                {
                                    user.PlaytomicUserId = parsedId;
                                    updatedCount++;
                                }
                            }
                        }
                    }
                }

                // If less than page size, we've reached the end
                if (members.Count < size)
                {
                    break;
                }

                page++;
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return updatedCount;
        }

        private class CategoryMemberDto
        {
            [JsonPropertyName("user_id")]
            public string UserId { get; set; }
            [JsonPropertyName("email")]
            public string Email { get; set; }
        }
    }
}
