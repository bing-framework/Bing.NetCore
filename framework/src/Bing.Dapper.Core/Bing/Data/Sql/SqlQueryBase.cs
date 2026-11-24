using System.Runtime.ExceptionServices;
using System.Collections;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Bing.Data.Sql;

/// <summary>只负责查询描述和查询终结方法的 SQL 查询入口。</summary>
public abstract partial class SqlQueryBase : SqlExecutionBase, ISqlQuery, ISqlQueryPlanExecutor, ISqlQueryBuilderSource
{
    /// <summary>初始化 SQL 查询入口。</summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">查询配置。</param>
    protected SqlQueryBase(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options, true)
    {
        var owner = CreateQueryResourceOwner();
        _queryResources = new SqlQueryResourceAccess(owner);
        _queryResourceCleanup = new SqlQueryResourceCleanup(owner);
    }

    private readonly SqlQueryResourceAccess _queryResources;
    private readonly SqlQueryResourceCleanup _queryResourceCleanup;
    private protected SqlQueryResourceAccess QueryResources => _queryResources;
    private protected SqlQueryResourceCleanup QueryResourceCleanup => _queryResourceCleanup;

    /// <inheritdoc />
    private protected override bool IsPrimaryReadTransaction => QueryResources.IsPrimaryReadTransaction;

    /// <summary>获取查询使用的可选事务。</summary>
    protected IDbTransaction GetQueryTransaction() => QueryResources.GetQueryTransaction();
    private protected Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken token = default) => QueryResources.GetQueryTransactionAsync(token);
    /// <summary>完成本次查询拥有的短事务。</summary>
    protected void CompleteQueryTransaction() => QueryResourceCleanup.CompleteQueryTransaction();
    private protected Task CompleteQueryTransactionAsync(CancellationToken token = default) => QueryResourceCleanup.CompleteQueryTransactionAsync(token);
    /// <summary>回滚本次查询拥有的短事务或标记工作单元失败。</summary>
    protected void RollbackQueryTransaction() => QueryResourceCleanup.RollbackQueryTransaction();
    private protected Task RollbackQueryTransactionAsync() => QueryResourceCleanup.RollbackQueryTransactionAsync();

    private protected override bool HasBuilderState => _sqlBuilder != null;

    /// <summary>
    /// Sql生成器
    /// </summary>
    private ISqlBuilder _sqlBuilder;

    /// <summary>
    /// 当前是否正在执行独立查询描述。
    /// </summary>
    /// <remarks>
    /// 独立查询描述复用 Root Query 的连接和诊断生命周期，但不能清空 Root Builder。
    /// </remarks>
    private int _queryPlanExecutionDepth;

    /// <summary>
    /// 获取Sql生成器
    /// </summary>
    protected ISqlBuilder SqlBuilder => _sqlBuilder ??= CreateSqlBuilder();

    /// <summary>
    /// 获取参数管理器
    /// </summary>
    protected IParameterManager ParameterManager => ((ISqlCommonPartAccessor)SqlBuilder).ParameterManager;

    /// <summary>
    /// 获取Select子句
    /// </summary>
    protected ISelectClause SelectClause => ((ISqlQueryClauseAccessor)SqlBuilder).SelectClause;

    /// <summary>
    /// 获取From子句
    /// </summary>
    protected IFromClause FromClause => ((ISqlQueryClauseAccessor)SqlBuilder).FromClause;

    /// <summary>
    /// 获取Join子句
    /// </summary>
    protected IJoinClause JoinClause => ((ISqlQueryClauseAccessor)SqlBuilder).JoinClause;

    /// <summary>
    /// 获取Where子句
    /// </summary>
    protected IWhereClause WhereClause => ((ISqlQueryClauseAccessor)SqlBuilder).WhereClause;

    /// <summary>
    /// 获取GroupBy子句
    /// </summary>
    protected IGroupByClause GroupByClause => ((ISqlQueryClauseAccessor)SqlBuilder).GroupByClause;

    /// <summary>
    /// 获取OrderBy子句
    /// </summary>
    protected IOrderByClause OrderByClause => ((ISqlQueryClauseAccessor)SqlBuilder).OrderByClause;

    /// <summary>
    /// 获取参数列表
    /// </summary>
    protected IReadOnlyDictionary<string, object> Params => SqlBuilder.GetParams();

    /// <summary>
    /// 获取是否包含联合操作
    /// </summary>
    protected bool IsUnion => ((IUnionAccessor)SqlBuilder).IsUnion;

    /// <summary>
    /// 获取联合操作项集合
    /// </summary>
    protected IReadOnlyList<BuilderItem> UnionItems => ((IUnionAccessor)SqlBuilder).UnionItems;

    /// <summary>
    /// 获取公用表表达式CTE集合
    /// </summary>
    protected IReadOnlyList<BuilderItem> CteItems => ((ICteAccessor)SqlBuilder).CteItems;



    /// <inheritdoc />
    /// <remarks>
    /// 创建供运行时绑定使用的独立 SQL Builder。
    /// </remarks>
    ISqlBuilder ISqlQueryBuilderSource.CreateIndependentSqlBuilder() => CreateIndependentSqlBuilder();

    /// <inheritdoc />
    public SqlFluentQuery Query()
    {
        QueryResources.EnsureExecutionAvailable();
        var executor = (ISqlQueryPlanExecutor)this;
        var builderSource = (ISqlQueryBuilderSource)this;
        return SqlQueryRuntimeFactory.CreateQuery(executor, builderSource.CreateIndependentSqlBuilder());
    }

    /// <inheritdoc />
    public SqlTextQuery Sql(string sql, object parameters = null)
    {
        QueryResources.EnsureExecutionAvailable();
        return SqlQueryRuntimeFactory.CreateTextQuery((ISqlQueryPlanExecutor)this, sql, parameters);
    }

    /// <inheritdoc />
    public SqlTextQuery SqlInterpolated(FormattableString sql)
    {
        QueryResources.EnsureExecutionAvailable();
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));

        var parameterPrefix = GetCurrentProvider().Dialect.GetPrefix();
        var arguments = sql.GetArguments();
        var parameters = new Dictionary<string, object>(arguments.Length);
        var parameterNames = new Dictionary<int, string>(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
        {
            EnsureInterpolatedParameterIsSupported(arguments[index]);
            var parameterName = GetInterpolatedParameterName(sql.Format, index, parameterPrefix);
            parameterNames.Add(index, parameterName);
            parameters.Add(parameterName, arguments[index]);
        }
        var commandText = CreateInterpolatedCommandText(sql.Format, parameterNames, parameterPrefix);
        return Sql(commandText, parameters);
    }

    /// <inheritdoc />
    public SqlProcedureQuery Procedure(string procedure, object parameters = null)
    {
        QueryResources.EnsureExecutionAvailable();
        if (string.IsNullOrWhiteSpace(procedure))
            throw new ArgumentException("存储过程名称不能为空。", nameof(procedure));
        EnsureStoredProceduresSupported();
        EnsureOutputParametersSupported(parameters);
        var executor = (ISqlQueryPlanExecutor)this;
        return SqlQueryRuntimeFactory.CreateProcedureQuery(executor, GetProcedure(procedure), parameters);
    }

    /// <summary>
    /// 将复合格式 SQL 转换为仅包含参数占位符的命令文本。
    /// </summary>
    /// <param name="format">插值字符串的复合格式文本。</param>
    /// <param name="parameterNames">格式项索引对应的参数名称。</param>
    /// <param name="parameterPrefix">当前 SQL 方言使用的参数前缀。</param>
    /// <returns>可交由 Dapper 执行的参数化 SQL 文本。</returns>
    /// <remarks>
    /// 插值值始终交由参数绑定器处理，因此格式项的对齐和格式说明仅用于验证复合格式语法，不会影响 SQL 文本。
    /// </remarks>
    /// <exception cref="FormatException">复合格式文本包含无效格式项时抛出。</exception>
    private static string CreateInterpolatedCommandText(string format, IReadOnlyDictionary<int, string> parameterNames,
        string parameterPrefix)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));
        if (parameterNames == null)
            throw new ArgumentNullException(nameof(parameterNames));
        if (string.IsNullOrWhiteSpace(parameterPrefix))
            throw new ArgumentException("SQL 方言参数前缀不能为空。", nameof(parameterPrefix));
        var result = new StringBuilder(format.Length + parameterNames.Count * 4);
        for (var position = 0; position < format.Length; position++)
        {
            var current = format[position];
            if (current == '{')
            {
                if (position + 1 < format.Length && format[position + 1] == '{')
                {
                    result.Append('{');
                    position++;
                    continue;
                }
                var indexStart = ++position;
                while (position < format.Length && char.IsDigit(format[position]))
                    position++;
                if (indexStart == position || int.TryParse(format.Substring(indexStart, position - indexStart), out var index) == false ||
                    parameterNames.TryGetValue(index, out var parameterName) == false)
                    throw new FormatException("插值 SQL 包含无效的格式项索引。");
                SkipInterpolatedFormatItem(format, ref position);
                result.Append(parameterPrefix).Append(parameterName);
                continue;
            }
            if (current == '}')
            {
                if (position + 1 >= format.Length || format[position + 1] != '}')
                    throw new FormatException("插值 SQL 包含未转义的右花括号。");
                result.Append('}');
                position++;
                continue;
            }
            result.Append(current);
        }
        return result.ToString();
    }

    /// <summary>
    /// 跳过复合格式项的可选对齐和格式说明，并定位到右花括号后的下一个字符。
    /// </summary>
    /// <param name="format">完整复合格式文本。</param>
    /// <param name="position">当前格式项索引后的读取位置。</param>
    /// <exception cref="FormatException">格式项未以右花括号结束时抛出。</exception>
    private static void SkipInterpolatedFormatItem(string format, ref int position)
    {
        if (position < format.Length && format[position] == ',')
        {
            position++;
            while (position < format.Length && char.IsWhiteSpace(format[position]))
                position++;
            if (position < format.Length && (format[position] == '+' || format[position] == '-'))
                position++;
            var alignmentStart = position;
            while (position < format.Length && char.IsDigit(format[position]))
                position++;
            if (alignmentStart == position)
                throw new FormatException("插值 SQL 格式项的对齐宽度无效。");
        }
        if (position < format.Length && format[position] == ':')
        {
            position++;
            while (position < format.Length && format[position] != '}')
            {
                if (format[position] is '{' or '\r' or '\n')
                    throw new FormatException("插值 SQL 格式项包含无效字符。");
                position++;
            }
        }
        if (position >= format.Length || format[position] != '}')
            throw new FormatException("插值 SQL 格式项缺少右花括号。");
    }

    /// <summary>
    /// 获取不与 SQL 文本中已有参数冲突的插值参数名。
    /// </summary>
    /// <param name="format">复合格式 SQL 文本。</param>
    /// <param name="index">插值参数索引。</param>
    /// <param name="parameterPrefix">当前 SQL 方言使用的参数前缀。</param>
    /// <returns>当前插值参数使用的名称。</returns>
    private static string GetInterpolatedParameterName(string format, int index, string parameterPrefix)
    {
        if (string.IsNullOrWhiteSpace(parameterPrefix))
            throw new ArgumentException("SQL 方言参数前缀不能为空。", nameof(parameterPrefix));
        var baseName = $"p{index}";
        var parameterName = baseName;
        var suffix = 0;
        while (SqlBuilderRuntimeBridge.ContainsParameterToken(format, parameterPrefix + parameterName))
            parameterName = $"{baseName}_{++suffix}";
        return parameterName;
    }

    /// <summary>
    /// 验证插值参数具有已定义的单值绑定语义。
    /// </summary>
    /// <param name="value">插值参数值。</param>
    /// <exception cref="NotSupportedException">参数为尚未定义展开语义的集合时抛出。</exception>
    private static void EnsureInterpolatedParameterIsSupported(object value)
    {
        if (value is null or string or byte[] || value is not IEnumerable)
            return;
        throw new NotSupportedException("插值 SQL 暂不支持集合参数，请使用显式参数化查询。");
    }

    /// <inheritdoc />
    public SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        QueryResources.EnsureExecutionAvailable();
        var executor = (ISqlQueryPlanExecutor)this;
        var builder = ((ISqlQueryBuilderSource)this).CreateIndependentSqlBuilder();
        builder.From<TEntity>(alias, schema);
        builder.Select<TEntity>();
        return SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder);
    }

    /// <inheritdoc />
    public SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery)
        where TProjection : class
    {
        QueryResources.EnsureExecutionAvailable();
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        var executor = (ISqlQueryPlanExecutor)this;
        var builder = ((ISqlQueryBuilderSource)this).CreateIndependentSqlBuilder();
        SqlBuilderRuntimeBridge.FromSubquery(builder, subquery);
        return SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder);
    }

    /// <summary>
    /// 在执行之后清空Sql和参数
    /// </summary>
    protected void ClearAfterExecution()
    {
        if (_sqlBuilder == null)
            return;
        _sqlBuilder.Clear();
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns>当前 SQL Builder 生成的 SQL 文本。</returns>
    protected string GetSql() => SqlBuilder.ToSql();

    /// <summary>
    /// 验证 SQL Builder 是否可通过查询结果 API 执行。
    /// </summary>
    /// <remarks>结构化写入必须配置 Returning，且不能通过只读数据源执行。</remarks>
    protected void ValidateQueryBuilder()
    {
        ValidateQueryBuilder(_sqlBuilder);
    }

    /// <summary>
    /// 验证 SQL Builder 是否可通过查询结果 API 执行。
    /// </summary>
    /// <param name="builder">待验证的 SQL Builder；原生 SQL 文本查询传入 null。</param>
    protected void ValidateQueryBuilder(ISqlBuilder builder)
    {
        if (builder == null)
            return;
        if (builder.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            return;
        if (builder is IReturningClauseAccessor { ReturningClause.IsEmpty: false })
        {
            EnsureWritableDataSource();
            return;
        }
        throw new InvalidOperationException("Mutation 必须配置 Returning 后才能通过查询结果 API 执行。");
    }

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters() => GetDbParameters(SqlBuilder);

    /// <summary>
    /// 准备命令执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <returns>本次执行的不可变准备快照。</returns>
    private protected SqlPreparedCommand PrepareCommand(SqlQueryPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, Logger.IsEnabled(LogLevel.Trace));
        // 公开 Parameters 为了保持冻结语义会返回深拷贝；本次执行只取一次，避免同一快照重复复制。
        var parameters = snapshot.Parameters;
        return new SqlPreparedCommand(snapshot.Sql, parameters,
            plan.IsGeneratedExecution ? parameters : GetDbParameters(parameters, snapshot.Sql), debugSql: snapshot.DebugSql);
    }

    /// <summary>
    /// 获取Sql参数值
    /// </summary>
    /// <typeparam name="T">参数值类型</typeparam>
    /// <param name="name">参数名</param>
    /// <returns>指定参数转换后的值；参数不存在时返回类型默认值。</returns>
    protected virtual T GetParam<T>(string name)
    {
        return (T)ParameterManager?.GetValue(name);
    }

    /// <summary>
    /// 清空查询构建状态和参数。
    /// </summary>
    protected void Clear()
    {
        if (_sqlBuilder == null)
            return;
        ClearAfterExecution();
        if (_sqlBuilder is ISqlCommonPartAccessor accessor)
            accessor.ParameterManager?.Clear();
    }

}
