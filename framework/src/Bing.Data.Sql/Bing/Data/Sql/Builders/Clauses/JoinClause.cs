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
        .Where(item => item.Item.Type != null)
        .Select(item => item.Item.Source ?? new TableSource($"join_{item.Index}", item.Item.Table, item.Item.Type))
        .ToList();

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
        AddItem(CreateStructuredJoinItem(joinType, reference, reference.EntityType, sourceReference, databaseContext));
        if (reference.EntityType != null)
        {
            FreezeExistingProjectionAlias(reference.EntityType);
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
    private void AddItem(JoinItem item)
    {
        _context.UseOperation(SqlOperationAction.QueryClause);
        item.SetDependency(_helper);
        _params.Add(item);
    }

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
    private void FreezeExistingProjectionAlias(Type entityType)
    {
        if (_register?.Contains(entityType) != true || _sqlBuilder.SelectClause is not SelectClause selectClause)
            return;
        selectClause.FreezeEntityAlias(entityType, _register.GetAlias(entityType));
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
        var sql = _sqlBuilder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        _register?.RegisterAlias(alias);
        AddItem(JoinItem.CreateRaw(joinType, $"({sql}){GetSubqueryAlias(alias)}", alias));
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
        var sql = _sqlBuilder is SqlBuilderBase sqlBuilder
            ? sqlBuilder.RenderSubquery(subquery.Builder)
            : subquery.Builder.ToSql();
        _register?.RegisterAlias(subquery.Alias);
        var table = SqlItem.Raw($"({sql}){GetSubqueryAlias(subquery.Alias)}");
        AddItem(JoinItem.CreateDerived(joinType, table,
            new TableSource($"join_{_params.Count}", table, typeof(TProjection), subquery.Alias,
                subquery.ProjectedMembers)));
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
        var expressions = Lambdas.GetGroupPredicates(expression);
        var items = expressions.Select(GetOnItems).ToList();
        join.On(items, _dialect);
    }

    /// <summary>
    /// 设置连接条件组
    /// </summary>
    /// <param name="group">条件组</param>
    private List<OnItem> GetOnItems(List<Expression> group) =>
        @group.Select(expression =>
        {
            var leftType = _resolver.GetType(expression, false);
            var rightType = _resolver.GetType(expression, true);
            var selfJoin = leftType == rightType;
            return new OnItem(
                GetColumn(expression, false, selfJoin), GetColumn(expression, true, selfJoin),
                Lambdas.GetOperator(expression).SafeValue());
        }).ToList();

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <param name="selfJoin">是否为同实体自连接。</param>
    private SqlItem GetColumn(Expression expression, bool right, bool selfJoin)
    {
        var type = _resolver.GetType(expression, right);
        var column = _resolver.GetColumn(expression, type, right);
        if (string.IsNullOrWhiteSpace(column))
        {
            var name = _parameterManager.GenerateName();
            _parameterManager.Add(name, Lambdas.GetValue(expression));
            return new SqlItem(name, raw: true);
        }

        return new SqlItem(GetColumn(type, column, selfJoin, right));
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
        return join;
    }

    #endregion

    #region 输出

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        for (var index = 0; index < _params.Count; index++)
        {
            if (index > 0)
                builder.AppendLine(" ");
            builder.Append(_params[index].ToSql(_dialect));
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
