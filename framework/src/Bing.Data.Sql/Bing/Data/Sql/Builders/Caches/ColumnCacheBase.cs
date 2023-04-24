using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Text;

namespace Bing.Data.Sql.Builders.Caches;

/// <summary>
/// 列缓存基类
/// </summary>
public abstract class ColumnCacheBase : IColumnCache
{
    /// <summary>
    /// Sql方言
    /// </summary>
    protected readonly IDialect Dialect;

    /// <summary>
    /// 初始化一个<see cref="ColumnCacheBase"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    protected ColumnCacheBase(IDialect dialect)
    {
        Dialect = dialect;
    }

    /// <summary>
    /// 从缓存中获取处理后的列
    /// </summary>
    /// <param name="column">列</param>
    public abstract string GetSafeColumn(string column);

    /// <summary>
    /// 从缓存中获取处理后的列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    public abstract string GetSafeColumns(string columns);

    /// <summary>
    /// 规范化列
    /// </summary>
    /// <param name="column">列</param>
    protected string NormalizeColumn(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return null;
        var item = new ColumnItemV1(Dialect, column);
        return item.ToResult();
    }

    /// <summary>
    /// 规范化列集合
    /// </summary>
    /// <param name="columns">列集合</param>
    protected string NormalizeColumns(string columns)
    {
        if (string.IsNullOrWhiteSpace(columns))
            return null;
        var result = new StringBuilder();
        var items = columns.Split(',')
            .Where(column => string.IsNullOrWhiteSpace(column) == false)
            .Select(column => new ColumnItemV1(Dialect, column));
        foreach (var item in items)
        {
            item.AppendTo(result);
            result.Append(",");
        }

        result.RemoveEnd(",");
        return result.ToString();
    }
}
