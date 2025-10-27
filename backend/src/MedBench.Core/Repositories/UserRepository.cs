using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;

namespace MedBench.Core.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _users;

        public UserRepository(IMongoCollection<User> users)
        {
            _users = users;

            // Create indexes for common queries
            
            // Index for email lookup - critical for authentication
            var emailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
            _users.Indexes.CreateOne(new CreateIndexModel<User>(emailIndex));

            // Index for model reviewers query
            var modelReviewerIndex = Builders<User>.IndexKeys.Ascending(u => u.IsModelReviewer);
            _users.Indexes.CreateOne(new CreateIndexModel<User>(modelReviewerIndex));

            // Compound index for model reviewers with ModelId
            var modelReviewerWithIdIndex = Builders<User>.IndexKeys
                .Ascending(u => u.IsModelReviewer)
                .Ascending(u => u.ModelId);
            _users.Indexes.CreateOne(new CreateIndexModel<User>(modelReviewerWithIdIndex));
        }

        public async Task<User> GetByIdAsync(string id)
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with ID {id} not found");
            return user;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            // normalize
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                user.Email = user.Email.Trim().ToLowerInvariant();
            }
            user.UpdatedAt = DateTime.UtcNow;
            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            // Full replace: expect caller to preserve sensitive fields
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                user.Email = user.Email.Trim().ToLowerInvariant();
            }
            user.UpdatedAt = DateTime.UtcNow;
            var result = await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
            if (result.ModifiedCount == 0)
                throw new KeyNotFoundException($"User with ID {user.Id} not found");
            return user;
        }

        public async Task<User> UpdateProfileAsync(User user)
        {
            // Update only non-auth fields to avoid overwriting password hash/salt inadvertently
            var update = Builders<User>.Update
                .Set(u => u.Name, user.Name)
                .Set(u => u.Email, string.IsNullOrWhiteSpace(user.Email) ? user.Email : user.Email.Trim().ToLowerInvariant())
                .Set(u => u.Roles, user.Roles ?? new List<string>())
                .Set(u => u.Expertise, user.Expertise)
                .Set(u => u.IsModelReviewer, user.IsModelReviewer)
                .Set(u => u.ModelId, user.ModelId)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _users.UpdateOneAsync(x => x.Id == user.Id, update);
            if (result.MatchedCount == 0)
                throw new KeyNotFoundException($"User with ID {user.Id} not found");

            return await GetByIdAsync(user.Id);
        }

        public async Task DeleteAsync(string id)
        {
            var result = await _users.DeleteOneAsync(u => u.Id == id);
            if (result.DeletedCount == 0)
                throw new KeyNotFoundException($"User with ID {id} not found");
        }

        public async Task<string?> GetUserIdByEmailAsync(string email)
        {
            var norm = email?.Trim().ToLowerInvariant();
            var user = await _users.Find(x => x.Email == norm).FirstOrDefaultAsync();
            return user?.Id;
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            var norm = email?.Trim().ToLowerInvariant();
            return await _users.Find(x => x.Email == norm).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<User>> GetModelReviewers()
        {
            return await _users.Find(u => u.IsModelReviewer).ToListAsync();
        }

        public async Task<IEnumerable<User>> GetModelReviewersFromIds(IEnumerable<string> userIds)
        {
            return await _users.Find(u => 
                userIds.Contains(u.Id) && 
                u.IsModelReviewer && 
                !string.IsNullOrEmpty(u.ModelId)
            ).ToListAsync();
        }
    }
} 