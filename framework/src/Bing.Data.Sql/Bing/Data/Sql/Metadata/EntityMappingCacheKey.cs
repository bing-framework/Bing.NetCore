using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射缓存键
/// </summary>
/// <param name="EntityType">实体类型</param>
/// <param name="DbKey">数据库标识</param>
/// <param name="DatabaseType">数据库类型</param>
/// <param name="Role">数据库角色</param>
/// <param name="Schema">架构</param>
/// <param name="TableRouteKey">表路由键</param>
/// <param name="MappingVersion">映射版本</param>
public sealed record EntityMappingCacheKey(
    Type EntityType,
    string DbKey,
    DatabaseType DatabaseType,
    DatabaseRole Role,
    string Schema,
    string TableRouteKey,
    string MappingVersion);
