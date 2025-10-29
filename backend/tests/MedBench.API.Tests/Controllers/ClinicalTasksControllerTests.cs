using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using MedBench.API.Controllers;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace MedBench.API.Tests.Controllers;

public class ClinicalTasksControllerTests
{
    private readonly Mock<IClinicalTaskRepository> _mockRepository;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<ClinicalTasksController>> _mockLogger;
    private readonly Mock<IDataSetRepository> _mockDataSetRepository;
    private readonly Mock<IModelRepository> _mockModelRepository;
    private readonly Mock<ITrialRepository> _mockTrialRepository;
    private readonly Mock<IExperimentRepository> _mockExperimentRepository;
    private readonly Mock<ITestScenarioRepository> _mockTestScenarioRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ClinicalTasksController _controller;
    private readonly string _userId = "test-user-id";

    public ClinicalTasksControllerTests()
    {
        _mockRepository = new Mock<IClinicalTaskRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<ClinicalTasksController>>();
        _mockDataSetRepository = new Mock<IDataSetRepository>();
        _mockModelRepository = new Mock<IModelRepository>();
        _mockTrialRepository = new Mock<ITrialRepository>();
        _mockExperimentRepository = new Mock<IExperimentRepository>();
        _mockTestScenarioRepository = new Mock<ITestScenarioRepository>();
    _mockConfiguration = new Mock<IConfiguration>();

        _controller = new ClinicalTasksController(
            _mockRepository.Object,
            _mockScopeFactory.Object,
            _mockLogger.Object,
            _mockDataSetRepository.Object,
            _mockModelRepository.Object,
            _mockTrialRepository.Object,
            _mockExperimentRepository.Object,
            _mockTestScenarioRepository.Object,
            _mockConfiguration.Object
        );

        // Setup ClaimsPrincipal
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId)
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
    public async Task GetAll_ReturnsOkResult_WithClinicalTasks()
    {
        // Arrange
        var tasks = new List<ClinicalTask>
        {
            new ClinicalTask { Id = "1", Name = "Task 1", OwnerId = _userId },
            new ClinicalTask { Id = "2", Name = "Task 2", OwnerId = _userId }
        };
        _mockRepository.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(tasks);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTasks = Assert.IsAssignableFrom<IEnumerable<ClinicalTask>>(okResult.Value);
        Assert.Equal(2, returnedTasks.Count());
    }

    [Fact]
    public async Task Get_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var task = new ClinicalTask { Id = "1", Name = "Test Task", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(task);

        // Act
        var result = await _controller.Get("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTask = Assert.IsType<ClinicalTask>(okResult.Value);
        Assert.Equal(task.Id, returnedTask.Id);
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
    public async Task Create_WithValidTask_ReturnsCreatedAtAction()
    {
        // Arrange
        var task = new ClinicalTask { Name = "New Task" };
        var createdTask = new ClinicalTask { Id = "1", Name = "New Task", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.CreateAsync(It.IsAny<ClinicalTask>()))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.Create(task);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedTask = Assert.IsType<ClinicalTask>(createdAtActionResult.Value);
        Assert.Equal(createdTask.Id, returnedTask.Id);
        Assert.Equal(_userId, returnedTask.OwnerId);
    }

    [Fact]
    public async Task Update_WithValidIdAndOwner_ReturnsOkResult()
    {
        // Arrange
        var task = new ClinicalTask { Id = "1", Name = "Updated Task", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(task);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<ClinicalTask>()))
            .ReturnsAsync(task);

        // Act
        var result = await _controller.Update("1", task);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTask = Assert.IsType<ClinicalTask>(okResult.Value);
        Assert.Equal(task.Id, returnedTask.Id);
    }

    [Fact]
    public async Task Update_WithDifferentOwner_ReturnsOkResult()
    {
        // Arrange
        var task = new ClinicalTask { Id = "1", Name = "Task", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(task);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<ClinicalTask>()))
            .ReturnsAsync(task);

        // Act
        var result = await _controller.Update("1", task);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTask = Assert.IsType<ClinicalTask>(okResult.Value);
        Assert.Equal(task.Id, returnedTask.Id);
    }

    [Fact]
    public async Task Delete_WithValidIdAndOwner_ReturnsNoContent()
    {
        // Arrange
        var task = new ClinicalTask { Id = "1", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(task);
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
        var task = new ClinicalTask { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(task);
        _mockRepository.Setup(repo => repo.DeleteAsync("1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NoContentResult>(result);
    }
} 