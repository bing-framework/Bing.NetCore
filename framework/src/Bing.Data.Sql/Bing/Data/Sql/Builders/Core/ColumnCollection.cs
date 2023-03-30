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
    private ColumnItem CreateItem(string column, string tableAlias)
    {
        var item = new SqlItem(column, tableAlias);
        return new ColumnItem(item.Name, item.Prefix, item.Alias);
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
    private ColumnItem CreateItem(string column, Type tableType, string columnAlias = null)
    {
        var item = new SqlItem(column, alias: columnAlias);
        return new ColumnItem(item.Name, columnAlias: item.Alias, tableType: tableType);
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
        AddColumn(new ColumnItem(sql, columnAlias: columnAlias, raw: true));
    }

    #endregion

    #region AddAggregationColumn(添加聚合列)

    /// <summary>
    /// 添加聚合列
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    public void AddAggregationColumn(string column, string columnAlias)
    {
        if (column.IsEmpty())
            return;
        AddColumn(new ColumnItem(column, columnAlias: columnAlias, isAggregation: true));
    }

    /// <summary>
    /// 添加聚合列
    /// </summary>
    /// <param name="aggregationFunc">聚合函数</param>
    /// <param name="column">列</param>
    /// <param name="tableType">表类型</param>
    /// <param name="columnAlias">列别名</param>
    public void AddAggregationColumn(string aggregationFunc, string column, Type tableType, string columnAlias)
    {
        if (column.IsEmpty())
            return;
        AddColumn(new ColumnItem(column, columnAlias: string.IsNullOrEmpty(columnAlias) ? column : columnAlias, isAggregation: true, tableType: tableType, aggregationFunc: aggregationFunc));
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
    public ColumnCollection Clone() => new ColumnCollection(_items.Select(t => t.Clone()).ToList());

    #endregion

    #region ToSql(获取列名列表)

    /// <summary>
    /// 获取列名列表
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="register">实体别名注册器</param>
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