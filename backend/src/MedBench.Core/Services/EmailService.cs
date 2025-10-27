using System.Net;
using System.Net.Mail;
using Azure;
using Azure.Core;
using Azure.Communication.Email;
using MedBench.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MedBench.Core.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly EmailClient? _acsClient;
    private readonly string _from;

    public EmailService(IConfiguration config)
    {
        _config = config;
        _from = _config["Email:From"] ?? "no-reply@medbench.local";
        var acsConn = _config["Email:Acs:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(acsConn))
        {
            try { _acsClient = new EmailClient(acsConn!); }
            catch { _acsClient = null; }
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
    var from = _from;
    var subject = "Reset your MedBench password";
    var body = $"Click the link below to reset your password:\n\n{resetLink}\n\nIf you did not request this, please ignore this email.";
    if (await TrySendWithAcsAsync(from, toEmail, subject, body)) return;
        var smtpHost = _config["Email:Smtp:Host"];
        var smtpPort = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 587;
        var smtpUser = _config["Email:Smtp:User"];
        var smtpPass = _config["Email:Smtp:Pass"];
        var useSsl = bool.TryParse(_config["Email:Smtp:UseSsl"], out var ssl) ? ssl : true;

        if (string.IsNullOrEmpty(smtpHost))
        {
            // Fallback: no SMTP configured, just log
            Console.WriteLine($"[EmailService] Reset link for {toEmail}: {resetLink}");
            return;
        }

        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = useSsl
        };
        if (!string.IsNullOrEmpty(smtpUser))
        {
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);
        }

        await client.SendMailAsync(message);
    }

    public async Task SendAdminInitiatedPasswordSetupEmailAsync(string toEmail, string resetLink, string organization)
    {
    var from = _from;
    var subject = $"{organization} Admin Request: Set your MedBench password";
    var body = $"An administrator at {organization} has requested that you set a password for your MedBench account.\n\n" +
           $"Use the link below to create your password:\n\n{resetLink}\n\n" +
           "If you did not expect this, you can ignore this email.";
    if (await TrySendWithAcsAsync(from, toEmail, subject, body)) return;
        var smtpHost = _config["Email:Smtp:Host"];
        var smtpPort = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 587;
        var smtpUser = _config["Email:Smtp:User"];
        var smtpPass = _config["Email:Smtp:Pass"];
        var useSsl = bool.TryParse(_config["Email:Smtp:UseSsl"], out var ssl) ? ssl : true;

        if (string.IsNullOrEmpty(smtpHost))
        {
            Console.WriteLine($"[EmailService] Admin-initiated set-password link for {toEmail}: {resetLink}");
            return;
        }

    using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = useSsl
        };
        if (!string.IsNullOrEmpty(smtpUser))
        {
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);
        }

        await client.SendMailAsync(message);
    }
    private async Task<bool> TrySendWithAcsAsync(string from, string to, string subject, string body)
    {
        if (_acsClient is null) return false;
        try
        {
            var content = new EmailContent(subject)
            {
                PlainText = body
            };
            var recipients = new List<EmailAddress> { new EmailAddress(to) };
            var message = new EmailMessage(from, new EmailRecipients(recipients), content);
            var operation = await _acsClient.SendAsync(WaitUntil.Completed, message);
            Console.WriteLine($"[EmailService] ACS email sent, status: {operation.Value.Status}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] ACS send failed, will fall back to SMTP if configured: {ex.Message}");
            return false;
        }
    }
}
