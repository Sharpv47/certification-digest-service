using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Models;

namespace Portfolio.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CertificationRecord> CertificationRecords => Set<CertificationRecord>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
}
