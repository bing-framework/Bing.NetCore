using System.Text;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 列集合
/// </summary>
public class ColumnCollection
{
    #region 字段

    /// <summary>
    /// 列集合
    /// </summary>
    private readonly List<ColumnItem> _items;

    #endregion

    #region 属性

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="index">索引</param>
    public ColumnItem this[int index] => _items[index];

    /// <summary>
    /// 集合数量
    /// </summary>
    public int Count => _items.Count;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="ColumnCollection"/>类型的实例
    /// </summary>
    /// <param name="items">列集合</param>
    public ColumnCollection(List<ColumnItem> items = null) => _items = items ?? new List<ColumnItem>();

    #endregion

    #region AddColumns(添加列集合)

    /// <summary>
    /// 添加列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    /// <param name="tableAlias">表别名</param>
    public void AddColumns(string columns, string tableAlias = null)
    {
        if (columns.IsEmpty())
            return;
        var items = columns.Split(',').Select(column => CreateItem(column, tableAlias)).ToList();
        items.ForEach(AddColumn);
    }

    /// <summary>
    /// 创建列
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="tableAlias">表别名</param>
    /// <returns>根据列名和表别名创建的列项。</returns>
    private ColumnItem CreateItem(string column, string tableAlias)
    {
        var item = new SqlItem(column, tableAlias);
        return ColumnItem.CreateColumn(item.Name, item.Prefix, item.Alias);
    }

    /// <summary>
    /// 添加列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    /// <param name="tableType">表类型</param>
    /// <param name="columnAlias">列别名</param>
    public void AddColumns(string columns, Type tableType, string columnAlias = null)
    {
        if (columns.IsEmpty())
            return;
        var items = columns.Split(',').Select(column => CreateItem(column, tableType, columnAlias)).ToList();
        items.ForEach(item =>
        {
            RemoveColumn(item);
            AddColumn(item);
        });
    }

    /// <summary>
    /// 创建列
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="tableType">表类型</param>
    /// <param name="columnAlias">列别名</param>
    /// <returns>根据列名、表类型和列别名创建的列项。</returns>
    private ColumnItem CreateItem(string column, Type tableType, string columnAlias = null)
    {
        var item = new SqlItem(column, alias: columnAlias);
        return ColumnItem.CreateColumn(item.Name, columnAlias: item.Alias, tableType: tableType);
    }

    #endregion

    #region AddColumn(添加列)

    /// <summary>
    /// 添加列
    /// </summary>
    /// <param name="item">列</param>
    public void AddColumn(ColumnItem item)
    {
        if (item == null)
            return;
        _items.Add(item);
    }

    /// <summary>
    /// 固定指定实体类型且尚未指定别名的投影表别名。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="tableAlias">表别名。</param>
    internal void FreezeTableAlias(Type entityType, string tableAlias)
    {
        if (entityType == null || string.IsNullOrWhiteSpace(tableAlias))
            return;
        foreach (var item in _items.Where(item => item.TableType == entityType && string.IsNullOrWhiteSpace(item.TableAlias)))
            item.TableAlias = tableAlias;
    }

    /// <summary>
    /// 清空全部列项。
    /// </summary>
    public void Clear() => _items.Clear();

    #endregion

    #region AddRawColumn(添加原始列)

    /// <summary>
    /// 添加原始列
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="columnAlias">列别名</param>
    public void AddRawColumn(string sql, string columnAlias = null)
    {
        if (sql.IsEmpty())
            return;
        AddColumn(ColumnItem.CreateRaw(sql, columnAlias));
    }

    #endregion

    #region AddAggregationColumn(添加聚合列)

    /// <summary>
    /// 添加结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">列名，可包含表别名限定符。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <param name="tableType">实体类型。</param>
    /// <param name="wildcard">是否使用通配符参数。</param>
    /// <param name="argumentRaw">是否将参数作为已解析 SQL 片段。</param>
    public void AddAggregationColumn(SqlAggregateFunction function, string column, string columnAlias = null,
        bool distinct = false, Type tableType = null, bool wildcard = false, bool argumentRaw = false)
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (wildcard && function != SqlAggregateFunction.Count)
            throw new ArgumentException("仅 Count 聚合支持通配符参数。", nameof(function));
        if (wildcard && distinct)
            throw new ArgumentException("Count(*) 不支持 Distinct 聚合参数。", nameof(distinct));

        if (wildcard)
        {
            AddColumn(ColumnItem.CreateAggregateWildcard(function, columnAlias));
            return;
        }

        if (argumentRaw)
        {
            SqlAggregateArgumentValidator.ValidateExpression(column, nameof(column));
            AddColumn(ColumnItem.CreateAggregateExpression(function, column, columnAlias, distinct));
            return;
        }

        AddStructuredAggregationColumn(function, SqlAggregateArgumentValidator.ParseStructuredColumn(column),
            columnAlias, distinct, tableType);
    }

    /// <summary>
    /// 添加已解析路径的结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">已解析的结构化列路径。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <param name="tableType">实体类型。</param>
    /// <param name="useDefaultAlias">未指定 Alias 时是否使用列路径的叶子名称。</param>
    internal void AddStructuredAggregationColumn(SqlAggregateFunction function, SqlIdentifierPath column,
        string columnAlias = null, bool distinct = false, Type tableType = null, bool useDefaultAlias = true)
    {
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        AddColumn(ColumnItem.CreateAggregate(function, column.Name, column.Prefix,
            string.IsNullOrEmpty(columnAlias) && useDefaultAlias ? column.LeafName : columnAlias, distinct,
            column.DatabaseName, tableType));
    }

    /// <summary>
    /// 添加带显式表别名的结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">已解析的结构化列路径。</param>
    /// <param name="tableAlias">聚合列所属表别名。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <param name="tableType">实体类型。</param>
    /// <param name="useDefaultAlias">未指定结果别名时是否使用列路径叶子名称。</param>
    internal void AddStructuredAggregationColumnWithAlias(SqlAggregateFunction function, SqlIdentifierPath column,
        string tableAlias, string columnAlias = null, bool distinct = false, Type tableType = null,
        bool useDefaultAlias = true)
    {
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        AddColumn(ColumnItem.CreateAggregate(function, column.Name, tableAlias,
            string.IsNullOrEmpty(columnAlias) && useDefaultAlias ? column.LeafName : columnAlias, distinct,
            column.DatabaseName, tableType));
    }

    #endregion

    #region RemoveColumns(移除列集合)

    /// <summary>
    /// 移除列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    /// <param name="tableAlias">表别名</param>
    public void RemoveColumns(string columns, string tableAlias = null)
    {
        if (columns.IsEmpty())
            return;
        var items = columns.Split(',').Select(column => CreateItem(column, tableAlias)).ToList();
        items.ForEach(RemoveColumn);
    }

    /// <summary>
    /// 移除列
    /// </summary>
    /// <param name="item">列</param>
    private void RemoveColumn(ColumnItem item)
    {
        if (item == null)
            return;
        _items.RemoveAll(t => t.Name == item.Name && t.TableAlias == item.TableAlias && t.TableType == item.TableType);
    }

    /// <summary>
    /// 移除列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    /// <param name="tableType">表实体类型</param>
    public void RemoveColumns(string columns, Type tableType)
    {
        if (columns.IsEmpty())
            return;
        var items = columns.Split(',').Select(column => CreateItem(column, tableType)).ToList();
        items.ForEach(RemoveColumn);
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前列集合的独立副本。</returns>
    public ColumnCollection Clone() => new ColumnCollection(_items.Select(t => t.Clone()).ToList());

    #endregion

    #region ToSql(获取列名列表)

    /// <summary>
    /// 获取列名列表
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="register">实体别名注册器</param>
    /// <returns>按指定 SQL 方言和实体别名注册器渲染的列列表。</returns>
    public string ToSql(IDialect dialect, IEntityAliasRegister register)
    {
        var result = new StringBuilder();
        foreach (var item in _items)
        {
            result.Append(item.ToSql(dialect, register));
            if (item.Raw == false)
                result.Append(",");
        }

        return result.ToString().TrimEnd(',');
    }

    #endregion
}