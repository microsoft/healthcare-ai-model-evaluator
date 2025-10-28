using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using MedBench.Core.Models;
using MedBench.Core.Repositories;
using MedBench.API.Controllers;
using Xunit;

namespace MedBench.API.Tests.Controllers;

public class ModelsControllerTests
{
    private readonly Mock<IModelRepository> _mockRepository;
    private readonly Mock<ILogger<ModelsController>> _mockLogger;
    private readonly ModelsController _controller;
    private readonly Mock<IModelRunnerFactory> _mockModelRunnerFactory;
    private readonly string _userId = "test-user-id";

    public ModelsControllerTests()
    {
        _mockRepository = new Mock<IModelRepository>();
        _mockLogger = new Mock<ILogger<ModelsController>>();
        _mockModelRunnerFactory = new Mock<IModelRunnerFactory>();
        _controller = new ModelsController(_mockRepository.Object, _mockLogger.Object, _mockModelRunnerFactory.Object);

        // Setup ClaimsPrincipal
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var controllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _controller.ControllerContext = controllerContext;
    }

    [Fact]
    public async Task GetAll_ReturnsOkResult_WithModels()
    {
        // Arrange
        var models = new List<Model>
        {
            new Model { Id = "1", Name = "Model 1", OwnerId = _userId },
            new Model { Id = "2", Name = "Model 2", OwnerId = _userId }
        };
        _mockRepository.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(models);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedModels = Assert.IsAssignableFrom<IEnumerable<Model>>(okResult.Value);
        Assert.Equal(2, returnedModels.Count());
    }

    [Fact]
    public async Task Get_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var model = new Model { Id = "1", Name = "Test Model", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(model);

        // Act
        var result = await _controller.Get("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedModel = Assert.IsType<Model>(okResult.Value);
        Assert.Equal(model.Id, returnedModel.Id);
    }

    [Fact]
    public async Task Get_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.Get("1");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_WithValidModel_ReturnsCreatedAtAction()
    {
        // Arrange
        var model = new Model { Name = "New Model" };
        var createdModel = new Model { Id = "1", Name = "New Model", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.CreateAsync(It.IsAny<Model>()))
            .ReturnsAsync(createdModel);

        // Act
        var result = await _controller.Create(model);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedModel = Assert.IsType<Model>(createdAtActionResult.Value);
        Assert.Equal(createdModel.Id, returnedModel.Id);
        Assert.Equal(_userId, returnedModel.OwnerId);
    }

    [Fact]
    public async Task Update_WithValidIdAndOwner_ReturnsOkResult()
    {
        // Arrange
        var model = new Model { Id = "1", Name = "Updated Model", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(model);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Model>()))
            .ReturnsAsync(model);

        // Act
        var result = await _controller.Update("1", model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedModel = Assert.IsType<Model>(okResult.Value);
        Assert.Equal(model.Id, returnedModel.Id);
    }

    [Fact]
    public async Task Update_WithDifferentOwner_ReturnsOkResult()
    {
        // Arrange
        var model = new Model { Id = "1", Name = "Model", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(model);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Model>()))
            .ReturnsAsync(model);

        // Act
        var result = await _controller.Update("1", model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedModel = Assert.IsType<Model>(okResult.Value);
        Assert.Equal(model.Id, returnedModel.Id);
    }

    [Fact]
    public async Task Delete_WithValidIdAndOwner_ReturnsNoContent()
    {
        // Arrange
        var model = new Model { Id = "1", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(model);
        _mockRepository.Setup(repo => repo.DeleteAsync("1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WithDifferentOwner_ReturnsNoContent()
    {
        // Arrange
        var model = new Model { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(model);
        _mockRepository.Setup(repo => repo.DeleteAsync("1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NoContentResult>(result);
    }
} 