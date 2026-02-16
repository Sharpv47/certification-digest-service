using Microsoft.EntityFrameworkCore;
using CertificationDigest.Api.Data;
using CertificationDigest.Api.Models;
using CertificationDigest.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

builder.Services.AddSingleton<SendGridEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/email/test", async (SendGridEmailSender email) =>
{
    var (status, messageId) = await email.SendAsync(
        "SendGrid Test",
        $"If you received this, SendGrid is wired up. UTC: {DateTime.UtcNow:o}");

    // Return SendGrid’s actual status (should be 202)
    return Results.StatusCode(status);
});

app.MapPost("/certifications/seed", async (AppDbContext db) =>
{
    if (await db.CertificationRecords.AnyAsync())
        return Results.Ok("Already seeded.");

    var items = new List<CertificationRecord>
    {
        new() { FullName = "John Doe", Certification = "TIPS", ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)), Notes="Sample" },
        new() { FullName = "Jane Doe", Certification = "CrowdControl", ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70)) },
        new() { FullName = "Bob Smith", Certification = "FoodService", ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) }
    };

    db.CertificationRecords.AddRange(items);
    await db.SaveChangesAsync();
    return Results.Ok("Seeded.");
});

app.MapGet("/certifications/expiring", async (int? days, AppDbContext db) =>
{
    var window = days ?? 60;
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var cutoff = today.AddDays(window);

    var list = await db.CertificationRecords
        .AsNoTracking()
        .Where(c => c.ExpiresOn >= today && c.ExpiresOn <= cutoff)
        .OrderBy(c => c.ExpiresOn)
        .ToListAsync();

    return Results.Ok(new { days = window, count = list.Count, items = list });
});

app.MapPost("/certifications/send-digest", async (int? days, AppDbContext db, SendGridEmailSender email) =>
{
    var window = days ?? 60;

    // week-based dedupe key
    var todayUtc = DateTime.UtcNow.Date;
    var week = System.Globalization.ISOWeek.GetWeekOfYear(todayUtc);
    var year = System.Globalization.ISOWeek.GetYear(todayUtc);
    var periodKey = $"{year}-W{week:D2}";
    var type = $"Digest:{window}days";

    var alreadySent = await db.NotificationLogs
        .AsNoTracking()
        .AnyAsync(n => n.NotificationType == type && n.PeriodKey == periodKey);

    if (alreadySent)
        return Results.Ok($"Digest already sent for {type} {periodKey}.");

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var cutoff = today.AddDays(window);

    static string HeaderLine(string title) => $"=== {title} ===";

    int DaysUntil(DateOnly d) => d.DayNumber - today.DayNumber;

    var expiring = await db.CertificationRecords
        .AsNoTracking()
        .Where(c => c.ExpiresOn >= today && c.ExpiresOn <= cutoff)
        .ToListAsync();

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
        new { Title = $"Expiring in 31–{window} days", Min = 31, Max = window }
    };

    var lines = new List<string>
    {
        $"PTCA Certification Digest (next {window} days)",
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

    var subject = $"PTCA Cert Digest — next {window} days — {periodKey}";
    var body = string.Join("\n", lines);

    var (status, _) = await email.SendAsync(subject, body);

    db.NotificationLogs.Add(new NotificationLog
    {
        NotificationType = type,
        PeriodKey = periodKey,
        SentAtUtc = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    return Results.StatusCode(status);
});

app.Run();