namespace MedBench.Core.Interfaces;

public interface IImageService
{
    Task<Image> ProcessDataUrlAndSaveImageAsync(string dataUrl);
    Task<Stream> GetImageStreamAsync(Image image);
} 