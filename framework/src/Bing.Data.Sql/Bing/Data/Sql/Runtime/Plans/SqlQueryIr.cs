using System.Data;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 单次查询执行使用的不可变中间表示。
/// </summary>
/// <remarks>
/// Builder 只在查询创建阶段负责渲染；执行阶段消费该 IR 保存的 SQL、参数和分页快照，
/// 因而不需要继续持有可变 Builder。IR 保持最小字段集合，不承载连接、事务或映射状态。
/// </remarks>
internal sealed class SqlQueryIr
{
    private readonly SqlBuilderExecutionSnapshot _snapshot;
    private readonly SqlQueryPagerSnapshot _pager;

    private SqlQueryIr(SqlBuilderExecutionSnapshot snapshot, CommandType commandType,
        SqlQueryPagerSnapshot pager)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        CommandType = commandType;
        _pager = pager;
    }

    /// <summary>
    /// 获取冻结的 SQL 与参数快照。
    /// </summary>
    internal SqlBuilderExecutionSnapshot Snapshot => _snapshot;

    /// <summary>模板只改变参数作用域，默认分页值快照保持共享且不可变。</summary>
    internal SqlQueryIr WithSnapshot(SqlBuilderExecutionSnapshot snapshot) => new(snapshot, CommandType, _pager);

    /// <summary>
    /// 获取当前查询使用的命令类型。
    /// </summary>
    internal CommandType CommandType { get; }

    /// <summary>是否能够直接从冻结的 Select 结构派生 Count。</summary>
    internal bool CanDeriveStructuredCount => Snapshot.SelectShape != null;

    /// <summary>通过已冻结的排序与参数描述派生页面，保留 Builder 兼容边界。</summary>
    internal bool CanDeriveStructuredPage(IPager pager) => Snapshot.CanDerivePage(pager.Order);

    /// <summary>创建不依赖规划 Builder 的独立页面 IR。</summary>
    internal SqlQueryIr DerivePage(IPager pager) =>
        new(Snapshot.WithPage(pager), System.Data.CommandType.Text, SqlQueryPagerSnapshot.Create(pager));

    /// <summary>
    /// 创建查询 IR。
    /// </summary>
    /// <param name="snapshot">已渲染的 SQL 和参数快照。</param>
    /// <param name="commandType">命令类型。</param>
    /// <param name="pager">Builder 当前分页参数；可以为空。</param>
    /// <returns>不可变查询 IR。</returns>
    internal static SqlQueryIr Create(SqlBuilderExecutionSnapshot snapshot, CommandType commandType,
        IPager pager) =>
        new(snapshot, commandType, SqlQueryPagerSnapshot.Create(pager));

    /// <summary>
    /// 兼容旧的内部调用签名；规划 Builder 已迁移到查询计划派生边界。
    /// </summary>
    /// <param name="snapshot">已渲染的 SQL 和参数快照。</param>
    /// <param name="commandType">命令类型。</param>
    /// <param name="pager">分页参数。</param>
    /// <param name="planningBuilder">旧版规划 Builder；当前 IR 不再持有它。</param>
    /// <returns>不包含 Builder 的统一查询 IR。</returns>
    internal static SqlQueryIr Create(SqlBuilderExecutionSnapshot snapshot, CommandType commandType,
        IPager pager, ISqlBuilder planningBuilder) => Create(snapshot, commandType, pager);

    /// <summary>
    /// 创建当前查询的 Count 派生 IR。
    /// </summary>
    /// <param name="alias">派生查询别名。</param>
    /// <returns>包含 Count SQL 的不可变查询 IR。</returns>
    internal SqlQueryIr DeriveCount(string alias = "__bing_raw_count")
    {
        if (Snapshot.SelectShape != null)
            return new SqlQueryIr(Snapshot.SelectShape.DeriveCount(Snapshot), System.Data.CommandType.Text, null);
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("派生查询别名不能为空。", nameof(alias));
        var sql = $"Select Count(*) From ({Snapshot.Sql}) As {alias}";
        return new SqlQueryIr(Snapshot.WithSql(sql),
            System.Data.CommandType.Text, null);
    }

    /// <summary>
    /// 创建本次查询的独立默认分页参数。
    /// </summary>
    /// <returns>分页快照；未配置分页时返回 null。</returns>
    internal IPager CreateDefaultPagerSnapshot() => _pager?.CreatePager();
}

/// <summary>
/// 分页参数的不可变值快照。
/// </summary>
internal sealed class SqlQueryPagerSnapshot
{
    private readonly int _page;
    private readonly int _pageSize;
    private readonly int _totalCount;
    private readonly bool _isTotalCountKnown;
    private readonly string _order;

    private SqlQueryPagerSnapshot(IPager pager)
    {
        _page = pager.Page;
        _pageSize = pager.PageSize;
        _totalCount = pager.TotalCount;
        _isTotalCountKnown = pager is Pager concretePager
            ? concretePager.IsTotalCountKnown
            : pager.TotalCount != 0;
        _order = pager.Order;
    }

    /// <summary>
    /// 从可变分页对象创建值快照。
    /// </summary>
    /// <param name="pager">待复制的分页参数。</param>
    /// <returns>分页快照；参数为空时返回 null。</returns>
    internal static SqlQueryPagerSnapshot Create(IPager pager) =>
        pager == null ? null : new SqlQueryPagerSnapshot(pager);

    /// <summary>
    /// 创建独立的分页参数对象。
    /// </summary>
    /// <returns>分页参数副本。</returns>
    internal IPager CreatePager() => new Pager(_page, _pageSize, _totalCount, _order, _isTotalCountKnown);
}
