using CaseZeroApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class MigrationModelTests
{
    [Fact]
    public void ReconciledSnapshotAndLatestMigration_ContainCareerAndForensicModels()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=model_metadata_only;Integrated Security=true")
            .Options);

        Assert.False(context.Database.HasPendingModelChanges());
        var migrations = context.GetService<IMigrationsAssembly>();
        var latest = migrations.CreateMigration(migrations.Migrations.Last().Value, context.Database.ProviderName!);
        Assert.Equal(
            migrations.ModelSnapshot!.Model.ToDebugString(MetadataDebugStringOptions.LongDefault),
            latest.TargetModel.ToDebugString(MetadataDebugStringOptions.LongDefault));
    }
}
