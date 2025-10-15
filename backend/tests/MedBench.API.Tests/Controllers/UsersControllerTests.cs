using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace MedBench.API.Tests.Controllers
{
    public class UsersControllerTests
    {
        private readonly Mock<IUserRepository> _mockRepository;
        private readonly Mock<ILogger<UsersController>> _mockLogger;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _mockRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<UsersController>>();
            _controller = new UsersController(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkResult_WithUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = "1", Name = "User 1", Email = "user1@example.com", Roles = new List<string> { "user" } },
                new User { Id = "2", Name = "User 2", Email = "user2@example.com", Roles = new List<string> { "admin" } }
            };
            _mockRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(users);

            // Act
            var result = await _controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUsers = Assert.IsAssignableFrom<IEnumerable<User>>(okResult.Value);
            Assert.Equal(2, returnedUsers.Count());
        }

        [Fact]
        public async Task Get_WithValidId_ReturnsOkResult()
        {
            // Arrange
            var user = new User 
            { 
                Id = "1", 
                Name = "Test User", 
                Email = "test@example.com",
                Roles = new List<string> { "user" }
            };
            _mockRepository.Setup(repo => repo.GetByIdAsync("1"))
                .ReturnsAsync(user);

            // Act
            var result = await _controller.Get("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUser = Assert.IsType<User>(okResult.Value);
            Assert.Equal(user.Id, returnedUser.Id);
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
        public async Task Create_WithValidUser_ReturnsCreatedAtAction()
        {
            // Arrange
            var user = new User 
            { 
                Name = "New User",
                Email = "new@example.com",
                Roles = new List<string> { "user" }
            };
            var createdUser = new User 
            { 
                Id = "1",
                Name = "New User",
                Email = "new@example.com",
                Roles = new List<string> { "user" }
            };
            _mockRepository.Setup(repo => repo.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync(createdUser);

            // Act
            var result = await _controller.Create(user);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnedUser = Assert.IsType<User>(createdAtActionResult.Value);
            Assert.Equal(createdUser.Id, returnedUser.Id);
        }

        [Fact]
        public async Task Update_WithValidId_ReturnsOkResult()
        {
            // Arrange
            var user = new User 
            { 
                Id = "1",
                Name = "Updated User",
                Email = "updated@example.com",
                Roles = new List<string> { "user" }
            };
            _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            // Act
            var result = await _controller.Update("1", user);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUser = Assert.IsType<User>(okResult.Value);
            Assert.Equal(user.Id, returnedUser.Id);
        }

        [Fact]
        public async Task Update_WithMismatchedId_ReturnsBadRequest()
        {
            // Arrange
            var user = new User 
            { 
                Id = "2",
                Name = "Wrong Id",
                Email = "wrong@example.com",
                Roles = new List<string> { "user" }
            };

            // Act
            var result = await _controller.Update("1", user);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task Update_WithNonexistentUser_ReturnsNotFound()
        {
            // Arrange
            var user = new User 
            { 
                Id = "1",
                Name = "Not Found",
                Email = "notfound@example.com",
                Roles = new List<string> { "user" }
            };
            _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<User>()))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.Update("1", user);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task Delete_WithValidId_ReturnsNoContent()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.DeleteAsync("1"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete("1");

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.DeleteAsync("1"))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.Delete("1");

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
} 