using Azure.Storage.Blobs;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Services;

public class ImageService : IImageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly IImageRepository _imageRepository;

    public ImageService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        IImageRepository imageRepository)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["AzureStorage:ImageContainer"] ?? "images";
        _imageRepository = imageRepository;
    }

    public async Task<Image> ProcessDataUrlAndSaveImageAsync(string dataUrl)
    {
        var match = Regex.Match(dataUrl, @"^data:image/(?<type>.+?);base64,(?<data>.+)$");
        
        if (!match.Success)
            throw new ArgumentException("Invalid data URL format");

        var imageType = match.Groups["type"].Value;
        var base64Data = match.Groups["data"].Value;
        var imageBytes = Convert.FromBase64String(base64Data);

        var blobName = $"{Guid.NewGuid()}.{imageType}";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(blobName);
        using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        var image = new Image
        {
            StorageAccount = _blobServiceClient.AccountName,
            Container = _containerName,
            BlobPath = blobName,
            ContentType = $"image/{imageType}",
            SizeInBytes = imageBytes.Length
        };

        return await _imageRepository.CreateAsync(image);
    }

    public async Task<Stream> GetImageStreamAsync(Image image)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(image.Container);
        var blobClient = containerClient.GetBlobClient(image.BlobPath);
        
        var response = await blobClient.DownloadAsync();
        Console.WriteLine(response.Value.Content);
        return response.Value.Content;
    }
} 