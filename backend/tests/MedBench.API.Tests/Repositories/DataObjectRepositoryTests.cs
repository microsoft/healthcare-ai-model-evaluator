using Xunit;
using Moq;
using MedBench.Core.Models;
using MedBench.Core.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Threading;

namespace MedBench.API.Tests.Repositories
{
    public class DataObjectRepositoryTests
    {
        private readonly Mock<Container> _mockDataObjects;
        private readonly Mock<Container> _mockDataSets;
        private readonly DataObjectRepository _repository;

        public DataObjectRepositoryTests()
        {
            _mockDataObjects = new Mock<Container>();
            _mockDataSets = new Mock<Container>();

            _mockDataSets
                .Setup(c => c.PatchItemAsync<DataSet>(
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<IReadOnlyList<PatchOperation>>(),
                    It.IsAny<PatchItemRequestOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ItemResponse<DataSet>>());

            _mockDataObjects
                .Setup(c => c.CreateItemAsync(
                    It.IsAny<DataObject>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ItemResponse<DataObject>>());

            _repository = new DataObjectRepository(_mockDataObjects.Object, _mockDataSets.Object);
        }

        [Fact]
        public async Task CreateManyAsync_ShouldUpdateDataSetCount()
        {
            // Arrange
            var dataSetId = "testDataSetId";
            var dataObjects = new List<DataObject>
            {
                new DataObject { DataSetId = dataSetId },
                new DataObject { DataSetId = dataSetId }
            };

            // Act
            var result = (await _repository.CreateManyAsync(dataObjects)).ToList();

            // Assert
            Assert.Equal(2, result.Count);

            _mockDataSets.Verify(c => c.PatchItemAsync<DataSet>(
                dataSetId,
                It.IsAny<PartitionKey>(),
                It.IsAny<IReadOnlyList<PatchOperation>>(),
                It.IsAny<PatchItemRequestOptions?>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockDataObjects.Verify(c => c.CreateItemAsync(
                It.IsAny<DataObject>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
} 