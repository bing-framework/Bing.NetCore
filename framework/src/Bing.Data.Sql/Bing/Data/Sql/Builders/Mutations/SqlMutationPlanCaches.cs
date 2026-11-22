using System.Runtime.CompilerServices;
using Bing.Data.Sql.Configs;
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
    private static readonly ConditionalWeakTable<IEntityMappingResolver, CachePartitions> Caches = new();

    /// <summary>
    /// 获取映射解析器对应的缓存。
    /// </summary>
    /// <param name="mappingResolver">决定实体映射配置边界的解析器实例。</param>
    /// <param name="metadataOptions">提供缓存容量配置的元数据选项。</param>
    /// <param name="models">生成式模型提供程序。</param>
    /// <returns>与该解析器实例绑定的 Mutation 缓存。</returns>
    public static SqlMutationPlanCache Get(IEntityMappingResolver mappingResolver, SqlMetadataOptions metadataOptions,
        IEntityModelMetadataProvider models = null)
    {
        if (mappingResolver == null)
            throw new ArgumentNullException(nameof(mappingResolver));
        return Caches.GetValue(mappingResolver, _ => new CachePartitions()).Get(metadataOptions, models);
    }

    /// <summary>按模型登记与严格模式隔离；每个分区仍沿用首次创建时的容量策略。</summary>
    private sealed class CachePartitions
    {
        private readonly List<(IEntityModelMetadataProvider Models, bool Strict, SqlMutationPlanCache Cache)> _items = new();

        internal SqlMutationPlanCache Get(SqlMetadataOptions options, IEntityModelMetadataProvider models)
        {
            var strict = options?.ExecutionMode == SqlExecutionMode.GeneratedOnly;
            var plans = options?.MutationPlanCacheCapacity;
            var getters = options?.MutationGetterCacheCapacity;
            lock (_items)
            {
                foreach (var item in _items)
                    if (ReferenceEquals(item.Models, models) && item.Strict == strict)
                        return item.Cache;
                var cache = new SqlMutationPlanCache(plans, getters, models, strict);
                _items.Add((models, strict, cache));
                return cache;
            }
        }
    }
}
