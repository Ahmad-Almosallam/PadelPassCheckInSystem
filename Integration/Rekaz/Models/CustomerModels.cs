using System.Text.Json.Serialization;

namespace PadelPassCheckInSystem.Integration.Rekaz.Models;

public class CustomerResponse
{
    [JsonPropertyName("items")]
    public List<RekazCustomer> Items { get; set; } = new();
    
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

public class RekazCustomer
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("customerNumber")]
    public int CustomerNumber { get; set; }
    
    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("customerType")]
    public int CustomerType { get; set; }
    
    [JsonPropertyName("address")]
    public string? Address { get; set; }
    
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }
    
    [JsonPropertyName("customFields")]
    public Dictionary<string, object> CustomFields { get; set; } = new();
    
    [JsonPropertyName("branchIds")]
    public List<string> BranchIds { get; set; } = new();
    
    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }
}
