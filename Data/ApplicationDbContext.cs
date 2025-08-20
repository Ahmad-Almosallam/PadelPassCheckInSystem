using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<EndUser> EndUsers { get; set; }
        public DbSet<CheckIn> CheckIns { get; set; }
        public DbSet<SubscriptionPause> SubscriptionPauses { get; set; }
        public DbSet<BranchTimeSlot> BranchTimeSlots { get; set; }
        public DbSet<PlaytomicIntegration> PlaytomicIntegrations { get; set; }

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Set schema for all Identity tables
            builder.HasDefaultSchema("access");

            // Set schema for Identity tables
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                builder.Entity(entityType.ClrType)
                    .ToTable(entityType.GetTableName(), "access");
            }
            
            builder.Entity<CheckIn>()
                .Property(x => x.PlayerAttended)
                .HasDefaultValue(true);

            builder.Entity<EndUser>()
                .Property(x => x.IsStoppedByWarning)
                .HasDefaultValue(false);
            

            // Unique constraint on EndUser phone number
            builder.Entity<EndUser>()
                .HasIndex(e => e.PhoneNumber)
                .IsUnique();

            // Unique constraint on EndUser unique identifier
            builder.Entity<EndUser>()
                .HasIndex(e => e.UniqueIdentifier)
                .IsUnique();

            // Composite index for check-in validation
            builder.Entity<CheckIn>()
                .HasIndex(c => new { c.EndUserId, c.CheckInDateTime })
                .HasDatabaseName("IX_CheckIn_EndUser_Date");

            // Configure relationships
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Branch)
                .WithMany(b => b.BranchUsers)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.SetNull);

            // SubscriptionPause relationships
            builder.Entity<SubscriptionPause>()
                .HasOne(sp => sp.EndUser)
                .WithMany(eu => eu.SubscriptionPauses)
                .HasForeignKey(sp => sp.EndUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SubscriptionPause>()
                .HasOne(sp => sp.CreatedByUser)
                .WithMany()
                .HasForeignKey(sp => sp.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // BranchTimeSlot relationships
            builder.Entity<BranchTimeSlot>()
                .HasOne(bts => bts.Branch)
                .WithMany(b => b.TimeSlots)
                .HasForeignKey(bts => bts.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for time slot queries
            builder.Entity<BranchTimeSlot>()
                .HasIndex(bts => new { bts.BranchId, bts.DayOfWeek, bts.IsActive })
                .HasDatabaseName("IX_BranchTimeSlot_Branch_Day_Active");

            // Index for subscription pause queries
            builder.Entity<SubscriptionPause>()
                .HasIndex(sp => new { sp.EndUserId, sp.IsActive })
                .HasDatabaseName("IX_SubscriptionPause_EndUser_Active");

            // Index for check-in court assignment
            builder.Entity<CheckIn>()
                .HasIndex(c => new { c.BranchId, c.CheckInDateTime })
                .HasDatabaseName("IX_CheckIn_Branch_DateTime");

            // Configure decimal precision for time-related calculations
            
            builder.Entity<CheckIn>()
                .Property(c => c.PlayDuration)
                .HasColumnType("time");
        }
    }
}