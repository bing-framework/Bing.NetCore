using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射缓存键
/// </summary>
/// <param name="EntityTypeHandle">实体类型句柄</param>
/// <param name="DbKey">数据源标识</param>
/// <param name="MappingProfile">映射配置名称</param>
/// <param name="TableRouteKey">表路由键</param>
/// <param name="DatabaseType">数据库类型</param>
/// <param name="Catalog">数据库目录</param>
/// <param name="PhysicalSchema">物理架构</param>
/// <param name="LogicalSchema">逻辑架构</param>
/// <param name="NamingMode">逻辑命名方式</param>
/// <param name="SchemaCompatibilityMode">旧架构兼容方式</param>
/// <param name="DatabaseLink">数据库链接名称</param>
/// <param name="AttachedAlias">SQLite 已附加数据库别名</param>
internal readonly record struct EntityMappingCacheKey(
    RuntimeTypeHandle EntityTypeHandle,
    string DbKey,
    string MappingProfile,
    string TableRouteKey,
    DatabaseType? DatabaseType,
    string Catalog,
    string PhysicalSchema,
    string LogicalSchema,
    LogicalTableNamingMode NamingMode,
    SchemaCompatibilityMode SchemaCompatibilityMode,
    string DatabaseLink,
    string AttachedAlias);
