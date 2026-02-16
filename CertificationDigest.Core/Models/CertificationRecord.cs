namespace CertificationDigest.Core.Models;

public class CertificationRecord
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Certification { get; set; } = string.Empty;

    public DateOnly ExpiresOn { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
