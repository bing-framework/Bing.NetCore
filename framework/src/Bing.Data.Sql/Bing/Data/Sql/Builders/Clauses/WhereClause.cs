using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Properties;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Where子句
/// </summary>
public class WhereClause : IWhereClause
{
    #region 字段

    /// <summary>
    /// Sql生成器
    /// </summary>
    protected readonly ISqlBuilder Builder;

    /// <summary>
    /// 辅助操作
    /// </summary>
    private readonly Helper _helper;

    /// <summary>
    /// 谓词表达式解析器
    /// </summary>
    private readonly PredicateExpressionResolver _expressionResolver;

    /// <summary>
    /// 方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 实体解析器
    /// </summary>
    private readonly IEntityResolver _resolver;

    /// <summary>
    /// 查询条件
    /// </summary>
    private ICondition _condition;

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    private readonly IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 参数工厂
    /// </summary>
    private readonly ISqlParameterFactory _sqlParameterFactory;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// Sql 配置
    /// </summary>
    private readonly SqlOptions _sqlOptions;

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    private readonly ISqlDatabaseContextResolver _databaseContextResolver;

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="WhereClause"/>类型的实例
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="condition">查询条件</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="sqlOptions">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文</param>
    public WhereClause(ISqlBuilder builder, IDialect dialect, IEntityResolver resolver, IEntityAliasRegister register,
        IParameterManager parameterManager, ICondition condition = null,
        IEntityMappingResolver entityMappingResolver = null, IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlParameterFactory sqlParameterFactory = null, SqlMetadataOptions metadataOptions = null,
        SqlOptions sqlOptions = null, ISqlDatabaseContextResolver databaseContextResolver = null,
        DatabaseContext databaseContext = null)
    {
        Builder = builder;
        _dialect = dialect;
        _resolver = resolver;
        _condition = condition;
        _entityMappingResolver = entityMappingResolver;
        _databaseContextAccessor = databaseContextAccessor;
        _sqlParameterFactory = sqlParameterFactory;
        _metadataOptions = metadataOptions;
        _sqlOptions = sqlOptions;
        _databaseContextResolver = databaseContextResolver;
        _databaseContext = DatabaseContextSnapshot.Create(databaseContext);
        _helper = new Helper(dialect, resolver, register, parameterManager, entityMappingResolver,
            databaseContextAccessor, sqlParameterFactory, metadataOptions, sqlOptions, databaseContextResolver,
            _databaseContext);
        _expressionResolver = new PredicateExpressionResolver(dialect, resolver, register, parameterManager,
            entityMappingResolver, databaseContextAccessor, sqlParameterFactory, metadataOptions, sqlOptions,
            databaseContextResolver, _databaseContext);
    }

    #endregion

    #region 克隆

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    public virtual IWhereClause Clone(ISqlBuilder builder, IEntityAliasRegister register, IParameterManager parameterManager) =>
        new WhereClause(builder, _dialect, _resolver, register, parameterManager,
            new SqlCondition(_condition?.GetCondition()), _entityMappingResolver, _databaseContextAccessor,
            _sqlParameterFactory, _metadataOptions, _sqlOptions, _databaseContextResolver, _databaseContext);

    /// <summary>
    /// 获取合并参数上下文后的子查询 SQL。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    private string GetSubquerySql(ISqlBuilder builder) =>
        Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();

    #endregion

    #region 条件连接

    /// <summary>
    /// And连接条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public void And(ICondition condition) => _condition = new AndCondition(_condition, condition);

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public void Or(ICondition condition) => _condition = new OrCondition(_condition, condition);

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="conditions">查询条件</param>
    public void Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
            return;
        foreach (var condition in conditions.Where(x => x != null))
        {
            var predicate = _expressionResolver.Resolve(condition);
            if (predicate != null)
                Or(predicate);
        }
    }

    /// <summary>
    /// Or连接条件（值为空时忽略条件）
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="conditions">查询条件</param>
    public void OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
            return;
        foreach (var condition in conditions.Where(x => x != null))
        {
            ValidateSingleCondition(condition);

            if (string.IsNullOrWhiteSpace(Lambdas.GetValue(condition).SafeString()))
                continue;

            var predicate = _expressionResolver.Resolve(condition);
            if (predicate != null)
                Or(predicate);
        }
    }

    /// <summary>
    /// 验证是否单一条件
    /// </summary>
    /// <param name="expression">条件表达式</param>
    private void ValidateSingleCondition(LambdaExpression expression)
    {
        if (Lambdas.GetConditionCount(expression) > 1)
            throw new InvalidOperationException(string.Format(LibraryResource.CanOnlyOneCondition, expression));
    }

    #endregion

    #region Where基础方法

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public void Where(ICondition condition) => And(condition);

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void Where(string column, object value, Operator @operator = Operator.Equal) => And(_helper.CreateCondition(column, value, @operator));

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void Where<TEntity>(Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal)
        where TEntity : class => And(_helper.CreateCondition(expression, typeof(TEntity), value, @operator));

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">查询条件表达式</param>
    public void Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var condition = _expressionResolver.Resolve(expression);
        And(condition);
    }

    #endregion

    #region Where子查询

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="operator">运算符</param>
    public void Where(string column, ISqlBuilder builder, Operator @operator = Operator.Equal)
    {
        if (builder == null)
            return;
        column = _helper.GetColumn(column);
        var sql = $"({GetSubquerySql(builder)})";
        And(SqlConditionFactory.Create(column, sql, @operator));
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="operator">运算符</param>
    public void Where<TEntity>(Expression<Func<TEntity, object>> expression, ISqlBuilder builder, Operator @operator = Operator.Equal) 
        where TEntity : class => Where(_helper.GetColumn(expression), builder, @operator);

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    /// <param name="operator">运算符</param>
    public void Where(string column, Action<ISqlBuilder> action, Operator @operator = Operator.Equal)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        Where(column, builder, @operator);
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="action">子查询操作</param>
    /// <param name="operator">运算符</param>
    public void Where<TEntity>(Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action, Operator @operator = Operator.Equal) 
        where TEntity : class => Where(_helper.GetColumn(expression), action, @operator);

    #endregion

    #region WhereIfNotEmpty

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
    /// <param name="operator">运算符</param>
    public void WhereIfNotEmpty(string column, object value, Operator @operator = Operator.Equal)
    {
        if (string.IsNullOrWhiteSpace(value.SafeString()))
            return;
        Where(column, value, @operator);
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
    /// <param name="operator">运算符</param>
    public void WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal) where TEntity : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (string.IsNullOrWhiteSpace(value.SafeString()))
            return;
        Where(expression, value, @operator);
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
    public void WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        ValidateSingleCondition(expression);
        if (string.IsNullOrWhiteSpace(Lambdas.GetValue(expression).SafeString()))
            return;
        Where(expression);
    }

    #endregion

    #region IsNull/IsNotNull

    /// <summary>
    /// 设置Is Null条件
    /// </summary>
    /// <param name="column">列名</param>
    public void IsNull(string column) => And(_helper.CreateCondition(column, null, Operator.Equal));

    /// <summary>
    /// 设置Is Null条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void IsNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class => IsNull(_helper.GetColumn(expression));

    /// <summary>
    /// 设置Is Not Null条件
    /// </summary>
    /// <param name="column">列名</param>
    public void IsNotNull(string column)
    {
        column = _helper.GetColumn(column);
        And(new IsNotNullCondition(column));
    }

    /// <summary>
    /// 设置Is Not Null条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void IsNotNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
    {
        var column = _helper.GetColumn(_resolver.GetColumn(expression), typeof(TEntity));
        IsNotNull(column);
    }

    #endregion

    #region IsEmpty/IsNotEmpty

    /// <summary>
    /// 设置空条件
    /// </summary>
    /// <param name="column">列名</param>
    public void IsEmpty(string column)
    {
        column = _helper.GetColumn(column);
        And(new OrCondition(new IsNullCondition(column), new EqualCondition(column, "''")));
    }

    /// <summary>
    /// 设置空条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void IsEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
    {
        var column = _helper.GetColumn(_resolver.GetColumn(expression), typeof(TEntity));
        IsEmpty(column);
    }

    /// <summary>
    /// 设置非空条件
    /// </summary>
    /// <param name="column">列名</param>
    public void IsNotEmpty(string column)
    {
        column = _helper.GetColumn(column);
        And(new AndCondition(new IsNotNullCondition(column), new NotEqualCondition(column, "''")));
    }

    /// <summary>
    /// 设置非空条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void IsNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
    {
        var column = _helper.GetColumn(_resolver.GetColumn(expression), typeof(TEntity));
        IsNotEmpty(column);
    }

    #endregion

    #region In/NotIn

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="values">值集合</param>
    public void In(string column, IEnumerable<object> values) => Where(column, values, Operator.In);

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="values">值集合</param>
    public void In<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values)
        where TEntity : class =>
        Where(expression, values, Operator.In);

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="builder">Sql生成器</param>
    public void In(string column, ISqlBuilder builder) => AppendSqlBuilder("In", column, builder);

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="builder">Sql生成器</param>
    public void In<TEntity>(Expression<Func<TEntity, object>> expression, ISqlBuilder builder) => 
        In(_helper.GetColumn(expression), builder);

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    public void In(string column, Action<ISqlBuilder> action)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        In(column, builder);
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="action">子查询操作</param>
    public void In<TEntity>(Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action) => 
        In(_helper.GetColumn(expression), action);

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="values">值集合</param>
    public void NotIn(string column, IEnumerable<object> values) => Where(column, values, Operator.NotIn);

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="values">值集合</param>
    public void NotIn<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class => 
        Where(expression, values, Operator.NotIn);

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="builder">Sql生成器</param>
    public void NotIn(string column, ISqlBuilder builder) => AppendSqlBuilder("Not In", column, builder);

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="builder">Sql生成器</param>
    public void NotIn<TEntity>(Expression<Func<TEntity, object>> expression, ISqlBuilder builder) => 
        NotIn(_helper.GetColumn(expression), builder);

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    public void NotIn(string column, Action<ISqlBuilder> action)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        NotIn(column, builder);
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="action">子查询操作</param>
    public void NotIn<TEntity>(Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action) => NotIn(_helper.GetColumn(expression), action);

    /// <summary>
    /// 添加子查询
    /// </summary>
    /// <param name="operation">操作符</param>
    /// <param name="column">列名</param>
    /// <param name="builder">Sql生成器</param>
    private void AppendSqlBuilder(string operation, string column, ISqlBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(column) || builder == null)
            return;
        var result = $"{_helper.GetColumn(column)} {operation} ({GetSubquerySql(builder)})";
        AppendSql(result);
    }

    #endregion

    #region Exists/NotExists

    /// <summary>
    /// 设置Exists条件
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    public void Exists(ISqlBuilder builder)
    {
        if (builder == null)
            return;
        var result = $"Exists ({GetSubquerySql(builder)})";
        AppendSql(result);
    }

    /// <summary>
    /// 设置Exists条件
    /// </summary>
    /// <param name="action">子查询操作</param>
    public void Exists(Action<ISqlBuilder> action)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        Exists(builder);
    }

    /// <summary>
    /// 设置Not Exists条件
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    public void NotExists(ISqlBuilder builder)
    {
        if (builder == null)
            return;
        var result = $"Not Exists ({GetSubquerySql(builder)})";
        AppendSql(result);
    }

    /// <summary>
    /// 设置Not Exists条件
    /// </summary>
    /// <param name="action">子查询操作</param>
    public void NotExists(Action<ISqlBuilder> action)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        NotExists(builder);
    }

    #endregion

    #region Between

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, int? min, int? max, Boundary boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, long? min, long? max, Boundary boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, float? min, float? max, Boundary boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, double? min, double? max, Boundary boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, decimal? min, decimal? max, Boundary boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <param name="boundary">包含边界</param>
    public void Between<TEntity>(Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max, bool includeTime, Boundary? boundary) where TEntity : class
    {
        Where(_helper.Between(expression, typeof(TEntity), GetMin(min, max, includeTime),
            GetMax(min, max, includeTime), GetBoundary(boundary, includeTime)));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, int? min, int? max, Boundary boundary) =>
        HandleBetween(column, min, max, boundary);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, long? min, long? max, Boundary boundary) =>
        HandleBetween(column, min, max, boundary);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, float? min, float? max, Boundary boundary) =>
        HandleBetween(column, min, max, boundary);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, double? min, double? max, Boundary boundary) =>
        HandleBetween(column, min, max, boundary);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, decimal? min, decimal? max, Boundary boundary) =>
        HandleBetween(column, min, max, boundary);

    /// <summary>
    /// 处理范围查询条件
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    private void HandleBetween<T>(string column, T? min, T? max, Boundary boundary) where T : struct, IComparable<T>
    {
        if (min.HasValue && max.HasValue && min.Value.CompareTo(max.Value) > 0)
        {
            Where(_helper.Between(column, max, min, boundary));
            return;
        }
        Where(_helper.Between(column, min, max, boundary));
    }


    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <param name="boundary">包含边界</param>
    public void Between(string column, DateTime? min, DateTime? max, bool includeTime, Boundary? boundary) =>
        Where(_helper.Between(column, GetMin(min, max, includeTime), GetMax(min, max, includeTime), GetBoundary(boundary, includeTime)));

    /// <summary>
    /// 获取最小日期
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    private DateTime? GetMin(DateTime? min, DateTime? max, bool includeTime)
    {
        if (min == null)
            return null;
        var result = min;
        if (min > max)
            result = max;
        return includeTime ? result : result.SafeValue().Date;
    }

    /// <summary>
    /// 获取最大日期
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <returns></returns>
    private DateTime? GetMax(DateTime? min, DateTime? max, bool includeTime)
    {
        if (max == null)
            return null;
        var result = max;
        if (min > max)
            result = min;
        return includeTime ? result : result.SafeValue().Date.AddDays(1);
    }

    /// <summary>
    /// 获取日期范围查询条件边界
    /// </summary>
    /// <param name="boundary">包含边界</param>
    /// <param name="includeTime">是否包含时间</param>
    private Boundary GetBoundary(Boundary? boundary, bool includeTime)
    {
        if (boundary != null)
            return boundary.SafeValue();
        return includeTime ? Boundary.Both : Boundary.Left;
    }

    #endregion

    #region 其他操作

    /// <summary>
    /// 添加到Where子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, _dialect);
        And(new SqlCondition(sql));
    }

    /// <summary>
    /// 输出Sql
    /// </summary>
    public string ToSql()
    {
        var condition = GetCondition();
        if (string.IsNullOrWhiteSpace(condition))
            return null;
        return $"Where {condition}";
    }

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public string GetCondition() => _condition?.GetCondition();

    #endregion

}
