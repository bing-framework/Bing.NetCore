using System.Linq.Expressions;
using System.Linq;
using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;
using Bing.Extensions;
using Bing.Properties;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Order By子句
/// </summary>
public partial class OrderByClause : IOrderByClause, ISqlMultiSourceOrderByClause
{
    /// <summary>
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// 排序项列表
    /// </summary>
    private readonly List<OrderByItem> _items;

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
    /// 初始化一个 <see cref="OrderByClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="items">排序项列表</param>
    public OrderByClause(SqlClauseContext context, List<OrderByItem> items = null)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        _context = context;
        _items = items ?? new List<OrderByItem>();
        _dialect = context.Dialect;
        _resolver = context.EntityResolver;
        _register = context.AliasRegister;
    }

    /// <inheritdoc />
    public virtual IOrderByClause Clone(SqlClauseContext context)
    {
        var clone = CreateClone(context, new List<OrderByItem>(_items));
        clone._logicalOrder.AddRange(_logicalOrder);
        return clone;
    }

    /// <summary>
    /// 创建克隆后的 Order By 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="items">已复制的排序项。</param>
    /// <returns>保留 Provider 子类类型的 Order By 子句。</returns>
    protected virtual OrderByClause CreateClone(SqlClauseContext context, List<OrderByItem> items) =>
        new OrderByClause(context, items);

    /// <inheritdoc />
    public void OrderBy(string order, string tableAlias = null)
    {
        if (string.IsNullOrWhiteSpace(order))
            return;
        var columns = order.Split(',').Where(column => string.IsNullOrWhiteSpace(column) == false).ToList();
        if (columns.Count == 0)
            return;
        _context.UseOperation(SqlOperationAction.QueryClause);
        columns.ForEach(column => AddItem(column, tableAlias: tableAlias));
    }

    /// <summary>
    /// 添加排序项
    /// </summary>
    /// <param name="column">排序列</param>
    /// <param name="desc">是否倒序</param>
    /// <param name="type">实体类型</param>
    /// <param name="tableAlias">表别名</param>
    protected void AddItem(string column, bool desc = false, Type type = null, string tableAlias = null)
    {
        if (column.IsEmpty())
            return;
        if (Exists(column, tableAlias))
            return;
        _items.Add(new OrderByItem(column, desc, type, prefix: tableAlias));
    }

    /// <summary>
    /// 是否已存在
    /// </summary>
    /// <param name="column">排序列</param>
    /// <param name="tableAlias">表别名</param>
    /// <returns>已存在匹配排序项时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    protected bool Exists(string column, string tableAlias)
    {
        var item = new OrderByItem(column, prefix: tableAlias);
        return _items.Exists(t =>
            t?.Column != null && item.Column != null &&
            t.Column.Equals(item.Column, StringComparison.OrdinalIgnoreCase) &&
            (item.Prefix.IsEmpty() || string.Equals(t.Prefix, item.Prefix, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public void OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false)
    {
        if (column == null)
            return;
        var resolvedColumn = _resolver.GetColumn(column);
        if (string.IsNullOrWhiteSpace(resolvedColumn))
            return;
        _context.UseOperation(SqlOperationAction.QueryClause);
        AddItem(resolvedColumn, desc, typeof(TEntity));
    }

    /// <summary>
    /// 追加已绑定到具体表源实例的排序列。
    /// </summary>
    /// <param name="columns">已按当前方言解析完成的列 SQL。</param>
    /// <param name="desc">是否按降序排列。</param>
    internal void AddBoundColumns(IEnumerable<string> columns, bool desc)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));
        var items = columns.Where(column => string.IsNullOrWhiteSpace(column) == false).ToList();
        if (items.Count == 0)
            return;
        _context.UseOperation(SqlOperationAction.QueryClause);
        foreach (var item in items)
            _items.Add(new OrderByItem(desc ? $"{item} Desc" : item, raw: true));
    }

    /// <inheritdoc />
    void ISqlMultiSourceOrderByClause.AppendBoundColumns(IEnumerable<string> columns, bool desc) =>
        AddBoundColumns(columns, desc);

    /// <inheritdoc />
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        sql = Helper.ResolveSql(sql, _dialect);
        _context.UseOperation(SqlOperationAction.QueryClause);
        _items.Add(new OrderByItem(sql, raw: true));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 在分页查询中验证排序项是否存在。
    /// </remarks>
    public void Validate(bool isPage)
    {
        if (isPage == false)
            return;
        if (_items.Count == 0 && _logicalOrder.Count == 0)
            throw new ArgumentException(LibraryResource.OrderIsEmptyForPage);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => AppendCore(builder, false);

    /// <summary>在同次输出中冻结排序；自定义别名注册器保留原来的 Builder 派生契约。</summary>
    internal SqlQueryOrder AppendFrozenTo(StringBuilder builder) =>
        AppendCore(builder, GetType() == typeof(OrderByClause) && _register.GetType() == typeof(EntityAliasRegister));

    private SqlQueryOrder AppendCore(StringBuilder builder, bool freeze)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (_items.Count == 0 && _logicalOrder.Count == 0)
            return freeze ? new SqlQueryOrder(Array.Empty<OrderByItem>(), "", "") : null;
        var startIndex = builder.Length;
        try
        {
            builder.Append("Order By ");
            if (!freeze)
            {
                builder.Append(string.Join(",", _items.Select(t => t.ToSql(_dialect, _register)).Concat(
                    _logicalOrder.Select(item => item.Column.ToSql(_context) + (item.Desc ? " Desc" : "")))));
                return null;
            }
            var regularSql = string.Join(",", _items.Select(t => t.ToSql(_dialect, _register)));
            var logicalSql = string.Join(",", _logicalOrder.Select(item =>
                item.Column.ToSql(_context) + (item.Desc ? " Desc" : "")));
            builder.Append(regularSql);
            if (regularSql.Length != 0 && logicalSql.Length != 0)
                builder.Append(',');
            builder.Append(logicalSql);
            return new SqlQueryOrder(_items.ToArray(), regularSql, logicalSql);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear() { _items.Clear(); _logicalOrder.Clear(); }

    /// <summary>冻结普通无前缀排序，供分页识别重复排序；其他排序继续由原子句处理。</summary>
    internal string GetFrozenPageOrder()
    {
        if (_items.Count == 0 || _logicalOrder.Count != 0 ||
            _items.Any(item => item.Raw || item.Type != null || !string.IsNullOrWhiteSpace(item.Prefix)))
            return null;
        return string.Join(",", _items.Select(item => item.Order + (item.Desc ? " Desc" : "")));
    }

    /// <inheritdoc />
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.Length == 0 ? null : result.ToString();
    }
}
