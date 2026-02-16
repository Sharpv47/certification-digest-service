using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CertificationDigest.Core.Services;

public class SendGridEmailSender
{
    private readonly IConfiguration _config;

    public SendGridEmailSender(IConfiguration config) => _config = config;

    public async Task<(int StatusCode, string? MessageId)> SendAsync(string subject, string plainTextBody)
    {
        var apiKey = _config["SendGrid:ApiKey"];
        var fromEmail = _config["SendGrid:FromEmail"];
        var fromName  = _config["SendGrid:FromName"] ?? "Cert Tracker";
        var toEmail   = _config["SendGrid:ToEmail"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(fromEmail) ||
            string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("Missing SendGrid configuration (ApiKey/FromEmail/ToEmail).");

        var client = new SendGridClient(apiKey);

        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress(fromEmail, fromName),
            new EmailAddress(toEmail),
            subject,
            plainTextBody,
            htmlContent: null);

        var response = await client.SendEmailAsync(msg);
        var status = (int)response.StatusCode;

        // SendGrid often returns 202 with empty body
        string? messageId = null;
        if (response.Headers != null && response.Headers.TryGetValues("X-Message-Id", out var vals))
            messageId = vals.FirstOrDefault();

        if (status >= 400)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            throw new InvalidOperationException($"SendGrid failed: {status} {response.StatusCode} - {errorBody}");
        }

        return (status, messageId);
    }
}
