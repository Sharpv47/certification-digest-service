using Microsoft.EntityFrameworkCore;
using CertificationDigest.Api.Models;

namespace CertificationDigest.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CertificationRecord> CertificationRecords => Set<CertificationRecord>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
}
