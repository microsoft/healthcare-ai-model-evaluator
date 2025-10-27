namespace MedBench.Core.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
    public string? Expertise { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Boolean IsModelReviewer { get; set; } = false;
    public string? ModelId { get; set; }//foreign key to model

    public Dictionary<string, string> Stats { get; set; } = new();

    // Local auth fields
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }
} 