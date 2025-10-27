namespace MedBench.Core.Interfaces;

public interface IUserRepository
{
    Task<User> GetByIdAsync(string id);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    // Update only non-auth profile fields to avoid clobbering password-related fields
    Task<User> UpdateProfileAsync(User user);
    Task DeleteAsync(string id);
    Task<string?> GetUserIdByEmailAsync(string email);
    Task<User?> FindByEmailAsync(string email);
    Task<IEnumerable<User>> GetModelReviewers();
    Task<IEnumerable<User>> GetModelReviewersFromIds(IEnumerable<string> userIds);
} 