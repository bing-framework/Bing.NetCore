using System.Linq.Expressions;
using System.Text;
using Bing.Data;
using Bing.Data.Enums;
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
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext Context;

    /// <summary>
    /// SQL 生成器。
    /// </summary>
    protected ISqlBuilder Builder => Context.Builder;

    /// <summary>
    /// 辅助操作
    /// </summary>
    private readonly Helper _helper;

    /// <summary>
    /// 谓词表达式解析器
    /// </summary>
    private readonly PredicateExpressionResolver _expressionResolver;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    private IDialect _dialect => Context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    private IEntityResolver _resolver => Context.EntityResolver;

    /// <summary>
    /// 查询条件
    /// </summary>
    private ICondition _condition;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="WhereClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public WhereClause(SqlClauseContext context)
        : this(context, null)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 Where 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="condition">查询条件。</param>
    protected WhereClause(SqlClauseContext context, ICondition condition)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _condition = condition;
        _helper = new Helper(Context);
        _expressionResolver = new PredicateExpressionResolver(Context, _helper);
    }

    #endregion

    #region 克隆

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的 Where 子句。</returns>
    public virtual IWhereClause Clone(SqlClauseContext context) =>
        CreateClone(context, new SqlCondition(_condition?.GetCondition()));

    /// <summary>
    /// 创建克隆后的 Where 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="condition">已深复制的查询条件。</param>
    /// <returns>保留 Provider 子类类型的 Where 子句。</returns>
    protected virtual WhereClause CreateClone(SqlClauseContext context, ICondition condition) =>
        new WhereClause(context, condition);

    /// <summary>
    /// 获取合并参数上下文后的子查询 SQL。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    private string GetSubquerySql(ISqlBuilder builder)
    {
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        return Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
    }

    #endregion

    #region 条件连接

    /// <summary>
    /// And连接条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public void And(ICondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition?.GetCondition()))
            return;
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        var result = new AndCondition(_condition, MergeBuilderCondition(condition));
        Context.UseOperation(SqlOperationAction.QueryClause);
        _condition = result;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public void Or(ICondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition?.GetCondition()))
            return;
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        var result = new OrCondition(_condition, MergeBuilderCondition(condition));
        Context.UseOperation(SqlOperationAction.QueryClause);
        _condition = result;
    }

    /// <summary>
    /// 合并作为条件使用的独立 Builder 参数。
    /// </summary>
    /// <param name="condition">查询条件。</param>
    /// <returns>可安全组合的查询条件。</returns>
    private ICondition MergeBuilderCondition(ICondition condition)
    {
        if (condition is not ISqlBuilder builder || Builder is not SqlBuilderBase sqlBuilder)
            return condition;
        return new SqlCondition(sqlBuilder.MergeSubqueryParameters(builder, builder.GetCondition()));
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="conditions">查询条件</param>
    public void Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
            return;
        AppendOrPredicates(conditions.Where(condition => condition != null));
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
        var items = new List<Expression<Func<TEntity, bool>>>();
        foreach (var condition in conditions.Where(condition => condition != null))
        {
            ValidateSingleCondition(condition);
            if (string.IsNullOrWhiteSpace(Lambdas.GetValue(condition).SafeString()))
                continue;
            Context.ValidateOperation(SqlOperationAction.QueryClause);
            items.Add(condition);
        }
        AppendOrPredicates(items);
    }

    /// <summary>
    /// 预检并追加一组 Or 谓词，避免后续解析失败时遗留前序条件或参数。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="conditions">待追加的谓词集合。</param>
    private void AppendOrPredicates<TEntity>(IEnumerable<Expression<Func<TEntity, bool>>> conditions)
    {
        var items = conditions?.ToList();
        if (items == null || items.Count == 0)
            return;
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        var parameterManager = CreateBufferedParameterManager();
        var context = new SqlClauseContext(Builder, Context.Provider, Context.EntityResolver, Context.AliasRegister,
            parameterManager, Context.ExecutionContext, Context.Services);
        var resolver = new PredicateExpressionResolver(context);
        var predicates = new List<ICondition>();
        foreach (var item in items)
        {
            var predicate = resolver.Resolve(item);
            if (string.IsNullOrWhiteSpace(predicate?.GetCondition()) == false)
                predicates.Add(predicate);
        }
        if (predicates.Count == 0)
            return;
        ICondition result = _condition;
        foreach (var predicate in predicates)
            result = new OrCondition(result, predicate);
        parameterManager.Commit();
        Context.UseOperation(SqlOperationAction.QueryClause);
        _condition = result;
    }

    /// <summary>
    /// 创建保留参数元数据的写入缓冲区。
    /// </summary>
    private BufferedParameterManager CreateBufferedParameterManager() =>
        Context.ParameterManager is IAdvancedParameterManager advancedParameterManager
            ? new BufferedAdvancedParameterManager(advancedParameterManager)
            : new BufferedParameterManager(Context.ParameterManager);

    /// <summary>
    /// 在独立参数副本上预检，并延迟向真实管理器写入参数的缓冲区。
    /// </summary>
    private class BufferedParameterManager : IParameterManager
    {
        /// <summary>
        /// 真实参数管理器。
        /// </summary>
        private readonly IParameterManager _target;

        /// <summary>
        /// 预检参数管理器。
        /// </summary>
        protected IParameterManager Probe { get; }

        /// <summary>
        /// 通过预检后的参数写入操作。
        /// </summary>
        private readonly List<Action<IParameterManager>> _operations = new();

        /// <summary>
        /// 初始化参数写入缓冲区。
        /// </summary>
        /// <param name="target">真实参数管理器。</param>
        public BufferedParameterManager(IParameterManager target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            Probe = _target.Clone();
        }

        /// <inheritdoc />
        public int Count => Probe.Count;

        /// <inheritdoc />
        public string GenerateName() => Probe.GenerateName();

        /// <inheritdoc />
        public string NormalizeName(string name) => Probe.NormalizeName(name);

        /// <inheritdoc />
        public void Add(string name, object value, Operator? @operator = null)
        {
            Probe.Add(name, value, @operator);
            _operations.Add(target => target.Add(name, value, @operator));
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> GetParams() => Probe.GetParams();

        /// <inheritdoc />
        public bool Contains(string name) => Probe.Contains(name);

        /// <inheritdoc />
        public object GetValue(string name) => Probe.GetValue(name);

        /// <inheritdoc />
        public IParameterManager Clone() => Probe.Clone();

        /// <inheritdoc />
        public IParameterManager CreateEmpty() => Probe.CreateEmpty();

        /// <inheritdoc />
        public void Clear() => throw new NotSupportedException("参数预检期间不支持清空缓冲区。");

        /// <summary>
        /// 将全部预检通过的写入操作提交到真实参数管理器。
        /// </summary>
        public void Commit()
        {
            foreach (var operation in _operations)
                operation(_target);
        }

        /// <summary>
        /// 记录增强参数的写入操作。
        /// </summary>
        /// <param name="operation">写入操作。</param>
        protected void Record(Action<IParameterManager> operation) => _operations.Add(operation);
    }

    /// <summary>
    /// 保留 SqlParam 元数据的参数写入缓冲区。
    /// </summary>
    private sealed class BufferedAdvancedParameterManager : BufferedParameterManager, IAdvancedParameterManager
    {
        /// <summary>
        /// 预检增强参数管理器。
        /// </summary>
        private IAdvancedParameterManager AdvancedProbe => (IAdvancedParameterManager)Probe;

        /// <summary>
        /// 初始化增强参数写入缓冲区。
        /// </summary>
        /// <param name="target">真实增强参数管理器。</param>
        public BufferedAdvancedParameterManager(IAdvancedParameterManager target)
            : base(target)
        {
        }

        /// <inheritdoc />
        public void Add(SqlParam parameter)
        {
            var snapshot = CloneSqlParameter(parameter);
            AdvancedProbe.Add(snapshot);
            Record(target => ((IAdvancedParameterManager)target).Add(CloneSqlParameter(snapshot)));
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => AdvancedProbe.GetSqlParams();

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> ExportValues() => AdvancedProbe.ExportValues();

        /// <summary>
        /// 深复制参数容器，避免预检实现修改后影响真实提交。
        /// </summary>
        /// <param name="parameter">源参数。</param>
        /// <returns>独立的参数副本。</returns>
        private static SqlParam CloneSqlParameter(SqlParam parameter)
        {
            if (parameter == null)
                return null;
            return new SqlParam(parameter.Name, parameter.Value, parameter.DbType, parameter.Direction,
                parameter.Size, parameter.Precision, parameter.Scale)
            {
                OriginalValue = parameter.OriginalValue,
                EntityType = parameter.EntityType,
                PropertyName = parameter.PropertyName,
                ColumnName = parameter.ColumnName,
                DatabaseType = parameter.DatabaseType,
                ProviderTypeName = parameter.ProviderTypeName,
                Source = parameter.Source,
                MetadataLevel = parameter.MetadataLevel,
                StorageKind = parameter.StorageKind,
                ConverterKind = parameter.ConverterKind,
                CustomConverterName = parameter.CustomConverterName
            };
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
    public void Where(string column, object value, Operator @operator = Operator.Equal)
    {
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        And(_helper.CreateCondition(column, value, @operator));
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void Where<TEntity>(Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        Context.ValidateOperation(SqlOperationAction.QueryClause);
        And(_helper.CreateCondition(expression, typeof(TEntity), value, @operator));
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">查询条件表达式</param>
    public void Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
    /// <remarks>In 和 NotIn 使用子查询集合语义，其他运算符按标量子查询条件处理。</remarks>
    public void Where(string column, ISqlBuilder builder, Operator @operator = Operator.Equal)
    {
        if (builder == null)
            return;
        if (@operator == Operator.In)
        {
            In(column, builder);
            return;
        }
        if (@operator == Operator.NotIn)
        {
            NotIn(column, builder);
            return;
        }
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
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
        if (Context.Provider.DatabaseType == DatabaseType.Oracle)
        {
            And(new IsNullCondition(column));
            return;
        }
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
        if (Context.Provider.DatabaseType == DatabaseType.Oracle)
        {
            And(new IsNotNullCondition(column));
            return;
        }
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
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
        Context.ValidateOperation(SqlOperationAction.QueryClause);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        ValidateBetweenOperation(min, max);
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
        HandleBetween(column, min, max, includeTime, boundary);

    /// <summary>
    /// 验证范围查询操作。
    /// </summary>
    /// <typeparam name="T">范围值类型。</typeparam>
    /// <param name="min">最小值。</param>
    /// <param name="max">最大值。</param>
    private void ValidateBetweenOperation<T>(T? min, T? max) where T : struct
    {
        if (min.HasValue == false && max.HasValue == false)
            return;
        Context.ValidateOperation(SqlOperationAction.QueryClause);
    }

    /// <summary>
    /// 处理日期范围查询。
    /// </summary>
    /// <param name="column">列名。</param>
    /// <param name="min">最小值。</param>
    /// <param name="max">最大值。</param>
    /// <param name="includeTime">是否包含时间。</param>
    /// <param name="boundary">包含边界。</param>
    private void HandleBetween(string column, DateTime? min, DateTime? max, bool includeTime, Boundary? boundary)
    {
        ValidateBetweenOperation(min, max);
        Where(_helper.Between(column, GetMin(min, max, includeTime), GetMax(min, max, includeTime),
            GetBoundary(boundary, includeTime)));
    }

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
    /// 获取日期范围的最终最大值。
    /// </summary>
    /// <param name="min">最小日期。</param>
    /// <param name="max">最大日期。</param>
    /// <param name="includeTime">为 true 时保留时间部分；为 false 时扩展到日期结束边界。</param>
    /// <returns>经范围纠正后的最大日期；未指定最大日期时返回 null。</returns>
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

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var condition = GetCondition();
        if (string.IsNullOrWhiteSpace(condition))
            return;
        builder.Append("Where ");
        builder.Append(condition);
    }

    /// <inheritdoc />
    public void Clear() => _condition = null;

    /// <summary>
    /// 输出Sql。
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.Length == 0 ? null : result.ToString();
    }

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public string GetCondition() => _condition?.GetCondition();

    #endregion

}
