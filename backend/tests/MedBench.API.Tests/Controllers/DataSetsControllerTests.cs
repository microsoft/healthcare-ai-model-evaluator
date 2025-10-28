namespace MedBench.API.Tests.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

public class DataSetsControllerTests
{
    private readonly Mock<IDataSetRepository> _mockDataSetRepo;
    private readonly Mock<IDataObjectRepository> _mockDataObjectRepo;
    private readonly Mock<ILogger<DataSetsController>> _mockLogger;
    private readonly Mock<IImageService> _mockImageService;
    private readonly DataSetsController _controller;

    public DataSetsControllerTests()
    {
        _mockDataSetRepo = new Mock<IDataSetRepository>();
        _mockDataObjectRepo = new Mock<IDataObjectRepository>();
        _mockLogger = new Mock<ILogger<DataSetsController>>();
        _mockImageService = new Mock<IImageService>();
        var mockDataFileService = new Mock<IDataFileService>();
        _controller = new DataSetsController(
            _mockDataSetRepo.Object,
            _mockDataObjectRepo.Object,
            _mockLogger.Object,
            _mockImageService.Object,
            mockDataFileService.Object
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
    public async Task GetAll_ShouldReturnAllDataSets()
    {
        // Arrange
        var expectedDataSets = new List<DataSet>
        {
            new DataSet { Id = "1", Name = "Test Dataset 1" },
            new DataSet { Id = "2", Name = "Test Dataset 2" }
        };

        _mockDataSetRepo.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(expectedDataSets);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDataSets = Assert.IsAssignableFrom<IEnumerable<DataSetListDto>>(okResult.Value);
        Assert.Equal(2, returnedDataSets.Count());
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var dataset = new DataSet 
        { 
            Id = "1", 
            Name = "Test Dataset",
            DataObjectCount = 5
        };
        _mockDataSetRepo.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(dataset);

        // Act
        var result = await _controller.GetById("1");

        // Assert
        var actionResult = Assert.IsType<ActionResult<DataSetDetailDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedDataset = Assert.IsType<DataSetDetailDto>(okResult.Value);
        Assert.Equal(dataset.Id, returnedDataset.Id);
        Assert.Equal(5, returnedDataset.DataObjectCount);
    }

    [Fact]
    public async Task GetDataObjects_ShouldReturnDataObjectsForDataSet()
    {
        // Arrange
        var dataSetId = "1";
        var expectedObjects = new List<DataObject>
        {
            new DataObject { Id = "1", DataSetId = dataSetId },
            new DataObject { Id = "2", DataSetId = dataSetId }
        };

        _mockDataObjectRepo.Setup(repo => repo.GetByDataSetIdAsync(dataSetId))
            .ReturnsAsync(expectedObjects);

        // Act
        var result = await _controller.GetDataObjects(dataSetId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedObjects = Assert.IsAssignableFrom<IEnumerable<DataObject>>(okResult.Value);
        Assert.Equal(expectedObjects.Count(), returnedObjects.Count());
        Assert.Equal(expectedObjects.First().Id, returnedObjects.First().Id);
    }

    [Fact]
    public async Task GetDataObject_WithValidIds_ReturnsOkResult()
    {
        // Arrange
        var dataset = new DataSet { Id = "1" };
        var dataObject = new DataObject { Id = "obj1", DataSetId = "1" };
        
        _mockDataSetRepo.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(dataset);
        _mockDataObjectRepo.Setup(repo => repo.GetByIdAsync("obj1"))
            .ReturnsAsync(dataObject);

        // Act
        var result = await _controller.GetDataObject("1", "obj1");

        // Assert
        var actionResult = Assert.IsType<ActionResult<DataObject>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedObject = Assert.IsType<DataObject>(okResult.Value);
        Assert.Equal("obj1", returnedObject.Id);
    }

    [Fact]
    public async Task GetDataObject_WithWrongDataSetId_ReturnsNotFound()
    {
        // Arrange
        var dataObject = new DataObject { Id = "obj1", DataSetId = "2" };
        
        _mockDataSetRepo.Setup(repo => repo.GetByIdAsync("1"))
            .ReturnsAsync(new DataSet { Id = "1" });
        _mockDataObjectRepo.Setup(repo => repo.GetByIdAsync("obj1"))
            .ReturnsAsync(dataObject);

        // Act
        var result = await _controller.GetDataObject("1", "obj1");

        // Assert
        var actionResult = Assert.IsType<ActionResult<DataObject>>(result);
        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    [Fact]
    public async Task Create_WithValidData_ShouldCreateDataSet()
    {
        // Arrange
        var createDto = new CreateDataSetWithFileDto
        {
            Name = "New Dataset",
            Description = "Test Description",
            // No need for DataObjects here as they're generated from the file
        };

        var createdDataSet = new DataSet
        {
            Id = "1",
            Name = createDto.Name,
            Description = createDto.Description
        };

        _mockDataSetRepo.Setup(repo => repo.CreateAsync(It.IsAny<DataSet>()))
            .ReturnsAsync(createdDataSet);

        // Act
        var result = await _controller.Create(createDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(createdDataSet.Id, ((DataSet)okResult.Value).Id);
    }

    [Fact]
    public async Task Update_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var updateDto = new UpdateDataSetDto { 
            Id = "1",
            Name = "Updated Dataset",
            Origin = "Test",
            Description = "Updated Test Dataset",
            AiModelType = "Test",
            Tags = new List<string>(),
            DataObjects = new List<DataObject>()
        };
        var dataset = new DataSet { Id = "1", Name = "Updated Dataset" };
        _mockDataSetRepo.Setup(repo => repo.UpdateAsync(It.IsAny<DataSet>()))
            .ReturnsAsync(dataset);

        // Act
        var result = await _controller.Update("1", updateDto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<DataSet>>(result);
        Assert.IsType<OkObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task Update_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var updateDto = new UpdateDataSetDto { 
            Id = "2",
            Name = "Wrong Id",
            Origin = "Test",
            Description = "Test Dataset",
            AiModelType = "Test",
            Tags = new List<string>(),
            DataObjects = new List<DataObject>()
        };

        // Act
        var result = await _controller.Update("1", updateDto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<DataSet>>(result);
        Assert.IsType<BadRequestResult>(actionResult.Result);
    }

    [Fact]
    public async Task Delete_ExistingDataSet_ShouldDeleteDataSetAndObjects()
    {
        // Arrange
        var dataSetId = "1";

        _mockDataSetRepo.Setup(repo => repo.DeleteAsync(dataSetId))
            .Returns(Task.CompletedTask);

        _mockDataObjectRepo.Setup(repo => repo.DeleteByDataSetIdAsync(dataSetId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(dataSetId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockDataObjectRepo.Verify(repo => repo.DeleteByDataSetIdAsync(dataSetId), Times.Once);
        _mockDataSetRepo.Verify(repo => repo.DeleteAsync(dataSetId), Times.Once);
    }

    [Fact]
    public async Task Delete_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockDataSetRepo.Setup(repo => repo.DeleteAsync("1"))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.Delete("1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
} 