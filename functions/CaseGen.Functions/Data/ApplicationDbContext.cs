using Microsoft.EntityFrameworkCore;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CaseSessionVisibleEmails> CaseSessionVisibleEmails { get; set; } = null!;
    public DbSet<ForensicRequest> ForensicRequests { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CaseSessionVisibleEmails>(entity =>
        {
            entity.ToTable("CaseSessionVisibleEmails");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<ForensicRequest>(entity =>
        {
            entity.ToTable("ForensicRequests");
            entity.HasKey(e => e.Id);
        });
    }
}
