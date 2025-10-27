using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MedBench.Core.Models;
using MedBench.Core.Services;
using MedBench.Core.Interfaces;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MedBench.Core.Tests.Services;

public class DataFileServiceTests
{
    private readonly Mock<ILogger<DataFileService>> _mockLogger;
    private readonly Mock<IDataObjectRepository> _mockDataObjectRepository;
    private readonly Mock<IDataSetRepository> _mockDataSetRepository;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly DataFileService _dataFileService;

    public DataFileServiceTests()
    {
        _mockLogger = new Mock<ILogger<DataFileService>>();
        _mockDataObjectRepository = new Mock<IDataObjectRepository>();
        _mockDataSetRepository = new Mock<IDataSetRepository>();
        _mockImageService = new Mock<IImageService>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup configuration
        _mockConfiguration.Setup(x => x["AzureStorage:DataFilesContainer"]).Returns("test-container");

        _dataFileService = new DataFileService(
            _mockBlobServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockDataObjectRepository.Object,
            _mockDataSetRepository.Object,
            _mockImageService.Object
        );
    }

    [Fact]
    public async Task CreateDataObjectFromJson_SimpleArrayMapping_CreatesMultipleInputs()
    {
        // Arrange
        var jsonString = """
        {
            "tags": ["tag1", "tag2", "tag3"],
            "text": "sample text"
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "tags" }, Type = "text", IsArray = true },
                new() { KeyPath = new List<string> { "text" }, Type = "text", IsArray = false }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.InputData.Count); // 3 tags + 1 text
        Assert.Equal("tag1", result.InputData[0].Content);
        Assert.Equal("tag2", result.InputData[1].Content);
        Assert.Equal("tag3", result.InputData[2].Content);
        Assert.Equal("sample text", result.InputData[3].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_ArrayOfObjects_ExtractsSpecificKeys()
    {
        // Arrange
        var jsonString = """
        {
            "images": [
                {"url": "image1.jpg", "caption": "First image"},
                {"url": "image2.jpg", "caption": "Second image"}
            ],
            "title": "Sample data"
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "images", "url" }, Type = "imageurl", IsArray = true },
                new() { KeyPath = new List<string> { "title" }, Type = "text", IsArray = false }
            },
            OutputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "images", "caption" }, Type = "text", IsArray = true }
            }
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        
        // Check inputs
        Assert.Equal(3, result.InputData.Count); // 2 image URLs + 1 title
        Assert.Equal("image1.jpg", result.InputData[0].Content);
        Assert.Equal("imageurl", result.InputData[0].Type);
        Assert.Equal("image2.jpg", result.InputData[1].Content);
        Assert.Equal("imageurl", result.InputData[1].Type);
        Assert.Equal("Sample data", result.InputData[2].Content);
        Assert.Equal("text", result.InputData[2].Type);
        
        // Check outputs
        Assert.Equal(2, result.OutputData.Count);
        Assert.Equal("First image", result.OutputData[0].Content);
        Assert.Equal("Second image", result.OutputData[1].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_NestedArrays_ConvertsToJsonString()
    {
        // Arrange - This tests the current limitation where nested arrays become JSON strings
        var jsonString = """
        {
            "images": [
                {
                    "urls": ["url1", "url2", "url3"],
                    "metadata": "data1"
                },
                {
                    "urls": ["url4", "url5"],
                    "metadata": "data2"
                }
            ]
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "images", "urls" }, Type = "text", IsArray = true }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InputData.Count);
        
        // Verify that nested arrays are converted to JSON strings (current behavior)
        Assert.Contains("url1", result.InputData[0].Content);
        Assert.Contains("url2", result.InputData[0].Content);
        Assert.Contains("url3", result.InputData[0].Content);
        Assert.Contains("url4", result.InputData[1].Content);
        Assert.Contains("url5", result.InputData[1].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_MixedArrayAndRegularMappings_ProcessesBoth()
    {
        // Arrange
        var jsonString = """
        {
            "tags": ["tag1", "tag2"],
            "title": "Main title",
            "authors": [
                {"name": "Author 1", "email": "author1@example.com"},
                {"name": "Author 2", "email": "author2@example.com"}
            ],
            "content": "Article content"
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "title" }, Type = "text", IsArray = false },
                new() { KeyPath = new List<string> { "tags" }, Type = "text", IsArray = true },
                new() { KeyPath = new List<string> { "authors", "name" }, Type = "text", IsArray = true }
            },
            OutputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "content" }, Type = "text", IsArray = false },
                new() { KeyPath = new List<string> { "authors", "email" }, Type = "text", IsArray = true }
            }
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        
        // Check inputs: 1 title + 2 tags + 2 author names = 5 total
        Assert.Equal(5, result.InputData.Count);
        Assert.Equal("Main title", result.InputData[0].Content);
        Assert.Equal("tag1", result.InputData[1].Content);
        Assert.Equal("tag2", result.InputData[2].Content);
        Assert.Equal("Author 1", result.InputData[3].Content);
        Assert.Equal("Author 2", result.InputData[4].Content);
        
        // Check outputs: 1 content + 2 author emails = 3 total
        Assert.Equal(3, result.OutputData.Count);
        Assert.Equal("Article content", result.OutputData[0].Content);
        Assert.Equal("author1@example.com", result.OutputData[1].Content);
        Assert.Equal("author2@example.com", result.OutputData[2].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_EmptyArray_HandlesGracefully()
    {
        // Arrange
        var jsonString = """
        {
            "tags": [],
            "title": "Title with no tags"
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "tags" }, Type = "text", IsArray = true },
                new() { KeyPath = new List<string> { "title" }, Type = "text", IsArray = false }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.InputData); // Only the title should be present
        Assert.Equal("Title with no tags", result.InputData[0].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_ArrayMappingWithMissingKey_SkipsInvalidItems()
    {
        // Arrange
        var jsonString = """
        {
            "items": [
                {"url": "valid1.jpg", "caption": "Valid item 1"},
                {"caption": "Missing URL"},
                {"url": "valid2.jpg", "caption": "Valid item 2"}
            ]
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "items", "url" }, Type = "imageurl", IsArray = true }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InputData.Count); // Only items with URLs
        Assert.Equal("valid1.jpg", result.InputData[0].Content);
        Assert.Equal("valid2.jpg", result.InputData[1].Content);
    }

    [Fact]
    public async Task CreateDataObjectFromJson_DeepArrayPath_ProcessesCorrectly()
    {
        // Arrange
        var jsonString = """
        {
            "data": {
                "nested": {
                    "items": [
                        {"value": "item1"},
                        {"value": "item2"}
                    ]
                }
            }
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "data", "nested", "items", "value" }, Type = "text", IsArray = true }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InputData.Count);
        Assert.Equal("item1", result.InputData[0].Content);
        Assert.Equal("item2", result.InputData[1].Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateDataObjectFromJson_ArrayWithEmptyLastKey_UsesArrayItemDirectly(string lastKey)
    {
        // Arrange
        var jsonString = """
        {
            "tags": ["direct1", "direct2", "direct3"]
        }
        """;
        
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
        var mapping = new DataFileMapping
        {
            InputMappings = new List<DataFileMappingItem>
            {
                new() { KeyPath = new List<string> { "tags", lastKey }.Where(x => !string.IsNullOrEmpty(x)).ToList(), Type = "text", IsArray = true }
            },
            OutputMappings = new List<DataFileMappingItem>()
        };

        // Act
        var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.InputData.Count);
        Assert.Equal("direct1", result.InputData[0].Content);
        Assert.Equal("direct2", result.InputData[1].Content);
        Assert.Equal("direct3", result.InputData[2].Content);
    }
} 