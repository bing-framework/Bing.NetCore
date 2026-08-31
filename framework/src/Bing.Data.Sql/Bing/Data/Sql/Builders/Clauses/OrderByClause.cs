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
public class OrderByClause : IOrderByClause
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
    /// 初始化一个<see cref="OrderByClause"/>类型的实例
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

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">重绑定后的子句运行上下文。</param>
    /// <returns>使用指定运行上下文创建的独立 Order By 子句。</returns>
    public virtual IOrderByClause Clone(SqlClauseContext context) =>
        CreateClone(context, new List<OrderByItem>(_items));

    /// <summary>
    /// 创建克隆后的 Order By 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="items">已复制的排序项。</param>
    /// <returns>保留 Provider 子类类型的 Order By 子句。</returns>
    protected virtual OrderByClause CreateClone(SqlClauseContext context, List<OrderByItem> items) =>
        new OrderByClause(context, items);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="order">排序列表</param>
    /// <param name="tableAlias">表别名</param>
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

    /// <summary>
    /// 排序
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">排序列</param>
    /// <param name="desc">是否倒序</param>
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

    /// <summary>
    /// 添加到OrderBy子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        sql = Helper.ResolveSql(sql, _dialect);
        _context.UseOperation(SqlOperationAction.QueryClause);
        _items.Add(new OrderByItem(sql, raw: true));
    }

    /// <summary>
    /// 在分页查询中验证排序项是否存在。
    /// </summary>
    /// <param name="isPage">是否处于分页查询。</param>
    public void Validate(bool isPage)
    {
        if (isPage == false)
            return;
        if (_items.Count == 0)
            throw new ArgumentException(LibraryResource.OrderIsEmptyForPage);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (_items.Count == 0)
            return;
        var startIndex = builder.Length;
        try
        {
            builder.Append("Order By ");
            builder.Append(_items.Select(t => t.ToSql(_dialect, _register)).Join());
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear() => _items.Clear();

    /// <summary>
    /// 获取Sql。
    /// </summary>
    /// <returns>当前 Order By 子句的 SQL 文本；没有排序项时返回 <see langword="null"/>。</returns>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.Length == 0 ? null : result.ToString();
    }
}