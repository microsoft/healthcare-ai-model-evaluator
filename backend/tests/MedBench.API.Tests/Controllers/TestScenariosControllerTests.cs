using Microsoft.Extensions.Logging;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using MedBench.API.Controllers;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Xunit;
namespace MedBench.API.Tests.Controllers;

public class TestScenariosControllerTests
{
    private readonly Mock<ITestScenarioRepository> _mockRepository;
    private readonly Mock<IClinicalTaskRepository> _mockClinicalTaskRepository;
    private readonly Mock<ILogger<TestScenariosController>> _mockLogger;
    private readonly TestScenariosController _controller;
    private readonly string _userId = "test-user-id";

    public TestScenariosControllerTests()
    {
        _mockRepository = new Mock<ITestScenarioRepository>();
        _mockClinicalTaskRepository = new Mock<IClinicalTaskRepository>();
        _mockLogger = new Mock<ILogger<TestScenariosController>>();
        _controller = new TestScenariosController(_mockRepository.Object, _mockClinicalTaskRepository.Object, _mockLogger.Object);

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
    public async Task GetAll_ReturnsOkResult_WithTestScenarios()
    {
        // Arrange
        var scenarios = new List<TestScenario>
        {
            new TestScenario { Id = "1", Name = "Scenario 1", OwnerId = _userId },
            new TestScenario { Id = "2", Name = "Scenario 2", OwnerId = _userId }
        };
        _mockRepository.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(scenarios);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedScenarios = Assert.IsAssignableFrom<IEnumerable<TestScenario>>(okResult.Value);
        Assert.Equal(2, returnedScenarios.Count());
    }

    [Fact]
    public async Task Get_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var scenario = new TestScenario { Id = "1", Name = "Test Scenario", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(scenario);

        // Act
        var result = await _controller.Get("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedScenario = Assert.IsType<TestScenario>(okResult.Value);
        Assert.Equal(scenario.Id, returnedScenario.Id);
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
    public async Task Create_WithValidScenario_ReturnsCreatedAtAction()
    {
        // Arrange
        var scenario = new TestScenario { Name = "New Scenario" };
        var createdScenario = new TestScenario { Id = "1", Name = "New Scenario", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.CreateAsync(It.IsAny<TestScenario>()))
            .ReturnsAsync(createdScenario);

        // Act
        var result = await _controller.Create(scenario);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedScenario = Assert.IsType<TestScenario>(createdAtActionResult.Value);
        Assert.Equal(createdScenario.Id, returnedScenario.Id);
        Assert.Equal(_userId, returnedScenario.OwnerId);
    }

    [Fact]
    public async Task Update_WithValidIdAndOwner_ReturnsOkResult()
    {
        // Arrange
        var scenario = new TestScenario { Id = "1", Name = "Updated Scenario", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(scenario);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<TestScenario>()))
            .ReturnsAsync(scenario);

        // Act
        var result = await _controller.Update("1", scenario);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedScenario = Assert.IsType<TestScenario>(okResult.Value);
        Assert.Equal(scenario.Id, returnedScenario.Id);
    }

    [Fact]
    public async Task Update_WithDifferentOwner_ReturnsForbid()
    {
        // Arrange
        var scenario = new TestScenario { Id = "1", Name = "Scenario", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(scenario);

        // Act
        var result = await _controller.Update("1", scenario);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WithValidIdAndOwner_ReturnsNoContent()
    {
        // Arrange
        var scenario = new TestScenario { Id = "1", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(scenario);
        _mockRepository.Setup(repo => repo.DeleteAsync("1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WithDifferentOwner_ReturnsForbid()
    {
        // Arrange
        var scenario = new TestScenario { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(scenario);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<ForbidResult>(result);
    }
} 