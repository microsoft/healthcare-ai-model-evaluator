using MedBench.Core.Cosmos;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly Container _container;

    public ImageRepository(CosmosContainerProvider containerProvider)
    {
        _container = containerProvider.GetContainer("Images");
    }

    public async Task<Image> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Image>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Image with ID {id} not found.");
        }
    }

    public async Task<Image> CreateAsync(Image image)
    {
        if (string.IsNullOrWhiteSpace(image.Id))
        {
            image.Id = Guid.NewGuid().ToString();
        }

        await _container.CreateItemAsync(image, new PartitionKey(image.Id));
        return image;
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<Image>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Image with ID {id} not found.");
        }
    }
} 