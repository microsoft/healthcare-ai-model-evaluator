using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using MedBench.Core.Services;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace MedBench.API.Tests.Controllers;

public class ExperimentsControllerTests
{
    private readonly Mock<IExperimentRepository> _mockRepository;
    private readonly Mock<ITrialRepository> _mockTrialRepository;
    private readonly Mock<ILogger<ExperimentsController>> _mockLogger;
    private readonly Mock<IExperimentProcessingService> _mockProcessingService;
    private readonly Mock<ITestScenarioRepository> _mockTestScenarioRepository;
    private readonly Mock<IClinicalTaskRepository> _mockClinicalTaskRepository;
    private readonly Mock<IDataSetRepository> _mockDataSetRepository;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly ExperimentsController _controller;
    private readonly string _userId = "test-user-id";
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IModelRepository> _mockModelRepository;

    public ExperimentsControllerTests()
    {
        _mockRepository = new Mock<IExperimentRepository>();
        _mockTrialRepository = new Mock<ITrialRepository>();
        _mockLogger = new Mock<ILogger<ExperimentsController>>();
        _mockProcessingService = new Mock<IExperimentProcessingService>();
        _mockTestScenarioRepository = new Mock<ITestScenarioRepository>();
        _mockClinicalTaskRepository = new Mock<IClinicalTaskRepository>();
        _mockDataSetRepository = new Mock<IDataSetRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockModelRepository = new Mock<IModelRepository>();
        _mockModelRepository.Setup(repo => repo.GetByIdAsync(It.IsAny<string>())    )
            .ReturnsAsync(new Model { Id = "test-model-id", Name = "Test Model" });
        _controller = new ExperimentsController(
            _mockRepository.Object,
            _mockTrialRepository.Object,
            _mockTestScenarioRepository.Object,
            _mockClinicalTaskRepository.Object,
            _mockDataSetRepository.Object,
            _mockLogger.Object,
            _mockProcessingService.Object,
            _mockScopeFactory.Object,
            _mockUserRepository.Object,
            _mockModelRepository.Object
        );

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
    public async Task GetAll_ReturnsOkResult_WithUserExperiments()
    {
        // Arrange
        var experiments = new List<Experiment>
        {
            new Experiment { Id = "1", Name = "Experiment 1", OwnerId = _userId },
            new Experiment { Id = "2", Name = "Experiment 2", OwnerId = _userId }
        };
        _mockRepository.Setup(repo => repo.GetByUserIdAsync(_userId))
            .ReturnsAsync(experiments);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiments = Assert.IsAssignableFrom<IEnumerable<Experiment>>(okResult.Value);
        Assert.Equal(2, returnedExperiments.Count());
    }

    [Fact]
    public async Task Get_WithValidIdAndOwner_ReturnsOkResult()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Name = "Test Experiment", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Get("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        Assert.Equal(experiment.Id, returnedExperiment.Id);
    }

    [Fact]
    public async Task Get_WithValidIdAndAssignedUser_ReturnsOkResult()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Name = "Test Experiment", 
            OwnerId = "other-user",
            ReviewerIds = new List<string> { _userId }
        };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Get("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        Assert.Equal(experiment.Id, returnedExperiment.Id);
    }

    [Fact]
    public async Task Get_WithUnauthorizedUser_ReturnsForbid()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Name = "Test Experiment", 
            OwnerId = "other-user",
            ReviewerIds = new List<string> { "another-user" }
        };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Get("1");

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WithValidExperiment_ReturnsCreatedAtAction()
    {
        // Arrange
        var experiment = new Experiment { Name = "New Experiment" };
        var createdExperiment = new Experiment { Id = "1", Name = "New Experiment", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.CreateAsync(It.IsAny<Experiment>()))
            .ReturnsAsync(createdExperiment);

        // Act
        var result = await _controller.Create(experiment);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        var returnedExperiment = Assert.IsType<Experiment>(createdAtActionResult.Value);
        Assert.Equal(createdExperiment.Id, returnedExperiment.Id);
        Assert.Equal(_userId, returnedExperiment.OwnerId);
    }

    [Fact]
    public async Task Update_WithValidIdAndOwner_ReturnsOkResult()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Name = "Updated Experiment", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Experiment>()))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Update("1", experiment);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        Assert.Equal(experiment.Id, returnedExperiment.Id);
    }

    [Fact]
    public async Task Update_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var experiment = new Experiment { Id = "2", Name = "Wrong Id" };

        // Act
        var result = await _controller.Update("1", experiment);

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Update_WithDifferentOwner_ReturnsForbid()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Update("1", experiment);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_WithValidIdAndOwner_ReturnsNoContent()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", OwnerId = _userId };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);
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
        var experiment = new Experiment { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetAssigned_ReturnsOkResult_WithAssignedExperiments()
    {
        // Arrange
        var experiments = new List<Experiment>
        {
            new Experiment 
            { 
                Id = "1", 
                Name = "Experiment 1", 
                OwnerId = "other-user",
                ReviewerIds = new List<string> { _userId }
            },
            new Experiment 
            { 
                Id = "2", 
                Name = "Experiment 2", 
                OwnerId = "other-user",
                ReviewerIds = new List<string> { _userId }
            },
            new Experiment 
            { 
                Id = "3", 
                Name = "Own Experiment", 
                OwnerId = _userId,
                ReviewerIds = new List<string> { _userId }
            }
        };
        _mockRepository.Setup(repo => repo.GetByUserIdAsync(_userId))
            .ReturnsAsync(experiments);

        // Act
        var result = await _controller.GetAssigned();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiments = Assert.IsAssignableFrom<IEnumerable<Experiment>>(okResult.Value);
        Assert.Equal(2, returnedExperiments.Count()); // Should only return experiments where user is assigned but not owner
        Assert.All(returnedExperiments, exp => 
        {
            Assert.Contains(_userId, exp.ReviewerIds);
            Assert.NotEqual(_userId, exp.OwnerId);
        });
    }

    [Fact]
    public async Task GetAssigned_WithNoUserId_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        // Act
        var result = await _controller.GetAssigned();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ProcessExperiment_WithValidExperiment_StartsProcessingAndReturnsOk()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Status = ExperimentStatus.Draft,
            ProcessingStatus = ProcessingStatus.NotProcessed
        };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Setup mock scope for background processing
        var mockScope = new Mock<IServiceScope>();
        var mockScopeServiceProvider = new Mock<IServiceProvider>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        // Setup service provider to return required services
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IExperimentRepository)))
            .Returns(_mockRepository.Object);
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IClinicalTaskRepository)))
            .Returns(Mock.Of<IClinicalTaskRepository>());
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IDataObjectRepository)))
            .Returns(Mock.Of<IDataObjectRepository>());
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(ITrialRepository)))
            .Returns(_mockTrialRepository.Object);
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IExperimentProcessingService)))
            .Returns(_mockProcessingService.Object);

        // Act
        var result = await _controller.ProcessExperiment("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        Assert.Equal(ProcessingStatus.Processing, returnedExperiment.ProcessingStatus);
        
        // Verify the experiment was updated
        _mockRepository.Verify(repo => repo.UpdateAsync(It.Is<Experiment>(
            e => e.ProcessingStatus == ProcessingStatus.Processing)), Times.Once);
    }

    [Fact]
    public async Task ProcessExperiment_WithNonDraftStatus_ReturnsBadRequest()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Status = ExperimentStatus.InProgress,
            ProcessingStatus = ProcessingStatus.NotProcessed
        };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.ProcessExperiment("1");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ProcessExperiment_AlreadyProcessing_ReturnsBadRequest()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Status = ExperimentStatus.Draft,
            ProcessingStatus = ProcessingStatus.Processing
        };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.ProcessExperiment("1");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_UpdatesExperimentAndTrials()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Status = ExperimentStatus.InProgress };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.UpdateStatus("1", ExperimentStatus.Completed);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        
        // Verify experiment status was updated
        Assert.Equal(ExperimentStatus.Completed, returnedExperiment.Status);
        Assert.Equal(ProcessingStatus.Finalizing, returnedExperiment.ProcessingStatus);
        
        // Verify trials were updated
        _mockTrialRepository.Verify(repo => 
            repo.UpdateExperimentStatusAsync("1", ExperimentStatus.Completed.ToString()), 
            Times.Once);
        
        // Verify collation was started
        _mockProcessingService.Verify(service => 
            service.CollateExperimentResults("1"), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_WithNonCompletedStatus_OnlyUpdatesStatusAndTrials()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Status = ExperimentStatus.Draft };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.UpdateStatus("1", ExperimentStatus.InProgress);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        
        // Verify experiment status was updated
        Assert.Equal(ExperimentStatus.InProgress, returnedExperiment.Status);
        
        // Verify trials were updated
        _mockTrialRepository.Verify(repo => 
            repo.UpdateExperimentStatusAsync("1", ExperimentStatus.InProgress.ToString()), 
            Times.Once);
        
        // Verify collation was NOT started
        _mockProcessingService.Verify(service => 
            service.CollateExperimentResults(It.IsAny<string>()), 
            Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.UpdateStatus("1", ExperimentStatus.Completed);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
        
        // Verify no updates were made
        _mockTrialRepository.Verify(repo => 
            repo.UpdateExperimentStatusAsync(It.IsAny<string>(), It.IsAny<string>()), 
            Times.Never);
        _mockProcessingService.Verify(service => 
            service.CollateExperimentResults(It.IsAny<string>()), 
            Times.Never);
    }
} 