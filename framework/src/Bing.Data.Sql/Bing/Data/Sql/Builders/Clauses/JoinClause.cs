using System.Linq.Expressions;
using System.Text;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Matedatas;
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
    private readonly ISqlBuilder _sqlBuilder;

    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 实体解析器
    /// </summary>
    private readonly IEntityResolver _resolver;

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    private readonly IEntityAliasRegister _register;

    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 表数据库
    /// </summary>
    protected ITableDatabase TableDatabase;

    /// <summary>
    /// 辅助操作
    /// </summary>
    private readonly Helper _helper;

    /// <summary>
    /// 连接参数
    /// </summary>
    private readonly List<JoinItem> _params;

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
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="joinItems">连接参数列表</param>
    public JoinClause(ISqlBuilder sqlBuilder
        , IDialect dialect
        , IEntityResolver resolver
        , IEntityAliasRegister register
        , IParameterManager parameterManager
        , ITableDatabase tableDatabase
        , List<JoinItem> joinItems = null)
    {
        _sqlBuilder = sqlBuilder;
        _dialect = dialect;
        _resolver = resolver;
        _register = register;
        _parameterManager = parameterManager;
        TableDatabase = tableDatabase;
        _helper = new Helper(dialect, resolver, register, parameterManager);
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
    public IJoinClause Clone(ISqlBuilder sqlBuilder, IEntityAliasRegister register, IParameterManager parameterManager)
    {
        var helper = new Helper(_dialect, _resolver, register, parameterManager);
        return new JoinClause(sqlBuilder, _dialect, _resolver, register, parameterManager, TableDatabase,
            _params.Select(t => t.Clone(helper)).ToList());
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
    /// 表连接
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    private void Join(string joinType, string table, string alias)
    {
        var item = CreateJoinItem(joinType, table, null, alias);
        AddItem(item);
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
        var table = _resolver.GetTableAndSchema(type);
        var item = CreateJoinItem(joinType, table, schema, alias, type);
        AddItem(item);
        _register.Register(type, _resolver.GetAlias(type, alias));
    }

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void Join(ISqlBuilder builder, string alias) => AppendJoin(JoinKey, builder, alias);

    /// <summary>
    /// 添加到连接子句
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    private void AppendJoin(string joinType, ISqlBuilder builder, string alias) =>
        AppendJoin(joinType, $"({builder.ToSql()}) As {_dialect.SafeName(alias)}");

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void Join(Action<ISqlBuilder> action, string alias) => AppendJoin(JoinKey, action, alias);

    /// <summary>
    /// 添加到连接子句
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    private void AppendJoin(string joinType, Action<ISqlBuilder> action, string alias)
    {
        if (action == null)
            return;
        var builder = _sqlBuilder.New();
        action(builder);
        AppendJoin(joinType, builder, alias);
    }

    #endregion

    #region AppendJoin(添加到内连接子句)

    /// <summary>
    /// 添加到内连接子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendJoin(string sql) => AppendJoin(JoinKey, sql);

    /// <summary>
    /// 添加到连接子句
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="sql">Sql语句</param>
    private void AppendJoin(string joinType, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, _dialect);
        var item = new JoinItem(joinType, sql, raw: true);
        AddItem(item);
    }

    #endregion

    #region LeftJoin(左外连接)

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void LeftJoin(string table, string alias = null) => Join(LeftJoinKey, table, alias);

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
    public void LeftJoin(ISqlBuilder builder, string alias) => AppendJoin(LeftJoinKey, builder, alias);

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void LeftJoin(Action<ISqlBuilder> action, string alias) => AppendJoin(LeftJoinKey, action, alias);

    #endregion

    #region AppendLeftJoin(添加到左外连接子句)

    /// <summary>
    /// 添加到左外连接子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
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
    public void RightJoin(ISqlBuilder builder, string alias) => AppendJoin(RightJoinKey, builder, alias);

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void RightJoin(Action<ISqlBuilder> action, string alias) => AppendJoin(RightJoinKey, action, alias);

    #endregion

    #region AppendRightJoin(添加到右外连接子句)

    /// <summary>
    /// 添加到右外连接子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendRightJoin(string sql) => AppendJoin(RightJoinKey, sql);

    #endregion

    #region On(设置连接条件)

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="condition">连接条件</param>
    public void On(ICondition condition) => _params.LastOrDefault()?.On(condition);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void On(string column, object value, Operator @operator = Operator.Equal) =>
        _params.LastOrDefault()?.On(column, value, @operator);

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
        var leftColumn = new SqlItem(GetColumn(left)).ToSql(_dialect);
        var rightColumn = new SqlItem(GetColumn(right)).ToSql(_dialect);
        var condition = SqlConditionFactory.Create(leftColumn, rightColumn, @operator);
        AppendOn(condition.GetCondition());
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
        var expressions = Lambdas.GetGroupPredicates(expression);
        var items = expressions.Select(GetOnItems).ToList();
        _params.LastOrDefault()?.On(items, _dialect);
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
    public void AppendOn(string sql) => _params.LastOrDefault()?.AppendOn(sql, _dialect);

    #endregion

    #region ToSql(输出Sql)

    /// <summary>
    /// 输出Sql
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        _params.ForEach(item => result.AppendLine($"{item.ToSql(_dialect, TableDatabase)} "));
        return result.ToString().Trim();
    }

    #endregion
}