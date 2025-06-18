using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<EndUser> EndUsers { get; set; }
        public DbSet<CheckIn> CheckIns { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
        }
    }
}
