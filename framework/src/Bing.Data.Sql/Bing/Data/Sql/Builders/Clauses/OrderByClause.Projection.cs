using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Clauses;

public partial class OrderByClause
{
    /// <summary>
    /// 保存排序子句在参数绑定前的逻辑列顺序。
    /// </summary>
    private List<(SqlProjectionColumn Column, bool Desc)> _logicalOrder = new();
    /// <summary>
    /// 获取投影中的逻辑列集合。
    /// </summary>
    internal IEnumerable<SqlProjectionColumn> LogicalColumns => _logicalOrder.Select(item => item.Column);

    /// <summary>
    /// 将逻辑排序列加入 OrderBy 子句。
    /// </summary>
    /// <param name="columns">要加入的投影列。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <remarks>
    /// 投影列不能包含结果别名；相同列和排序方向不会重复加入。
    /// </remarks>
    internal void AddLogicalColumns(IEnumerable<SqlProjectionColumn> columns, bool desc)
    {
        var items = columns.ToList();
        foreach (var column in items)
        {
            if (column.ResultAlias != null) throw new ArgumentException("OrderBy 中不能设置结果别名。");
            column.ToColumn(_context);
        }
        _context.UseOperation(SqlOperationAction.QueryClause);
        foreach (var column in items)
            if (!_logicalOrder.Any(item => item.Column.Identity == column.Identity && item.Desc == desc))
                _logicalOrder.Add((column, desc));
    }
}
