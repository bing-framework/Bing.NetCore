using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 结构化写操作的数据边界类型。
/// </summary>
public enum SqlDataBoundaryOperation
{
    /// <summary>更新操作。</summary>
    Update,

    /// <summary>删除操作。</summary>
    Delete,

    /// <summary>逻辑删除操作。</summary>
    SoftDelete,

    /// <summary>恢复已逻辑删除实体的操作。</summary>
    Restore,

    /// <summary>显式物理清除操作。</summary>
    Purge
}

/// <summary>
/// 为结构化 Update 或 Delete 贡献数据隔离谓词的稳定扩展点。
/// </summary>
/// <remarks>
/// 实现不得直接操作 Mutation Clause、参数管理器或 Builder。应仅通过
/// <see cref="SqlDataBoundaryContext.AddEquals"/> 贡献由框架统一绑定参数的等值条件。
/// </remarks>
public interface ISqlDataBoundaryContributor
{
    /// <summary>
    /// 判断当前结构化目标和操作是否需要应用该数据边界。
    /// </summary>
    /// <param name="context">当前写入数据边界上下文。</param>
    /// <returns>需要追加边界时返回 <see langword="true"/>。</returns>
    bool ShouldApply(SqlDataBoundaryContext context);

    /// <summary>
    /// 为当前结构化写入目标贡献数据边界条件。
    /// </summary>
    /// <param name="context">当前写入数据边界上下文。</param>
    void Apply(SqlDataBoundaryContext context);
}

/// <summary>
/// 写入数据边界 Contributor 可见的受控上下文。
/// </summary>
public sealed class SqlDataBoundaryContext
{
    /// <summary>
    /// 当前写入的受控运行上下文。
    /// </summary>
    private readonly SqlMutationContext _mutationContext;

    /// <summary>
    /// 接收边界谓词的内部 Where 子句。
    /// </summary>
    private readonly IMutationWhereClause _whereClause;

    /// <summary>
    /// 初始化写入数据边界上下文。
    /// </summary>
    /// <param name="mutationContext">当前 Mutation 上下文。</param>
    /// <param name="target">结构化写入目标。</param>
    /// <param name="operation">当前写入操作类型。</param>
    /// <param name="whereClause">接收边界谓词的内部 Where 子句。</param>
    internal SqlDataBoundaryContext(SqlMutationContext mutationContext, SqlTableReference target,
        SqlDataBoundaryOperation operation, IMutationWhereClause whereClause)
    {
        _mutationContext = mutationContext ?? throw new ArgumentNullException(nameof(mutationContext));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Operation = operation;
        _whereClause = whereClause ?? throw new ArgumentNullException(nameof(whereClause));
    }

    /// <summary>
    /// 当前结构化写入目标的实体类型。
    /// </summary>
    public Type EntityType => Target.EntityType;

    /// <summary>
    /// 当前结构化写入目标。
    /// </summary>
    public SqlTableReference Target { get; }

    /// <summary>
    /// 当前写入操作类型。
    /// </summary>
    public SqlDataBoundaryOperation Operation { get; }

    /// <summary>
    /// 当前 Builder 固定使用的数据库上下文。
    /// </summary>
    public DatabaseContext DatabaseContext => _mutationContext.ExecutionContext.DatabaseContext;

    /// <summary>
    /// 判断指定过滤状态键是否在当前异步执行流中启用。
    /// </summary>
    /// <typeparam name="TFilter">过滤器稳定状态键。</typeparam>
    /// <returns>未显式禁用时返回 <see langword="true"/>。</returns>
    public bool IsEnabled<TFilter>() where TFilter : class =>
        _mutationContext.Services.DataFilter?.IsEnabled<TFilter>() != false;

    /// <summary>
    /// 为当前目标实体的已映射属性追加参数化等值条件。
    /// </summary>
    /// <param name="propertyName">实体属性名称。</param>
    /// <param name="value">条件参数值。</param>
    /// <exception cref="InvalidOperationException">目标未携带实体类型或属性未映射时抛出。</exception>
    public void AddEquals(string propertyName, object value)
    {
        if (EntityType == null)
            throw new InvalidOperationException("写入数据边界只能应用于结构化实体目标。");
        var mapping = _mutationContext.Services.EntityMappingResolver.Resolve(EntityType, DatabaseContext);
        var column = mapping?.Columns?.Values.FirstOrDefault(item =>
            string.Equals(item.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
        if (column == null)
            throw new InvalidOperationException($"实体 {EntityType.Name} 的属性 {propertyName} 未映射到数据库列。");
        var parameter = _mutationContext.Services.ParameterFactory.Create(_mutationContext.ParameterManager.GenerateName(),
            value, column, DatabaseContext, EntityType, SqlParameterSource.SqlBuilder);
        if (_mutationContext.ParameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            _mutationContext.ParameterManager.Add(parameter.Name, parameter.Value);
        var left = string.IsNullOrWhiteSpace(Target.Alias)
            ? _mutationContext.Dialect.SafeName(column.ColumnName)
            : $"{_mutationContext.Dialect.SafeName(Target.Alias)}.{_mutationContext.Dialect.SafeName(column.ColumnName)}";
        _whereClause.And(SqlConditionFactory.Create(left,
            _mutationContext.Dialect.GetParamName(parameter.Name), Operator.Equal));
    }
}
