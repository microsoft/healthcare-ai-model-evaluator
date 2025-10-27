using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using MedBench.Core.Services;
namespace MedBench.API.Tests.Controllers
{
    public class TrialsControllerTests
    {
        private readonly Mock<ITrialRepository> _mockRepository;
        private readonly Mock<IExperimentRepository> _mockExperimentRepo;
        private readonly Mock<ILogger<TrialsController>> _mockLogger;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ITestScenarioRepository> _mockTestScenarioRepository;
        private readonly Mock<IModelRepository> _mockModelRepository;
        private readonly Mock<IClinicalTaskRepository> _mockClinicalTaskRepository;
        private readonly Mock<ILogger<StatCalculatorService>> _mockStatLogger;
        private readonly StatCalculatorService _statCalculatorService;
        private readonly TrialsController _controller;
        private readonly string _userId = "test-user-id";

        public TrialsControllerTests()
        {
            _mockRepository = new Mock<ITrialRepository>();
            _mockExperimentRepo = new Mock<IExperimentRepository>();
            _mockLogger = new Mock<ILogger<TrialsController>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTestScenarioRepository = new Mock<ITestScenarioRepository>();
            _mockModelRepository = new Mock<IModelRepository>();
            _mockClinicalTaskRepository = new Mock<IClinicalTaskRepository>();
            _mockStatLogger = new Mock<ILogger<StatCalculatorService>>();
            
            // Create real StatCalculatorService with mocked dependencies
            _statCalculatorService = new StatCalculatorService(
                _mockRepository.Object,
                _mockUserRepository.Object,
                _mockStatLogger.Object,
                _mockExperimentRepo.Object,
                _mockTestScenarioRepository.Object,
                _mockModelRepository.Object,
                _mockClinicalTaskRepository.Object
            );
            
            _controller = new TrialsController(
                _mockRepository.Object, 
                _mockExperimentRepo.Object, 
                _mockUserRepository.Object, 
                _mockLogger.Object, 
                _statCalculatorService,
                _mockTestScenarioRepository.Object
            );

            // Setup ClaimsPrincipal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task GetPendingTrialCounts_ReturnsOkResult_WithCounts()
        {
            // Arrange
            var expectedCounts = new Dictionary<string, int>
            {
                { "Simple Evaluation", 5 },
                { "Simple Validation", 3 }
            };

            _mockRepository.Setup(repo => repo.GetPendingTrialCountsByType(_userId, It.IsAny<string[]>(), It.IsAny<string[]>()))
                .ReturnsAsync(expectedCounts);

            // Act
            var result = await _controller.GetPendingTrialCounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCounts = Assert.IsType<Dictionary<string, int>>(okResult.Value);
            Assert.Equal(expectedCounts, returnedCounts);
        }

        [Fact]
        public async Task GetNextPendingTrial_ReturnsOkResult_WhenTrialExists()
        {
            // Arrange
            var trials = new List<Trial>
            {
                new Trial 
                { 
                    Id = "1", 
                    Status = "pending",
                    ExperimentType = "Simple Evaluation"
                }
            };

            _mockRepository.Setup(repo => repo.GetPendingTrialsAsync(_userId))
                .ReturnsAsync(trials);

            // Act
            var result = await _controller.GetNextPendingTrial("Simple Evaluation");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedTrial = Assert.IsType<Trial>(okResult.Value);
            Assert.Equal("1", returnedTrial.Id);
        }

        [Fact]
        public async Task GetNextPendingTrial_ReturnsNotFound_WhenNoTrialsAvailable()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.GetPendingTrialsAsync(_userId))
                .ReturnsAsync(new List<Trial>());

            // Act
            var result = await _controller.GetNextPendingTrial("Simple Evaluation");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var message = Assert.IsType<Anonymous>(notFoundResult.Value);
            Assert.Equal("No pending trials available.", message.message);
        }

        [Fact]
        public async Task UpdateTrial_ReturnsOkResult_WithUpdatedTrial()
        {
            // Arrange
            var existingTrial = new Trial 
            { 
                Id = "1", 
                UserId = _userId,
                ExperimentId = "exp1",
                Status = "pending",
                Response = new TrialResponse(),
                Flags = new List<TrialFlag>(),
                ModelOutputs = new List<ModelOutput>(),
                StartedOn = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                DataObjectId = "data1"
            };
            
            var updateDto = new TrialUpdateDto
            {
                Status = "done",
                Response = new TrialResponse { Text = "Updated response" },
                Flags = new List<TrialFlag> { new TrialFlag { Text = "flag1" } }
            };

            var user = new User 
            { 
                Id = _userId, 
                Stats = new Dictionary<string, string>() 
            };

            _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
                .ReturnsAsync(existingTrial);
            _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Trial>()))
                .ReturnsAsync((Trial t) => t);
            _mockUserRepository.Setup(repo => repo.GetByIdAsync(_userId))
                .ReturnsAsync(user);
            _mockUserRepository.Setup(repo => repo.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            _mockExperimentRepo.Setup(repo => repo.GetByIdAsync("exp1"))
                .ReturnsAsync(new Experiment { Id = "exp1" });
            _mockExperimentRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Experiment>()))
                .ReturnsAsync((Experiment e) => e);

            // Mock for StatCalculatorService calls
            _mockRepository.Setup(repo => repo.GetTrialsByExperimentAndDataObject("exp1", "data1"))
                .ReturnsAsync(new List<Trial>());

            // Act
            var result = await _controller.UpdateTrial("1", updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTrial = Assert.IsType<Trial>(okResult.Value);
            Assert.Equal("done", returnedTrial.Status);
            Assert.Equal("Updated response", returnedTrial.Response.Text);
            Assert.Single(returnedTrial.Flags);
        }

        private class Anonymous
        {
            public string message { get; set; } = string.Empty;
        }
    }
}