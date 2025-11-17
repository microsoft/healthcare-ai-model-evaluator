using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MedBench.Core.Services;

public class LocalAuthService : ILocalAuthService
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;

    public LocalAuthService(IUserRepository users, IConfiguration config, IEmailService email)
    {
        _users = users;
        _config = config;
        _email = email;
    }

    public async Task<(string token, User user)> LoginAsync(string email, string password)
    {
        var normEmail = email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(normEmail);
        Console.WriteLine($"[LocalAuth] Login attempt for {normEmail}");
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordSalt) || string.IsNullOrWhiteSpace(user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials not found");

        if (!VerifyPassword(password, user.PasswordSalt!, user.PasswordHash!))
            throw new UnauthorizedAccessException("Invalid credentials invalid");

        var token = GenerateJwt(user);
        return (token, user);
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var normEmail = email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(normEmail);
        if (user == null) return; // don't leak existence

        user.PasswordResetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
        await _users.UpdateAsync(user);
        var webBaseUrl = _config["Web:BaseUrl"] ?? _config["Frontend:BaseUrl"] ?? _config["StaticWebApp:BaseUrl"];
        if (!string.IsNullOrEmpty(webBaseUrl))
        {
            var link = $"{webBaseUrl.TrimEnd('/')}/reset-password?resetToken={Uri.EscapeDataString(user.PasswordResetToken)}";
            await _email.SendPasswordResetEmailAsync(user.Email, link);
        }
        else
        {
            Console.WriteLine($"[LocalAuth] Reset link for {user.Email}: /reset-password?resetToken={user.PasswordResetToken}");
        }
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Invalid token");
        // Normalize token variants (space to +, base64url to base64, add padding)
        string NormalizeToBase64(string t)
        {
            var s = t.Trim().Replace(' ', '+').Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return s;
        }

        var candidate = token.Trim();
        var normalized = NormalizeToBase64(candidate);

        var all = await _users.GetAllAsync();
        var user = all.FirstOrDefault(u =>
            u.PasswordResetExpires > DateTime.UtcNow &&
            (string.Equals(u.PasswordResetToken, candidate, StringComparison.Ordinal) ||
             string.Equals(u.PasswordResetToken, normalized, StringComparison.Ordinal))
        );
        if (user == null) throw new ArgumentException("Invalid or expired token");

        ValidatePasswordComplexity(newPassword);

        user.PasswordHash = HashPassword(newPassword, out var salt);
        user.PasswordSalt = salt;
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;
        await _users.UpdateAsync(user);
    }

    public async Task SetPasswordForUserAsync(string email, string newPassword)
    {
        var normEmail = email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(normEmail);
        if (user == null) throw new ArgumentException("User not found");
        ValidatePasswordComplexity(newPassword);
        user.PasswordHash = HashPassword(newPassword, out var salt);
        user.PasswordSalt = salt;
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;
        await _users.UpdateAsync(user);
    }

    public string HashPassword(string password, out string salt)
    {
        salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        using var deriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(deriveBytes.GetBytes(32));
    }

    public bool VerifyPassword(string password, string salt, string hash)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256);
        var computed = Convert.ToBase64String(deriveBytes.GetBytes(32));
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hash), Convert.FromBase64String(computed));
    }

    private string GenerateJwt(User user)
    {
        var secret = _config["LocalAuth:JwtSecret"] ?? throw new InvalidOperationException("Missing LocalAuth:JwtSecret");
        var issuer = _config["LocalAuth:Issuer"] ?? "haime-local";
        var audience = _config["LocalAuth:Audience"] ?? "haime-api";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim("auth_provider", "local")
        };
        foreach (var role in user.Roles ?? new List<string>())
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        int categories = 0;
        if (password.Any(char.IsLower)) categories++;
        if (password.Any(char.IsUpper)) categories++;
        if (password.Any(char.IsDigit)) categories++;
        if (password.Any(ch => !char.IsLetterOrDigit(ch))) categories++;
        if (categories < 3)
            throw new ArgumentException("Password must include 3 of 4: upper, lower, number, symbol.");
    }
}
