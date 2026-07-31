using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// Provider 专用批量 Update 渲染所需的结构化输入。
/// </summary>
public sealed class SqlBatchUpdateRenderContext
{
    /// <summary>
    /// 当前映射解析器分区内的编译属性 Getter 缓存。
    /// </summary>
    private readonly SqlMutationPlanCache _planCache;

    /// <summary>
    /// 初始化批量 Update 渲染上下文。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">Builder 共享服务。</param>
    /// <param name="databaseContext">本批命令固定使用的数据库上下文。</param>
    /// <param name="mapping">待更新实体的表和列映射。</param>
    /// <param name="updateColumns">需要写入的非键列。</param>
    /// <param name="keys">用于匹配目标行的主键列。</param>
    /// <param name="concurrencyColumns">用于并发校验的令牌列。</param>
    /// <param name="entities">本批待更新实体快照。</param>
    /// <param name="options">Update 的并发与原始值选项。</param>
    public SqlBatchUpdateRenderContext(ISqlProvider provider, SqlBuilderServices services, DatabaseContext databaseContext,
        EntityMappingMetadata mapping, IReadOnlyList<ColumnMappingMetadata> updateColumns,
        IReadOnlyList<ColumnMappingMetadata> keys, IReadOnlyList<ColumnMappingMetadata> concurrencyColumns,
        IReadOnlyCollection<object> entities, SqlUpdateOptions options)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        DatabaseContext = databaseContext;
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        UpdateColumns = updateColumns ?? throw new ArgumentNullException(nameof(updateColumns));
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
        ConcurrencyColumns = concurrencyColumns ?? throw new ArgumentNullException(nameof(concurrencyColumns));
        Entities = entities?.ToArray() ?? throw new ArgumentNullException(nameof(entities));
        Options = options;
        _planCache = SqlMutationPlanCaches.Get(Services.EntityMappingResolver);
    }

    /// <summary>当前 SQL Provider。</summary>
    public ISqlProvider Provider { get; }

    /// <summary>Builder 共享服务。</summary>
    public SqlBuilderServices Services { get; }

    /// <summary>当前数据库上下文。</summary>
    public DatabaseContext DatabaseContext { get; }

    /// <summary>实体映射信息。</summary>
    public EntityMappingMetadata Mapping { get; }

    /// <summary>需要写入的列。</summary>
    public IReadOnlyList<ColumnMappingMetadata> UpdateColumns { get; }

    /// <summary>主键列。</summary>
    public IReadOnlyList<ColumnMappingMetadata> Keys { get; }

    /// <summary>并发令牌列。</summary>
    public IReadOnlyList<ColumnMappingMetadata> ConcurrencyColumns { get; }

    /// <summary>待更新实体。</summary>
    public IReadOnlyList<object> Entities { get; }

    /// <summary>更新选项。</summary>
    public SqlUpdateOptions Options { get; }

    /// <summary>
    /// 获取指定实体并发列在更新条件中应使用的值。
    /// </summary>
    /// <param name="entity">当前实体。</param>
    /// <param name="column">并发列映射。</param>
    /// <returns>已配置原始值；未配置时返回实体当前值。</returns>
    public object GetConcurrencyValue(object entity, ColumnMappingMetadata column)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        return TryGetOriginalValue(column.PropertyName, out var value)
            ? value
            : GetValue(entity, column);
    }

    /// <summary>
    /// 尝试读取更新选项中的并发原始值。
    /// </summary>
    /// <param name="propertyName">并发属性名称。</param>
    /// <param name="value">读取到的显式原始值。</param>
    /// <returns><c>true</c> 表示已配置原始值；<c>false</c> 时调用方应回退到实体当前值。</returns>
    private bool TryGetOriginalValue(string propertyName, out object value)
    {
        if (Options != null)
            return Options.TryGetOriginalValue(propertyName, out value);
        value = null;
        return false;
    }

    /// <summary>
    /// 获取指定源对象的列值。
    /// </summary>
    /// <param name="source">实体或并发原始值对象。</param>
    /// <param name="column">列映射。</param>
    /// <returns>属性值。</returns>
    public object GetValue(object source, ColumnMappingMetadata column)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        return _planCache.GetValue(source, column);
    }
}