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

            // Setup the database to return the mock collections
            _mockDb.Setup(db => db.GetCollection<DataObject>("DataObjects", null))
                .Returns(_mockCollection.Object);
            _mockDb.Setup(db => db.GetCollection<DataSet>("DataSets", null))
                .Returns(_mockDataSetCollection.Object);

            // Mock the index manager to avoid null reference exception during index creation
            var mockIndexManager = new Mock<IMongoIndexManager<DataObject>>();
            _mockCollection.Setup(c => c.Indexes).Returns(mockIndexManager.Object);

            _repository = new DataObjectRepository(_mockDb.Object);
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
    }
} 