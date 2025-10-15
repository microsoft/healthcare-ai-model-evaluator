using MedBench.Core.Models;

namespace MedBench.Core.Interfaces;

public interface ILocalAuthService
{
    Task<(string token, MedBench.Core.Models.User user)> LoginAsync(string email, string password);
    Task RequestPasswordResetAsync(string email);
    Task ResetPasswordAsync(string token, string newPassword);
    Task SetPasswordForUserAsync(string email, string newPassword);
    string HashPassword(string password, out string salt);
    bool VerifyPassword(string password, string salt, string hash);
}
