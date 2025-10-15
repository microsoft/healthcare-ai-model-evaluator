using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly IMongoCollection<Image> _images;

    public ImageRepository(IMongoClient mongoClient, IConfiguration configuration)
    {
        var database = mongoClient.GetDatabase(configuration["CosmosDb:DatabaseName"]);
        _images = database.GetCollection<Image>("Images");
    }

    public async Task<Image> GetByIdAsync(string id)
    {
        var image = await _images.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (image == null)
            throw new KeyNotFoundException($"Image with ID {id} not found.");
        return image;
    }

    public async Task<Image> CreateAsync(Image image)
    {
        await _images.InsertOneAsync(image);
        return image;
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _images.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"Image with ID {id} not found.");
    }
} 