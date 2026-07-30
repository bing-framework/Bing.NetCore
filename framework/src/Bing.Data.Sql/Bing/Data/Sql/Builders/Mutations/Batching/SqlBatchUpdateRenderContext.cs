using System.Reflection;
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
    /// 初始化批量 Update 渲染上下文。
    /// </summary>
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
        var property = source.GetType().GetProperty(column.PropertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property == null || property.GetIndexParameters().Length != 0)
            throw new InvalidOperationException($"原始值对象未包含属性 {column.PropertyName}。");
        return property.GetValue(source);
    }
}