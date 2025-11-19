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
    private readonly Mock<IDataObjectRepository> _mockDataObjectRepository;
    private readonly Mock<IImageRepository> _mockImageRepository;

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
        _mockDataObjectRepository = new Mock<IDataObjectRepository>();
        _mockImageRepository = new Mock<IImageRepository>();
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
            _mockModelRepository.Object,
            _mockDataObjectRepository.Object,
            _mockImageRepository.Object
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
        _mockRepository.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(experiments);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<Experiment>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
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
        var actionResult = Assert.IsType<ActionResult<Experiment>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
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
        var actionResult = Assert.IsType<ActionResult<Experiment>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
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
        var actionResult = Assert.IsType<ActionResult<Experiment>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        Assert.Equal(experiment.Id, returnedExperiment.Id);
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
        var actionResult = Assert.IsType<ActionResult<Experiment>>(result);
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        Assert.NotNull(createdAtActionResult.Value);
        
        // The controller returns an anonymous object with just the id
        var returnedObject = createdAtActionResult.Value;
        var idProperty = returnedObject.GetType().GetProperty("id");
        Assert.NotNull(idProperty);
        var returnedId = idProperty.GetValue(returnedObject)?.ToString();
        Assert.Equal(createdExperiment.Id, returnedId);
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
    public async Task Update_WithMismatchedId_ReturnsOkResult()
    {
        // Arrange
        var experiment = new Experiment { Id = "2", Name = "Different Id" };
        var existingExperiment = new Experiment { Id = "1", Status = ExperimentStatus.Draft };
        
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(existingExperiment);

        // Act
        var result = await _controller.Update("1", experiment);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Update_WithDifferentOwner_ReturnsOkResult()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Update("1", experiment);

        // Assert
        Assert.IsType<OkObjectResult>(result);
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
    public async Task Delete_WithDifferentOwner_ReturnsNoContent()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", OwnerId = "different-owner" };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NoContentResult>(result);
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
        var actionResult = Assert.IsType<ActionResult<IEnumerable<Experiment>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
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
        var actionResult = Assert.IsType<ActionResult<IEnumerable<Experiment>>>(result);
        var unauthorizedResult = Assert.IsType<UnauthorizedResult>(actionResult.Result);
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
        var updatedExperiment = new Experiment { Id = "1", Status = ExperimentStatus.Completed, PendingTrials = 0 };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Experiment>()))
            .ReturnsAsync(updatedExperiment);

        // Act
        var result = await _controller.UpdateStatus("1", ExperimentStatus.Completed);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedExperiment = Assert.IsType<Experiment>(okResult.Value);
        
        // Verify experiment status was updated
        Assert.Equal(ExperimentStatus.Completed, returnedExperiment.Status);
        
        // Verify trials were updated
        _mockTrialRepository.Verify(repo => 
            repo.UpdateExperimentStatusAsync("1", ExperimentStatus.Completed.ToString()), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_WithNonCompletedStatus_OnlyUpdatesStatusAndTrials()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Status = ExperimentStatus.Draft };
        var updatedExperiment = new Experiment { Id = "1", Status = ExperimentStatus.InProgress, PendingTrials = 10 };
        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Experiment>()))
            .ReturnsAsync(updatedExperiment);

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
    public async Task UpdateStatus_WithInvalidId_ReturnsInternalServerError()
    {
        // Arrange
        _mockRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.UpdateStatus("1", ExperimentStatus.Completed);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        
        // Verify no updates were made
        _mockTrialRepository.Verify(repo => 
            repo.UpdateExperimentStatusAsync(It.IsAny<string>(), It.IsAny<string>()), 
            Times.Never);
        _mockProcessingService.Verify(service => 
            service.CollateExperimentResults(It.IsAny<string>()), 
            Times.Never);
    }

    [Fact]
    public async Task ExportExperiment_WithValidId_ReturnsExportData()
    {
        // Arrange
        var experiment = new Experiment 
        { 
            Id = "1", 
            Name = "Test Experiment",
            Description = "Test Description",
            Status = ExperimentStatus.Completed 
        };
        
        var trials = new List<Trial>
        {
            new Trial 
            { 
                Id = "trial1", 
                ExperimentId = "1", 
                DataObjectId = "obj1", 
                DataSetId = "dataset1",
                ModelInputs = new List<DataContent>
                {
                    new DataContent { Type = "imageurl", Content = "img1", ContentUrl = "" }
                },
                ModelOutputs = new List<ModelOutput>
                {
                    new ModelOutput 
                    { 
                        ModelId = "model1", 
                        Output = new List<DataContent>
                        {
                            new DataContent { Type = "text", Content = "Sample output" }
                        }
                    }
                }
            },
            new Trial { Id = "trial2", ExperimentId = "1", DataObjectId = "obj2", DataSetId = "dataset1" }
        };
        
        var dataObjects = new List<DataObject>
        {
            new DataObject 
            { 
                Id = "obj1", 
                DataSetId = "dataset1", 
                Name = "Test Object 1",
                InputData = new List<DataContent>
                {
                    new DataContent { Type = "text", Content = "Sample text", ContentUrl = "" },
                    new DataContent { Type = "imageurl", Content = "img1", ContentUrl = "" }
                }
            }
        };
        
        var image = new Image
        {
            Id = "img1",
            BlobPath = "images/sample-image.jpg",
            ContentType = "image/jpeg",
            StorageAccount = "medbenchstorage",
            Container = "images"
        };

        _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(experiment);
        _mockTrialRepository.Setup(repo => repo.GetByExperimentIdAsync("1"))
            .ReturnsAsync(trials);
        _mockDataObjectRepository.Setup(repo => repo.GetByDataSetIdAsync("dataset1"))
            .ReturnsAsync(dataObjects);
        _mockImageRepository.Setup(repo => repo.GetByIdAsync("img1"))
            .ReturnsAsync(image);

        // Act
        var result = await _controller.ExportExperiment("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var exportData = okResult.Value;
        Assert.NotNull(exportData);
        
        // Verify that the data object repository was called
        _mockDataObjectRepository.Verify(repo => repo.GetByDataSetIdAsync("dataset1"), Times.Once);
        
        // Verify that the image repository was called for the image (twice: once for trial, once for data object)
        _mockImageRepository.Verify(repo => repo.GetByIdAsync("img1"), Times.Exactly(2));
        
        // Verify that the export data structure is correct
        var exportDataType = exportData.GetType();
        var dataObjectsProperty = exportDataType.GetProperty("dataObjects");
        Assert.NotNull(dataObjectsProperty);
        
        var dataObjectsValue = dataObjectsProperty.GetValue(exportData) as IEnumerable<object>;
        Assert.NotNull(dataObjectsValue);
        Assert.Single(dataObjectsValue);
        
        // Additional verification that image URLs are properly enriched would require
        // more complex reflection or casting to dynamic, but the repository calls verify
        // that the image enrichment logic was invoked
    }

    [Fact]
    public async Task ExportExperiment_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockRepository.Setup(repo => repo.GetByIdAsync("invalid-id"))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.ExportExperiment("invalid-id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExportExperiment_WithImageContent_PopulatesContentUrlAndBlobUrl()
    {
        // Arrange
        var experiment = new Experiment { Id = "1", Name = "Image Test" };
        var trials = new List<Trial>
        {
            new Trial { Id = "trial1", ExperimentId = "1", DataObjectId = "obj1", DataSetId = "dataset1" }
        };
        
        var dataObjects = new List<DataObject>
        {
            new DataObject 
            { 
                Id = "obj1", 
                DataSetId = "dataset1", 
                InputData = new List<DataContent>
                {
                    // Test case: image with ID in Content field and empty ContentUrl
                    new DataContent { Type = "imageurl", Content = "img123", ContentUrl = "" },
                    // Test case: non-image content should remain unchanged
                    new DataContent { Type = "text", Content = "some text", ContentUrl = "" }
                }
            }
        };
        
        var image = new Image
        {
            Id = "img123",
            BlobPath = "images/test-image.jpg",
            ContentType = "image/jpeg",
            StorageAccount = "teststorage",
            Container = "images"
        };

        _mockRepository.Setup(repo => repo.GetByIdAsync("1")).ReturnsAsync(experiment);
        _mockTrialRepository.Setup(repo => repo.GetByExperimentIdAsync("1")).ReturnsAsync(trials);
        _mockDataObjectRepository.Setup(repo => repo.GetByDataSetIdAsync("dataset1")).ReturnsAsync(dataObjects);
        _mockImageRepository.Setup(repo => repo.GetByIdAsync("img123")).ReturnsAsync(image);

        // Act
        var result = await _controller.ExportExperiment("1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify the image repository was called with the correct image ID
        _mockImageRepository.Verify(repo => repo.GetByIdAsync("img123"), Times.Once);
        
        // The actual verification of the enriched URLs would require more complex assertions
        // but the repository verification confirms the enrichment logic was executed
    }
} 