using System.Runtime.CompilerServices;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 按映射解析器弱引用分区保存 Mutation 缓存，避免跨元数据配置共享计划。
/// </summary>
internal static class SqlMutationPlanCaches
{
    /// <summary>
    /// 按映射解析器实例弱引用分区保存的计划缓存，解析器释放后对应缓存可被回收。
    /// </summary>
    private static readonly ConditionalWeakTable<IEntityMappingResolver, SqlMutationPlanCache> Caches = new();

    /// <summary>
    /// 获取映射解析器对应的缓存。
    /// </summary>
    /// <param name="mappingResolver">决定实体映射配置边界的解析器实例。</param>
    /// <returns>与该解析器实例绑定的 Mutation 缓存。</returns>
    public static SqlMutationPlanCache Get(IEntityMappingResolver mappingResolver)
    {
        if (mappingResolver == null)
            throw new ArgumentNullException(nameof(mappingResolver));
        return Caches.GetValue(mappingResolver, _ => new SqlMutationPlanCache());
    }
}