using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PadelPassCheckInSystem.Integration.Rekaz.Enums;

namespace PadelPassCheckInSystem.Models.Entities;

public class EndUserSubscription
{
    public int Id { get; set; }

    [Required] public Guid RekazId { get; set; }

    [Required]
    [ForeignKey(nameof(EndUser))]
    public int EndUserId { get; set; }

    [Required] public DateTime StartDate { get; set; }
    [Required] public DateTime EndDate { get; set; }
    [Required] public SubscriptionStatus Status { get; set; }
    [MaxLength(100)] public string Name { get; set; }
    [Required] public decimal Price { get; set; }
    [Required] public decimal Discount { get; set; }
    [Required] public bool IsPaused { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }

    public virtual EndUser EndUser { get; set; }
}