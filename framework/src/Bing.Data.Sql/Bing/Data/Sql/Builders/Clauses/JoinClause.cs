using System.Linq.Expressions;
using System.Text;
using Bing.Data;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// 表连接子句
/// </summary>
public class JoinClause : IJoinClause
{
    #region 字段

    /// <summary>
    /// Join关键字
    /// </summary>
    private const string JoinKey = "Join";

    /// <summary>
    /// Left Join关键字
    /// </summary>
    private const string LeftJoinKey = "Left Join";

    /// <summary>
    /// Right Join关键字
    /// </summary>
    private const string RightJoinKey = "Right Join";

    /// <summary>
    /// Full Join 关键字。
    /// </summary>
    private const string FullJoinKey = "Full Join";

    /// <summary>
    /// Cross Join 关键字。
    /// </summary>
    private const string CrossJoinKey = "Cross Join";

    /// <summary>
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// SQL 生成器。
    /// </summary>
    protected ISqlBuilder _sqlBuilder => _context.Builder;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    protected IDialect _dialect => _context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    protected IEntityResolver _resolver => _context.EntityResolver;

    /// <summary>
    /// 实体别名注册器。
    /// </summary>
    protected IEntityAliasRegister _register => _context.AliasRegister;

    /// <summary>
    /// 参数管理器。
    /// </summary>
    protected IParameterManager _parameterManager => _context.ParameterManager;

    /// <summary>
    /// 辅助操作
    /// </summary>
    protected readonly Helper _helper;

    /// <summary>
    /// 实体映射解析器。
    /// </summary>
    protected IEntityMappingResolver _entityMappingResolver => _context.Services.EntityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    protected IDatabaseContextAccessor _databaseContextAccessor => _context.Services.DatabaseContextAccessor;

    /// <summary>
    /// SQL 参数工厂。
    /// </summary>
    protected ISqlParameterFactory _sqlParameterFactory => _context.Services.ParameterFactory;

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    protected SqlMetadataOptions _metadataOptions => _context.Services.MetadataOptions;

    /// <summary>
    /// SQL 配置。
    /// </summary>
    protected SqlOptions _sqlOptions => _context.Services.Options;

    /// <summary>
    /// SQL 数据库上下文解析器。
    /// </summary>
    protected ISqlDatabaseContextResolver _databaseContextResolver => _context.Services.DatabaseContextResolver;

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    protected DatabaseContext _databaseContext => _context.ExecutionContext.DatabaseContext;

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    protected ISqlObjectNameFormatter _objectNameFormatter => _context.Services.ObjectNameFormatter;

    /// <summary>
    /// 跨数据库查询校验器。
    /// </summary>
    protected ISqlCrossDatabaseQueryValidator _crossDatabaseQueryValidator =>
        _context.Services.CrossDatabaseQueryValidator;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected ISqlTableReferenceValidator _tableReferenceValidator => _context.Services.TableReferenceValidator;

    /// <summary>
    /// SQL 字符串表引用解析器。
    /// </summary>
    protected ISqlTableReferenceParser _tableReferenceParser => _context.Provider.TableReferenceParser;

    /// <summary>
    /// 连接参数
    /// </summary>
    protected readonly List<JoinItem> _params;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="JoinClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public JoinClause(SqlClauseContext context)
        : this(context, null)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化表连接子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="joinItems">连接参数列表。</param>
    protected JoinClause(SqlClauseContext context, List<JoinItem> joinItems)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _helper = new Helper(context);
        _params = joinItems ?? new List<JoinItem>();
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的连接子句。</returns>
    public virtual IJoinClause Clone(SqlClauseContext context) => CreateClone(context, CloneItems(context));

    /// <summary>
    /// 创建克隆后的连接子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="joinItems">已深复制的连接项。</param>
    /// <returns>保留 Provider 子类类型的连接子句。</returns>
    protected virtual JoinClause CreateClone(SqlClauseContext context, List<JoinItem> joinItems) =>
        new JoinClause(context, joinItems);

    /// <summary>
    /// 克隆连接项。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的连接项列表。</returns>
    private List<JoinItem> CloneItems(SqlClauseContext context)
    {
        var helper = new Helper(context);
        return _params.Select(item => item.Clone(helper)).ToList();
    }

    #endregion

    #region Find(查找连接项)

    /// <summary>
    /// 查找连接项
    /// </summary>
    /// <param name="type">表实体类型</param>
    public IJoinOn Find(Type type) => _params.Find(t => t.Type == type);

    /// <summary>
    /// 判断当前子句是否包含指定连接类型。
    /// </summary>
    /// <param name="joinType">连接类型关键字。</param>
    /// <returns>包含指定连接类型时返回 true。</returns>
    internal bool ContainsJoinType(string joinType) => _params.Any(item =>
        string.Equals(item.JoinType, joinType, StringComparison.Ordinal));

    /// <summary>
    /// 获取类型化连接在当前查询图中的表源快照。
    /// </summary>
    /// <remarks>
    /// 原始连接和派生表没有关联实体类型，不能参与强类型 Lambda 参数绑定。
    /// </remarks>
    /// <returns>按连接追加顺序排列的类型化表源。</returns>
    internal IReadOnlyList<TableSource> GetTypedSources() => _params
        .Select((item, index) => new { Item = item, Index = index })
        .Select(item => new
        {
            item.Item,
            item.Index,
            EntityType = item.Item.Source?.EntityType ?? item.Item.Type ??
                (item.Item.Table as StructuredSqlItem)?.Reference?.EntityType
        })
        .Where(item => item.EntityType != null)
        .Select(item => item.Item.Source ?? new TableSource($"join_{item.Index}", item.Item.Table, item.EntityType,
            GetSourceAlias(item.Item)))
        .ToList();

    /// <summary>
    /// 按 SQL 追加顺序导出当前 Join 图，供过滤器放置规划器判断每个来源的保留侧语义。
    /// </summary>
    /// <param name="rootSources">当前 From 子句中的结构化根来源。</param>
    /// <returns>按 Join SQL 顺序排列的不可变拓扑边。</returns>
    internal IReadOnlyList<SqlFilterJoin> GetFilterTopology(IReadOnlyList<TableSource> rootSources)
    {
        var leftSourceIds = rootSources?.Select(source => source.SourceId).ToList() ?? new List<string>();
        var result = new List<SqlFilterJoin>(_params.Count);
        foreach (var item in _params)
        {
            var rightSourceId = item.Source?.SourceId;
            if (string.IsNullOrWhiteSpace(rightSourceId))
            {
                var index = _params.IndexOf(item);
                var entityType = item.Source?.EntityType ?? item.Type ??
                    (item.Table as StructuredSqlItem)?.Reference?.EntityType;
                if (entityType != null)
                    rightSourceId = $"join_{index}";
            }
            result.Add(new SqlFilterJoin(GetFilterJoinKind(item.JoinType), leftSourceIds.ToArray(), rightSourceId));
            if (string.IsNullOrWhiteSpace(rightSourceId) == false)
                leftSourceIds.Add(rightSourceId);
        }
        return result;
    }

    /// <summary>
    /// 将已规划的过滤谓词追加到指定 Join 的 On 条件。
    /// </summary>
    /// <param name="sourceId">目标 Join 右侧来源标识。</param>
    /// <param name="column">已方言转义的列引用。</param>
    /// <param name="value">参数值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <exception cref="InvalidOperationException">指定来源不是可写入 On 的 Join 右侧时抛出。</exception>
    internal void AddFilterCondition(string sourceId, string column, object value, Operator @operator = Operator.Equal)
    {
        var item = _params.FirstOrDefault(candidate => string.Equals(candidate.Source?.SourceId, sourceId,
            StringComparison.Ordinal));
        if (item == null)
            throw new InvalidOperationException($"未找到过滤器表源 {sourceId}。");
        item.On(column, value, @operator);
    }

    /// <summary>
    /// 将 Join SQL 关键字转换为过滤器规划使用的明确 Join 类型。
    /// </summary>
    /// <param name="joinType">Join SQL 关键字。</param>
    /// <returns>对应的结构化 Join 类型。</returns>
    /// <exception cref="InvalidOperationException">Join 类型不受框架结构化拓扑支持时抛出。</exception>
    private static SqlFilterJoinKind GetFilterJoinKind(string joinType)
    {
        return joinType switch
        {
            JoinKey => SqlFilterJoinKind.Inner,
            LeftJoinKey => SqlFilterJoinKind.Left,
            RightJoinKey => SqlFilterJoinKind.Right,
            FullJoinKey => SqlFilterJoinKind.Full,
            CrossJoinKey => SqlFilterJoinKind.Cross,
            _ => throw new InvalidOperationException($"不支持为 Join 类型 {joinType} 建立过滤器拓扑。")
        };
    }

    /// <summary>
    /// 为最后一个连接设置已按表源实例绑定的参数化条件。
    /// </summary>
    /// <param name="condition">已按当前方言解析的连接条件。</param>
    internal void SetBoundOn(ICondition condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        GetLastJoinOrThrow().On(condition);
    }

    /// <summary>
    /// 验证最后一个连接允许追加 On 条件。
    /// </summary>
    internal void ValidateLastJoinSupportsOn() => GetLastJoinOrThrow();

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化连接表。
    /// </summary>
    /// <typeparam name="TEntity">连接表实体类型。</typeparam>
    /// <param name="joinType">连接类型关键字。</param>
    /// <param name="fromClause">当前查询的根来源子句。</param>
    /// <param name="predicate">覆盖全部 Lambda 来源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    internal void Join<TEntity>(string joinType, FromClause fromClause, LambdaExpression predicate,
        string alias = null, string schema = null) where TEntity : class
    {
        if (fromClause == null)
            throw new ArgumentNullException(nameof(fromClause));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        _context.ValidateOperation(SqlOperationAction.QueryClause);
        (_sqlBuilder as SqlBuilderBase)?.ValidateTypedJoinCapability(joinType);
        var entityType = typeof(TEntity);
        var reference = _resolver.GetTableReference(entityType) with { Alias = alias, EntityType = entityType };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
        var resolvedAlias = _resolver.GetAlias(entityType, reference.Alias);
        var registerProbe = _register?.Clone();

        var databaseContext = GetCurrentDatabaseContext();
        var sourceReference = GetSourceReference(databaseContext);
        PreflightTypedJoin(reference, databaseContext, sourceReference);
        var item = CreateStructuredJoinItem(joinType, reference, entityType, sourceReference, databaseContext);
        var candidateSource = new TableSource($"join_{_params.Count}", item.Table, entityType, resolvedAlias);
        item.Source = candidateSource;

        var parameterProbe = _parameterManager.Clone();
        var sources = fromClause.Sources.Concat(GetTypedSources()).Append(candidateSource).ToList();
        var condition = fromClause.ResolveMultiSourcePredicate(predicate, sources, parameterProbe);
        item.On(condition);

        var selectClause = _sqlBuilder.SelectClause as SelectClause;
        var selectBefore = selectClause?.Clone(_context) as SelectClause;
        var selectProbe = selectBefore?.Clone(_context) as SelectClause;
        var aliasBefore = _register?.Clone();
        var parameterManagerBefore = _parameterManager;
        var operationBefore = (_sqlBuilder as SqlBuilderBase)?.OperationKind ?? SqlOperationKind.None;

        var itemCommitted = false;
        try
        {
            FreezeExistingProjectionAlias(entityType, selectProbe, _register);
            registerProbe?.Register(entityType, resolvedAlias);
            CommitSelectClause(selectProbe);
            CommitAliasRegister(registerProbe);
            _context.UseOperation(SqlOperationAction.QueryClause);
            CommitParameterManager(parameterProbe, fromClause);
            item.SetDependency(_helper);
            CommitJoinItem(item);
            itemCommitted = true;
        }
        catch
        {
            if (itemCommitted == false)
                _params.Remove(item);
            RestoreSelectClause(selectBefore);
            RestoreAliasRegister(aliasBefore, resolvedAlias);
            RestoreParameters(parameterManagerBefore);
            RestoreOperationState(operationBefore);
            throw;
        }
    }

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化内连接表。
    /// </summary>
    internal void Join<TEntity>(FromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => Join<TEntity>(JoinKey, fromClause, predicate, alias, schema);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化左外连接表。
    /// </summary>
    internal void LeftJoin<TEntity>(FromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => Join<TEntity>(LeftJoinKey, fromClause, predicate, alias, schema);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化右外连接表。
    /// </summary>
    internal void RightJoin<TEntity>(FromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => Join<TEntity>(RightJoinKey, fromClause, predicate, alias, schema);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化全外连接表。
    /// </summary>
    internal void FullJoin<TEntity>(FromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => Join<TEntity>(FullJoinKey, fromClause, predicate, alias, schema);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化派生表连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="joinType">连接类型关键字。</param>
    /// <param name="fromClause">当前查询的根来源子句。</param>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <param name="predicate">覆盖全部 Lambda 来源的连接条件。</param>
    private void Join<TProjection>(string joinType, FromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class
    {
        if (fromClause == null)
            throw new ArgumentNullException(nameof(fromClause));
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        subquery.ValidateCompatible(_sqlBuilder);
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        (_sqlBuilder as SqlBuilderBase)?.ValidateTypedJoinCapability(joinType);
        var registerProbe = _register?.Clone();
        registerProbe?.RegisterAlias(subquery.Alias);
        var subqueryAlias = GetSubqueryAlias(subquery.Alias);
        var sqlBuilder = _sqlBuilder as SqlBuilderBase;
        var parameterManagerBefore = _parameterManager;
        var subqueryParameterNamesBefore = sqlBuilder?.CloneSubqueryParameterNames();
        var aliasBefore = _register?.Clone();
        var operationBefore = sqlBuilder?.OperationKind ?? SqlOperationKind.None;
        JoinItem item = null;
        var itemCommitted = false;

        try
        {
            var parameterProbe = parameterManagerBefore.Clone();
            var subqueryParameterNamesProbe = sqlBuilder?.CloneSubqueryParameterNames();
            var sql = sqlBuilder?.RenderSubquery(subquery.Builder, parameterProbe,
                subqueryParameterNamesProbe) ?? subquery.Builder.ToSql();
            var table = SqlItem.Raw($"({sql}){subqueryAlias}");
            var source = new TableSource($"join_{_params.Count}", table, typeof(TProjection), subquery.Alias,
                subquery.ProjectedMembers);
            item = JoinItem.CreateDerived(joinType, table, source);
            var sources = fromClause.Sources.Concat(GetTypedSources()).Append(source).ToList();
            var condition = fromClause.ResolveMultiSourcePredicate(predicate, sources, parameterProbe);
            item.On(condition);

            _context.UseOperation(SqlOperationAction.QueryClause);
            if (sqlBuilder != null)
            {
                sqlBuilder.ReplaceParameterManager(parameterProbe);
                sqlBuilder.ReplaceSubqueryParameterNames(subqueryParameterNamesProbe);
            }
            else
                fromClause.MergeNewParameters(parameterProbe);
            item.SetDependency(_helper);
            _params.Add(item);
            _register?.RegisterAlias(subquery.Alias);
            sqlBuilder?.RegisterSubqueryParent(subquery.ParentQueryContextId);
            itemCommitted = true;
        }
        catch
        {
            if (itemCommitted == false && item != null)
                _params.Remove(item);
            RestoreAliasRegister(aliasBefore, subquery.Alias);
            RestoreParameters(parameterManagerBefore);
            if (sqlBuilder != null)
                sqlBuilder.ReplaceSubqueryParameterNames(subqueryParameterNamesBefore);
            RestoreOperationState(operationBefore);
            throw;
        }
    }

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化派生表内连接。
    /// </summary>
    internal void Join<TProjection>(FromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => Join(JoinKey, fromClause, subquery, predicate);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化派生表左外连接。
    /// </summary>
    internal void LeftJoin<TProjection>(FromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => Join(LeftJoinKey, fromClause, subquery, predicate);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化派生表右外连接。
    /// </summary>
    internal void RightJoin<TProjection>(FromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => Join(RightJoinKey, fromClause, subquery, predicate);

    /// <summary>
    /// 原子添加带 Lambda On 谓词的类型化派生表全外连接。
    /// </summary>
    internal void FullJoin<TProjection>(FromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => Join(FullJoinKey, fromClause, subquery, predicate);

    /// <summary>
    /// 预检类型化连接表，保证名称、Provider 和跨库约束都在状态提交前完成。
    /// </summary>
    /// <param name="reference">待连接的结构化表引用。</param>
    /// <param name="databaseContext">当前执行数据库上下文。</param>
    /// <param name="sourceReference">当前根表结构化引用。</param>
    private void PreflightTypedJoin(SqlTableReference reference, DatabaseContext databaseContext,
        SqlTableReference sourceReference)
    {
        var builder = _sqlBuilder as SqlBuilderBase;
        var databaseType = builder?.ResolveProviderDatabaseType(reference) ?? _sqlBuilder.Provider?.DatabaseType;
        if (databaseType == null)
            throw new InvalidOperationException("无法确定结构化连接表引用的数据库类型。");
        _tableReferenceValidator.Validate(reference, databaseType.Value);
        _objectNameFormatter.Format(reference, _dialect, databaseType);
        if (sourceReference != null)
            _crossDatabaseQueryValidator?.Validate(databaseContext, sourceReference, reference);
        else
            _crossDatabaseQueryValidator?.ValidateTarget(databaseContext, reference);
    }

    #endregion

    #region Join(内连接)

    /// <summary>
    /// 内连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void Join(string table, string alias = null) => Join(JoinKey, table, alias);

    /// <summary>
    /// 内连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    public void Join(SqlTableReference reference) => Join(JoinKey, reference);

    /// <summary>
    /// 表连接
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    private void Join(string joinType, string table, string alias)
    {
        var parsedTable = ParseTableName(table, alias);
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = _register?.Clone();
        registerProbe?.RegisterAlias(parsedTable.Alias);
        AddItem(CreateJoinItem(joinType, parsedTable.TableName, parsedTable.Schema, parsedTable.Alias));
        _register?.RegisterAlias(parsedTable.Alias);
    }

    /// <summary>
    /// 解析字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected virtual (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        var parsedTable = _tableReferenceParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <summary>
    /// 添加结构化表引用连接项。
    /// </summary>
    /// <param name="joinType">连接类型。</param>
    /// <param name="reference">结构化表引用。</param>
    private void Join(string joinType, SqlTableReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var resolvedAlias = reference.EntityType == null ? reference.Alias :
            _resolver.GetAlias(reference.EntityType, reference.Alias);
        var registerProbe = _register?.Clone();
        if (reference.EntityType == null)
            registerProbe?.RegisterAlias(reference.Alias);
        else
            registerProbe?.Register(reference.EntityType, resolvedAlias);
        var databaseContext = GetCurrentDatabaseContext();
        var sourceReference = GetSourceReference(databaseContext);
        AddItem(CreateStructuredJoinItem(joinType, reference, reference.EntityType, sourceReference, databaseContext),
            resolvedAlias);
        if (reference.EntityType != null)
        {
            FreezeExistingProjectionAlias(reference.EntityType, _sqlBuilder.SelectClause as SelectClause, _register);
            _register?.Register(reference.EntityType, resolvedAlias);
        }
        else
            _register?.RegisterAlias(reference.Alias);
    }

    /// <summary>
    /// 创建连接项
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    /// <param name="type">类型</param>
    protected virtual JoinItem CreateJoinItem(string joinType, string table, string schema, string alias,
        Type type = null) => JoinItem.CreateTable(joinType, table, schema, alias, type);

    /// <summary>
    /// 添加连接项
    /// </summary>
    /// <param name="item">表连接项</param>
    /// <param name="sourceAlias">查询图中用于限定列的逻辑别名。</param>
    private void AddItem(JoinItem item, string sourceAlias = null)
    {
        _context.UseOperation(SqlOperationAction.QueryClause);
        var entityType = item.Type ?? (item.Table as StructuredSqlItem)?.Reference?.EntityType;
        if (entityType != null && item.Source == null)
            item.Source = new TableSource($"join_{_params.Count}", item.Table, entityType,
                sourceAlias ?? GetSourceAlias(item));
        item.SetDependency(_helper);
        _params.Add(item);
    }

    /// <summary>
    /// 解析 Join 表源在最终 SQL 中使用的别名。
    /// </summary>
    /// <param name="item">连接项。</param>
    /// <returns>已冻结的表别名。</returns>
    private static string GetSourceAlias(JoinItem item) =>
        (item.Table as StructuredSqlItem)?.Reference?.Alias ?? item.Table?.Alias;

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public void Join<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        Join<TEntity>(JoinKey, alias, schema);

    /// <summary>
    /// 表连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="joinType">连接类型</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    private void Join<TEntity>(string joinType, string alias, string schema)
    {
        var type = typeof(TEntity);
        var reference = _resolver.GetTableReference(type) with { Alias = alias, EntityType = type };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
        Join(joinType, reference);
    }

    /// <summary>
    /// 创建结构化连接项
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="reference">结构化表引用</param>
    /// <param name="type">实体类型</param>
    /// <param name="sourceReference">From 子句的结构化表引用</param>
    /// <param name="databaseContext">执行数据库上下文</param>
    protected virtual JoinItem CreateStructuredJoinItem(string joinType, SqlTableReference reference, Type type,
        SqlTableReference sourceReference, DatabaseContext databaseContext)
    {
        return JoinItem.Create(joinType, new StructuredSqlItem(reference, _objectNameFormatter, databaseContext,
            (_sqlBuilder as SqlBuilderBase)?.ResolveProviderDatabaseType(reference), _tableReferenceValidator,
            _crossDatabaseQueryValidator, sourceReference), type);
    }

    /// <summary>
    /// 获取类型化 Join 的源表引用。
    /// </summary>
    /// <param name="databaseContext">执行数据库上下文。</param>
    /// <returns>From 表引用；原始 From 使用最小执行上下文引用。</returns>
    private SqlTableReference GetSourceReference(DatabaseContext databaseContext)
    {
        if (_sqlBuilder is SqlBuilderBase builder)
        {
            var reference = builder.GetStructuredFromReference();
            if (reference != null)
                return reference;
        }
        return null;
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    private DatabaseContext GetCurrentDatabaseContext()
    {
        if (_sqlBuilder is SqlBuilderBase builder)
            return builder.GetDatabaseContext();
        return _databaseContext ?? _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
            _databaseContextAccessor?.Current ?? _metadataOptions?.DefaultDatabaseContext;
    }

    /// <summary>
    /// 在同一实体重复连接前固定既有投影使用的表别名。
    /// </summary>
    /// <param name="entityType">即将连接的实体类型。</param>
    /// <param name="selectClause">候选 Select 子句。</param>
    /// <param name="register">用于解析既有实体别名的注册器。</param>
    internal virtual void FreezeExistingProjectionAlias(Type entityType, SelectClause selectClause,
        IEntityAliasRegister register)
    {
        if (register?.Contains(entityType) != true || selectClause == null)
            return;
        selectClause.FreezeEntityAlias(entityType, register.GetAlias(entityType));
    }

    /// <summary>
    /// 提交候选 Select 子句。
    /// </summary>
    /// <param name="selectClause">候选 Select 子句。</param>
    internal virtual void CommitSelectClause(SelectClause selectClause)
    {
        if (_sqlBuilder.SelectClause is SelectClause current && selectClause != null)
            current.RestoreFrom(selectClause);
    }

    /// <summary>
    /// 提交实体别名注册。
    /// </summary>
    /// <param name="aliasRegister">候选实体别名注册器。</param>
    internal virtual void CommitAliasRegister(IEntityAliasRegister aliasRegister)
    {
        if (_register is EntityAliasRegister current && aliasRegister is EntityAliasRegister candidate)
            current.RestoreFrom(candidate);
    }

    /// <summary>
    /// 提交连接项。
    /// </summary>
    /// <param name="item">候选连接项。</param>
    internal virtual void CommitJoinItem(JoinItem item) => _params.Add(item);

    /// <summary>
    /// 提交候选参数状态。
    /// </summary>
    /// <param name="parameterManager">候选参数管理器。</param>
    /// <param name="fromClause">当前 From 子句。</param>
    internal virtual void CommitParameterManager(IParameterManager parameterManager, FromClause fromClause)
    {
        if (_sqlBuilder is SqlBuilderBase builder)
            builder.ReplaceParameterManager(parameterManager);
        else
            fromClause.MergeNewParameters(parameterManager);
    }

    /// <summary>
    /// 恢复候选失败前的 Select 状态。
    /// </summary>
    /// <param name="selectClause">失败前的 Select 快照。</param>
    private void RestoreSelectClause(SelectClause selectClause)
    {
        if (_sqlBuilder.SelectClause is SelectClause current && selectClause != null)
            current.RestoreFrom(selectClause);
    }

    /// <summary>
    /// 恢复候选失败前的实体别名状态。
    /// </summary>
    /// <param name="aliasRegister">失败前的别名快照。</param>
    /// <param name="alias">本次候选别名。</param>
    private void RestoreAliasRegister(IEntityAliasRegister aliasRegister, string alias)
    {
        if (_register is EntityAliasRegister current && aliasRegister is EntityAliasRegister snapshot)
        {
            current.RestoreFrom(snapshot);
            return;
        }
        if (_register is IEntityAliasRegisterLifecycle lifecycle)
            lifecycle.ReleaseAlias(alias);
    }

    /// <summary>
    /// 恢复候选失败前的参数状态。
    /// </summary>
    /// <param name="parameterManager">失败前的参数快照。</param>
    private void RestoreParameters(IParameterManager parameterManager)
    {
        if (_sqlBuilder is SqlBuilderBase builder && parameterManager != null)
            builder.RestoreParameterManager(parameterManager);
    }

    /// <summary>
    /// 恢复候选失败前的 Builder 操作状态。
    /// </summary>
    /// <param name="operationKind">失败前的操作状态。</param>
    private void RestoreOperationState(SqlOperationKind operationKind)
    {
        if (_sqlBuilder is SqlBuilderBase builder)
            builder.RestoreOperationState(operationKind);
    }

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void Join(ISqlBuilder builder, string alias) => JoinSubquery(JoinKey, builder, alias);

    /// <summary>
    /// 内连接严格 DTO 派生表。
    /// </summary>
    internal void Join<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        JoinSubquery(JoinKey, subquery);

    /// <summary>
    /// 添加到连接子句
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    private void JoinSubquery(string joinType, ISqlBuilder builder, string alias)
    {
        if (builder == null)
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = _register?.Clone();
        registerProbe?.RegisterAlias(alias);
        var subqueryAlias = GetSubqueryAlias(alias);
        var sql = _sqlBuilder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        _register?.RegisterAlias(alias);
        AddItem(JoinItem.CreateRaw(joinType, $"({sql}){subqueryAlias}", alias));
    }

    /// <summary>
    /// 添加保留投影成员绑定信息的类型化派生表。
    /// </summary>
    private void JoinSubquery<TProjection>(string joinType, SqlSubquery<TProjection> subquery)
        where TProjection : class
    {
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        subquery.ValidateCompatible(_sqlBuilder);
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = _register?.Clone();
        registerProbe?.RegisterAlias(subquery.Alias);
        var subqueryAlias = GetSubqueryAlias(subquery.Alias);
        var sql = _sqlBuilder is SqlBuilderBase sqlBuilder
            ? sqlBuilder.RenderSubquery(subquery.Builder)
            : subquery.Builder.ToSql();
        _register?.RegisterAlias(subquery.Alias);
        var table = SqlItem.Raw($"({sql}){subqueryAlias}");
        AddItem(JoinItem.CreateDerived(joinType, table,
            new TableSource($"join_{_params.Count}", table, typeof(TProjection), subquery.Alias,
                subquery.ProjectedMembers)));
        (_sqlBuilder as SqlBuilderBase)?.RegisterSubqueryParent(subquery.ParentQueryContextId);
    }


    /// <summary>
    /// 获取派生表连接别名的方言渲染文本。
    /// </summary>
    /// <param name="alias">派生表别名。</param>
    /// <returns>包含前导空格的别名 SQL 文本。</returns>
    protected virtual string GetSubqueryAlias(string alias) => $" As {_dialect.SafeName(alias)}";

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void Join(Action<ISqlBuilder> action, string alias) => JoinSubquery(JoinKey, action, alias);

    /// <summary>
    /// 添加到连接子句
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    private void JoinSubquery(string joinType, Action<ISqlBuilder> action, string alias)
    {
        if (action == null)
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var builder = _sqlBuilder.New();
        action(builder);
        JoinSubquery(joinType, builder, alias);
    }

    #endregion

    /// <summary>
    /// 添加原始连接 SQL。
    /// </summary>
    /// <param name="joinType">连接类型。</param>
    /// <param name="sql">原始 SQL。</param>
    private void AppendJoin(string joinType, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        AddItem(JoinItem.CreateRaw(joinType, sql));
    }

    /// <summary>
    /// 添加原始内连接 SQL。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    public void AppendJoin(string sql) => AppendJoin(JoinKey, sql);

    #region LeftJoin(左外连接)

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void LeftJoin(string table, string alias = null) => Join(LeftJoinKey, table, alias);

    /// <summary>
    /// 左外连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    public void LeftJoin(SqlTableReference reference) => Join(LeftJoinKey, reference);

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public void LeftJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        Join<TEntity>(LeftJoinKey, alias, schema);

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void LeftJoin(ISqlBuilder builder, string alias) => JoinSubquery(LeftJoinKey, builder, alias);

    /// <summary>
    /// 左外连接严格 DTO 派生表。
    /// </summary>
    internal void LeftJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        JoinSubquery(LeftJoinKey, subquery);

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void LeftJoin(Action<ISqlBuilder> action, string alias) => JoinSubquery(LeftJoinKey, action, alias);

    /// <summary>
    /// 添加原始左连接 SQL。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    public void AppendLeftJoin(string sql) => AppendJoin(LeftJoinKey, sql);

    #endregion

    #region RightJoin(右外连接)

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void RightJoin(string table, string alias = null) => Join(RightJoinKey, table, alias);

    /// <summary>
    /// 右外连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    public void RightJoin(SqlTableReference reference) => Join(RightJoinKey, reference);

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public void RightJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        Join<TEntity>(RightJoinKey, alias, schema);

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void RightJoin(ISqlBuilder builder, string alias) => JoinSubquery(RightJoinKey, builder, alias);

    /// <summary>
    /// 右外连接严格 DTO 派生表。
    /// </summary>
    internal void RightJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        JoinSubquery(RightJoinKey, subquery);

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void RightJoin(Action<ISqlBuilder> action, string alias) => JoinSubquery(RightJoinKey, action, alias);

    /// <summary>
    /// 添加原始右连接 SQL。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    public void AppendRightJoin(string sql) => AppendJoin(RightJoinKey, sql);

    #endregion

    #region FullJoin(全外连接)

    /// <inheritdoc />
    public void FullJoin(string table, string alias = null) => Join(FullJoinKey, table, alias);

    /// <inheritdoc />
    public void FullJoin(SqlTableReference reference) => Join(FullJoinKey, reference);

    /// <inheritdoc />
    public void FullJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        Join<TEntity>(FullJoinKey, alias, schema);

    /// <summary>
    /// 全外连接严格 DTO 派生表。
    /// </summary>
    internal void FullJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        JoinSubquery(FullJoinKey, subquery);

    /// <inheritdoc />
    public void AppendFullJoin(string sql) => AppendJoin(FullJoinKey, sql);

    #endregion

    #region CrossJoin(交叉连接)

    /// <inheritdoc />
    public void CrossJoin(string table, string alias = null) => Join(CrossJoinKey, table, alias);

    /// <inheritdoc />
    public void CrossJoin(SqlTableReference reference) => Join(CrossJoinKey, reference);

    /// <inheritdoc />
    public void CrossJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        Join<TEntity>(CrossJoinKey, alias, schema);

    /// <summary>
    /// 交叉连接严格 DTO 派生表。
    /// </summary>
    internal void CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        JoinSubquery(CrossJoinKey, subquery);

    /// <inheritdoc />
    public void AppendCrossJoin(string sql) => AppendJoin(CrossJoinKey, sql);

    #endregion

    #region On(设置连接条件)

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="condition">连接条件</param>
    public void On(ICondition condition) => GetLastJoinOrThrow().On(MergeBuilderCondition(condition));

    /// <summary>
    /// 合并作为连接条件使用的独立 Builder 参数。
    /// </summary>
    /// <param name="condition">连接条件。</param>
    /// <returns>可安全追加到当前 Join 的条件。</returns>
    private ICondition MergeBuilderCondition(ICondition condition)
    {
        if (condition is not ISqlBuilder builder || _sqlBuilder is not SqlBuilderBase sqlBuilder)
            return condition;
        return new SqlCondition(sqlBuilder.MergeSubqueryParameters(builder, builder.GetCondition()));
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void On(string column, object value, Operator @operator = Operator.Equal) =>
        GetLastJoinOrThrow().On(column, value, @operator);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="left">左表列名</param>
    /// <param name="right">右表列名</param>
    /// <param name="operator">条件运算符</param>
    public void On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right, Operator @operator = Operator.Equal) where TLeft : class where TRight : class
    {
        var join = GetLastJoinOrThrow();
        var selfJoin = typeof(TLeft) == typeof(TRight);
        var leftColumn = new SqlItem(GetColumn(left, selfJoin, false)).ToSql(_dialect);
        var rightColumn = new SqlItem(GetColumn(right, selfJoin, true)).ToSql(_dialect);
        var condition = SqlConditionFactory.Create(leftColumn, rightColumn, @operator);
        join.AppendOn(condition.GetCondition(), _dialect);
    }

    /// <summary>
    /// 获取列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名</param>
    /// <param name="selfJoin">是否为同实体自连接。</param>
    /// <param name="right">是否为连接条件右侧。</param>
    private string GetColumn<TEntity>(Expression<Func<TEntity, object>> column, bool selfJoin = false,
        bool right = false) => GetColumn(typeof(TEntity), _resolver.GetColumn(column), selfJoin, right);

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="column">列名</param>
    /// <param name="selfJoin">是否为同实体自连接。</param>
    /// <param name="right">是否为连接条件右侧。</param>
    private string GetColumn(Type entity, string column, bool selfJoin = false, bool right = false) =>
        $"{GetAlias(entity, selfJoin, right)}.{column}";

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="expression">条件表达式</param>
    public void On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression) where TLeft : class where TRight : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var join = GetLastJoinOrThrow();
        var existingParameters = _parameterManager.GetParams();
        var snapshot = _parameterManager.Clone();
        var expressions = Lambdas.GetGroupPredicates(expression);
        var items = expressions.Select(item => GetOnItems(item, snapshot)).ToList();
        var parameters = snapshot.GetParams()
            .Where(parameter => existingParameters.ContainsKey(parameter.Key) == false)
            .ToList();
        var validation = _parameterManager.Clone();
        AddParameters(validation, parameters);
        join.On(items, _dialect);
        AddParameters(_parameterManager, parameters);
    }

    /// <summary>
    /// 设置连接条件组
    /// </summary>
    /// <param name="group">条件组</param>
    /// <param name="parameterManager">用于解析当前条件组的参数管理器。</param>
    private List<OnItem> GetOnItems(List<Expression> group, IParameterManager parameterManager) =>
        @group.Select(expression =>
        {
            var leftType = _resolver.GetType(expression, false);
            var rightType = _resolver.GetType(expression, true);
            var selfJoin = leftType == rightType;
            return new OnItem(
                GetColumn(expression, false, selfJoin, parameterManager),
                GetColumn(expression, true, selfJoin, parameterManager),
                Lambdas.GetOperator(expression).SafeValue());
        }).ToList();

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <param name="selfJoin">是否为同实体自连接。</param>
    /// <param name="parameterManager">用于解析闭包值的参数管理器。</param>
    private SqlItem GetColumn(Expression expression, bool right, bool selfJoin, IParameterManager parameterManager)
    {
        var type = _resolver.GetType(expression, right);
        var column = _resolver.GetColumn(expression, type, right);
        if (string.IsNullOrWhiteSpace(column))
        {
            var name = parameterManager.GenerateName();
            parameterManager.Add(name, Lambdas.GetValue(expression));
            return new SqlItem(name, raw: true);
        }

        return new SqlItem(GetColumn(type, column, selfJoin, right));
    }

    /// <summary>
    /// 向目标参数管理器按当前 Lambda On 的普通参数语义写入参数。
    /// </summary>
    /// <param name="parameterManager">目标参数管理器。</param>
    /// <param name="parameters">待写入参数。</param>
    private static void AddParameters(IParameterManager parameterManager,
        IEnumerable<KeyValuePair<string, object>> parameters)
    {
        foreach (var parameter in parameters)
            parameterManager.Add(parameter.Key, parameter.Value);
    }

    /// <summary>
    /// 获取连接条件成员使用的实体别名。
    /// </summary>
    /// <param name="entity">实体类型。</param>
    /// <param name="selfJoin">是否为同实体自连接。</param>
    /// <param name="right">是否为连接条件右侧。</param>
    /// <returns>表别名。</returns>
    private string GetAlias(Type entity, bool selfJoin, bool right) =>
        selfJoin ? _register.GetSelfJoinAlias(entity, right) : _register.GetAlias(entity);

    #endregion

    #region AppendOn(添加到On子句)

    /// <summary>
    /// 添加到On子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendOn(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        GetLastJoinOrThrow().AppendOn(sql, _dialect);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取最后一个连接项。
    /// </summary>
    /// <exception cref="InvalidOperationException">不存在可追加 On 条件的连接项。</exception>
    private JoinItem GetLastJoinOrThrow()
    {
        var join = _params.LastOrDefault();
        if (join == null)
            throw new InvalidOperationException("当前不存在可追加 On 条件的 Join。");
        join.ValidateSupportsOn();
        return join;
    }

    #endregion

    #region 输出

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            for (var index = 0; index < _params.Count; index++)
            {
                if (index > 0)
                    builder.AppendLine(" ");
                builder.Append(_params[index].ToSql(_dialect));
            }
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_register is IEntityAliasRegisterLifecycle lifecycle)
        {
            foreach (var alias in _params.Select(item => item.Source?.Alias ?? item.Table?.Alias)
                         .Where(alias => string.IsNullOrWhiteSpace(alias) == false)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                lifecycle.ReleaseAlias(alias);
        }
        _params.Clear();
    }

    /// <summary>
    /// 输出Sql。
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.ToString();
    }

    #endregion
}
