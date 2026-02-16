using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CertificationDigest.Api.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Missing ConnectionStrings:Default. Add it to appsettings.Development.json.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(conn, sql => sql.EnableRetryOnFailure())
            .Options;

        return new AppDbContext(options);
    }
}