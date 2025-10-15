using Microsoft.Extensions.Logging;

namespace MedBench.API.Tests.Controllers;

public class ImagesControllerTests
{
    private readonly Mock<IImageRepository> _mockRepository;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<ILogger<ImagesController>> _mockLogger;
    private readonly ImagesController _controller;

    public ImagesControllerTests()
    {
        _mockRepository = new Mock<IImageRepository>();
        _mockImageService = new Mock<IImageService>();
        _mockLogger = new Mock<ILogger<ImagesController>>();
        _controller = new ImagesController(
            _mockRepository.Object,
            _mockImageService.Object,
            _mockLogger.Object
        );

        // Setup ClaimsPrincipal
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Set the User property on ControllerBase
        var controllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _controller.ControllerContext = controllerContext;
    }

    [Fact]
    public async Task GetImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var image = new Image
        {
            Id = "1",
            ContentType = "image/jpeg",
            BlobPath = "test.jpg"
        };
        var stream = new MemoryStream();

        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(image);
        _mockImageService.Setup(service => service.GetImageStreamAsync(image))
            .ReturnsAsync(stream);

        // Act
        var result = await _controller.GetImage("1");

        // Assert
        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
    }

    [Fact]
    public async Task GetImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.GetImage("1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetImage_WhenServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var image = new Image { Id = "1" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(image);
        _mockImageService.Setup(service => service.GetImageStreamAsync(image))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetImage("1");

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }
} 