using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射缓存键
/// </summary>
/// <param name="EntityTypeHandle">实体类型句柄</param>
/// <param name="DbKey">数据源标识</param>
/// <param name="DatabaseType">数据库类型</param>
/// <param name="MappingProfile">映射配置名称</param>
/// <param name="TableRouteKey">表路由键</param>
internal readonly record struct EntityMappingCacheKey(
    RuntimeTypeHandle EntityTypeHandle,
    string DbKey,
    DatabaseType? DatabaseType,
    string MappingProfile,
    string TableRouteKey);
