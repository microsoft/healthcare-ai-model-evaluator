using MedBench.Core.Interfaces;
using System.Security.Claims;

namespace MedBench.API.Middleware;

public class UserIdMiddleware
{
    private readonly RequestDelegate _next;

    public UserIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
    {
        // Print all claims
        //Console.WriteLine("Claims: " + string.Join(", ", context.User.Claims.Select(c => $"{c.Type}: {c.Value}")));

    var email = context.User.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
            ?? context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
       
        
        if (!string.IsNullOrEmpty(email))
        {
            var userId = await userRepository.GetUserIdByEmailAsync(email);
            
            
            if (!string.IsNullOrEmpty(userId))
            {
                var identity = context.User.Identity as ClaimsIdentity;
                // Remove existing NameIdentifier claim if it exists
                var existingClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (existingClaim != null)
                {
                
                    identity?.RemoveClaim(existingClaim);
                }
                // Add MongoDB user ID as NameIdentifier claim
                identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            
            }
        }

        await _next(context);
    }
} 