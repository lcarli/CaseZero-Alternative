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
        public DbSet<ForensicRequest> ForensicRequests { get; set; }
        public DbSet<Note> Notes { get; set; }

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
                .HasForeignKey(e => e.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Email>()
                .HasOne(e => e.FromUser)
                .WithMany()
                .HasForeignKey(e => e.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

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

            // Configure ForensicRequest
            builder.Entity<ForensicRequest>()
                .HasOne(fr => fr.User)
                .WithMany()
                .HasForeignKey(fr => fr.UserId);

            // Note: Seed data moved to DataSeedingService to avoid dynamic values in migrations
            // This allows migrations to be deterministic and avoids PendingModelChangesWarning
        }
    }
}