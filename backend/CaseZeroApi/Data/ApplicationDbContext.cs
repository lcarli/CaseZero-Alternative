using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CaseZeroApi.Models;

namespace CaseZeroApi.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Case> Cases { get; set; }
        public DbSet<UserCase> UserCases { get; set; }
        public DbSet<CaseProgress> CaseProgresses { get; set; }
        public DbSet<Evidence> Evidences { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure UserCase as a join table
            builder.Entity<UserCase>()
                .HasKey(uc => new { uc.UserId, uc.CaseId });

            builder.Entity<UserCase>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.UserCases)
                .HasForeignKey(uc => uc.UserId);

            builder.Entity<UserCase>()
                .HasOne(uc => uc.Case)
                .WithMany(c => c.UserCases)
                .HasForeignKey(uc => uc.CaseId);

            // Configure CaseProgress
            builder.Entity<CaseProgress>()
                .HasOne(cp => cp.User)
                .WithMany(u => u.CaseProgresses)
                .HasForeignKey(cp => cp.UserId);

            builder.Entity<CaseProgress>()
                .HasOne(cp => cp.Case)
                .WithMany(c => c.CaseProgresses)
                .HasForeignKey(cp => cp.CaseId);

            // Configure Evidence
            builder.Entity<Evidence>()
                .HasOne(e => e.Case)
                .WithMany(c => c.Evidences)
                .HasForeignKey(e => e.CaseId);

            builder.Entity<Evidence>()
                .HasOne(e => e.CollectedByUser)
                .WithMany()
                .HasForeignKey(e => e.CollectedByUserId)
                .IsRequired(false);

            // Seed some initial cases
            builder.Entity<Case>().HasData(
                new Case
                {
                    Id = "CASE-2024-001",
                    Title = "Roubo no Banco Central",
                    Description = "Investigação de roubo milionário no Banco Central",
                    Status = CaseStatus.InProgress,
                    Priority = CasePriority.High,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new Case
                {
                    Id = "CASE-2024-002",
                    Title = "Fraude Corporativa TechCorp",
                    Description = "Suspeita de fraude contábil na empresa TechCorp",
                    Status = CaseStatus.Open,
                    Priority = CasePriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new Case
                {
                    Id = "CASE-2024-003",
                    Title = "Homicídio no Porto",
                    Description = "Investigação de homicídio na área portuária",
                    Status = CaseStatus.Resolved,
                    Priority = CasePriority.High,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    ClosedAt = DateTime.UtcNow.AddDays(-2)
                }
            );
        }
    }
}