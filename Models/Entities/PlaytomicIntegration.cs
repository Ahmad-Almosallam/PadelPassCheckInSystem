using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.Entities
{
    public class PlaytomicIntegration
    {
        public int Id { get; set; }

        [Required]
        [StringLength(2000)]
        public string AccessToken { get; set; }

        public DateTime AccessTokenExpiration { get; set; }

        [Required]
        [StringLength(2000)]
        public string RefreshToken { get; set; }

        public DateTime RefreshTokenExpiration { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
