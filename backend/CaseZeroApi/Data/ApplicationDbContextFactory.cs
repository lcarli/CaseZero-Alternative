using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CaseZeroApi.Data;

namespace CaseZeroApi
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            
            // Get connection string from environment variable or use default
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback to LocalDB for local development
                connectionString = "Server=(localdb)\\mssqllocaldb;Database=casezero-db;Trusted_Connection=True;MultipleActiveResultSets=true";
            }
            
            // Always use SQL Server
            optionsBuilder.UseSqlServer(connectionString);
            
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
