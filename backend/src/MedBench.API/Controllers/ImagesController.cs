using Microsoft.AspNetCore.Mvc;

namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImagesController : ControllerBase
{
    private readonly IImageRepository _imageRepository;
    private readonly IImageService _imageService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IImageRepository imageRepository,
        IImageService imageService,
        ILogger<ImagesController> logger)
    {
        _imageRepository = imageRepository;
        _imageService = imageService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> GetImage(string id)
    {
        try
        {
            var image = await _imageRepository.GetByIdAsync(id);
            var stream = await _imageService.GetImageStreamAsync(image);
            
            return File(stream, image.ContentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image {Id}", id);
            return StatusCode(500, "Error retrieving image");
        }
    }
} 