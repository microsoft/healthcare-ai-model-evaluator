namespace MedBench.Core.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendAdminInitiatedPasswordSetupEmailAsync(string toEmail, string resetLink, string organization);
}
