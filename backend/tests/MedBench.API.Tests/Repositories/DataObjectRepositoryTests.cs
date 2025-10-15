using Xunit;
using Moq;
using MongoDB.Driver;
using MedBench.Core.Models;
using MedBench.Core.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace MedBench.API.Tests.Repositories
{
    public class DataObjectRepositoryTests
    {
        private readonly Mock<IMongoCollection<DataObject>> _mockCollection;
        private readonly Mock<IMongoCollection<DataSet>> _mockDataSetCollection;
        private readonly Mock<IMongoDatabase> _mockDb;
        private readonly DataObjectRepository _repository;

        public DataObjectRepositoryTests()
        {
            _mockCollection = new Mock<IMongoCollection<DataObject>>();
            _mockDataSetCollection = new Mock<IMongoCollection<DataSet>>();
            _mockDb = new Mock<IMongoDatabase>();

            _mockDb.Setup(db => db.GetCollection<DataObject>("DataObjects", null))
                .Returns(_mockCollection.Object);
            _mockDb.Setup(db => db.GetCollection<DataSet>("DataSets", null))
                .Returns(_mockDataSetCollection.Object);

            _repository = new DataObjectRepository(_mockDb.Object);
        }

        [Fact]
        public async Task GetByDataSetIdAsync_ShouldReturnDataObjects()
        {
            // Arrange
            var dataSetId = "testDataSetId";
            var expectedDataObjects = new List<DataObject>
            {
                new DataObject { Id = "1", DataSetId = dataSetId },
                new DataObject { Id = "2", DataSetId = dataSetId }
            };

            var mockCursor = new Mock<IAsyncCursor<DataObject>>();
            mockCursor.Setup(c => c.Current).Returns(expectedDataObjects);
            mockCursor.SetupSequence(c => c.MoveNext(default))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<DataObject>>(),
                It.IsAny<FindOptions<DataObject>>(),
                default))
                .ReturnsAsync(mockCursor.Object);

            // Act
            var result = await _repository.GetByDataSetIdAsync(dataSetId);

            // Assert
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Equal(expectedDataObjects[0].Id, resultList[0].Id);
            Assert.Equal(expectedDataObjects[1].Id, resultList[1].Id);
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

            _mockDataSetCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<DataSet>>(),
                It.IsAny<UpdateDefinition<DataSet>>(),
                It.IsAny<UpdateOptions>(),
                default))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            // Act
            var result = await _repository.CreateManyAsync(dataObjects);

            // Assert
            Assert.Equal(2, result.Count());
            _mockDataSetCollection.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<DataSet>>(),
                It.IsAny<UpdateDefinition<DataSet>>(),
                It.IsAny<UpdateOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task DeleteByDataSetIdAsync_ShouldUpdateDataSetCount()
        {
            // Arrange
            var dataSetId = "testDataSetId";
            
            _mockCollection.Setup(c => c.CountDocumentsAsync(
                It.IsAny<FilterDefinition<DataObject>>(),
                It.IsAny<CountOptions>(),
                default))
                .ReturnsAsync(2);

            // Act
            await _repository.DeleteByDataSetIdAsync(dataSetId);

            // Assert
            _mockDataSetCollection.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<DataSet>>(),
                It.IsAny<UpdateDefinition<DataSet>>(),
                It.IsAny<UpdateOptions>(),
                default), Times.Once);
        }
    }
} 