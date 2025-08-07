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
        public DbSet<ForensicAnalysis> ForensicAnalyses { get; set; }
        public DbSet<CaseSubmission> CaseSubmissions { get; set; }
        public DbSet<Suspect> Suspects { get; set; }
        public DbSet<Email> Emails { get; set; }
        public DbSet<CaseSession> CaseSessions { get; set; }

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

            // Configure ForensicAnalysis
            builder.Entity<ForensicAnalysis>()
                .HasOne(fa => fa.Evidence)
                .WithMany(e => e.ForensicAnalyses)
                .HasForeignKey(fa => fa.EvidenceId);

            builder.Entity<ForensicAnalysis>()
                .HasOne(fa => fa.RequestedByUser)
                .WithMany(u => u.ForensicAnalysesRequested)
                .HasForeignKey(fa => fa.RequestedByUserId);

            // Configure CaseSubmission
            builder.Entity<CaseSubmission>()
                .HasOne(cs => cs.Case)
                .WithMany(c => c.CaseSubmissions)
                .HasForeignKey(cs => cs.CaseId);

            builder.Entity<CaseSubmission>()
                .HasOne(cs => cs.SubmittedByUser)
                .WithMany(u => u.CaseSubmissions)
                .HasForeignKey(cs => cs.SubmittedByUserId);

            builder.Entity<CaseSubmission>()
                .HasOne(cs => cs.EvaluatedByUser)
                .WithMany(u => u.CaseSubmissionsEvaluated)
                .HasForeignKey(cs => cs.EvaluatedByUserId)
                .IsRequired(false);

            // Configure Suspect
            builder.Entity<Suspect>()
                .HasOne(s => s.Case)
                .WithMany(c => c.Suspects)
                .HasForeignKey(s => s.CaseId);

            builder.Entity<Suspect>()
                .HasOne(s => s.AddedByUser)
                .WithMany()
                .HasForeignKey(s => s.AddedByUserId)
                .IsRequired(false);

            // Configure Email
            builder.Entity<Email>()
                .HasOne(e => e.ToUser)
                .WithMany()
                .HasForeignKey(e => e.ToUserId);

            builder.Entity<Email>()
                .HasOne(e => e.FromUser)
                .WithMany()
                .HasForeignKey(e => e.FromUserId);

            builder.Entity<Email>()
                .HasOne(e => e.Case)
                .WithMany()
                .HasForeignKey(e => e.CaseId)
                .IsRequired(false);

            // Configure CaseSession
            builder.Entity<CaseSession>()
                .HasOne(cs => cs.User)
                .WithMany()
                .HasForeignKey(cs => cs.UserId);

            // Seed some initial cases with GDD enhancements
            builder.Entity<Case>().HasData(
                new Case
                {
                    Id = "CASE-2024-001",
                    Title = "Roubo no Banco Central",
                    Description = "Investigação de roubo milionário no Banco Central",
                    Status = CaseStatus.InProgress,
                    Priority = CasePriority.High,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    Type = CaseType.Investigation,
                    MinimumRankRequired = DetectiveRank.Detective2,
                    Location = "Banco Central - Centro da Cidade",
                    IncidentDate = DateTime.UtcNow.AddDays(-8),
                    BriefingText = "Roubo milionário ocorreu durante a madrugada. Sistema de segurança foi comprometido de forma sofisticada.",
                    HasMultipleSuspects = true,
                    EstimatedDifficultyLevel = 7,
                    CorrectSuspectName = "Marcus Silva",
                    MaxScore = 100.0
                },
                new Case
                {
                    Id = "CASE-2024-002",
                    Title = "Fraude Corporativa TechCorp",
                    Description = "Suspeita de fraude contábil na empresa TechCorp",
                    Status = CaseStatus.Open,
                    Priority = CasePriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    Type = CaseType.Investigation,
                    MinimumRankRequired = DetectiveRank.Detective,
                    Location = "TechCorp Headquarters",
                    IncidentDate = DateTime.UtcNow.AddDays(-10),
                    BriefingText = "Auditoria interna detectou irregularidades nos relatórios financeiros dos últimos 2 anos.",
                    HasMultipleSuspects = true,
                    EstimatedDifficultyLevel = 5,
                    CorrectSuspectName = "Ana Rodriguez",
                    MaxScore = 100.0
                },
                new Case
                {
                    Id = "CASE-2024-003",
                    Title = "Homicídio no Porto",
                    Description = "Investigação de homicídio na área portuária",
                    Status = CaseStatus.Resolved,
                    Priority = CasePriority.High,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    ClosedAt = DateTime.UtcNow.AddDays(-2),
                    Type = CaseType.Investigation,
                    MinimumRankRequired = DetectiveRank.Sergeant,
                    Location = "Porto da Cidade - Cais 7",
                    IncidentDate = DateTime.UtcNow.AddDays(-31),
                    BriefingText = "Corpo encontrado no cais 7 durante patrulha noturna. Indícios de luta e possível execução.",
                    HasMultipleSuspects = true,
                    EstimatedDifficultyLevel = 8,
                    CorrectSuspectName = "Roberto Santos",
                    MaxScore = 100.0
                }
            );
        }
    }
}