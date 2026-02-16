using Microsoft.EntityFrameworkCore;
using CertificationDigest.Core.Data;
using CertificationDigest.Core.Models;

namespace CertificationDigest.Core.Services;

public static class CertificationQueries
{
    public static async Task<List<CertificationRecord>> GetExpiringAsync(
        AppDbContext db,
        int windowDays,
        DateOnly todayUtc)
    {
        var cutoff = todayUtc.AddDays(windowDays);

        return await db.CertificationRecords
            .AsNoTracking()
            .Where(c => c.ExpiresOn >= todayUtc && c.ExpiresOn <= cutoff)
            .OrderBy(c => c.ExpiresOn)
            .ToListAsync();
    }
}
