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
    /// Sql生成器
    /// </summary>
    protected readonly ISqlBuilder _sqlBuilder;

    /// <summary>
    /// Sql方言
    /// </summary>
    protected readonly IDialect _dialect;

    /// <summary>
    /// 实体解析器
    /// </summary>
    protected readonly IEntityResolver _resolver;

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    protected readonly IEntityAliasRegister _register;

    /// <summary>
    /// 参数管理器
    /// </summary>
    protected readonly IParameterManager _parameterManager;

    /// <summary>
    /// 辅助操作
    /// </summary>
    protected readonly Helper _helper;

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    protected readonly IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    protected readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 参数工厂
    /// </summary>
    protected readonly ISqlParameterFactory _sqlParameterFactory;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    protected readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// Sql 配置
    /// </summary>
    protected readonly SqlOptions _sqlOptions;

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    protected readonly ISqlDatabaseContextResolver _databaseContextResolver;

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    protected readonly DatabaseContext _databaseContext;

    /// <summary>
    /// SQL 对象名格式化器
    /// </summary>
    protected readonly ISqlObjectNameFormatter _objectNameFormatter;

    /// <summary>
    /// 跨数据库查询校验器
    /// </summary>
    protected readonly ISqlCrossDatabaseQueryValidator _crossDatabaseQueryValidator;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected readonly ISqlTableReferenceValidator _tableReferenceValidator;

    /// <summary>
    /// 连接参数
    /// </summary>
    protected readonly List<JoinItem> _params;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="JoinClause"/>类型的实例
    /// </summary>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="joinItems">连接参数列表</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="sqlOptions">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文</param>
    public JoinClause(ISqlBuilder sqlBuilder
        , IDialect dialect
        , IEntityResolver resolver
        , IEntityAliasRegister register
        , IParameterManager parameterManager
        , List<JoinItem> joinItems = null
        , IEntityMappingResolver entityMappingResolver = null
        , IDatabaseContextAccessor databaseContextAccessor = null
        , ISqlParameterFactory sqlParameterFactory = null
        , SqlMetadataOptions metadataOptions = null
        , SqlOptions sqlOptions = null
        , ISqlDatabaseContextResolver databaseContextResolver = null
        , ISqlObjectNameFormatter objectNameFormatter = null
        , ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null
        , ISqlTableReferenceValidator tableReferenceValidator = null
        , DatabaseContext databaseContext = null)
    {
        _sqlBuilder = sqlBuilder;
        _dialect = dialect;
        _resolver = resolver;
        _register = register;
        _parameterManager = parameterManager;
        _entityMappingResolver = entityMappingResolver;
        _databaseContextAccessor = databaseContextAccessor;
        _sqlParameterFactory = sqlParameterFactory;
        _metadataOptions = metadataOptions;
        _sqlOptions = sqlOptions;
        _databaseContextResolver = databaseContextResolver;
        _databaseContext = DatabaseContextSnapshot.Create(databaseContext);
        _objectNameFormatter = objectNameFormatter ?? new DefaultSqlObjectNameFormatter();
        _crossDatabaseQueryValidator = crossDatabaseQueryValidator ?? new DefaultSqlCrossDatabaseQueryValidator();
        _tableReferenceValidator = tableReferenceValidator ?? new DefaultSqlTableReferenceValidator();
        _helper = new Helper(dialect, resolver, register, parameterManager, entityMappingResolver,
            databaseContextAccessor, sqlParameterFactory, metadataOptions, sqlOptions, databaseContextResolver,
            _databaseContext);
        _params = joinItems ?? new List<JoinItem>();
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    public virtual IJoinClause Clone(ISqlBuilder sqlBuilder, IEntityAliasRegister register, IParameterManager parameterManager)
    {
        return new JoinClause(sqlBuilder, _dialect, _resolver, register, parameterManager,
            CloneItems(register, parameterManager), _entityMappingResolver, _databaseContextAccessor,
            _sqlParameterFactory, _metadataOptions, _sqlOptions, _databaseContextResolver, _objectNameFormatter,
            _crossDatabaseQueryValidator, _tableReferenceValidator, _databaseContext);
    }

    /// <summary>
    /// 克隆连接项。
    /// </summary>
    /// <param name="register">实体别名注册器。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <returns>独立的连接项列表。</returns>
    protected List<JoinItem> CloneItems(IEntityAliasRegister register, IParameterManager parameterManager)
    {
        var helper = new Helper(_dialect, _resolver, register, parameterManager, _entityMappingResolver,
            _databaseContextAccessor, _sqlParameterFactory, _metadataOptions, _sqlOptions, _databaseContextResolver,
            _databaseContext);
        return _params.Select(item => item.Clone(helper)).ToList();
    }

    #endregion

    #region Find(查找连接项)

    /// <summary>
    /// 查找连接项
    /// </summary>
    /// <param name="type">表实体类型</param>
    public IJoinOn Find(Type type) => _params.Find(t => t.Type == type);

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
        _register?.RegisterAlias(parsedTable.Alias);
        AddItem(CreateJoinItem(joinType, parsedTable.TableName, parsedTable.Schema, parsedTable.Alias));
    }

    /// <summary>
    /// 解析字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected virtual (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        var parsedTable = SqlTableNameParser.Parse(table, alias);
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
        if (reference.EntityType == null)
            _register?.RegisterAlias(reference.Alias);
        var databaseContext = GetCurrentDatabaseContext();
        var sourceReference = GetSourceReference(databaseContext);
        AddItem(CreateStructuredJoinItem(joinType, reference, reference.EntityType, sourceReference, databaseContext));
        if (reference.EntityType != null)
            _register?.Register(reference.EntityType, _resolver.GetAlias(reference.EntityType, reference.Alias));
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
        Type type = null) => new JoinItem(joinType, table, schema, alias, type: type);

    /// <summary>
    /// 添加连接项
    /// </summary>
    /// <param name="item">表连接项</param>
    private void AddItem(JoinItem item)
    {
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
        return new JoinItem(joinType, new StructuredSqlItem(reference, _objectNameFormatter, databaseContext,
            (_sqlBuilder as SqlBuilderBase)?.ResolveProviderDatabaseType(reference), _tableReferenceValidator,
            _crossDatabaseQueryValidator, sourceReference), type, null);
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
    /// 内连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void Join(ISqlBuilder builder, string alias) => JoinSubquery(JoinKey, builder, alias);

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
        _register?.RegisterAlias(alias);
        var sql = _sqlBuilder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        AddItem(new JoinItem(joinType, $"({sql}) As {_dialect.SafeName(alias)}", raw: true));
    }

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
        AddItem(new JoinItem(joinType, sql, raw: true));
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

    #region On(设置连接条件)

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="condition">连接条件</param>
    public void On(ICondition condition) => GetLastJoinOrThrow().On(condition);

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
        var leftColumn = new SqlItem(GetColumn(left)).ToSql(_dialect);
        var rightColumn = new SqlItem(GetColumn(right)).ToSql(_dialect);
        var condition = SqlConditionFactory.Create(leftColumn, rightColumn, @operator);
        join.AppendOn(condition.GetCondition(), _dialect);
    }

    /// <summary>
    /// 获取列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名</param>
    private string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) =>
        GetColumn(typeof(TEntity), _resolver.GetColumn(column));

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="column">列名</param>
    private string GetColumn(Type entity, string column) => $"{_register.GetAlias(entity)}.{column}";

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
        @group.Select(expression => new OnItem(
            GetColumn(expression, false), GetColumn(expression, true), Lambdas.GetOperator(expression).SafeValue()
        )).ToList();

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    private SqlItem GetColumn(Expression expression, bool right)
    {
        var type = _resolver.GetType(expression, right);
        var column = _resolver.GetColumn(expression, type, right);
        if (string.IsNullOrWhiteSpace(column))
        {
            var name = _parameterManager.GenerateName();
            _parameterManager.Add(name, Lambdas.GetValue(expression));
            return new SqlItem(name, raw: true);
        }

        return new SqlItem(GetColumn(type, column));
    }

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

    #region ToSql(输出Sql)

    /// <summary>
    /// 输出Sql
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        _params.ForEach(item => result.AppendLine($"{item.ToSql(_dialect)} "));
        return result.ToString().Trim();
    }

    #endregion
}
