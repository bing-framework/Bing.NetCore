using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>查询片段在组合树中的用途。</summary>
public enum SqlQueryFragmentKind
{
    /// <summary>未指定具体用途的 SQL 片段。</summary>
    Raw,
    /// <summary>可作为查询主体的 Select 片段。</summary>
    Select,
    /// <summary>可作为 CTE 定义的片段。</summary>
    Cte,
    /// <summary>可作为 Union 分支的片段。</summary>
    Union,
    /// <summary>可作为谓词或 Exists/In 子查询的片段。</summary>
    Predicate
}

/// <summary>查询片段是否引用外层查询列。</summary>
public enum SqlQueryCorrelationKind
{
    /// <summary>片段可以独立执行。</summary>
    Independent,
    /// <summary>片段依赖外层查询作用域。</summary>
    Correlated
}

/// <summary>可冻结的 SQL 查询片段契约。</summary>
public interface ISqlQueryFragment
{
    /// <summary>生成不受后续 Builder 修改影响的片段快照。</summary>
    SqlQueryFragmentSnapshot Freeze();
}

/// <summary>冻结后的 SQL 查询片段快照。</summary>
public sealed class SqlQueryFragmentSnapshot
{
    private readonly ReadOnlyCollection<SqlParam> _parameters;
    private readonly IReadOnlyDictionary<string, SqlParameterKey> _parameterKeys;

    internal SqlQueryFragmentSnapshot(string sql, IEnumerable<SqlParam> parameters,
        SqlQueryFragmentKind kind, SqlQueryCorrelationKind correlation,
        IReadOnlyDictionary<string, SqlParameterKey> parameterKeys = null, SqlQueryIr queryIr = null)
    {
        Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        _parameters = queryIr?.Snapshot.FrozenParameters ?? new ReadOnlyCollection<SqlParam>((parameters ?? Array.Empty<SqlParam>())
            .Where(parameter => parameter != null)
            .Select(parameter => SqlQueryFragment.Clone(parameter)).ToList());
        Kind = kind;
        Correlation = correlation;
        _parameterKeys = parameterKeys;
        QueryIr = queryIr;
    }

    /// <summary>内建 Builder 冻结的结构；外部文本片段不推断结构。</summary>
    internal SqlQueryIr QueryIr { get; }

    /// <summary>获取冻结 SQL。</summary>
    public string Sql { get; }

    /// <summary>获取冻结参数。</summary>
    public IReadOnlyList<SqlParam> Parameters => SqlQueryFragment.CloneParameters(_parameters);

    /// <summary>获取参数槽位。</summary>
    public IReadOnlyList<SqlParameterKey> Slots =>
        _parameters.Select(parameter => SqlQueryFragment.GetSlot(parameter.Name, _parameterKeys)).ToArray();

    /// <summary>获取片段用途。</summary>
    public SqlQueryFragmentKind Kind { get; }

    /// <summary>获取片段相关性。</summary>
    public SqlQueryCorrelationKind Correlation { get; }

    /// <summary>将快照渲染到新的参数作用域。</summary>
    public SqlQueryFragment Render(SqlQueryScope scope = null) =>
        new SqlQueryFragment(Sql, _parameters, Kind, Correlation, _parameterKeys, QueryIr).Render(scope);
}

/// <summary>以名称绑定冻结查询片段的 CTE 定义，可在多个查询之间共享。</summary>
public sealed class SqlCteDefinition
{
    private readonly SqlQueryFragment _fragment;

    /// <summary>创建 CTE 定义，并立即捕获片段的 SQL 和参数。</summary>
    /// <remarks>自定义片段仅冻结一次；冻结失败会在创建定义时抛出。</remarks>
    public SqlCteDefinition(string name, ISqlQueryFragment fragment)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("CTE 名称不能为空。", nameof(name));
        Name = name.Trim();
        if (fragment == null)
            throw new ArgumentNullException(nameof(fragment));
        // 内建片段已封闭并隔离参数，可直接持有；外部实现先转换为独立快照。
        _fragment = fragment as SqlQueryFragment ?? fragment.Freeze().Render();
    }

    /// <summary>获取 CTE 名称。</summary>
    public string Name { get; }

    /// <summary>获取创建定义时捕获的不可变 CTE 片段。</summary>
    public ISqlQueryFragment Fragment => _fragment;

    /// <summary>将 CTE 渲染到指定作用域。</summary>
    public SqlQueryFragment Render(SqlQueryScope scope = null)
    {
        var fragment = _fragment.Render(scope);
        return fragment.CopyAs(Name + " AS (" + fragment.Sql + ")", SqlQueryFragmentKind.Cte);
    }
}

/// <summary>可冻结、可组合的查询模板契约。</summary>
public interface ISqlQueryTemplate
{
    /// <summary>创建模板的独立查询片段。</summary>
    ISqlQueryFragment CreateFragment();

    /// <summary>将模板包装为指定名称的 CTE 定义。</summary>
    SqlCteDefinition AsCte(string name);
}

/// <summary>
/// 可复用查询片段的参数作用域。
/// </summary>
/// <remarks>
/// 作用域为参数和表别名生成稳定前缀，并持有已冻结的 CTE 定义，不保存 Builder 或执行资源，
/// 因而可以在多个 Query 之间安全共享。
/// </remarks>
public sealed class SqlQueryScope
{
    private readonly string _prefix;
    private readonly ReadOnlyCollection<SqlCteDefinition> _ctes;

    /// <summary>创建根作用域。</summary>
    public SqlQueryScope(string name = "q")
    {
        _prefix = Normalize(name, "q");
        _ctes = new ReadOnlyCollection<SqlCteDefinition>(Array.Empty<SqlCteDefinition>());
    }

    private SqlQueryScope(string prefix, bool normalized, IEnumerable<SqlCteDefinition> ctes = null)
    {
        _prefix = normalized ? prefix : Normalize(prefix, "q");
        _ctes = new ReadOnlyCollection<SqlCteDefinition>((ctes ?? Array.Empty<SqlCteDefinition>()).ToList());
    }

    /// <summary>获取当前作用域的稳定名称。</summary>
    public string Name => _prefix;

    /// <summary>创建不共享参数名称的子作用域。</summary>
    /// <param name="name">子作用域名称。</param>
    /// <returns>新的查询作用域。</returns>
    public SqlQueryScope CreateChild(string name) =>
        new SqlQueryScope(_prefix + "_" + Normalize(name, "child"), true, _ctes);

    /// <summary>获取当前作用域已注册的 CTE 定义。</summary>
    public IReadOnlyList<SqlCteDefinition> Ctes => _ctes;

    /// <summary>返回包含新 CTE 定义的独立作用域。</summary>
    public SqlQueryScope Add(SqlCteDefinition cte)
    {
        if (cte == null)
            throw new ArgumentNullException(nameof(cte));
        if (_ctes.Any(item => string.Equals(item.Name, cte.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("查询作用域中已存在同名 CTE：" + cte.Name);
        return new SqlQueryScope(_prefix, true, _ctes.Concat(new[] { cte }));
    }

    /// <summary>为逻辑参数生成作用域内名称。</summary>
    /// <param name="name">原始参数名称。</param>
    /// <returns>带作用域前缀的参数名称。</returns>
    public string ParameterName(string name) =>
        _prefix + "_" + Normalize(name, "p");

    /// <summary>为来源实体生成作用域内别名。</summary>
    /// <param name="name">来源名称。</param>
    /// <param name="index">同名来源的序号。</param>
    /// <returns>稳定的表别名。</returns>
    public string Alias(string name, int index = 0) =>
        _prefix + "_" + Normalize(name, "t") + (index > 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);

    private static string Normalize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var chars = value.Trim().Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}

/// <summary>
/// 可独立组合的 SQL 文本与参数片段。
/// </summary>
public sealed class SqlQueryFragment : ISqlQueryFragment
{
    private readonly ReadOnlyCollection<SqlParam> _parameters;

    // 只有显式使用逻辑键的片段需要额外的键集合包装。
    private sealed class KeyedParameterCollection : ReadOnlyCollection<SqlParam>
    {
        internal KeyedParameterCollection(IList<SqlParam> parameters, IReadOnlyDictionary<string, SqlParameterKey> keys)
            : base(parameters) => Keys = keys;

        internal IReadOnlyDictionary<string, SqlParameterKey> Keys { get; }
    }

    private IReadOnlyDictionary<string, SqlParameterKey> ParameterKeys =>
        (_parameters as KeyedParameterCollection)?.Keys;

    private static ReadOnlyCollection<SqlParam> WrapParameters(List<SqlParam> parameters,
        IReadOnlyDictionary<string, SqlParameterKey> keys) => keys == null
        ? new ReadOnlyCollection<SqlParam>(parameters)
        : new KeyedParameterCollection(parameters, keys);

    private static ReadOnlyCollection<SqlParam> ShareParameters(SqlQueryIr ir,
        IReadOnlyDictionary<string, SqlParameterKey> keys) => keys == null
        ? ir.Snapshot.FrozenParameters
        : new KeyedParameterCollection(ir.Snapshot.FrozenParameters, keys);

    /// <summary>创建一个 SQL 片段。</summary>
    /// <param name="sql">片段 SQL 文本。</param>
    /// <param name="parameters">片段参数。</param>
    /// <param name="kind">片段用途。</param>
    /// <param name="correlation">片段相关性。</param>
    public SqlQueryFragment(string sql, IEnumerable<SqlParam> parameters = null,
        SqlQueryFragmentKind kind = SqlQueryFragmentKind.Raw,
        SqlQueryCorrelationKind correlation = SqlQueryCorrelationKind.Independent)
        : this(sql, parameters, kind, correlation, null)
    {
    }

    internal SqlQueryFragment(string sql, IEnumerable<SqlParam> parameters, SqlQueryFragmentKind kind,
        SqlQueryCorrelationKind correlation, IReadOnlyDictionary<string, SqlParameterKey> parameterKeys,
        SqlQueryIr queryIr = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 片段不能为空。", nameof(sql));
        Sql = sql;
        _parameters = queryIr != null ? ShareParameters(queryIr, parameterKeys) : WrapParameters((parameters ?? Array.Empty<SqlParam>())
            .Where(parameter => parameter != null)
            .Select(parameter => Clone(parameter)).ToList(), parameterKeys);
        Kind = kind;
        Correlation = correlation;
        QueryIr = queryIr;
    }

    private SqlQueryFragment(string sql, SqlQueryFragmentKind kind,
        SqlQueryCorrelationKind correlation, List<SqlParam> ownedParameters,
        IReadOnlyDictionary<string, SqlParameterKey> parameterKeys, SqlQueryIr queryIr)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 片段不能为空。", nameof(sql));
        Sql = sql;
        _parameters = queryIr != null ? ShareParameters(queryIr, parameterKeys) :
            WrapParameters(ownedParameters ?? throw new ArgumentNullException(nameof(ownedParameters)), parameterKeys);
        Kind = kind;
        Correlation = correlation;
        QueryIr = queryIr;
    }

    /// <summary>接收仅由当前调用创建且已完成深拷贝的参数，调用后不得再修改该列表或其元素。</summary>
    internal static SqlQueryFragment FromOwnedParameters(string sql, List<SqlParam> parameters,
        SqlQueryFragmentKind kind, SqlQueryCorrelationKind correlation,
        IReadOnlyDictionary<string, SqlParameterKey> parameterKeys = null, SqlQueryIr queryIr = null) =>
        new(sql, kind, correlation, parameters, parameterKeys, queryIr);

    internal SqlQueryFragment Copy() => new(Sql, _parameters, Kind, Correlation, ParameterKeys, QueryIr);

    /// <summary>与片段 SQL、参数一同冻结的查询结构，不引用 Builder。</summary>
    internal SqlQueryIr QueryIr { get; }

    internal SqlQueryFragment CopyAs(string sql, SqlQueryFragmentKind kind) =>
        new(sql, _parameters, kind, Correlation, ParameterKeys);

    internal bool HasParameterKeys => ParameterKeys != null && ParameterKeys.Count != 0;

    internal bool TryGetParameterKey(string name, out SqlParameterKey key)
    {
        if (ParameterKeys is { } keys && keys.TryGetValue(NormalizeName(name), out key))
            return true;
        key = default;
        return false;
    }

    internal static SqlParameterKey GetSlot(string name, IReadOnlyDictionary<string, SqlParameterKey> keys) =>
        keys != null && keys.TryGetValue(NormalizeName(name), out var key) ? key : new SqlParameterKey(name);

    /// <summary>获取片段 SQL。</summary>
    public string Sql { get; }

    /// <summary>获取片段用途。</summary>
    public SqlQueryFragmentKind Kind { get; }

    /// <summary>获取片段相关性。</summary>
    public SqlQueryCorrelationKind Correlation { get; }

    /// <summary>获取参数快照。</summary>
    public IReadOnlyList<SqlParam> Parameters => CloneParameters(_parameters);

    /// <summary>供模板构建槽位时只读访问内部快照；修改前必须先复制参数。</summary>
    internal IReadOnlyList<SqlParam> TemplateParameters => _parameters;

    /// <summary>获取参数槽键集合。</summary>
    public IReadOnlyList<SqlParameterKey> Slots =>
        _parameters.Select(parameter => GetSlot(parameter.Name, ParameterKeys)).ToArray();

    /// <summary>为片段参数指定逻辑身份，返回新的片段。</summary>
    /// <param name="name">当前片段中的参数名。</param>
    /// <param name="key">用于显式共享或区分参数的逻辑键。</param>
    /// <returns>带逻辑参数身份的独立片段。</returns>
    /// <remarks>逻辑键的作用域与名称按大小写敏感的值语义比较，不会改变原片段。</remarks>
    public SqlQueryFragment WithParameterKey(string name, SqlParameterKey key)
    {
        var normalized = NormalizeName(name);
        if (normalized.Length == 0 || !_parameters.Any(parameter =>
                string.Equals(NormalizeName(parameter.Name), normalized, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("片段中不存在指定参数。", nameof(name));
        if (string.IsNullOrWhiteSpace(key.Name))
            throw new ArgumentException("逻辑参数键的名称不能为空。", nameof(key));
        var keys = new Dictionary<string, SqlParameterKey>(StringComparer.OrdinalIgnoreCase);
        if (ParameterKeys is { } existingKeys)
            foreach (var pair in existingKeys)
                keys.Add(pair.Key, pair.Value);
        keys[normalized] = key;
        return new SqlQueryFragment(Sql, _parameters, Kind, Correlation, keys, QueryIr);
    }

    /// <inheritdoc />
    public SqlQueryFragmentSnapshot Freeze() =>
        new(Sql, _parameters, Kind, Correlation, ParameterKeys, QueryIr);

    /// <summary>将片段渲染到指定作用域。</summary>
    /// <param name="scope">参数作用域；为空时保持原始名称。</param>
    /// <returns>带作用域参数名称的独立片段。</returns>
    public SqlQueryFragment Render(SqlQueryScope scope = null)
    {
        if (scope == null || _parameters.Count == 0 && QueryIr == null)
            return Copy();
        var parameters = new List<SqlParam>(_parameters.Count);
        var parameterNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SqlParameterKey> scopedKeys = null;

        string Rename(string name)
        {
            var normalized = NormalizeName(name);
            var sourceName = "@" + normalized;
            string renamed;
            if (normalized.Length > 0 && parameterNames.TryGetValue(sourceName, out var existing))
            {
                // 相同逻辑参数在不同前缀下共用同一个作用域名称。
                renamed = existing.Substring(1);
            }
            else
            {
                var baseName = scope.ParameterName(name);
                renamed = baseName;
                var suffix = 0;
                while (!usedNames.Add(renamed))
                    renamed = baseName + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (normalized.Length > 0)
                {
                    parameterNames[sourceName] = "@" + renamed;
                    parameterNames[":" + normalized] = ":" + renamed;
                    parameterNames["?" + normalized] = "?" + renamed;
                }
            }
            return renamed;
        }

        foreach (var parameter in _parameters)
        {
            var renamed = Rename(parameter.Name);
            parameters.Add(Clone(parameter, renamed));
            if (TryGetParameterKey(parameter.Name, out var key))
            {
                scopedKeys ??= new Dictionary<string, SqlParameterKey>(StringComparer.OrdinalIgnoreCase);
                scopedKeys[renamed] = key;
            }
        }
        // 无分页模板也要重命名预留槽位，后续派生才能继续使用同一作用域。
        if (QueryIr?.Snapshot.Pagination is { } pagination)
        {
            Rename(pagination.OffsetParameter);
            Rename(pagination.LimitParameter);
        }
        var boundaries = QueryIr?.Snapshot.SelectShape?.GetBoundaries();
        var sql = SqlBuilderBase.ReplaceParameterTokens(Sql, parameterNames, ignoreCase: true, boundaries: boundaries);
        var ir = QueryIr?.WithSnapshot(QueryIr.Snapshot.WithScope(sql, parameters, parameterNames, boundaries));
        return FromOwnedParameters(sql, parameters, Kind, Correlation, scopedKeys, ir);
    }

    /// <summary>按顺序组合多个片段，并为每个片段分配子作用域。</summary>
    /// <param name="separator">片段之间的 SQL 分隔符。</param>
    /// <param name="fragments">待组合片段。</param>
    /// <returns>组合后的不可变片段。</returns>
    public static SqlQueryFragment Compose(string separator, params SqlQueryFragment[] fragments)
    {
        if (fragments == null || fragments.Length == 0)
            throw new ArgumentException("至少需要一个 SQL 片段。", nameof(fragments));
        var scope = new SqlQueryScope("fragment");
        var rendered = fragments.Where(fragment => fragment != null)
            .Select((fragment, index) => fragment.Render(scope.CreateChild(index.ToString(System.Globalization.CultureInfo.InvariantCulture))))
            .ToArray();
        if (rendered.Length == 0)
            throw new ArgumentException("至少需要一个非空 SQL 片段。", nameof(fragments));
        Dictionary<string, SqlParameterKey> keys = null;
        foreach (var fragment in rendered)
        {
            if (!fragment.HasParameterKeys)
                continue;
            foreach (var parameter in fragment.TemplateParameters)
                if (fragment.TryGetParameterKey(parameter.Name, out var key))
                {
                    keys ??= new Dictionary<string, SqlParameterKey>(StringComparer.OrdinalIgnoreCase);
                    keys[NormalizeName(parameter.Name)] = key;
                }
        }
        return new SqlQueryFragment(string.Join(separator ?? string.Empty, rendered.Select(fragment => fragment.Sql)),
            rendered.SelectMany(fragment => fragment.Parameters), SqlQueryFragmentKind.Raw,
            rendered.Any(fragment => fragment.Correlation == SqlQueryCorrelationKind.Correlated)
                ? SqlQueryCorrelationKind.Correlated : SqlQueryCorrelationKind.Independent, keys);
    }

    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }

    internal static SqlParam Clone(SqlParam source, string name = null)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return SqlParameterSnapshot.CloneSqlParameter(source, name ?? source.Name);
    }

    /// <summary>
    /// 创建参数集合的深度副本，避免调用方通过返回的 <see cref="SqlParam"/> 修改片段内部状态。
    /// </summary>
    /// <param name="parameters">源参数集合。</param>
    /// <returns>与源集合及参数值容器均隔离的参数列表。</returns>
    internal static IReadOnlyList<SqlParam> CloneParameters(IEnumerable<SqlParam> parameters)
    {
        if (parameters == null)
            return Array.Empty<SqlParam>();
        return parameters.Where(parameter => parameter != null).Select(parameter => Clone(parameter)).ToArray();
    }
}

/// <summary>
/// 已冻结的可复用 SQL 查询模板。
/// </summary>
/// <remarks>
/// 模板保存 SQL、冻结参数及内建 Builder 的查询结构，不持有 Builder、连接或事务；同一模板可以渲染到多个 Query。
/// 需要继续编辑原结构时先调用 Builder.Clone；冻结后使用 From、With 或 Union 组合。
/// </remarks>
public sealed class SqlQueryTemplate
    : ISqlQueryTemplate, ISqlQueryFragment
{
    private readonly SqlQueryFragment _fragment;

    /// <summary>创建查询模板。</summary>
    /// <param name="sql">模板 SQL。</param>
    /// <param name="parameters">模板参数。</param>
    public SqlQueryTemplate(string sql, IEnumerable<SqlParam> parameters = null)
    {
        _fragment = new SqlQueryFragment(sql, parameters);
    }

    // 内建片段已封闭并隔离参数；接收现有快照，避免冻结时再次复制整组参数。
    internal SqlQueryTemplate(SqlQueryFragment fragment) => _fragment = fragment;

    internal SqlQueryTemplate(string sql, IEnumerable<SqlParam> parameters,
        SqlQueryFragmentKind kind, SqlQueryCorrelationKind correlation)
    {
        _fragment = new SqlQueryFragment(sql, parameters, kind, correlation);
    }

    /// <summary>获取模板 SQL。</summary>
    public string Sql => _fragment.Sql;

    /// <summary>获取模板参数。</summary>
    public IReadOnlyList<SqlParam> Parameters => _fragment.Parameters;

    /// <summary>获取模板参数槽。</summary>
    public IReadOnlyList<SqlParameterKey> Slots => _fragment.Slots;

    /// <summary>按作用域渲染模板。</summary>
    /// <param name="scope">目标作用域。</param>
    /// <returns>独立的 SQL 片段。</returns>
    public SqlQueryFragment Render(SqlQueryScope scope = null) => _fragment.Render(scope);

    /// <inheritdoc />
    public SqlQueryFragmentSnapshot Freeze() => _fragment.Freeze();

    /// <summary>从多个片段创建模板。</summary>
    /// <param name="separator">片段分隔符。</param>
    /// <param name="fragments">待组合片段。</param>
    /// <returns>组合后的模板。</returns>
    public static SqlQueryTemplate Compose(string separator, params SqlQueryFragment[] fragments)
    {
        var fragment = SqlQueryFragment.Compose(separator, fragments);
        return new SqlQueryTemplate(fragment);
    }

    /// <summary>创建模板的独立查询片段。</summary>
    public SqlQueryFragment CreateFragment() => _fragment.Copy();

    ISqlQueryFragment ISqlQueryTemplate.CreateFragment() => CreateFragment();

    /// <inheritdoc />
    public SqlCteDefinition AsCte(string name) => new(name, CreateFragment());
}

/// <summary>SQL 模板参数槽键。</summary>
public readonly record struct SqlParameterKey
{
    /// <summary>创建空参数槽键。</summary>
    public SqlParameterKey()
    {
        Scope = string.Empty;
        Name = string.Empty;
    }

    /// <summary>创建参数槽键。</summary>
    /// <param name="name">参数名称。</param>
    public SqlParameterKey(string name)
        : this(string.Empty, name)
    {
    }

    /// <summary>创建带逻辑作用域的参数槽键。</summary>
    /// <param name="scope">逻辑参数作用域。</param>
    /// <param name="name">参数名称。</param>
    public SqlParameterKey(string scope, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("参数名称不能为空。", nameof(name));
        Scope = scope?.Trim() ?? string.Empty;
        Name = name.Trim().TrimStart('@', ':', '?');
    }

    /// <summary>逻辑参数作用域。</summary>
    public string Scope { get; }

    /// <summary>参数名称。</summary>
    public string Name { get; }
}

/// <summary>将可变 Builder 冻结为可复用查询模板的扩展。</summary>
public static class SqlQueryTemplateExtensions
{
    /// <summary>
    /// 使用 Builder 独立副本生成模板，调用后原 Builder 状态不被渲染过程改变。
    /// </summary>
    /// <remarks>SQL、内建查询结构与动态过滤值在调用时捕获；模板不保留草稿或过滤器，需要重新取值时重新冻结。</remarks>
    /// <param name="builder">待冻结的 Builder。</param>
    /// <param name="scope">可选的模板作用域。</param>
    /// <returns>冻结后的查询模板。</returns>
    public static SqlQueryTemplate ToTemplate(this ISqlBuilder builder, SqlQueryScope scope = null)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var snapshot = builder.Clone() ?? throw new InvalidOperationException("Builder 克隆不能为空。");
        var render = (snapshot as SqlBuilderBase)?.CreateRenderSnapshot();
        var sql = render?.Sql ?? snapshot.ToSql();
        var parameterSource = render?.Builder ?? snapshot;
        var keys = render?.GetFragmentParameterKeys() ??
                   (parameterSource as SqlBuilderBase)?.GetFragmentParameterKeys(sql);
        var ir = render?.SelectShape == null ? null : SqlQueryIr.Create(
            SqlBuilderRuntimeBridge.CreateExecutionSnapshot(render), CommandType.Text, parameterSource.Pager);
        var parameters = ir == null ? ((ISqlParameter)parameterSource).GetSqlParams().Values : null;
        var fragment = new SqlQueryFragment(sql, parameters, SqlQueryFragmentKind.Raw,
            SqlQueryCorrelationKind.Independent, keys, ir);
        if (scope != null)
            fragment = fragment.Render(scope);
        return new SqlQueryTemplate(fragment);
    }
}
