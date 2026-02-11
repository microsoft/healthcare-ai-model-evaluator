using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Cosmos;

public sealed class CosmosContainerProvider
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;

    public CosmosContainerProvider(CosmosClient client, string databaseName)
    {
        _client = client;
        _databaseName = databaseName;
    }

    public Container GetContainer(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name is required", nameof(containerName));
        }

        return _client.GetContainer(_databaseName, containerName);
    }
}
