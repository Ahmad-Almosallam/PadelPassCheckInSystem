using PadelPassCheckInSystem.Integration.Rekaz.Models;
using PadelPassCheckInSystem.Settings;
using System.Text.Json;
using System.Text;

namespace PadelPassCheckInSystem.Integration.Rekaz;

public class RekazClient
{
    private readonly HttpClient _httpClient;
    private readonly RekazSettings _rekazSettings;
    private readonly ILogger<RekazClient> _logger;

    public RekazClient(
        HttpClient httpClient,
        RekazSettings rekazSettings,
        ILogger<RekazClient> logger)
    {
        _httpClient = httpClient;
        _rekazSettings = rekazSettings;
        _logger = logger;

        // Set up authentication header
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_rekazSettings.ApiKey));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        // Set up tenant header
        _httpClient.DefaultRequestHeaders.Add("__tenant", _rekazSettings.TenantId);
    }

    public async Task<CustomerResponse> GetCustomersAsync(
        int maxResultCount)
    {
        try
        {
            var url = $"{_rekazSettings.BaseUrl}/customers?maxResultCount={maxResultCount}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var customerResponse = JsonSerializer.Deserialize<CustomerResponse>(jsonContent, options);

            return customerResponse ?? new CustomerResponse();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while fetching customers from Rekaz API.");
            throw new InvalidOperationException($"Failed to retrieve customers from Rekaz API: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while processing customers response from Rekaz API.");
            throw new InvalidOperationException($"Failed to parse customers response from Rekaz API: {ex.Message}", ex);
        }
    }
}