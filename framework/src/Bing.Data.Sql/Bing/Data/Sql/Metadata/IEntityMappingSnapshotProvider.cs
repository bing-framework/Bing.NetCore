namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 为查询或 Builder 生命周期提供稳定的实体映射解析器。
/// </summary>
public interface IEntityMappingSnapshotProvider
{
    /// <summary>
    /// 捕获当前已发布的实体映射解析器。
    /// </summary>
    /// <returns>当前已发布的实体映射解析器快照。</returns>
    IEntityMappingResolver CaptureSnapshot();
}
