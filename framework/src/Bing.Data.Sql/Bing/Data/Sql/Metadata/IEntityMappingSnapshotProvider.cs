namespace Bing.Data.Sql.Metadata;

/// <summary>
/// Provides a stable entity mapping resolver for a query or builder lifetime.
/// </summary>
public interface IEntityMappingSnapshotProvider
{
    /// <summary>Captures the currently published mapping resolver.</summary>
    IEntityMappingResolver CaptureSnapshot();
}
