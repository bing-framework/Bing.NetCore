using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 查询形状缓存使用的结构键。
/// </summary>
/// <remarks>
/// 键只描述会改变 SQL 结构或参数布局的输入，不包含请求参数值、连接、事务或读取器。
/// </remarks>
public readonly record struct SqlQueryShapeKey
{
    /// <summary>创建查询形状键。</summary>
    public SqlQueryShapeKey(string queryId, string providerKey, string mappingVersion,
        string routeKey, int optionalMask, string collectionShape)
    {
        QueryId = queryId;
        ProviderKey = providerKey;
        MappingVersion = mappingVersion;
        RouteKey = routeKey;
        OptionalMask = optionalMask;
        CollectionShape = collectionShape;
    }

    /// <summary>获取查询标识。</summary>
    public string QueryId { get; init; }
    /// <summary>获取 Provider 标识。</summary>
    public string ProviderKey { get; init; }
    /// <summary>获取映射版本。</summary>
    public string MappingVersion { get; init; }
    /// <summary>获取路由键。</summary>
    public string RouteKey { get; init; }
    /// <summary>获取可选片段掩码。</summary>
    public int OptionalMask { get; init; }
    /// <summary>获取集合参数形状。</summary>
    public string CollectionShape { get; init; }
}

/// <summary>
/// 查询形状中的一个可选 SQL 片段。
/// </summary>
public sealed class SqlQueryOptionalFragment
{
    private readonly SqlQueryFragment _fragment;

    /// <summary>
    /// 创建可选 SQL 片段。
    /// </summary>
    /// <param name="bit">片段占用的 OptionalMask 位，范围为 0 到 30。</param>
    /// <param name="fragment">片段内容。</param>
    public SqlQueryOptionalFragment(int bit, SqlQueryFragment fragment)
    {
        if (bit < 0 || bit > 30)
            throw new ArgumentOutOfRangeException(nameof(bit), bit, "可选片段位必须位于 0 到 30 之间。");
        _fragment = fragment ?? throw new ArgumentNullException(nameof(fragment));
        Bit = bit;
    }

    /// <summary>
    /// 使用 SQL 文本和参数创建可选 SQL 片段。
    /// </summary>
    /// <param name="bit">片段占用的 OptionalMask 位，范围为 0 到 30。</param>
    /// <param name="sql">片段 SQL 文本。</param>
    /// <param name="parameters">片段参数。</param>
    public SqlQueryOptionalFragment(int bit, string sql, IEnumerable<SqlParam> parameters)
        : this(bit, new SqlQueryFragment(sql, parameters))
    {
    }

    /// <summary>获取片段占用的位。</summary>
    public int Bit { get; }

    /// <summary>获取片段对应的掩码。</summary>
    public int Mask => 1 << Bit;

    /// <summary>获取片段副本。</summary>
    public SqlQueryFragment Fragment => _fragment.Copy();

    /// <summary>获取模板内部只读片段。</summary>
    internal SqlQueryFragment TemplateFragment => _fragment;
}

/// <summary>
/// 包含固定和可选 SQL 片段的不可变查询模板。
/// </summary>
/// <remarks>
/// 模板只保存 SQL 结构和参数槽位。每次 <see cref="Render(int, IReadOnlyDictionary{string, object}, SqlQueryScope)"/>
/// 都会生成新的参数对象，因此同一模板可以安全地在多个请求和多个 Query 之间共享。
/// 每个模板至多保留 256 个已完成的形状，缓存只包含模板默认值，不保存 Render 传入的请求值。
/// 带结构键的重载会使用完整键隔离不同 Provider、映射版本、路由及集合形状。
/// 未指定逻辑键的同名槽按名称共享，显式逻辑键按键共享；共享槽要求绑定配置一致，
/// 默认值不同时，渲染必须提供本次请求值。
/// </remarks>
public sealed class SqlQueryShapeTemplate
{
    private readonly SqlQueryFragment _required;
    private readonly ReadOnlyCollection<SqlQueryOptionalFragment> _optional;
    private readonly (int Mask, SqlQueryFragment Fragment)[] _fragments;
    private readonly string _separator;
    private readonly int _supportedMask;
    private readonly SqlQueryShapeCache<SqlQueryShapeKey, CompiledShape> _shapes = new();
    private readonly Func<SqlQueryShapeKey, CompiledShape> _shapeFactory;

    /// <summary>
    /// 创建只包含必选片段的查询形状模板。
    /// </summary>
    /// <param name="required">必选 SQL 片段。</param>
    public SqlQueryShapeTemplate(SqlQueryFragment required)
        : this(required, Array.Empty<SqlQueryOptionalFragment>(), " ")
    {
    }

    /// <summary>
    /// 创建包含必选和可选片段的查询形状模板。
    /// </summary>
    /// <param name="required">必选 SQL 片段。</param>
    /// <param name="optional">可选 SQL 片段。</param>
    public SqlQueryShapeTemplate(SqlQueryFragment required, IEnumerable<SqlQueryOptionalFragment> optional)
        : this(required, optional, " ")
    {
    }

    /// <summary>
    /// 创建包含必选和可选片段的查询形状模板。
    /// </summary>
    /// <param name="required">必选 SQL 片段。</param>
    /// <param name="optional">可选 SQL 片段。</param>
    /// <param name="separator">必选片段与可选片段之间的分隔符。</param>
    public SqlQueryShapeTemplate(SqlQueryFragment required,
        IEnumerable<SqlQueryOptionalFragment> optional, string separator)
        : this(required, optional, separator, null)
    {
    }

    /// <summary>
    /// 创建固定前缀、可选片段和固定尾部组成的查询形状模板。
    /// </summary>
    /// <param name="required">位于可选片段之前的固定 SQL 片段。</param>
    /// <param name="optional">按位选择并位于固定尾部之前的 SQL 片段。</param>
    /// <param name="separator">相邻片段之间的分隔符。</param>
    /// <param name="suffix">位于可选片段之后的固定 SQL 片段，可为空。</param>
    public SqlQueryShapeTemplate(SqlQueryFragment required,
        IEnumerable<SqlQueryOptionalFragment> optional, string separator, SqlQueryFragment suffix)
    {
        _required = Clone(required ?? throw new ArgumentNullException(nameof(required)));
        _separator = separator ?? string.Empty;
        var items = (optional ?? Array.Empty<SqlQueryOptionalFragment>()).ToArray();
        if (items.Any(item => item == null))
            throw new ArgumentException("可选片段集合不能包含 null。", nameof(optional));
        if (items.GroupBy(item => item.Bit).Any(group => group.Count() > 1))
            throw new ArgumentException("每个可选片段必须使用唯一的 OptionalMask 位。", nameof(optional));
        _optional = new ReadOnlyCollection<SqlQueryOptionalFragment>(items
            .OrderBy(item => item.Bit)
            .Select(item => new SqlQueryOptionalFragment(item.Bit, Clone(item.Fragment)))
            .ToList());
        var fragments = _optional.Select(item => (item.Mask, item.TemplateFragment)).ToList();
        if (suffix != null)
            fragments.Add((0, Clone(suffix)));
        _fragments = fragments.ToArray();
        _supportedMask = _optional.Aggregate(0, (mask, item) => mask | item.Mask);
        _shapeFactory = CompileShape;
    }

    private SqlQueryShapeTemplate(SqlQueryFragment required,
        IEnumerable<(SqlQueryFragment Fragment, int? OptionalBit)> fragments, string separator)
    {
        _required = Clone(required ?? throw new ArgumentNullException(nameof(required)));
        _separator = separator ?? string.Empty;
        var ordered = new List<(int Mask, SqlQueryFragment Fragment)>();
        var optional = new List<SqlQueryOptionalFragment>();
        var mask = 0;
        foreach (var (fragment, bit) in fragments ?? throw new ArgumentNullException(nameof(fragments)))
        {
            if (fragment == null)
                throw new ArgumentException("片段集合不能包含 null。", nameof(fragments));
            if (bit.HasValue)
            {
                var part = new SqlQueryOptionalFragment(bit.Value, Clone(fragment));
                optional.Add(part);
                ordered.Add((part.Mask, part.TemplateFragment));
                mask |= part.Mask;
            }
            else
                ordered.Add((0, Clone(fragment)));
        }
        _optional = new ReadOnlyCollection<SqlQueryOptionalFragment>(optional);
        _fragments = ordered.ToArray();
        _supportedMask = mask;
        _shapeFactory = CompileShape;
    }

    /// <summary>按声明顺序创建包含交错固定片段和可选片段的查询模板。</summary>
    /// <param name="required">固定的首个 SQL 片段。</param>
    /// <param name="fragments">后续片段及其可选条件位；条件位为 null 时片段始终保留。</param>
    /// <param name="separator">相邻已启用片段之间的分隔符。</param>
    /// <returns>可由多个请求复用的查询形状模板。</returns>
    /// <remarks>多个片段可使用同一个条件位，同时启用或禁用；片段和参数在创建时复制。</remarks>
    public static SqlQueryShapeTemplate CreateOrdered(SqlQueryFragment required,
        IEnumerable<(SqlQueryFragment Fragment, int? OptionalBit)> fragments, string separator = " ") =>
        new(required, fragments, separator);

    /// <summary>
    /// 使用必选 SQL 文本和参数创建查询形状模板。
    /// </summary>
    /// <param name="requiredSql">必选 SQL 文本。</param>
    /// <param name="requiredParameters">必选参数。</param>
    /// <param name="optional">可选 SQL 片段。</param>
    /// <param name="separator">必选片段与可选片段之间的分隔符。</param>
    public SqlQueryShapeTemplate(string requiredSql, IEnumerable<SqlParam> requiredParameters,
        IEnumerable<SqlQueryOptionalFragment> optional, string separator)
        : this(new SqlQueryFragment(requiredSql, requiredParameters), optional, separator)
    {
    }

    /// <summary>获取必选片段副本。</summary>
    public SqlQueryFragment Required => Clone(_required);

    /// <summary>获取可选片段副本；有序模板保留声明顺序和重复的条件位。</summary>
    public IReadOnlyList<SqlQueryOptionalFragment> OptionalFragments => _optional.ToArray();

    /// <summary>获取可选片段支持的掩码。</summary>
    public int SupportedMask => _supportedMask;

    /// <summary>获取片段之间的分隔符。</summary>
    public string Separator => _separator;

    /// <summary>获取此模板当前缓存的查询形状数。</summary>
    internal int CachedShapeCount => _shapes.Count;

    /// <summary>渲染不启用可选片段的查询。</summary>
    public SqlQueryFragment Render()
        => Render(0, null, null);

    /// <summary>按可选片段掩码渲染查询。</summary>
    /// <param name="optionalMask">要启用的可选片段掩码。</param>
    public SqlQueryFragment Render(int optionalMask)
        => Render(optionalMask, null, null);

    /// <summary>按可选片段掩码渲染查询并绑定本次请求的参数值。</summary>
    /// <param name="optionalMask">要启用的可选片段掩码。</param>
    /// <param name="values">按参数名提供的本次请求值。</param>
    public SqlQueryFragment Render(int optionalMask, IReadOnlyDictionary<string, object> values)
        => Render(optionalMask, values, null);

    /// <summary>按逻辑参数身份绑定本次请求值。</summary>
    /// <param name="optionalMask">要启用的可选片段掩码。</param>
    /// <param name="values">按逻辑参数键提供的本次请求值。</param>
    /// <param name="unkeyedValues">未指定逻辑键的参数按名称提供的请求值。</param>
    /// <param name="scope">可选的输出参数作用域。</param>
    /// <returns>独立的 SQL 与参数快照。</returns>
    /// <remarks>显式逻辑键由 <paramref name="values"/> 绑定；其他槽由 <paramref name="unkeyedValues"/> 绑定。</remarks>
    public SqlQueryFragment RenderWithKeys(int optionalMask, IReadOnlyDictionary<SqlParameterKey, object> values,
        IReadOnlyDictionary<string, object> unkeyedValues = null, SqlQueryScope scope = null) =>
        RenderCore(new SqlQueryShapeKey(null, null, null, null, optionalMask, null), unkeyedValues, scope, values);

    /// <summary>按结构键选择缓存形状，并按逻辑参数身份绑定本次请求值。</summary>
    /// <param name="shape">查询结构键。</param>
    /// <param name="values">按逻辑参数键提供的本次请求值。</param>
    /// <param name="unkeyedValues">未指定逻辑键的参数按名称提供的请求值。</param>
    /// <param name="scope">可选的输出参数作用域。</param>
    /// <returns>独立的 SQL 与参数快照。</returns>
    public SqlQueryFragment RenderWithKeys(SqlQueryShapeKey shape, IReadOnlyDictionary<SqlParameterKey, object> values,
        IReadOnlyDictionary<string, object> unkeyedValues, SqlQueryScope scope) =>
        RenderCore(shape, unkeyedValues, scope, values);

    /// <summary>
    /// 按可选片段掩码渲染查询，并绑定本次请求的参数值和参数作用域。
    /// </summary>
    /// <param name="optionalMask">要启用的可选片段掩码。</param>
    /// <param name="values">按参数名提供的本次请求值；键名可以包含参数前缀。</param>
    /// <param name="scope">可选的参数作用域。</param>
    public SqlQueryFragment Render(int optionalMask, IReadOnlyDictionary<string, object> values, SqlQueryScope scope) =>
        RenderCore(new SqlQueryShapeKey(null, null, null, null, optionalMask, null), values, scope, null);

    /// <summary>按编译后参数槽位顺序绑定值并渲染查询。</summary>
    /// <param name="optionalMask">要启用的可选片段掩码。</param>
    /// <param name="values">按编译后参数槽位顺序排列的本次请求值；null 是有效参数值。</param>
    /// <returns>独立的 SQL 与参数快照。</returns>
    /// <remarks>值按启用片段的参数声明顺序绑定；重复逻辑参数只占首次声明的槽位。值数量必须与槽位数精确匹配，不会使用模板默认值补位；此顺序不按 SQL 文本中的标记位置推断。</remarks>
    public SqlQueryFragment RenderValues(int optionalMask, ReadOnlySpan<object> values) =>
        RenderValues(new SqlQueryShapeKey(null, null, null, null, optionalMask, null), values);

    /// <summary>
    /// 按查询结构键渲染模板；键中的 Provider、映射和路由只用于隔离缓存，不改变片段选择。
    /// </summary>
    /// <param name="shape">查询结构键。</param>
    /// <param name="values">本次请求的参数值。</param>
    /// <param name="scope">可选的参数作用域。</param>
    /// <returns>独立的 SQL 与参数快照。</returns>
    public SqlQueryFragment Render(SqlQueryShapeKey shape,
        IReadOnlyDictionary<string, object> values, SqlQueryScope scope) =>
        RenderCore(shape, values, scope, null);

    /// <summary>按结构键和编译后参数槽位顺序绑定值并渲染查询。</summary>
    /// <param name="shape">查询结构键。</param>
    /// <param name="values">按编译后参数槽位顺序排列的本次请求值；数量必须与启用形状的槽位数一致，null 是有效参数值。</param>
    /// <param name="scope">可选的参数作用域。</param>
    /// <returns>独立的 SQL 与参数快照。</returns>
    /// <remarks>值按启用片段的参数声明顺序绑定；重复逻辑参数只占首次声明的槽位。值数量必须与槽位数精确匹配，不会使用模板默认值补位；此顺序不按 SQL 文本中的标记位置推断。</remarks>
    public SqlQueryFragment RenderValues(SqlQueryShapeKey shape, ReadOnlySpan<object> values,
        SqlQueryScope scope = null)
    {
        var compiled = GetCompiledShape(shape);
        if (values.Length != compiled.Parameters.Length)
            throw new ArgumentException("值数量必须与查询形状的参数槽位数量一致。", nameof(values));

        var parameters = new List<SqlParam>(compiled.Parameters.Length);
        for (var index = 0; index < compiled.Parameters.Length; index++)
            parameters.Add(SqlParameterSnapshot.CloneSqlParameterWithValue(compiled.Parameters[index], values[index]));

        return CreateRenderedFragment(compiled, parameters, scope);
    }

    private SqlQueryFragment RenderCore(SqlQueryShapeKey shape, IReadOnlyDictionary<string, object> values,
        SqlQueryScope scope, IReadOnlyDictionary<SqlParameterKey, object> keyedValues)
    {
        var compiled = GetCompiledShape(shape);
        var parameters = new List<SqlParam>(compiled.Parameters.Length);
        for (var index = 0; index < compiled.Parameters.Length; index++)
        {
            object value;
            var key = compiled.Keys == null ? null : compiled.Keys[index];
            bool hasValue;
            if (key.HasValue)
            {
                value = null;
                hasValue = keyedValues != null && keyedValues.TryGetValue(key.Value, out value);
            }
            else
                hasValue = TryGetValue(values, compiled.Names[index], out value);
            if (!hasValue && compiled.ConflictingDefaults != null && compiled.ConflictingDefaults[index])
            {
                if (key.HasValue)
                    throw new InvalidOperationException("同一逻辑参数的默认值或转换状态不一致，请使用逻辑键绑定本次请求值：" +
                        key.Value.Scope + "/" + key.Value.Name);
                throw new InvalidOperationException("同名参数的默认值或转换状态不一致，请绑定本次请求值或使用不同的参数名：" + compiled.Names[index]);
            }
            var bound = hasValue ? SqlParameterSnapshot.CloneSqlParameterWithValue(compiled.Parameters[index], value)
                : SqlQueryFragment.Clone(compiled.Parameters[index]);
            parameters.Add(bound);
        }

        return CreateRenderedFragment(compiled, parameters, scope);
    }

    private CompiledShape GetCompiledShape(SqlQueryShapeKey shape)
    {
        var optionalMask = shape.OptionalMask;
        if ((optionalMask & ~_supportedMask) != 0)
            throw new ArgumentOutOfRangeException(nameof(optionalMask), optionalMask,
                "OptionalMask 包含模板未声明的片段位。");
        return _shapes.GetOrAdd(shape, _shapeFactory);
    }

    private static SqlQueryFragment CreateRenderedFragment(CompiledShape compiled, List<SqlParam> parameters,
        SqlQueryScope scope)
    {
        var result = SqlQueryFragment.FromOwnedParameters(compiled.Sql, parameters, compiled.Kind, compiled.Correlation,
            compiled.ParameterKeys);
        return scope == null ? result : result.Render(scope);
    }

    private CompiledShape CompileShape(SqlQueryShapeKey shape)
    {
        var fragments = new List<SqlQueryFragment> { _required };
        foreach (var (mask, fragment) in _fragments)
            if (mask == 0 || (shape.OptionalMask & mask) != 0)
                fragments.Add(fragment);
        if (fragments.Any(item => item.HasParameterKeys))
            return CompileKeyedShape(fragments);
        var sql = string.Join(_separator, fragments.Select(item => item.Sql));
        var parameters = new List<SqlParam>();
        var parameterNames = new List<string>();
        var seenNames = new Dictionary<string, (int Index, string Sql)>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> conflictingDefaults = null;
        Dictionary<string, string> tokenNames = null;
        var kind = _required.Kind;
        var correlation = _required.Correlation;
        foreach (var fragment in fragments)
        {
            if (fragment.Correlation == SqlQueryCorrelationKind.Correlated)
                correlation = SqlQueryCorrelationKind.Correlated;
            foreach (var parameter in fragment.TemplateParameters)
            {
                var name = NormalizeName(parameter.Name);
                if (seenNames.TryGetValue(name, out var existingSlot))
                {
                    var index = existingSlot.Index;
                    var existing = parameters[index];
                    if (!HasSameContract(existing, parameter))
                        throw new InvalidOperationException("同名参数的类型、映射或转换配置不一致，请使用不同的参数名：" + name);
                    if (!HasSameDefault(existing, parameter))
                        (conflictingDefaults ??= new HashSet<int>()).Add(index);
                    if (!string.Equals(existing.Name.Trim(), parameter.Name.Trim(), StringComparison.Ordinal))
                    {
                        var token = FindFirstToken(existing, existingSlot.Sql, parameter);
                        var repeatedToken = FindFirstToken(parameter, fragment.Sql, existing);
                        if (token != null && repeatedToken != null && !string.Equals(token, repeatedToken, StringComparison.Ordinal))
                        {
                            tokenNames ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            tokenNames["@" + name] = token;
                            tokenNames[":" + name] = token;
                            tokenNames["?" + name] = token;
                        }
                    }
                    continue;
                }
                seenNames.Add(name, (parameters.Count, fragment.Sql));
                parameterNames.Add(name);
                parameters.Add(parameter);
            }
        }
        var defaultConflicts = conflictingDefaults == null ? null : new bool[parameters.Count];
        if (conflictingDefaults != null)
            foreach (var index in conflictingDefaults)
                defaultConflicts[index] = true;
        if (tokenNames != null)
        {
            // 会话转义模式未知时，反斜杠紧邻引号会改变词法边界，不能安全改写参数。
            if (sql.Contains("\\'", StringComparison.Ordinal) || sql.Contains("\\\"", StringComparison.Ordinal))
                throw new InvalidOperationException("SQL 包含无法确定转义模式的引号，请统一共享参数的拼写后重试。");
            sql = SqlBuilderBase.ReplaceParameterTokens(sql, tokenNames, ignoreCase: true);
        }
        return new CompiledShape(sql, parameters.ToArray(), parameterNames.ToArray(), defaultConflicts, kind, correlation);
    }

    // 逻辑键在片段级别归并；不同身份仅在发生物理名称冲突时重命名。
    private CompiledShape CompileKeyedShape(IReadOnlyList<SqlQueryFragment> fragments)
    {
        var legacySlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var keyedSlots = new Dictionary<SqlParameterKey, int>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parameters = new List<SqlParam>();
        var parameterNames = new List<string>();
        var keys = new List<SqlParameterKey?>();
        var tokens = new List<string>();
        var sqlParts = new List<string>(fragments.Count);
        var resultKeys = new Dictionary<string, SqlParameterKey>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> conflictingDefaults = null;
        var correlation = _required.Correlation;

        foreach (var fragment in fragments)
        {
            if (fragment.Correlation == SqlQueryCorrelationKind.Correlated)
                correlation = SqlQueryCorrelationKind.Correlated;
            Dictionary<string, string> replacements = null;
            foreach (var parameter in fragment.TemplateParameters)
            {
                var name = NormalizeName(parameter.Name);
                var keyed = fragment.TryGetParameterKey(name, out var key);
                var found = keyed ? keyedSlots.TryGetValue(key, out var slot) : legacySlots.TryGetValue(name, out slot);
                var token = FindFirstToken(parameter, fragment.Sql, parameter);
                if (found)
                {
                    if (!HasSameContract(parameters[slot], parameter))
                        throw new InvalidOperationException("同一逻辑参数的类型、映射或转换配置不一致：" + name);
                    if (!HasSameDefault(parameters[slot], parameter))
                        (conflictingDefaults ??= new HashSet<int>()).Add(slot);
                    if (tokens[slot] == null && token != null)
                        tokens[slot] = token[0] + NormalizeName(parameters[slot].Name);
                }
                else
                {
                    var physicalName = name;
                    var suffix = 0;
                    while (!usedNames.Add(physicalName))
                        physicalName = name + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var physicalToken = token == null ? null : token[0] + physicalName;
                    var original = parameter.Name.Trim();
                    var storedName = original[0] is '@' or ':' or '?' ? physicalToken : physicalName;
                    slot = parameters.Count;
                    parameters.Add(string.Equals(original, storedName, StringComparison.Ordinal)
                        ? parameter : SqlQueryFragment.Clone(parameter, storedName));
                    parameterNames.Add(name);
                    keys.Add(keyed ? key : null);
                    tokens.Add(physicalToken);
                    if (keyed)
                    {
                        keyedSlots.Add(key, slot);
                        resultKeys.Add(physicalName, key);
                    }
                    else
                        legacySlots.Add(name, slot);
                }

                var target = tokens[slot];
                if (token != null && target != null)
                {
                    replacements ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    replacements["@" + name] = target;
                    replacements[":" + name] = target;
                    replacements["?" + name] = target;
                }
            }
            var sql = fragment.Sql;
            if (replacements != null)
            {
                var rewritten = SqlBuilderBase.ReplaceParameterTokens(sql, replacements, ignoreCase: true);
                if (!string.Equals(sql, rewritten, StringComparison.Ordinal))
                {
                    if (sql.Contains("\\'", StringComparison.Ordinal) || sql.Contains("\\\"", StringComparison.Ordinal))
                        throw new InvalidOperationException("SQL 包含无法确定转义模式的引号，请统一参数拼写后重试。");
                    sql = rewritten;
                }
            }
            sqlParts.Add(sql);
        }

        var defaultConflicts = conflictingDefaults == null ? null : new bool[parameters.Count];
        if (conflictingDefaults != null)
            foreach (var index in conflictingDefaults)
                defaultConflicts[index] = true;
        return new CompiledShape(string.Join(_separator, sqlParts), parameters.ToArray(), parameterNames.ToArray(),
            defaultConflicts, _required.Kind, correlation, keys.ToArray(), resultKeys);
    }

    // 无前缀的参数以首次声明片段中的实际标记为准，不推测 Provider 使用何种前缀。
    private static string FindFirstToken(SqlParam first, string sql, SqlParam repeated)
    {
        var original = first.Name.Trim();
        if (original[0] is '@' or ':' or '?')
            return original;
        foreach (var prefix in new[] { '@', ':', '?' })
        {
            var token = prefix + original;
            if (SqlBuilderBase.ContainsParameterToken(sql, token))
                return token;
            var probe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [token] = "\u0001" };
            if (!string.Equals(sql, SqlBuilderBase.ReplaceParameterTokens(sql, probe, ignoreCase: true), StringComparison.Ordinal))
                return token;
        }
        var other = repeated.Name.Trim();
        return other[0] is '@' or ':' or '?' ? other : null;
    }

    // 形状编译及片段合并复用固定绑定合同比较，不需要反射。
    internal static bool HasSameContract(SqlParam first, SqlParam second) =>
        first.DbType == second.DbType && first.Direction == second.Direction &&
        first.Size == second.Size && first.Precision == second.Precision && first.Scale == second.Scale &&
        first.EntityType == second.EntityType && first.PropertyName == second.PropertyName &&
        first.ColumnName == second.ColumnName && first.DatabaseType == second.DatabaseType &&
        first.ProviderTypeName == second.ProviderTypeName && first.Source == second.Source &&
        first.MetadataLevel == second.MetadataLevel && first.StorageKind == second.StorageKind &&
        first.ConverterKind == second.ConverterKind && first.CustomConverterName == second.CustomConverterName;

    private static bool HasSameDefault(SqlParam first, SqlParam second) =>
        HasSameConversionState(first, second) && ValuesEqual(first.Value, second.Value);

    internal static bool HasSameConversionState(SqlParam first, SqlParam second) =>
        first.IsProviderValue == second.IsProviderValue && ValuesEqual(first.OriginalValue, second.OriginalValue);

    // 参数快照会复制数组和字典，比较这些容器的内容；其他值沿用自身的相等语义。
    internal static bool ValuesEqual(object first, object second)
    {
        if (ReferenceEquals(first, second))
            return true;
        if (first == null || second == null)
            return false;
        if (first is byte[] firstBytes && second is byte[] secondBytes)
            return firstBytes.AsSpan().SequenceEqual(secondBytes);
        if (first is Array firstArray && second is Array secondArray)
        {
            if (firstArray.GetType() != secondArray.GetType() || firstArray.Rank != secondArray.Rank)
                return false;
            for (var dimension = 0; dimension < firstArray.Rank; dimension++)
                if (firstArray.GetLength(dimension) != secondArray.GetLength(dimension) ||
                    firstArray.GetLowerBound(dimension) != secondArray.GetLowerBound(dimension))
                    return false;
            var other = secondArray.GetEnumerator();
            foreach (var item in firstArray)
                if (!other.MoveNext() || !ValuesEqual(item, other.Current))
                    return false;
            return true;
        }
        if (first is IReadOnlyDictionary<string, object> firstDictionary &&
            second is IReadOnlyDictionary<string, object> secondDictionary)
        {
            if (firstDictionary.Count != secondDictionary.Count)
                return false;
            foreach (var item in firstDictionary)
                if (!secondDictionary.TryGetValue(item.Key, out var value) || !ValuesEqual(item.Value, value))
                    return false;
            return true;
        }
        return first.Equals(second);
    }

    /// <summary>只保存模板自身的 SQL、默认参数与槽位，不保存请求输入。</summary>
    private sealed class CompiledShape
    {
        internal CompiledShape(string sql, SqlParam[] parameters, string[] names, bool[] conflictingDefaults,
            SqlQueryFragmentKind kind, SqlQueryCorrelationKind correlation, SqlParameterKey?[] keys = null,
            IReadOnlyDictionary<string, SqlParameterKey> parameterKeys = null)
        {
            Sql = sql;
            Parameters = parameters;
            Names = names;
            ConflictingDefaults = conflictingDefaults;
            Kind = kind;
            Correlation = correlation;
            Keys = keys;
            ParameterKeys = parameterKeys;
        }

        internal string Sql { get; }
        internal SqlParam[] Parameters { get; }
        internal string[] Names { get; }
        internal bool[] ConflictingDefaults { get; }
        internal SqlQueryFragmentKind Kind { get; }
        internal SqlQueryCorrelationKind Correlation { get; }
        internal SqlParameterKey?[] Keys { get; }
        internal IReadOnlyDictionary<string, SqlParameterKey> ParameterKeys { get; }
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object> values, string normalizedName,
        out object value)
    {
        if (values != null)
        {
            if (values.TryGetValue(normalizedName, out value))
                return true;
            if (values.TryGetValue("@" + normalizedName, out value))
                return true;
            if (values.TryGetValue(":" + normalizedName, out value))
                return true;
            if (values.TryGetValue("?" + normalizedName, out value))
                return true;
        }
        value = null;
        return false;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("参数名称不能为空。", nameof(name));
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }

    private static SqlQueryFragment Clone(SqlQueryFragment source) =>
        source.Copy();
}

/// <summary>
/// 共享不可变查询形状值的有界并发缓存。
/// </summary>
/// <typeparam name="TKey">查询形状键类型。</typeparam>
/// <typeparam name="TValue">不可变缓存值类型。</typeparam>
/// <remarks>
/// 命中路径只访问并发字典；容量淘汰在新增项生成成功后执行，避免请求热路径依赖全局锁。
/// 并发生成中的条目可能暂时超过容量，生成结束后会恢复到容量以内。
/// </remarks>
internal sealed class SqlQueryShapeCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<CacheValue>> _entries = new();
    private readonly ConcurrentQueue<KeyValuePair<TKey, Lazy<CacheValue>>> _order = new();
    private readonly int? _capacity;
    private int _successfulCount;
    private long _hitCount;
    private long _missCount;
    private long _bypassCount;
    private long _evictionCount;

    /// <summary>创建查询形状缓存。</summary>
    /// <param name="capacity">容量；null 表示不限制，0 表示旁路缓存。</param>
    internal SqlQueryShapeCache(int? capacity = 256)
    {
        if (capacity.HasValue && capacity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "缓存容量不能小于 0。");
        _capacity = capacity;
    }

    /// <summary>获取当前条目数。</summary>
    internal int Count => _entries.Count;

    /// <summary>获取命中次数。</summary>
    internal long HitCount => Interlocked.Read(ref _hitCount);

    /// <summary>获取未命中次数。</summary>
    internal long MissCount => Interlocked.Read(ref _missCount);

    /// <summary>获取旁路次数。</summary>
    internal long BypassCount => Interlocked.Read(ref _bypassCount);

    /// <summary>获取淘汰次数。</summary>
    internal long EvictionCount => Interlocked.Read(ref _evictionCount);

    /// <summary>获取等待淘汰的已成功生成条目数。</summary>
    internal int TrackedCount => _order.Count;

    /// <summary>获取或创建缓存值。</summary>
    /// <param name="key">查询形状键。</param>
    /// <param name="factory">值工厂。</param>
    /// <returns>共享的不可变缓存值。</returns>
    internal TValue GetOrAdd(TKey key, Func<TValue> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        if (_capacity == 0)
        {
            Interlocked.Increment(ref _bypassCount);
            return factory();
        }

        if (_entries.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref _hitCount);
            return GetValue(key, existing);
        }

        Interlocked.Increment(ref _missCount);
        return Add(key, new Lazy<CacheValue>(() => new CacheValue(factory()), LazyThreadSafetyMode.ExecutionAndPublication));
    }

    /// <summary>使用预先创建的按键工厂获取或创建缓存值，命中时无需创建闭包。</summary>
    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        if (_capacity == 0)
        {
            Interlocked.Increment(ref _bypassCount);
            return factory(key);
        }
        if (_entries.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref _hitCount);
            return GetValue(key, existing);
        }

        Interlocked.Increment(ref _missCount);
        return Add(key, CreateLazy(key, factory));
    }

    private static Lazy<CacheValue> CreateLazy(TKey key, Func<TKey, TValue> factory) =>
        new(() => new CacheValue(factory(key)), LazyThreadSafetyMode.ExecutionAndPublication);

    private TValue Add(TKey key, Lazy<CacheValue> candidate)
    {
        var actual = _entries.GetOrAdd(key, candidate);
        if (ReferenceEquals(actual, candidate))
        {
            // 只有生成成功的条目才进入淘汰队列，失败重试不会积累过期键。
            var value = GetValue(key, candidate);
            if (_capacity.HasValue)
            {
                // 先计数再入队；并发淘汰可能暂时看不到此条目，入队后会再次检查容量。
                Interlocked.Increment(ref _successfulCount);
                _order.Enqueue(new KeyValuePair<TKey, Lazy<CacheValue>>(key, candidate));
                Trim();
            }
            return value;
        }
        Interlocked.Increment(ref _hitCount);
        return GetValue(key, actual);
    }

    private TValue GetValue(TKey key, Lazy<CacheValue> lazy)
    {
        try
        {
            return lazy.Value.Value;
        }
        catch
        {
            // 失败的生成结果不能污染后续请求；调用方的异常原样保留。
            // 键与 Lazy 必须原子匹配，避免删除其他线程已经重新生成的同键值。
            Remove(key, lazy);
            throw;
        }
    }

    private bool Remove(TKey key, Lazy<CacheValue> value) =>
        ((ICollection<KeyValuePair<TKey, Lazy<CacheValue>>>)_entries)
        .Remove(new KeyValuePair<TKey, Lazy<CacheValue>>(key, value));

    // Lazy<T> 的裁剪合同要求 T 具有公共无参构造函数；值类型槽位无需限制缓存值类型。
    private readonly struct CacheValue
    {
        internal CacheValue(TValue value) => Value = value;
        internal TValue Value { get; }
    }

    private void Trim()
    {
        if (!_capacity.HasValue)
            return;
        while (true)
        {
            // 不用 ConcurrentDictionary.Count：它为取得精确计数会同步所有内部桶。
            var count = Volatile.Read(ref _successfulCount);
            if (count <= _capacity.Value)
                return;
            if (Interlocked.CompareExchange(ref _successfulCount, count - 1, count) != count)
                continue;
            if (!_order.TryDequeue(out var entry))
            {
                // 已计数的生成可能尚未入队；恢复预留并处理并发入队。
                Interlocked.Increment(ref _successfulCount);
                if (_order.IsEmpty)
                    return;
                continue;
            }
            if (Remove(entry.Key, entry.Value))
                Interlocked.Increment(ref _evictionCount);
        }
    }
}
