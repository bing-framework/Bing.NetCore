using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders;
using System.Collections;
using Bing.Properties;

namespace Bing.Data.Sql;

/// <summary>
/// Builder 单次执行使用的冻结渲染快照。
/// </summary>
public sealed class SqlBuilderExecutionSnapshot
{
    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderExecutionSnapshot"/> 类型的实例。
    /// </summary>
    /// <param name="sql">冻结后的 SQL 文本。</param>
    /// <param name="parameters">与 SQL 使用同一参数状态的参数快照。</param>
    /// <param name="debugSql">与 SQL 使用同一参数状态生成的调试 SQL。</param>
    public SqlBuilderExecutionSnapshot(string sql, IEnumerable<SqlParam> parameters, string debugSql = null)
        : this(sql, parameters, debugSql, null)
    {
    }

    /// <summary>
    /// 初始化一个包含 Provider 字面值解析器的执行快照。
    /// </summary>
    /// <param name="sql">冻结后的 SQL 文本。</param>
    /// <param name="parameters">与 SQL 使用同一参数状态的参数快照。</param>
    /// <param name="debugSql">与 SQL 使用同一参数状态生成的调试 SQL。</param>
    /// <param name="paramLiteralsResolver">当前 Provider 的参数字面值解析器。</param>
    /// <param name="selectShape">与 SQL 同次渲染产生的 Select 结构边界。</param>
    /// <param name="pagination">与冻结结构对应的分页绑定描述。</param>
    internal SqlBuilderExecutionSnapshot(string sql, IEnumerable<SqlParam> parameters, string debugSql,
        IParamLiteralsResolver paramLiteralsResolver, SqlQuerySelectShape selectShape = null,
        SqlQueryPagination pagination = null)
    {
        Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        _parameters = Array.AsReadOnly(parameters.Where(parameter => parameter != null)
            .Select(SqlParameterSnapshot.CloneSqlParameter)
            .ToArray());
        HasDebugSql = debugSql != null;
        DebugSql = debugSql ?? sql;
        _paramLiteralsResolver = paramLiteralsResolver;
        SelectShape = selectShape;
        _pagination = pagination;
    }

    /// <summary>从同一次渲染冻结的 Select 子句边界；原生文本快照不具有该结构。</summary>
    internal SqlQuerySelectShape SelectShape { get; }

    /// <summary>
    /// 获取冻结后的 SQL 文本。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 获取与 SQL 使用同一参数状态生成的调试 SQL。
    /// </summary>
    public string DebugSql { get; }

    /// <summary>
    /// 获取是否在创建快照时显式生成了调试 SQL。
    /// </summary>
    internal bool HasDebugSql { get; }

    /// <summary>
    /// 获取与 SQL 使用同一参数状态的不可变参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters =>
        _parameters.Select(SqlParameterSnapshot.CloneSqlParameter).ToArray();

    /// <summary>
    /// 创建供内部执行计划使用的参数副本。
    /// </summary>
    /// <remarks>该入口避免先创建公开集合再进行第二次快照。</remarks>
    internal object CreateParameterSnapshot() =>
        _parameters.Select(SqlParameterSnapshot.CloneSqlParameter).ToArray();

    /// <summary>使用相同的冻结参数生成新的 SQL 执行快照。</summary>
    internal SqlBuilderExecutionSnapshot WithSql(string sql) =>
        new(sql, _parameters, null, _paramLiteralsResolver);

    /// <summary>复用已经渲染的结构，参数仍按每次请求独立冻结。</summary>
    internal SqlBuilderExecutionSnapshot WithParameters(IEnumerable<SqlParam> parameters) =>
        new(Sql, parameters, null, _paramLiteralsResolver, SelectShape, _pagination);

    /// <summary>从冻结参数中仅移除外层分页参数，避免重跑参数转换。</summary>
    internal SqlBuilderExecutionSnapshot WithoutPage(string sql, string offsetParameter, string limitParameter) =>
        new(sql, _parameters.Where(parameter => parameter.Name != offsetParameter && parameter.Name != limitParameter),
            null, _paramLiteralsResolver);

    /// <summary>参数来自框架容器，且排序可复用或已冻结时，可直接派生分页。</summary>
    internal bool CanDerivePage(string order) => _pagination != null &&
        (SelectShape.CanReusePage(order) || SelectShape.Order != null);

    /// <summary>排序与分页均已冻结时，无需再保留用于其他分页排序的 Builder。</summary>
    internal bool CanDeriveAnyPage => _pagination != null && SelectShape.Order != null;

    /// <summary>只转换新的偏移量和页大小，其余参数保持冻结后的值和元数据。</summary>
    internal SqlBuilderExecutionSnapshot WithPage(IPager pager)
    {
        var order = SelectShape.CanReusePage(pager.Order) ? SelectShape.Order :
            SelectShape.Order.Append(pager.Order, _pagination.OrderDialect);
        if (!SelectShape.HasOrder && order?.HasOrder != true)
            throw new ArgumentException(LibraryResource.OrderIsEmptyForPage);
        var sql = Sql;
        var orderEnd = SelectShape.OrderEnd;
        if (!ReferenceEquals(order, SelectShape.Order))
        {
            var ordered = Sql.Substring(0, SelectShape.SourceEnd) + " " + Environment.NewLine + order.ToSql();
            orderEnd = ordered.Length;
            sql = SelectShape.HasPage ? ordered + Sql.Substring(SelectShape.OrderEnd) : ordered;
        }
        var hasOffset = !string.IsNullOrWhiteSpace(SelectShape.OffsetParameter);
        if (!SelectShape.HasPage)
            _pagination.ValidateAppend(_parameters.Count, hasOffset ? 1 : 2);
        var offset = _pagination.Dialect.GetParamValue(pager.GetSkipCount());
        var limit = _pagination.Dialect.GetParamValue(pager.PageSize);
        if (!SelectShape.HasPage)
        {
            sql += " " + Environment.NewLine +
                _pagination.Renderer.Render(_pagination.OffsetParameter, _pagination.LimitParameter);
            // Skip-only 已有 offset：原位替换，避免重复槽位，并保留其后的子查询参数顺序。
            var appended = hasOffset
                ? _parameters.Select(parameter => parameter.Name == SelectShape.OffsetParameter
                    ? CreatePageParameter(parameter.Name, offset) : parameter)
                    .Append(CreatePageParameter(_pagination.LimitParameter, limit))
                : _parameters.Concat(new[]
                {
                    CreatePageParameter(_pagination.OffsetParameter, offset),
                    CreatePageParameter(_pagination.LimitParameter, limit)
                });
            return new SqlBuilderExecutionSnapshot(sql, appended, null, _paramLiteralsResolver,
                SelectShape.WithPage(sql.Length, _pagination.OffsetParameter, _pagination.LimitParameter, order, orderEnd), _pagination);
        }
        var parameters = _parameters.Select(parameter => parameter.Name == SelectShape.OffsetParameter
            ? CreatePageParameter(parameter.Name, offset)
            : parameter.Name == SelectShape.LimitParameter
                ? CreatePageParameter(parameter.Name, limit)
                : parameter);
        var shape = ReferenceEquals(order, SelectShape.Order) ? SelectShape :
            SelectShape.WithPage(sql.Length, SelectShape.OffsetParameter, SelectShape.LimitParameter, order, orderEnd);
        return new SqlBuilderExecutionSnapshot(sql, parameters, null, _paramLiteralsResolver, shape,
            _pagination);
    }

    /// <summary>与 ParameterManager.Add 的普通分页参数契约一致。</summary>
    private static SqlParam CreatePageParameter(string name, object value) => new(name, value)
    {
        Source = SqlParameterSource.Basic,
        MetadataLevel = SqlParameterMetadataLevel.Weak
    };

    /// <summary>仅复制需要与规划 Builder 隔离的可变参数。</summary>
    internal SqlParam[] CreateMutableParameterSnapshot()
    {
        List<SqlParam> result = null;
        foreach (var parameter in _parameters)
        {
            if (parameter.Value is not Array && parameter.Value is not IDictionary &&
                parameter.OriginalValue is not Array && parameter.OriginalValue is not IDictionary)
                continue;
            (result ??= new List<SqlParam>()).Add(SqlParameterSnapshot.CloneSqlParameter(parameter));
        }
        return result?.ToArray() ?? Array.Empty<SqlParam>();
    }

    private readonly IReadOnlyCollection<SqlParam> _parameters;

    /// <summary>
    /// 获取当前快照使用的参数字面值解析器。
    /// </summary>
    private readonly IParamLiteralsResolver _paramLiteralsResolver;

    private readonly SqlQueryPagination _pagination;

    /// <summary>
    /// 基于冻结 SQL 和参数生成带 Provider 方言的调试 SQL。
    /// </summary>
    /// <returns>冻结快照对应的调试 SQL。</returns>
    internal SqlBuilderExecutionSnapshot WithDebugSql(IParamLiteralsResolver paramLiteralsResolver = null)
    {
        if (HasDebugSql)
            return this;
        var resolver = paramLiteralsResolver ?? _paramLiteralsResolver ?? ParamLiteralsResolver.Instance;
        var debugSql = SqlBuilderBase.CreateDebugSql(Sql, _parameters, resolver);
        return new SqlBuilderExecutionSnapshot(Sql, _parameters, debugSql, resolver, SelectShape, _pagination);
    }
}
