using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 表连接项
/// </summary>
public class JoinItem : IJoinOn
{
    #region 字段

    /// <summary>
    /// 辅助操作
    /// </summary>
    private Helper _helper;

    #endregion

    #region 属性

    /// <summary>
    /// 连接类型
    /// </summary>
    public string JoinType { get; }

    /// <summary>
    /// 表
    /// </summary>
    public SqlItem Table { get; }

    /// <summary>
    /// 表实体类型
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// 类型化派生表的绑定来源；普通连接为 null。
    /// </summary>
    internal TableSource Source { get; }

    /// <summary>
    /// 连接条件
    /// </summary>
    public ICondition Condition { get; private set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建连接项。
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表项。</param>
    /// <param name="type">表实体类型。</param>
    /// <param name="condition">连接条件。</param>
    public static JoinItem Create(string joinType, SqlItem table, Type type = null, ICondition condition = null) =>
        new(joinType, table, type, condition);

    /// <summary>
    /// 创建保留 DTO 投影绑定信息的类型化派生表连接项。
    /// </summary>
    internal static JoinItem CreateDerived(string joinType, SqlItem table, TableSource source) =>
        new(joinType, table, source?.EntityType, null, source);

    /// <summary>
    /// 创建结构化表连接项。
    /// </summary>
    /// <param name="joinType">连接类型。</param>
    /// <param name="table">表名。</param>
    /// <param name="schema">架构名。</param>
    /// <param name="alias">别名。</param>
    /// <param name="type">表实体类型。</param>
    /// <returns>结构化表连接项。</returns>
    public static JoinItem CreateTable(string joinType, string table, string schema = null, string alias = null,
        Type type = null) => Create(joinType, SqlItem.Parse(table, schema, alias), type);

    /// <summary>
    /// 创建原子表名连接项。
    /// </summary>
    /// <param name="joinType">连接类型。</param>
    /// <param name="table">表名。</param>
    /// <param name="schema">架构名。</param>
    /// <param name="alias">别名。</param>
    /// <param name="type">表实体类型。</param>
    /// <returns>原子表名连接项。</returns>
    public static JoinItem CreateAtomicTable(string joinType, string table, string schema = null, string alias = null,
        Type type = null) => Create(joinType, SqlItem.Atomic(table, schema, alias), type);

    /// <summary>
    /// 创建原始 SQL 连接项。
    /// </summary>
    /// <param name="joinType">连接类型。</param>
    /// <param name="sql">原始 SQL。</param>
    /// <param name="alias">别名。</param>
    /// <param name="type">表实体类型。</param>
    /// <returns>原始 SQL 连接项。</returns>
    public static JoinItem CreateRaw(string joinType, string sql, string alias = null, Type type = null) =>
        Create(joinType, SqlItem.Raw(sql, alias), type);

    /// <summary>
    /// 初始化一个<see cref="JoinItem"/>类型的实例
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表</param>
    /// <param name="type">表实体类型</param>
    /// <param name="condition">连接条件列表</param>
    /// <param name="source">类型化派生表的来源绑定信息。</param>
    private JoinItem(string joinType, SqlItem table, Type type, ICondition condition, TableSource source = null)
    {
        JoinType = joinType;
        Table = table;
        Type = type;
        Condition = condition;
        Source = source;
    }

    #endregion

    #region SetDependency(设置依赖项)

    /// <summary>
    /// 设置依赖项
    /// </summary>
    /// <param name="helper">辅助操作</param>
    public void SetDependency(Helper helper) => _helper = helper;

    #endregion

    #region On(设置连接条件)

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="condition">连接条件</param>
    public void On(ICondition condition)
    {
        EnsureSupportsOn();
        if (condition == null)
            return;
        Condition = new AndCondition(Condition, condition);
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public void On(string column, object value, Operator @operator = Operator.Equal)
    {
        EnsureSupportsOn();
        if (_helper == null)
            return;
        var condition = _helper.CreateCondition(column, value, @operator);
        On(condition);
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="items">连接条件项</param>
    /// <param name="dialect">Sql方言</param>
    public void On(List<List<OnItem>> items, IDialect dialect)
    {
        EnsureSupportsOn();
        if (items == null)
            return;
        ICondition orCondition = null;
        foreach (var onItems in items)
        {
            ICondition condition = null;
            foreach (var item in onItems)
                condition = new AndCondition(condition, SqlConditionFactory.Create(item.Left.ToSql(dialect), item.Right.ToSql(dialect), item.Operator));
            orCondition = new OrCondition(orCondition, condition);
        }
        On(orCondition);
    }

    #endregion

    #region AppendOn(添加到On子句)

    /// <summary>
    /// 添加到On子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="dialect">Sql方言</param>
    public void AppendOn(string sql, IDialect dialect)
    {
        EnsureSupportsOn();
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, dialect);
        On(new SqlCondition(sql));
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    public JoinItem Clone(Helper helper)
    {
        var condition = Condition == null ? null : new SqlCondition(Condition.GetCondition());
        var result = Source == null
            ? Create(JoinType, Table?.Clone(), Type, condition)
            : new JoinItem(JoinType, Table?.Clone(), Type, condition, Source.Clone());
        result.SetDependency(helper);
        return result;
    }

    #endregion

    #region ToSql(获取Join语句)

    /// <summary>
    /// 获取Join语句
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    public string ToSql(IDialect dialect = null)
    {
        var table = Table.ToSql(dialect);
        return $"{JoinType} {table}{GetOn()}";
    }

    /// <summary>
    /// 获取 On 语句。
    /// </summary>
    private string GetOn()
    {
        if (Condition == null)
            return null;
        return HasRawOnCondition() ? $" And {Condition.GetCondition()}" : $" On {Condition.GetCondition()}";
    }

    /// <summary>
    /// 验证当前连接允许追加 On 条件。
    /// </summary>
    private void EnsureSupportsOn()
    {
        if (string.Equals(JoinType, "Cross Join", StringComparison.Ordinal))
            throw new InvalidOperationException("Cross Join 不支持 On 条件。");
    }

    /// <summary>
    /// 是否已在原始 Join 文本中提供 On 条件。
    /// </summary>
    private bool HasRawOnCondition()
    {
        if (Table?.IsRaw != true || string.IsNullOrWhiteSpace(Table.Name))
            return false;
        return SqlBuilderBase.ContainsSqlKeyword(Table.Name, "On");
    }

    #endregion
}