namespace MedBench.Core.Interfaces;

/// <summary>
/// Service for migrating existing models to use Key Vault storage for integration settings
/// </summary>
public interface IModelMigrationService
{
    /// <summary>
    /// Migrates all existing models that have integration settings but no secret references
    /// to store their integration settings in Key Vault
    /// </summary>
    /// <returns>Number of models migrated</returns>
    Task<int> MigrateModelsToKeyVaultAsync();

    /// <summary>
    /// Checks if there are any models that need migration
    /// </summary>
    /// <returns>True if migration is needed, false otherwise</returns>
    Task<bool> IsMigrationNeededAsync();
}