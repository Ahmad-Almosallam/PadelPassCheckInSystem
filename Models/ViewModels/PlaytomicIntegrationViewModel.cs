using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class PlaytomicIntegrationViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Access Token")]
        public string AccessToken { get; set; }

        [Display(Name = "Access Token Expiration")]
        public DateTime AccessTokenExpiration { get; set; }

        [Required]
        [Display(Name = "Refresh Token")]
        public string RefreshToken { get; set; }

        [Display(Name = "Refresh Token Expiration")]
        public DateTime RefreshTokenExpiration { get; set; }

        public bool IsAccessTokenExpiringSoon => AccessTokenExpiration <= DateTime.UtcNow.AddMinutes(3);
        public bool IsRefreshTokenExpired => RefreshTokenExpiration <= DateTime.UtcNow;
    }

    public class PlaytomicTokenRefreshRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }

    public class PlaytomicTokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("access_token_expiration")]
        public string AccessTokenExpiration { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("refresh_token_expiration")]
        public string RefreshTokenExpiration { get; set; }
    }
}
