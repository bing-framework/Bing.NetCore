using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders;
using System.Collections;
using System.Collections.ObjectModel;
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

    /// <summary>只接收框架已冻结的参数；派生计划共享不变项，公开读取和执行绑定仍独立复制。</summary>
    private SqlBuilderExecutionSnapshot(SqlBuilderExecutionSnapshot source, string sql,
        ReadOnlyCollection<SqlParam> frozenParameters, SqlQuerySelectShape selectShape = null,
        SqlQueryPagination pagination = null, string debugSql = null, IParamLiteralsResolver paramLiteralsResolver = null)
    {
        Sql = sql;
        HasDebugSql = debugSql != null;
        DebugSql = debugSql ?? sql;
        _parameters = frozenParameters;
        _paramLiteralsResolver = paramLiteralsResolver ?? source._paramLiteralsResolver;
        SelectShape = selectShape;
        _pagination = pagination;
    }

    /// <summary>从同一次渲染冻结的 Select 子句边界；原生文本快照不具有该结构。</summary>
    internal SqlQuerySelectShape SelectShape { get; }

    /// <summary>模板重命名时包含尚未绑定的分页槽位。</summary>
    internal SqlQueryPagination Pagination => _pagination;

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

    /// <summary>仅供内建不可变片段共享；对外读取仍必须克隆参数和值容器。</summary>
    internal ReadOnlyCollection<SqlParam> FrozenParameters => _parameters;

    /// <summary>
    /// 创建供内部执行计划使用的参数副本。
    /// </summary>
    /// <remarks>该入口避免先创建公开集合再进行第二次快照。</remarks>
    internal object CreateParameterSnapshot() =>
        _parameters.Select(SqlParameterSnapshot.CloneSqlParameter).ToArray();

    /// <summary>使用相同的冻结参数生成新的 SQL 执行快照。</summary>
    internal SqlBuilderExecutionSnapshot WithSql(string sql) =>
        new(this, sql, _parameters);

    /// <summary>复用已经渲染的结构，参数仍按每次请求独立冻结。</summary>
    internal SqlBuilderExecutionSnapshot WithParameters(IEnumerable<SqlParam> parameters) =>
        new(Sql, parameters, null, _paramLiteralsResolver, SelectShape, _pagination);

    /// <summary>参数作用域改变 SQL 长度时，同步替换结构位置与分页绑定描述。</summary>
    internal SqlBuilderExecutionSnapshot WithScope(string sql, List<SqlParam> ownedParameters,
        IReadOnlyDictionary<string, string> names, int[] boundaries) =>
        new(this, sql, ownedParameters.AsReadOnly(), SelectShape?.WithScope(boundaries, names),
            _pagination?.WithScope(names));

    /// <summary>从冻结参数中仅移除外层分页参数，避免重跑参数转换。</summary>
    internal SqlBuilderExecutionSnapshot WithoutPage(string sql, string offsetParameter, string limitParameter)
    {
        if (string.IsNullOrEmpty(offsetParameter) && string.IsNullOrEmpty(limitParameter))
            return new SqlBuilderExecutionSnapshot(this, sql, _parameters);
        var parameters = Array.AsReadOnly(_parameters
            .Where(parameter => parameter.Name != offsetParameter && parameter.Name != limitParameter).ToArray());
        return new SqlBuilderExecutionSnapshot(this, sql, parameters);
    }

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
        // 自定义方言也可能返回可变容器：仅新转换的分页值需要再次冻结。
        var offset = SqlParameterSnapshot.SnapshotValue(_pagination.Dialect.GetParamValue(pager.GetSkipCount()));
        var limit = SqlParameterSnapshot.SnapshotValue(_pagination.Dialect.GetParamValue(pager.PageSize));
        if (!SelectShape.HasPage)
        {
            sql += " " + Environment.NewLine +
                _pagination.Renderer.Render(_pagination.OffsetParameter, _pagination.LimitParameter);
        }
        var appendedCount = SelectShape.HasPage ? 0 : hasOffset ? 1 : 2;
        var parameters = new SqlParam[_parameters.Count + appendedCount];
        for (var index = 0; index < _parameters.Count; index++)
        {
            var parameter = _parameters[index];
            parameters[index] = parameter.Name == SelectShape.OffsetParameter
                ? CreatePageParameter(parameter.Name, offset)
                : parameter.Name == SelectShape.LimitParameter
                    ? CreatePageParameter(parameter.Name, limit)
                    : parameter;
        }
        // Skip-only 的 offset 原位替换，保留后续嵌套查询参数的顺序。
        if (!SelectShape.HasPage)
        {
            if (!hasOffset)
                parameters[_parameters.Count] = CreatePageParameter(_pagination.OffsetParameter, offset);
            parameters[parameters.Length - 1] = CreatePageParameter(_pagination.LimitParameter, limit);
        }
        // 作用域模板的既有参数名可以不带前缀；保持其绑定身份，SQL 占位符由分页描述负责。
        var offsetName = hasOffset ? SelectShape.OffsetParameter : _pagination.OffsetParameter;
        var limitName = SelectShape.HasPage ? SelectShape.LimitParameter : _pagination.LimitParameter;
        var shape = !SelectShape.HasPage || !ReferenceEquals(order, SelectShape.Order)
            ? SelectShape.WithPage(sql.Length, offsetName, limitName, order, orderEnd)
            : SelectShape;
        return new SqlBuilderExecutionSnapshot(this, sql, Array.AsReadOnly(parameters), shape, _pagination);
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

    private readonly ReadOnlyCollection<SqlParam> _parameters;

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
        return new SqlBuilderExecutionSnapshot(this, Sql, _parameters, SelectShape, _pagination, debugSql, resolver);
    }
}
