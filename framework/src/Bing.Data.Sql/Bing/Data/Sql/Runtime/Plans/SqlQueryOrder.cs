using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Extensions;

namespace Bing.Data.Sql;

/// <summary>冻结排序文本与列身份；追加分页排序时不回读子句、映射或别名注册器。</summary>
internal sealed class SqlQueryOrder
{
    private readonly OrderByItem[] _items;
    private readonly string _regularSql;
    private readonly string _logicalSql;

    internal SqlQueryOrder(OrderByItem[] items, string regularSql, string logicalSql)
    {
        _items = items;
        _regularSql = regularSql;
        _logicalSql = logicalSql;
    }

    internal bool HasOrder => _regularSql.Length != 0 || _logicalSql.Length != 0;

    /// <summary>普通项排在逻辑 Lambda 项之前，与 OrderByClause 的既有输出顺序一致。</summary>
    internal string ToSql() => "Order By " + _regularSql +
        (_regularSql.Length != 0 && _logicalSql.Length != 0 ? "," : "") + _logicalSql;

    /// <summary>仅在有新列时复制身份集合；原查询和重复派生共享只读排序描述。</summary>
    internal SqlQueryOrder Append(string order, IDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(order))
            return this;
        List<OrderByItem> items = null;
        List<string> sql = null;
        foreach (var column in order.Split(','))
        {
            if (string.IsNullOrWhiteSpace(column))
                continue;
            var item = new OrderByItem(column);
            var existing = items == null ? (IEnumerable<OrderByItem>)_items : items;
            // 无前缀的请求匹配任意已有同名列；方向不参与身份比较，保留首个方向。
            if (existing.Any(value => value?.Column != null && item.Column != null &&
                value.Column.Equals(item.Column, StringComparison.OrdinalIgnoreCase) &&
                (item.Prefix.IsEmpty() || string.Equals(value.Prefix, item.Prefix, StringComparison.OrdinalIgnoreCase))))
                continue;
            (items ??= new List<OrderByItem>(_items)).Add(item);
            (sql ??= new List<string>()).Add(item.ToSqlWithPrefix(dialect, item.Prefix));
        }
        if (items == null)
            return this;
        var appended = string.Join(",", sql);
        return new SqlQueryOrder(items.ToArray(),
            _regularSql.Length == 0 ? appended : _regularSql + "," + appended, _logicalSql);
    }
}
