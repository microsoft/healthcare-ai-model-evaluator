using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MedBench.Core.Models;
using MedBench.Core.Services;
using MedBench.Core.Interfaces;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace MedBench.Core.Tests.TestRunner;

public class ArrayMappingTestRunner
{
    private readonly DataFileService _dataFileService;
    private readonly Mock<ILogger<DataFileService>> _mockLogger;

    public ArrayMappingTestRunner()
    {
        _mockLogger = new Mock<ILogger<DataFileService>>();
        var mockDataObjectRepository = new Mock<IDataObjectRepository>();
        var mockDataSetRepository = new Mock<IDataSetRepository>();
        var mockImageService = new Mock<IImageService>();
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockConfiguration = new Mock<IConfiguration>();

        mockConfiguration.Setup(x => x["AzureStorage:DataFilesContainer"]).Returns("test-container");

        _dataFileService = new DataFileService(
            mockBlobServiceClient.Object,
            mockConfiguration.Object,
            _mockLogger.Object,
            mockDataObjectRepository.Object,
            mockDataSetRepository.Object,
            mockImageService.Object
        );
    }

    public async Task<bool> RunAllTests()
    {
        Console.WriteLine("🧪 Starting Array Mapping Functionality Tests");
        Console.WriteLine("=" * 50);

        var testResults = new List<TestResult>();

        // Test Case 1: Simple Array of Strings
        testResults.Add(await TestSimpleArrayOfStrings());

        // Test Case 2: Array of Objects
        testResults.Add(await TestArrayOfObjects());

        // Test Case 3: Mixed Array and Regular Mappings
        testResults.Add(await TestMixedMappings());

        // Test Case 4: Nested Arrays (Current Limitation)
        testResults.Add(await TestNestedArrays());

        // Test Case 5: Empty Arrays
        testResults.Add(await TestEmptyArrays());

        // Test Case 6: Deep Nesting
        testResults.Add(await TestDeepNesting());

        // Print Results Summary
        PrintTestSummary(testResults);

        return testResults.All(r => r.Passed);
    }

    private async Task<TestResult> TestSimpleArrayOfStrings()
    {
        var testName = "Simple Array of Strings";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
            var jsonString = """
            {
                "tags": ["medical", "urgent", "followup"],
                "text": "Patient shows improvement"
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

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            // Verify results
            var expectedInputCount = 4; // 3 tags + 1 text
            var actualInputCount = result?.InputData.Count ?? 0;
            
            var expectedTags = new[] { "medical", "urgent", "followup" };
            var actualTags = result?.InputData.Take(3).Select(x => x.Content).ToArray() ?? Array.Empty<string>();

            var passed = actualInputCount == expectedInputCount && 
                        expectedTags.SequenceEqual(actualTags) &&
                        result?.InputData.Last().Content == "Patient shows improvement";

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   Tags match: {expectedTags.SequenceEqual(actualTags)}");
            Console.WriteLine($"   ✅ Test {(passed ? "PASSED" : "FAILED")}");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private async Task<TestResult> TestArrayOfObjects()
    {
        var testName = "Array of Objects";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
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

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            var expectedInputCount = 3; // 2 URLs + 1 title
            var expectedOutputCount = 2; // 2 captions
            var actualInputCount = result?.InputData.Count ?? 0;
            var actualOutputCount = result?.OutputData.Count ?? 0;

            var urls = result?.InputData.Take(2).Select(x => x.Content).ToArray() ?? Array.Empty<string>();
            var expectedUrls = new[] { "image1.jpg", "image2.jpg" };

            var passed = actualInputCount == expectedInputCount && 
                        actualOutputCount == expectedOutputCount &&
                        urls.SequenceEqual(expectedUrls);

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   Expected outputs: {expectedOutputCount}, Actual: {actualOutputCount}");
            Console.WriteLine($"   URLs match: {urls.SequenceEqual(expectedUrls)}");
            Console.WriteLine($"   ✅ Test {(passed ? "PASSED" : "FAILED")}");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private async Task<TestResult> TestMixedMappings()
    {
        var testName = "Mixed Array and Regular Mappings";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
            var jsonString = """
            {
                "title": "Medical Report",
                "authors": [
                    {"name": "Dr. Smith", "email": "smith@hospital.com"},
                    {"name": "Dr. Jones", "email": "jones@hospital.com"}
                ],
                "content": "Patient examination results",
                "tags": ["cardiology", "routine"]
            }
            """;

            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
            var mapping = new DataFileMapping
            {
                InputMappings = new List<DataFileMappingItem>
                {
                    new() { KeyPath = new List<string> { "title" }, Type = "text", IsArray = false },
                    new() { KeyPath = new List<string> { "authors", "name" }, Type = "text", IsArray = true },
                    new() { KeyPath = new List<string> { "tags" }, Type = "text", IsArray = true }
                },
                OutputMappings = new List<DataFileMappingItem>
                {
                    new() { KeyPath = new List<string> { "content" }, Type = "text", IsArray = false },
                    new() { KeyPath = new List<string> { "authors", "email" }, Type = "text", IsArray = true }
                }
            };

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            var expectedInputCount = 5; // 1 title + 2 author names + 2 tags
            var expectedOutputCount = 3; // 1 content + 2 author emails
            var actualInputCount = result?.InputData.Count ?? 0;
            var actualOutputCount = result?.OutputData.Count ?? 0;

            var passed = actualInputCount == expectedInputCount && actualOutputCount == expectedOutputCount;

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   Expected outputs: {expectedOutputCount}, Actual: {actualOutputCount}");
            Console.WriteLine($"   ✅ Test {(passed ? "PASSED" : "FAILED")}");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private async Task<TestResult> TestNestedArrays()
    {
        var testName = "Nested Arrays (Current Limitation)";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
            var jsonString = """
            {
                "items": [
                    {"urls": ["url1", "url2", "url3"], "metadata": "data1"},
                    {"urls": ["url4", "url5"], "metadata": "data2"}
                ]
            }
            """;

            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
            var mapping = new DataFileMapping
            {
                InputMappings = new List<DataFileMappingItem>
                {
                    new() { KeyPath = new List<string> { "items", "urls" }, Type = "text", IsArray = true }
                },
                OutputMappings = new List<DataFileMappingItem>()
            };

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            var expectedInputCount = 2; // 2 JSON strings (current limitation)
            var actualInputCount = result?.InputData.Count ?? 0;

            // Verify that nested arrays are converted to JSON strings
            var firstInput = result?.InputData.FirstOrDefault()?.Content ?? "";
            var containsNestedArrayData = firstInput.Contains("url1") && firstInput.Contains("url2");

            var passed = actualInputCount == expectedInputCount && containsNestedArrayData;

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   Contains nested array data: {containsNestedArrayData}");
            Console.WriteLine($"   ⚠️  Test {(passed ? "PASSED" : "FAILED")} (Current Limitation Confirmed)");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private async Task<TestResult> TestEmptyArrays()
    {
        var testName = "Empty Arrays";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
            var jsonString = """
            {
                "tags": [],
                "title": "No tags example"
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

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            var expectedInputCount = 1; // Only title, empty array produces no inputs
            var actualInputCount = result?.InputData.Count ?? 0;

            var passed = actualInputCount == expectedInputCount && 
                        result?.InputData.First().Content == "No tags example";

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   ✅ Test {(passed ? "PASSED" : "FAILED")}");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private async Task<TestResult> TestDeepNesting()
    {
        var testName = "Deep Nesting";
        Console.WriteLine($"\n🔍 Running Test: {testName}");

        try
        {
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

            var result = await _dataFileService.CreateDataObjectFromJson(jsonObject, mapping, "test-dataset");

            var expectedInputCount = 2;
            var actualInputCount = result?.InputData.Count ?? 0;
            var expectedValues = new[] { "item1", "item2" };
            var actualValues = result?.InputData.Select(x => x.Content).ToArray() ?? Array.Empty<string>();

            var passed = actualInputCount == expectedInputCount && expectedValues.SequenceEqual(actualValues);

            Console.WriteLine($"   Expected inputs: {expectedInputCount}, Actual: {actualInputCount}");
            Console.WriteLine($"   Values match: {expectedValues.SequenceEqual(actualValues)}");
            Console.WriteLine($"   ✅ Test {(passed ? "PASSED" : "FAILED")}");

            return new TestResult { Name = testName, Passed = passed };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Test FAILED with exception: {ex.Message}");
            return new TestResult { Name = testName, Passed = false, Error = ex.Message };
        }
    }

    private void PrintTestSummary(List<TestResult> results)
    {
        Console.WriteLine("\n" + "=" * 50);
        Console.WriteLine("📊 TEST SUMMARY");
        Console.WriteLine("=" * 50);

        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);

        foreach (var result in results)
        {
            var status = result.Passed ? "✅ PASSED" : "❌ FAILED";
            Console.WriteLine($"{status}: {result.Name}");
            if (!result.Passed && !string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"         Error: {result.Error}");
            }
        }

        Console.WriteLine($"\nResults: {passed} passed, {failed} failed out of {results.Count} total");
        
        if (failed == 0)
        {
            Console.WriteLine("🎉 All tests passed! Array mapping functionality is working correctly.");
        }
        else
        {
            Console.WriteLine("⚠️  Some tests failed. Please review the implementation.");
        }
    }

    private class TestResult
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}

// Console application entry point for running tests
public class Program
{
    public static async Task Main(string[] args)
    {
        var testRunner = new ArrayMappingTestRunner();
        var allTestsPassed = await testRunner.RunAllTests();
        
        Environment.ExitCode = allTestsPassed ? 0 : 1;
    }
} 