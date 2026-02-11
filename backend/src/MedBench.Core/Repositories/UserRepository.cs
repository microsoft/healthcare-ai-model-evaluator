using MedBench.Core.Cosmos;
using MedBench.Core.Interfaces;
using Microsoft.Azure.Cosmos;
using UserModel = MedBench.Core.Models.User;

namespace MedBench.Core.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Container _container;

    public UserRepository(CosmosContainerProvider containerProvider)
    {
        _container = containerProvider.GetContainer("Users");
    }

    public async Task<UserModel> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<UserModel>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }
    }

    public async Task<IEnumerable<UserModel>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<UserModel>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<UserModel> CreateAsync(UserModel user)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            user.Id = Guid.NewGuid().ToString();
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _container.CreateItemAsync(user, new PartitionKey(user.Id));
        return user;
    }

    public async Task<UserModel> UpdateAsync(UserModel user)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new ArgumentException("User must have an Id", nameof(user));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
        }

        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _container.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));
            return user;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"User with ID {user.Id} not found");
        }
    }

    public async Task<UserModel> UpdateProfileAsync(UserModel user)
    {
        var existing = await GetByIdAsync(user.Id);

        existing.Name = user.Name;
        existing.Email = string.IsNullOrWhiteSpace(user.Email) ? user.Email : user.Email.Trim().ToLowerInvariant();
        existing.Roles = user.Roles ?? new List<string>();
        existing.Expertise = user.Expertise;
        existing.IsModelReviewer = user.IsModelReviewer;
        existing.ModelId = user.ModelId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _container.ReplaceItemAsync(existing, existing.Id, new PartitionKey(existing.Id));
        return existing;
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<UserModel>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }
    }

    public async Task<string?> GetUserIdByEmailAsync(string email)
    {
        var norm = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(norm)) return null;

        var id = await CosmosQueryHelpers.QuerySingleOrDefaultAsync<string>(
            _container,
            new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.Email = @email")
                .WithParameter("@email", norm));

        return id;
    }

    public async Task<UserModel?> FindByEmailAsync(string email)
    {
        var norm = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(norm)) return null;

        return await CosmosQueryHelpers.QuerySingleOrDefaultAsync<UserModel>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE c.Email = @email")
                .WithParameter("@email", norm));
    }

    public async Task<IEnumerable<UserModel>> GetModelReviewers()
    {
        return await CosmosQueryHelpers.QueryAsync<UserModel>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE c.IsModelReviewer = true"));
    }

    public async Task<IEnumerable<UserModel>> GetModelReviewersFromIds(IEnumerable<string> userIds)
    {
        var ids = userIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0) return Array.Empty<UserModel>();

        return await CosmosQueryHelpers.QueryAsync<UserModel>(
            _container,
            new QueryDefinition(
                    "SELECT * FROM c " +
                    "WHERE c.IsModelReviewer = true " +
                    "AND IS_DEFINED(c.ModelId) AND c.ModelId != '' " +
                    "AND ARRAY_CONTAINS(@ids, c.id)")
                .WithParameter("@ids", ids));
    }
}