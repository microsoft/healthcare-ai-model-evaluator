using System.Security.Claims;

namespace MedBench.Core.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return userId;
    }
} 