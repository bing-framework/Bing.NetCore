using System.Linq.Expressions;
using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Group By子句
/// </summary>
public class GroupByClause : IGroupByClause
{
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
    /// 分组条件
    /// </summary>
    private readonly List<SqlItem> _group;

    /// <summary>
    /// 分组条件
    /// </summary>
    private string _having;

    /// <summary>
    /// 是否存在分组
    /// </summary>
    public bool IsGroup => _group.Count > 0;

    /// <summary>
    /// 分组列表
    /// </summary>
    public string GroupColumns => _group.Select(t => t.ToSql(_dialect)).Join();

    /// <summary>
    /// 初始化一个<see cref="GroupByClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="group">分组字段</param>
    /// <param name="having">分组条件</param>
    public GroupByClause(SqlClauseContext context, List<SqlItem> group = null, string having = null)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        _dialect = context.Dialect;
        _resolver = context.EntityResolver;
        _register = context.AliasRegister;
        _group = group ?? new List<SqlItem>();
        _having = having;
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">重绑定后的子句运行上下文。</param>
    public virtual IGroupByClause Clone(SqlClauseContext context) => new GroupByClause(context,
        _group.Select(item => item.Clone()).ToList(), _having);

    /// <summary>
    /// 分组
    /// </summary>
    /// <param name="columns">分组字段</param>
    /// <param name="having">分组条件</param>
    public void GroupBy(string columns, string having = null)
    {
        if (string.IsNullOrWhiteSpace(columns))
            return;
        _group.AddRange(columns.Split(',').Select(item => new SqlItem(item)));
        _having = having;
    }

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">分组字段</param>
    public void GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns)
    {
        if (columns == null)
            return;
        foreach (var column in columns)
            GroupBy(column);
    }

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">分组字段</param>
    /// <param name="having">分组条件</param>
    public void GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null)
    {
        if (column == null)
            return;
        _group.Add(new SqlItem(_resolver.GetColumn(column), _register.GetAlias(typeof(TEntity))));
        _having = having;
    }

    /// <summary>
    /// 添加到GroupBy子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, _dialect);
        _group.Add(new SqlItem(sql, raw: true));
    }

    /// <summary>
    /// 获取Sql
    /// </summary>
    public string ToSql()
    {
        if (IsGroup == false)
            return null;
        var result = new StringBuilder();
        result.Append($"Group By {GroupColumns}");
        if (string.IsNullOrWhiteSpace(_having))
            return result.ToString();
        result.Append($" Having {_having}");
        return result.ToString();
    }
}