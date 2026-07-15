namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射缓存键
/// </summary>
/// <param name="EntityType">实体类型</param>
/// <param name="DbKey">数据源标识</param>
/// <param name="MappingProfile">映射配置名称</param>
/// <param name="Schema">架构</param>
/// <param name="TableRouteKey">表路由键</param>
public sealed record EntityMappingCacheKey(
    Type EntityType,
    string DbKey,
    string MappingProfile,
    string Schema,
    string TableRouteKey);
