namespace MedBench.Core.Interfaces;

public interface IImageRepository
{
    Task<Image> GetByIdAsync(string id);
    Task<Image> CreateAsync(Image image);
    Task DeleteAsync(string id);
} 