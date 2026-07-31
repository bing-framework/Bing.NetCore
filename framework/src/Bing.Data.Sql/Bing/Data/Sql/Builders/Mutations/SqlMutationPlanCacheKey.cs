using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Mutation Plan 缓存键。
/// </summary>
/// <param name="EntityTypeHandle">实体运行时类型句柄。</param>
/// <param name="ProviderKey">决定 SQL 方言的规范化 Provider Key。</param>
/// <param name="Database">目标表所属数据库名称。</param>
/// <param name="Schema">目标表所属架构名称。</param>
/// <param name="Table">目标物理表名称。</param>
/// <param name="MappingProfile">参与实体映射解析的配置档案名称。</param>
/// <param name="TableRouteKey">参与表路由的分区标识。</param>
/// <param name="Operation">当前实体 Mutation 操作类型。</param>
/// <param name="IncludeSignature">显式包含属性集合的稳定签名。</param>
/// <param name="ExcludeSignature">显式排除属性集合的稳定签名。</param>
internal readonly record struct SqlMutationPlanCacheKey(RuntimeTypeHandle EntityTypeHandle, string ProviderKey,
    string Database, string Schema, string Table, string MappingProfile, string TableRouteKey,
    SqlMutationOperation Operation, string IncludeSignature, string ExcludeSignature)
{
    /// <summary>
    /// 根据映射和列筛选配置创建缓存键。
    /// </summary>
    /// <param name="mapping">实体表、路由和列映射信息。</param>
    /// <param name="providerKey">当前 SQL Provider Key。</param>
    /// <param name="operation">需要创建计划的 Mutation 操作。</param>
    /// <param name="includes">显式包含属性集合。</param>
    /// <param name="excludes">显式排除属性集合。</param>
    /// <returns>按 Provider、映射路由和属性筛选隔离的规范化缓存键。</returns>
    public static SqlMutationPlanCacheKey Create(EntityMappingMetadata mapping, string providerKey,
        SqlMutationOperation operation, IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        var table = mapping.Table;
        return new SqlMutationPlanCacheKey(mapping.EntityType.TypeHandle, Normalize(providerKey), Normalize(table?.Database),
            Normalize(table?.Schema), Normalize(table?.TableName), Normalize(mapping.MappingProfile),
            Normalize(mapping.TableRouteKey), operation, CreateSignature(includes), CreateSignature(excludes));
    }

    /// <summary>
    /// 将可选缓存键片段规范化为大小写无关值。
    /// </summary>
    /// <param name="value">待规范化的缓存键片段。</param>
    /// <returns>去除首尾空白并转换为大写的值；空值返回空字符串。</returns>
    private static string Normalize(string value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// 创建属性集合的顺序无关稳定签名。
    /// </summary>
    /// <param name="properties">待签名的属性集合。</param>
    /// <returns>去除空白、规范化、去重并排序后的属性签名；空集合返回空字符串。</returns>
    private static string CreateSignature(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return string.Empty;
        return string.Join("\u001F", properties.Where(property => string.IsNullOrWhiteSpace(property) == false)
            .Select(Normalize).Distinct(StringComparer.Ordinal).OrderBy(property => property, StringComparer.Ordinal));
    }
}