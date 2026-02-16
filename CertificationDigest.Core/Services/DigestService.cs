using Microsoft.EntityFrameworkCore;
using CertificationDigest.Core.Data;
using CertificationDigest.Core.Models;

namespace CertificationDigest.Core.Services;

public class DigestService
{
    private readonly AppDbContext _db;
    private readonly SendGridEmailSender _email;

    public DigestService(AppDbContext db, SendGridEmailSender email)
    {
        _db = db;
        _email = email;
    }

    public record DigestResult(bool AlreadySent, int WindowDays, string PeriodKey, int ExpiringCount, int SendGridStatus);

    public async Task<DigestResult> SendDigestAsync(int windowDays)
    {
        // week-based dedupe key (UTC)
        var todayUtcDate = DateTime.UtcNow.Date;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(todayUtcDate);
        var year = System.Globalization.ISOWeek.GetYear(todayUtcDate);
        var periodKey = $"{year}-W{week:D2}";
        var type = $"Digest:{windowDays}days";

        var alreadySent = await _db.NotificationLogs
            .AsNoTracking()
            .AnyAsync(n => n.NotificationType == type && n.PeriodKey == periodKey);

        if (alreadySent)
            return new DigestResult(true, windowDays, periodKey, 0, 200);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        int DaysUntil(DateOnly d) => d.DayNumber - today.DayNumber;

        var expiring = await CertificationQueries.GetExpiringAsync(_db, windowDays, today);

        var expiringWithDays = expiring
            .Select(c => new
            {
                c.FullName,
                c.Certification,
                c.ExpiresOn,
                DaysLeft = DaysUntil(c.ExpiresOn),
                c.Notes
            })
            .OrderBy(x => x.DaysLeft)
            .ThenBy(x => x.FullName)
            .ToList();

        var buckets = new[]
        {
            new { Title = "Expiring in 0–14 days", Min = 0,  Max = 14 },
            new { Title = "Expiring in 15–30 days", Min = 15, Max = 30 },
            new { Title = $"Expiring in 31–{windowDays} days", Min = 31, Max = windowDays }
        };

        static string HeaderLine(string title) => $"=== {title} ===";

        var lines = new List<string>
        {
            $"PTCA Certification Digest (next {windowDays} days)",
            $"Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            "",
            $"Total expiring: {expiringWithDays.Count}",
            ""
        };

        if (expiringWithDays.Count == 0)
        {
            lines.Add("No certifications expiring in this window.");
        }
        else
        {
            foreach (var b in buckets)
            {
                var bucketItems = expiringWithDays
                    .Where(x => x.DaysLeft >= b.Min && x.DaysLeft <= b.Max)
                    .ToList();

                lines.Add(HeaderLine(b.Title));

                if (bucketItems.Count == 0)
                {
                    lines.Add("  (none)");
                }
                else
                {
                    foreach (var x in bucketItems)
                    {
                        var note = string.IsNullOrWhiteSpace(x.Notes) ? "" : $" — {x.Notes}";
                        lines.Add($"  {x.DaysLeft,3}d | {x.ExpiresOn:yyyy-MM-dd} | {x.FullName} | {x.Certification}{note}");
                    }
                }

                lines.Add("");
            }

            lines.Add("Note: This is an automated digest. If multiple members are expiring soon, consider scheduling a renewal class.");
        }

        var subject = $"PTCA Cert Digest — next {windowDays} days — {periodKey}";
        var body = string.Join("\n", lines);

        var (status, _) = await _email.SendAsync(subject, body);

        _db.NotificationLogs.Add(new NotificationLog
        {
            NotificationType = type,
            PeriodKey = periodKey,
            SentAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return new DigestResult(false, windowDays, periodKey, expiringWithDays.Count, status);
    }
}
