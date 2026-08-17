using Bing.Data.Filters;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 过滤器可见的查询来源、拓扑和谓词贡献上下文。
/// </summary>
/// <remarks>
/// 此类型只暴露稳定的过滤扩展点。过滤器不能访问或转换具体 Clause、参数管理器和 Builder 内部状态，
/// 所有谓词由渲染器在完整 Join 拓扑已知时统一放置。
/// </remarks>
public sealed class SqlFilterContext
{
    /// <summary>
    /// 过滤器提交且尚未放置的来源级谓词集合。
    /// </summary>
    private readonly List<SqlFilterPredicate> _predicates = new();

    /// <summary>
    /// 当前查询图中可参与全局过滤的结构化表来源。
    /// </summary>
    public IReadOnlyList<SqlFilterSource> Sources { get; }

    /// <summary>
    /// 当前查询图按 SQL 连接顺序排列的 Join 拓扑。
    /// </summary>
    public IReadOnlyList<SqlFilterJoin> Joins { get; }

    /// <summary>
    /// 当前 Builder 固定使用的数据库上下文。
    /// </summary>
    public DatabaseContext DatabaseContext { get; }

    /// <summary>
    /// 初始化过滤器贡献上下文。
    /// </summary>
    /// <param name="dialect">当前 SQL 方言。</param>
    /// <param name="clause">当前查询子句访问器，仅由框架内部用于提取拓扑。</param>
    /// <param name="services">Builder 共享服务。</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文。</param>
    internal SqlFilterContext(IDialect dialect, ISqlQueryClauseAccessor clause, SqlBuilderServices services,
        DatabaseContext databaseContext = null)
    {
        if (clause == null)
            throw new ArgumentNullException(nameof(clause));
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var roots = (clause.FromClause as FromClause)?.Sources ?? Array.Empty<TableSource>();
        var joinClause = clause.JoinClause as JoinClause;
        Sources = roots.Concat(joinClause?.GetTypedSources() ?? Array.Empty<TableSource>())
            .Select(source => new SqlFilterSource(source.SourceId, source.EntityType, source.Alias,
                roots.Contains(source) ? SqlFilterSourceKind.Root : SqlFilterSourceKind.Join))
            .ToArray();
        Joins = joinClause?.GetFilterTopology(roots) ?? Array.Empty<SqlFilterJoin>();
        DatabaseContext = DatabaseContextSnapshot.Create(databaseContext) ??
                          services.DatabaseContextResolver?.Resolve(services.Options) ??
                          services.Options.GetDatabaseContext() ?? services.DatabaseContextAccessor?.Current ??
                          services.MetadataOptions?.DefaultDatabaseContext;
        Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        EntityMappingResolver = services.EntityMappingResolver;
        EntityModelMetadataProvider = services.EntityModelMetadataProvider;
        DataFilter = services.DataFilter;
    }

    /// <summary>
    /// 当前 Builder 的 SQL 方言，仅用于从实体属性构造安全列引用。
    /// </summary>
    private IDialect Dialect { get; }

    /// <summary>
    /// 实体映射解析器，仅用于从实体属性构造安全列引用。
    /// </summary>
    private IEntityMappingResolver EntityMappingResolver { get; }

    /// <summary>
    /// 实体原始元数据提供器，映射未提供字段时作为回退。
    /// </summary>
    private IEntityModelMetadataProvider EntityModelMetadataProvider { get; }

    /// <summary>
    /// 当前异步执行流的数据过滤状态。
    /// </summary>
    private IDataFilter DataFilter { get; }

    /// <summary>
    /// 判断指定过滤状态键是否在当前执行流中启用。
    /// </summary>
    /// <typeparam name="TFilter">过滤器的稳定状态键。</typeparam>
    /// <returns>未显式禁用时返回 <see langword="true"/>。</returns>
    public bool IsEnabled<TFilter>() where TFilter : class => DataFilter?.IsEnabled<TFilter>() != false;

    /// <summary>
    /// 为指定实体来源解析成员的方言安全列引用。
    /// </summary>
    /// <param name="source">过滤器当前处理的来源。</param>
    /// <param name="propertyName">实体属性名称。</param>
    /// <param name="required">找不到映射时是否拒绝继续构建 SQL。</param>
    /// <returns>包含来源别名的安全列引用。</returns>
    /// <exception cref="InvalidOperationException">来源没有实体、别名或必须的字段映射时抛出。</exception>
    public string GetColumn(SqlFilterSource source, string propertyName, bool required = false)
    {
        if (source?.EntityType == null)
            throw new InvalidOperationException("过滤器只能为结构化实体来源创建谓词。");
        if (string.IsNullOrWhiteSpace(source.Alias))
            throw new InvalidOperationException($"过滤器来源 {source.SourceId} 缺少表别名。");
        var column = ResolveColumn(source.EntityType, propertyName);
        if (string.IsNullOrWhiteSpace(column))
        {
            if (required)
                throw new InvalidOperationException($"实体 {source.EntityType.Name} 的属性 {propertyName} 未映射到数据库列。");
            column = propertyName;
        }
        return $"{Dialect.SafeName(source.Alias)}.{Dialect.SafeName(column)}";
    }

    /// <summary>
    /// 提交一个由框架统一创建参数并按完整 Join 拓扑放置的来源级谓词。
    /// </summary>
    /// <param name="source">谓词归属的结构化来源。</param>
    /// <param name="column">已方言转义的完整列引用。</param>
    /// <param name="value">参数值。</param>
    public void AddPredicate(SqlFilterSource source, string column, object value)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentException("过滤器谓词列不能为空。", nameof(column));
        _predicates.Add(new SqlFilterPredicate(source.SourceId, column, value, Operator.Equal));
    }

    /// <summary>
    /// 获取当前过滤器贡献的内部谓词快照。
    /// </summary>
    /// <returns>按过滤器提交顺序排列的不可变快照。</returns>
    internal IReadOnlyList<SqlFilterPredicate> GetPredicates() => _predicates.ToArray();

    /// <summary>
    /// 从实体映射或原始模型元数据中解析列名。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>已映射列名；无法解析时返回 <see langword="null"/>。</returns>
    private string ResolveColumn(Type entityType, string propertyName)
    {
        var mapping = EntityMappingResolver?.Resolve(entityType, DatabaseContext);
        if (mapping?.Columns != null)
        {
            if (mapping.Columns.TryGetValue(propertyName, out var column))
                return column.ColumnName;
            var mappedColumn = mapping.Columns.Values.FirstOrDefault(item =>
                string.Equals(item.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (mappedColumn != null)
                return mappedColumn.ColumnName;
        }
        var model = EntityModelMetadataProvider?.GetMetadata(entityType);
        return model?.Properties != null && model.Properties.TryGetValue(propertyName, out var property)
            ? property.ColumnName
            : null;
    }
}
