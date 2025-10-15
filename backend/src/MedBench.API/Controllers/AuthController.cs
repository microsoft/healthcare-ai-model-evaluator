using MedBench.Core.Interfaces;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ILocalAuthService auth, IUserRepository users, IEmailService email, IConfiguration config) : ControllerBase
{
    private readonly ILocalAuthService _auth = auth;
    private readonly IUserRepository _users = users;
    private readonly IEmailService _email = email;
    private readonly IConfiguration _config = config;

    public record LoginRequest(string Email, string Password);
    public record UserDto(string Id, string Name, string Email, List<string> Roles, string? Expertise, bool IsModelReviewer, string? ModelId)
    {
        public static UserDto From(MedBench.Core.Models.User u) => new(u.Id, u.Name, u.Email, u.Roles ?? new List<string>(), u.Expertise, u.IsModelReviewer, u.ModelId);
    }
    public record LoginResponse(string Token, UserDto User);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);
    public record AdminInitiateResetRequest(string Email, string? Organization);
    public record AdminSetPasswordRequest(string Email, string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        try
        {
            var (token, user) = await _auth.LoginAsync(req.Email, req.Password);
            return Ok(new LoginResponse(token, UserDto.From(user)));
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[AuthController] Login failed for {req.Email}: {ex.Message}");
            return Unauthorized(new { message = "Invalid credentials" });
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var enabled = string.Equals(_config["Email:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            // behave as success to avoid leaking policy
            return Ok();
        }
        await _auth.RequestPasswordResetAsync(req.Email);
        return Ok();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        try
        {
            await _auth.ResetPasswordAsync(req.Token, req.NewPassword);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            return Unauthorized();
        }
    }

    [HttpGet("me")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<UserDto>> Me()
    {
        // Try to resolve user by userId claim
        Console.WriteLine("[AuthController] Fetching current user info", System.Security.Claims.ClaimTypes.NameIdentifier);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        try
        {
            var user = await _users.GetByIdAsync(userId);
            return Ok(UserDto.From(user));
        }
        catch
        {
            return Unauthorized();
        }
    }

    [HttpPost("admin/initiate-reset")]
    [Authorize(Policy = "RequireAdministratorRole")]
    public async Task<IActionResult> AdminInitiateReset([FromBody] AdminInitiateResetRequest req)
    {
        var enabled = string.Equals(_config["Email:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            return BadRequest(new { message = "Email is disabled" });
        }
        var user = await _users.FindByEmailAsync(req.Email);
        if (user == null)
        {
            // don't leak existence
            return Ok();
        }
        user.PasswordResetToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);
        await _users.UpdateAsync(user);

        var webBaseUrl = _config["Web:BaseUrl"] ?? _config["Frontend:BaseUrl"] ?? _config["StaticWebApp:BaseUrl"];
        var link = !string.IsNullOrEmpty(webBaseUrl)
            ? $"{webBaseUrl.TrimEnd('/')}/reset-password?resetToken={Uri.EscapeDataString(user.PasswordResetToken)}&email={Uri.EscapeDataString(user.Email)}"
            : $"/reset-password?resetToken={Uri.EscapeDataString(user.PasswordResetToken)}&email={Uri.EscapeDataString(user.Email)}";

        var org = string.IsNullOrWhiteSpace(req.Organization) ? "your organization" : req.Organization!.Trim();
        await _email.SendAdminInitiatedPasswordSetupEmailAsync(user.Email, link, org);
        return Ok();
    }

    [HttpPost("admin/set-password")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> AdminSetPassword([FromBody] AdminSetPasswordRequest req)
    {
        try
        {
            await _auth.SetPasswordForUserAsync(req.Email, req.NewPassword);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult Config()
    {
        var emailEnabled = string.Equals(_config["Email:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        return Ok(new { emailEnabled });
    }
}
