using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CMCS.Models
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<ClaimItem> ClaimItems { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Approval> Approvals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User (legacy table)
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserId);

            // Configure Claim - Lecturer relationship (using legacy User table)
            modelBuilder.Entity<Claim>()
                .HasOne(c => c.Lecturer)
                .WithMany(u => u.Claims)
                .HasForeignKey(c => c.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Claim - ApprovedByUser relationship (using legacy User table)
            modelBuilder.Entity<Claim>()
                .HasOne(c => c.ApprovedByUser)
                .WithMany()
                .HasForeignKey(c => c.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Configure ClaimItem - Claim relationship
            modelBuilder.Entity<ClaimItem>()
                .HasOne(ci => ci.Claim)
                .WithMany(c => c.ClaimItems)
                .HasForeignKey(ci => ci.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Document - Claim relationship
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Claim)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Approval - Claim relationship
            modelBuilder.Entity<Approval>()
                .HasOne(a => a.Claim)
                .WithMany(c => c.Approvals)
                .HasForeignKey(a => a.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Approval - Approver relationship (using legacy User table)
            modelBuilder.Entity<Approval>()
                .HasOne(a => a.Approver)
                .WithMany(u => u.Approvals)
                .HasForeignKey(a => a.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}