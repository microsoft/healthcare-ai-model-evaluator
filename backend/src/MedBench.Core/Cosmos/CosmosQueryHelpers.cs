using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Cosmos;

internal static class CosmosQueryHelpers
{
    public static async Task<List<T>> QueryAsync<T>(Container container, QueryDefinition query, CancellationToken cancellationToken = default)
    {
        var results = new List<T>();

        using var iterator = container.GetItemQueryIterator<T>(
            queryDefinition: query,
            requestOptions: new QueryRequestOptions { MaxBufferedItemCount = 100, MaxConcurrency = -1 });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Resource);
        }

        return results;
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(Container container, QueryDefinition query, CancellationToken cancellationToken = default)
    {
        using var iterator = container.GetItemQueryIterator<T>(
            queryDefinition: query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        if (!iterator.HasMoreResults)
        {
            return default;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.Resource.FirstOrDefault();
    }

    public static async Task<TScalar> QueryScalarAsync<TScalar>(Container container, QueryDefinition query, CancellationToken cancellationToken = default)
    {
        using var iterator = container.GetItemQueryIterator<TScalar>(
            queryDefinition: query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.Resource.First();
    }
}
