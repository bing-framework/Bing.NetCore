using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Insert SQL Builder。
/// </summary>
public sealed class SqlInsertBuilder : SqlMutationBuilderBase, ISqlInsertBuilder
{
    /// <summary>
    /// Insert Select 来源查询的独立快照。
    /// </summary>
    private ISqlBuilder _selectQuery;

    /// <summary>
    /// 初始化一个 <see cref="SqlInsertBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">可共享的 Builder 服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="clauseFactory">Mutation Clause Factory。</param>
    public SqlInsertBuilder(ISqlProvider provider, SqlBuilderServices services, IParameterManager parameterManager = null,
        ISqlMutationClauseFactory clauseFactory = null)
        : base(provider, services, parameterManager, clauseFactory)
    {
        InsertClause = ClauseFactory.CreateInsert(MutationContext);
        InsertColumnsClause = ClauseFactory.CreateInsertColumns(MutationContext);
        ValuesClause = ClauseFactory.CreateValues(MutationContext);
    }

    /// <inheritdoc />
    public IInsertClause InsertClause { get; private set; }

    /// <inheritdoc />
    public IInsertColumnsClause InsertColumnsClause { get; private set; }

    /// <inheritdoc />
    public IValuesClause ValuesClause { get; private set; }

    /// <summary>
    /// 获取当前 Insert 是否使用查询结果作为数据源。
    /// </summary>
    internal bool IsInsertSelect => _selectQuery != null;

    /// <summary>实体生成 SQL 只接管空的默认 Builder，避免忽略调用方已有的可变子句及参数。</summary>
    internal bool CanUseGeneratedEntitySql => ClauseFactory is DefaultSqlMutationClauseFactory &&
        _selectQuery == null && InsertClause.Table == null && InsertColumnsClause.Columns.Count == 0 &&
        ValuesClause.RowCount == 0 && ReturningClause.IsEmpty && ParameterManager.Count == 0 &&
        ParameterManager.GetType() == typeof(ParameterManager);

    /// <inheritdoc />
    public ISqlInsertBuilder InsertSelect(ISqlBuilder query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (_selectQuery != null)
            throw new InvalidOperationException("Insert Select 数据源已经设置。");
        if (ValuesClause.RowCount != 0)
            throw new InvalidOperationException("Insert Select 不能与 Values 同时使用。");
        if (query.Provider == null || string.Equals(query.Provider.Key, Provider.Key, StringComparison.OrdinalIgnoreCase) == false)
            throw new InvalidOperationException("Insert Select 来源查询必须使用相同的 SQL Provider。");
        if (query is not SqlBuilderBase)
            throw new NotSupportedException("Insert Select 当前要求来源查询由 SqlBuilderBase 实现，以合并参数快照。");

        var snapshot = query.Clone();
        if (snapshot is not SqlBuilderBase || snapshot is not ISqlQueryClauseAccessor)
            throw new NotSupportedException("Insert Select 来源查询未提供可渲染的 Select 子句。");
        if (snapshot is ICteAccessor { CteItems.Count: > 0 } || snapshot is IUnionAccessor { IsUnion: true })
            throw new NotSupportedException("Insert Select 当前不支持 CTE 或 Union 来源查询。");
        if (snapshot.SelectClause.ProjectionCount is { } projectionCount &&
            InsertColumnsClause.Columns.Count > 0 && InsertColumnsClause.Columns.Count != projectionCount)
            throw new InvalidOperationException("Insert 目标列数量与 Select 投影数量不一致。");

        MutationContext.ValidateOperation(SqlOperationAction.InsertInto);
        _selectQuery = snapshot;
        return this;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            if (_selectQuery == null)
            {
                var validationContext = Validate(ParameterManager);
                AppendTargetAndReturning(builder, validationContext, startIndex, null);
                ValuesClause.AppendTo(builder);
                AppendReturning(builder, validationContext, SqlReturningClausePosition.End, startIndex);
                return;
            }

            var candidateParameters = ParameterManager.Clone();
            var selectSql = RenderSelectSource(candidateParameters);
            var selectValidationContext = Validate(candidateParameters);
            AppendTargetAndReturning(builder, selectValidationContext, startIndex, selectSql);
            AppendReturning(builder, selectValidationContext, SqlReturningClausePosition.End, startIndex);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public string ToSql() => Render(AppendTo);

    /// <inheritdoc />
    public SqlWriteCommand BuildCommand()
    {
        if (_selectQuery == null)
            return BuildCommand(ToSql);
        var candidateParameters = ParameterManager.Clone();
        var selectSql = RenderSelectSource(candidateParameters);
        var validationContext = Validate(candidateParameters);
        var sql = Render(builder =>
        {
            var startIndex = builder.Length;
            AppendTargetAndReturning(builder, validationContext, startIndex, selectSql);
            AppendReturning(builder, validationContext, SqlReturningClausePosition.End, startIndex);
        });
        return new SqlWriteCommand(sql, GetParameters(candidateParameters), Provider, SqlOperationKind.InsertSelect,
            !ReturningClause.IsEmpty);
    }

    /// <inheritdoc />
    public ISqlInsertBuilder New() => new SqlInsertBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <summary>实体计划已核对形状时复用生成 SQL，保留结构及参数上限验证。</summary>
    internal SqlWriteCommand BuildCommand(string generatedSql, bool hasReturning = false)
    {
        if (generatedSql == null || _selectQuery != null || hasReturning != !ReturningClause.IsEmpty)
            return BuildCommand();
        var context = Validate(ParameterManager);
        if (hasReturning)
            ReturningClause.Validate(context);
        return new SqlWriteCommand(generatedSql, GetParameters(), Provider, SqlOperationKind.InsertValues, hasReturning);
    }

    /// <inheritdoc />
    public ISqlInsertBuilder Clone()
    {
        var result = new SqlInsertBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.InsertClause = InsertClause.Clone(result.MutationContext);
        result.InsertColumnsClause = InsertColumnsClause.Clone(result.MutationContext);
        result.ValuesClause = ValuesClause.Clone(result.MutationContext);
        result.ReturningClause = ReturningClause.Clone(result.MutationContext);
        result._selectQuery = _selectQuery?.Clone();
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        InsertClause.Clear();
        InsertColumnsClause.Clear();
        ValuesClause.Clear();
        ReturningClause.Clear();
        _selectQuery = null;
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Insert 结构。
    /// </summary>
    private SqlValidationContext Validate(IParameterManager parameterManager)
    {
        var context = new SqlValidationContext(Provider, parameterManager.Count, false, SqlExecutionKind.Insert);
        InsertClause.Validate(context);
        if (_selectQuery == null)
        {
            InsertColumnsClause.Validate(context);
            ValuesClause.Validate(context);
            if (InsertColumnsClause.Columns.Count != ValuesClause.ColumnCount)
                throw new InvalidOperationException("Insert 列数量与 Values 列数量不一致。");
        }
        else
        {
            if (ValuesClause.RowCount != 0)
                throw new InvalidOperationException("Insert Select 不能与 Values 同时使用。");
            if (InsertColumnsClause.Columns.Count > 0 && _selectQuery.SelectClause.ProjectionCount is { } projectionCount &&
                InsertColumnsClause.Columns.Count != projectionCount)
                throw new InvalidOperationException("Insert Select 的目标列数量与查询输出列数量不一致。");
        }
        ValidateParameterLimit(parameterManager);
        return context;
    }

    /// <summary>
    /// 输出目标表、目标列和位于数据来源前的 Returning 子句。
    /// </summary>
    /// <param name="builder">接收 Insert SQL 的字符串构建器。</param>
    /// <param name="validationContext">当前 Insert 验证上下文。</param>
    /// <param name="startIndex">当前 Insert SQL 的缓冲区起始位置。</param>
    /// <param name="selectSql">Insert Select 来源 SQL；为空时不输出查询。</param>
    private void AppendTargetAndReturning(StringBuilder builder, SqlValidationContext validationContext,
        int startIndex, string selectSql)
    {
        InsertClause.AppendTo(builder);
        if (InsertColumnsClause.Columns.Count > 0)
            InsertColumnsClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.BeforeSource, startIndex);
        if (selectSql != null)
            builder.Append(" \r\n").Append(selectSql);
    }

    /// <summary>
    /// 在独立参数候选管理器中渲染并合并来源查询参数。
    /// </summary>
    /// <param name="parameterManager">接收查询参数的候选管理器。</param>
    /// <returns>来源查询 SQL。</returns>
    private string RenderSelectSource(IParameterManager parameterManager)
    {
        if (_selectQuery is not SqlBuilderBase sqlBuilder)
            throw new NotSupportedException("Insert Select 当前要求来源查询由 SqlBuilderBase 实现，以合并参数快照。");
        var parameterNames = new Dictionary<object, Dictionary<string, string>>();
        Dictionary<string, SqlFragmentParameterBinding> parameterKeys = null;
        return sqlBuilder.RenderSubquery(_selectQuery, parameterManager, parameterNames, ref parameterKeys);
    }
}
