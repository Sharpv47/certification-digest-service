using CertificationDigest.Core.Data;
using CertificationDigest.Core.Models;
using CertificationDigest.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const int DefaultWindowDays = 60;

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

// Services
builder.Services.AddTransient<SendGridEmailSender>(); // safer default than Singleton
builder.Services.AddTransient<DigestService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/email/test", async (SendGridEmailSender email) =>
{
    var (status, _) = await email.SendAsync(
        "SendGrid Test",
        $"If you received this, SendGrid is wired up. UTC: {DateTime.UtcNow:o}");

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
    var window = days ?? DefaultWindowDays;
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var list = await CertificationQueries.GetExpiringAsync(db, window, today);

    return Results.Ok(new { days = window, count = list.Count, items = list });
});

app.MapPost("/certifications/send-digest", async (int? days, DigestService digest) =>
{
    var window = days ?? DefaultWindowDays;

    var result = await digest.SendDigestAsync(window);

    if (result.AlreadySent)
        return Results.Ok($"Digest already sent for Digest:{window}days {result.PeriodKey}.");

    return Results.StatusCode(result.SendGridStatus);
});

app.Run();