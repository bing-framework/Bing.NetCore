using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Matedatas;

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
    /// 连接条件
    /// </summary>
    public ICondition Condition { get; private set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="JoinItem"/>类型的实例
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    /// <param name="raw">是否使用原始值</param>
    /// <param name="isSplit">是否用句点分割表名</param>
    /// <param name="type">表实体类型</param>
    public JoinItem(string joinType, string table, string schema = null, string alias = null, bool raw = false,
        bool isSplit = true, Type type = null)
    {
        JoinType = joinType;
        Table = new SqlItem(table, schema, alias, raw, isSplit);
        Type = type;
    }

    /// <summary>
    /// 初始化一个<see cref="JoinItem"/>类型的实例
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表</param>
    /// <param name="type">表实体类型</param>
    /// <param name="condition">连接条件列表</param>
    public JoinItem(string joinType, SqlItem table, Type type, ICondition condition)
    {
        JoinType = joinType;
        Table = table;
        Type = type;
        Condition = condition;
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
        var result = new JoinItem(JoinType, Table, Type, new SqlCondition(Condition?.GetCondition()));
        result.SetDependency(helper);
        return result;
    }

    #endregion

    #region ToSql(获取Join语句)

    /// <summary>
    /// 获取Join语句
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="tableDatabase">表数据表</param>
    public string ToSql(IDialect dialect = null, ITableDatabase tableDatabase = null)
    {
        var table = Table.ToSql(dialect, tableDatabase);
        return $"{JoinType} {table}{GetOn()}";
    }

    /// <summary>
    /// 获取On语句
    /// </summary>
    private string GetOn() => Condition == null ? null : $" On {Condition.GetCondition()}";

    #endregion
}