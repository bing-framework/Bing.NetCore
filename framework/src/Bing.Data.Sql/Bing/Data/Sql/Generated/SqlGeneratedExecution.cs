using Bing.Data.Sql.Builders.Params;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql;

/// <summary>
/// 由现有执行器提供连接、事务和资源生命周期的生成式列表执行能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlGeneratedPlanExecutor
{
    /// <summary>
    /// 执行生成式查询并读取结果列表。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前数据行读取结果对象的委托。</param>
    /// <param name="timeout">命令超时秒数；为空时沿用执行器配置。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <returns>从查询结果读取的对象列表。</returns>
    List<TResult> ToGeneratedList<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read, int? timeout);
    /// <summary>
    /// 异步执行生成式查询并读取结果列表。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前数据行读取结果对象的委托。</param>
    /// <param name="timeout">命令超时秒数；为空时沿用执行器配置。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <returns>从查询结果读取的对象列表。</returns>
    Task<List<TResult>> ToGeneratedListAsync<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read,
        int? timeout, CancellationToken cancellationToken);
}

/// <summary>
/// 由现有执行器提供生成式单行读取能力的可选扩展。
/// </summary>
/// <remarks>
/// 实现该接口可以在读取第二行后立即报告基数错误，避免把单行终结退化为完整列表物化。
/// 未实现的第三方执行器继续由 <see cref="ISqlGeneratedPlanExecutor" /> 提供兼容回退。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlGeneratedSinglePlanExecutor
{
    /// <summary>执行生成式单行查询；零行返回默认值，多行抛出异常。</summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前结果行读取结果对象的委托。</param>
    /// <param name="timeout">命令超时秒数。</param>
    /// <returns>唯一结果或默认值。</returns>
    TResult ToGeneratedSingle<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read, int? timeout);

    /// <summary>异步执行生成式单行查询；零行返回默认值，多行抛出异常。</summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前结果行读取结果对象的委托。</param>
    /// <param name="timeout">命令超时秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>唯一结果或默认值。</returns>
    Task<TResult> ToGeneratedSingleAsync<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read,
        int? timeout, CancellationToken cancellationToken);
}

/// <summary>
/// 由执行器提供生成式基数终结能力。
/// </summary>
/// <remarks>
/// 生成读取器必须在数据库读取过程中完成 First、Single 等基数判断，
/// 避免为了生成式查询把整个结果集物化到内存。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlGeneratedTerminalPlanExecutor
{
    /// <summary>执行生成式基数终结查询。</summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前结果行读取结果的委托。</param>
    /// <param name="terminal">要执行的基数终结操作。</param>
    /// <param name="timeout">命令超时秒数。</param>
    /// <returns>基数终结结果。</returns>
    TResult ToGeneratedTerminal<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read,
        SqlGeneratedTerminal terminal, int? timeout);

    /// <summary>异步执行生成式基数终结查询。</summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="read">从当前结果行读取结果的委托。</param>
    /// <param name="terminal">要执行的基数终结操作。</param>
    /// <param name="timeout">命令超时秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>基数终结结果。</returns>
    Task<TResult> ToGeneratedTerminalAsync<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read,
        SqlGeneratedTerminal terminal, int? timeout, CancellationToken cancellationToken);
}

/// <summary>
/// 生成式查询支持的基数终结操作。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum SqlGeneratedTerminal
{
    /// <summary>读取第一行；没有结果时抛出异常。</summary>
    First,
    /// <summary>读取第一行；没有结果时返回默认值。</summary>
    FirstOrDefault,
    /// <summary>要求恰好一行；没有结果或多行时抛出异常。</summary>
    Single,
    /// <summary>要求至多一行；多行时抛出异常。</summary>
    SingleOrDefault
}

/// <summary>
/// 生成读取器使用的当前行与成员序号。
/// </summary>
/// <remarks>生成读取器所需的当前行和成员序号。被排除的成员没有序号。</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlGeneratedRow
{
    /// <summary>
    /// 保存生成行按列名查找序号的映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, int> _ordinals;
    /// <summary>
    /// 初始化一个 <see cref="SqlGeneratedRow" /> 类型的实例。
    /// </summary>
    /// <param name="reader">用于读取数据库查询结果的读取器。</param>
    /// <param name="ordinals">结果列名称到列序号的映射。</param>
    internal SqlGeneratedRow(DbDataReader reader, IReadOnlyDictionary<string, int> ordinals)
    { Reader = reader; _ordinals = ordinals; }
    /// <summary>
    /// 获取读取当前结果行的数据读取器。
    /// </summary>
    public DbDataReader Reader { get; }
    /// <summary>
    /// 尝试获取结果成员对应的数据读取序号。
    /// </summary>
    /// <param name="memberName">结果成员名称。</param>
    /// <param name="ordinal">接收成员序号的输出参数。</param>
    /// <returns>找到成员时返回 true 并设置 ordinal；未找到时返回 false。</returns>
    public bool TryGetOrdinal(string memberName, out int ordinal) => _ordinals.TryGetValue(memberName, out ordinal);
}

/// <summary>
/// 连接生成读取器与查询计划的执行入口。
/// </summary>
/// <remarks>
/// 把生成读取器接入查询计划；不持有连接或改变 map 委托语义。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlGeneratedExecution
{
    /// <summary>绑定生成式查询参数，并在 SQL 代码区展开集合参数。</summary>
    /// <param name="command">待执行的数据库命令。</param>
    /// <param name="parameters">查询计划中的输入参数。</param>
    public static void BindParameters(DbCommand command, IEnumerable<SqlParam> parameters)
    {
        var values = parameters as IReadOnlyList<SqlParam> ?? parameters.ToArray();
        HashSet<string> occupiedNames = null;
        Dictionary<string, string> replacements = null;

        foreach (var value in values)
        {
            if (value == null)
                continue;
            if (value.Direction.HasValue && value.Direction != ParameterDirection.Input)
                throw new NotSupportedException("生成式查询只接受输入参数。");
            if (value.Value is not IEnumerable sequence || value.Value is string || value.Value is byte[])
            {
                AddGeneratedParameter(command, value.Name, value.Value, value);
                continue;
            }

            if (occupiedNames == null)
            {
                // SQL 的反斜杠转义规则取决于数据库和会话设置，不能安全判断参数是否在字符串中。
                if (command.CommandText.Contains("\\'", StringComparison.Ordinal) ||
                    command.CommandText.Contains("\\\"", StringComparison.Ordinal))
                    throw new InvalidOperationException("SQL 包含无法确定转义模式的引号，不能展开集合参数。");

                occupiedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in values)
                    if (input != null)
                        occupiedNames.Add(NormalizeGeneratedParameterName(input.Name));
                foreach (DbParameter parameter in command.Parameters)
                    occupiedNames.Add(NormalizeGeneratedParameterName(parameter.ParameterName));
                replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var name = NormalizeGeneratedParameterName(value.Name);
            var prefix = GeneratedParameterPrefix(value.Name);
            var placeholders = new StringBuilder("(");
            var index = 0;
            foreach (var item in sequence)
            {
                if (index > 0)
                    placeholders.Append(',');
                var baseName = name + index.ToString(CultureInfo.InvariantCulture);
                var itemName = baseName;
                for (var suffix = 1; !occupiedNames.Add(itemName); suffix++)
                    itemName = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                placeholders.Append(prefix).Append(itemName);
                AddGeneratedParameter(command, itemName, item, value);
                index++;
            }
            replacements[prefix + name] = index == 0 ? "(SELECT 1 WHERE 1 = 0)" : placeholders.Append(')').ToString();
        }

        // 所有集合一次性替换，避免前一个集合的新名称被后一个集合再次替换。
        if (replacements != null)
            command.CommandText = SqlBuilderBase.ReplaceParameterTokens(command.CommandText, replacements, ignoreCase: true);
    }

    private static void AddGeneratedParameter(DbCommand command, string name, object value, SqlParam metadata)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = NormalizeGeneratedParameterName(name);
        parameter.Value = value ?? DBNull.Value;
        if (metadata.DbType.HasValue) parameter.DbType = metadata.DbType.Value;
        if (metadata.Direction.HasValue) parameter.Direction = metadata.Direction.Value;
        if (metadata.Size.HasValue) parameter.Size = metadata.Size.Value;
        if (metadata.Precision.HasValue) parameter.Precision = metadata.Precision.Value;
        if (metadata.Scale.HasValue) parameter.Scale = metadata.Scale.Value;
        command.Parameters.Add(parameter);
    }

    private static string NormalizeGeneratedParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }

    private static string GeneratedParameterPrefix(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var first = name.Trim()[0];
            if (first is '@' or ':' or '?') return first.ToString();
        }
        return "@";
    }

    /// <summary>
    /// 已验证的生成查询计划及结果列序号。
    /// </summary>
    private readonly struct PreparedPlan
    {
        internal PreparedPlan(ISqlQueryPlanExecutor executor, SqlQueryPlan plan,
            Dictionary<string, int>[] ordinals)
        {
            Executor = executor;
            Plan = plan;
            Ordinals = ordinals;
        }

        internal ISqlQueryPlanExecutor Executor { get; }
        internal SqlQueryPlan Plan { get; }
        internal Dictionary<string, int>[] Ordinals { get; }
    }

    /// <summary>
    /// 验证生成执行投影并准备查询计划和成员序号映射。
    /// </summary>
    /// <param name="query">要执行的 Lambda 查询。</param>
    /// <param name="resultType">查询结果类型。</param>
    /// <param name="inputs">可选的多对象映射实体类型。</param>
    /// <param name="bind">绑定生成查询参数的回调。</param>
    /// <param name="forPaging">是否需要保留分页派生来源。</param>
    /// <returns>包含查询计划执行器、查询计划和各映射段成员序号的元组。</returns>
    /// <remarks>
    /// 该方法只负责一次性完成结构验证和计划冻结；列表/单行读取器要求由调用方单独检查，
    /// 这样 Scalar、Page 等仍可复用同一份计划和静态参数绑定而不强制依赖生成列表接口。
    /// </remarks>
    private static PreparedPlan PreparePlan(SqlLambdaQuery query, Type resultType, Type[] inputs,
        Action<DbCommand, IEnumerable<SqlParam>> bind, bool forPaging = false)
    {
        if (bind == null) throw new ArgumentNullException(nameof(bind));
        if (query == null) throw new ArgumentNullException(nameof(query));
        var core = query.GeneratedCore;
        if (inputs == null && core.TryGeneratedTemplatePlan(resultType, out var templatePlan, out var templateOrdinals))
        {
            templatePlan.IsGeneratedExecution = true;
            templatePlan.GeneratedParameterBinder = bind;
            return new PreparedPlan(core.Executor, templatePlan, templateOrdinals);
        }
        var select = (SelectClause)((ISqlQueryClauseAccessor)core.ReadBuilder).SelectClause;
        select.ValidateResult(resultType, inputs, core.GeneratedSplitOn);
        if (select.HasLegacyExplicitColumns)
            throw new NotSupportedException("生成读取器不支持混合旧投影，请使用 SelectAll 或结构化 Select。");
        var layout = select.ProjectionLayout();
        var sources = SqlLambdaSourceResolver.Sources((ISqlQueryClauseAccessor)core.ReadBuilder);
        if (layout.Count == 0)
            layout = select.EntityColumns(sources[0], false);
        if (inputs != null)
        {
            if (sources.Count != inputs.Length || sources.Where((source, index) => source.EntityType != inputs[index]).Any())
                throw new NotSupportedException("生成多映射输入类型必须与来源顺序一致。");
            var expectedOrder = sources.SelectMany(source => layout.Where(column => column.SourceId == source.SourceId)).ToArray();
            if (!layout.SequenceEqual(expectedOrder))
                throw new NotSupportedException("生成多映射要求每个来源的列连续排列。");
            var boundaries = core.GeneratedSplitOn.Split(',').Select(value => value.Trim()).ToArray();
            for (var index = 1; index < sources.Count; index++)
            {
                var first = layout.FirstOrDefault(column => column.SourceId == sources[index].SourceId);
                if (first == null) throw new NotSupportedException("生成多映射分段不能为空。");
                var item = first.ToColumn(select.ProjectionContext);
                var boundary = boundaries.Length == 1 ? boundaries[0] : boundaries.Length == sources.Count - 1 ? boundaries[index - 1] : null;
                if (!string.Equals(item.ColumnAlias ?? item.Name, boundary, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("生成多映射分段首列必须与 SplitOn 一致。");
            }
        }
        var segments = inputs == null ? null : sources.Select((source, index) => (source.SourceId, index))
            .ToDictionary(item => item.SourceId, item => item.index);
        var ordinals = Enumerable.Range(0, inputs?.Length ?? 1)
            .Select(_ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)).ToArray();
        for (var index = 0; index < layout.Count; index++)
        {
            var column = layout[index];
            var segment = inputs == null ? 0 : segments.TryGetValue(column.SourceId, out var sourceIndex) ? sourceIndex : -1;
            if (segment < 0 || segment >= ordinals.Length) throw new NotSupportedException("生成读取器不能识别投影分段。");
            var item = column.ToColumn(select.ProjectionContext);
            ordinals[segment].Add(item.ColumnAlias ?? item.Name, index);
        }
        // 在进入执行生命周期和打开连接前完成能力与参数形态检查。
        var plan = core.GeneratedPlan(forPaging);
        plan.IsGeneratedExecution = true;
        plan.GeneratedParameterBinder = bind;
        return new PreparedPlan(core.Executor, plan, ordinals);
    }

    /// <summary>
    /// 验证生成执行投影并要求执行器提供静态逐行读取能力。
    /// </summary>
    private static (ISqlGeneratedPlanExecutor Executor, SqlQueryPlan Plan, Dictionary<string, int>[] Ordinals) Prepare(
        SqlLambdaQuery query, Type resultType, Type[] inputs, Action<DbCommand, IEnumerable<SqlParam>> bind)
    {
        var prepared = PreparePlan(query, resultType, inputs, bind);
        if (prepared.Executor is not ISqlGeneratedPlanExecutor executor)
            throw new NotSupportedException("当前执行器缺少生成式列表执行能力。");
        return (executor, prepared.Plan, prepared.Ordinals);
    }

    /// <summary>
    /// 执行生成查询并将每一行映射为结果列表。
    /// </summary>
    /// <param name="query">要执行的 Lambda 查询。</param>
    /// <param name="read">将生成行转换为结果的读取函数。</param>
    /// <param name="bind">绑定生成查询参数的回调。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <returns>按读取函数生成的结果列表。</returns>
    public static List<TResult> ToList<TResult>(SqlLambdaQuery query, Func<SqlGeneratedRow, TResult> read, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var state = Prepare(query, typeof(TResult), null, bind);
        return state.Executor.ToGeneratedList(state.Plan, reader => read(new SqlGeneratedRow(reader, state.Ordinals[0])), timeout);
    }

    /// <summary>
    /// 异步执行生成查询并将每一行映射为结果列表。
    /// </summary>
    /// <param name="query">要执行的 Lambda 查询。</param>
    /// <param name="read">将生成行转换为结果的读取函数。</param>
    /// <param name="bind">绑定生成查询参数的回调。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <returns>按读取函数生成的结果列表。</returns>
    public static Task<List<TResult>> ToListAsync<TResult>(SqlLambdaQuery query, Func<SqlGeneratedRow, TResult> read, Action<DbCommand, IEnumerable<SqlParam>> bind,
        int? timeout, CancellationToken cancellationToken)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), null, bind);
        return state.Executor.ToGeneratedListAsync(state.Plan, reader => read(new SqlGeneratedRow(reader, state.Ordinals[0])), timeout, cancellationToken);
    }

    /// <summary>
    /// 执行生成查询并将每一行映射为结果列表。
    /// </summary>
    /// <param name="query">要执行的 Lambda 查询。</param>
    /// <param name="first">读取第一映射段结果的函数。</param>
    /// <param name="second">读取第二映射段结果的函数。</param>
    /// <param name="map">将两个映射段合并为结果的函数。</param>
    /// <param name="bind">绑定生成查询参数的回调。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <typeparam name="TFirst">第一映射段的类型。</typeparam>
    /// <typeparam name="TSecond">第二映射段的类型。</typeparam>
    /// <typeparam name="TResult">合并后的查询结果类型。</typeparam>
    /// <returns>按 map 函数生成的结果列表。</returns>
    /// <remarks>
    /// 第二映射段的首列为数据库 NULL 时向 map 传入 default(TSecond)。
    /// </remarks>
    public static List<TResult> ToList<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<SqlGeneratedRow, TFirst> first, Func<SqlGeneratedRow, TSecond> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var splitOrdinal = state.Ordinals[1].Values.Min();
        return state.Executor.ToGeneratedList(state.Plan, reader => map(first(new SqlGeneratedRow(reader, state.Ordinals[0])),
            reader.IsDBNull(splitOrdinal) ? default : second(new SqlGeneratedRow(reader, state.Ordinals[1]))), timeout);
    }

    /// <summary>
    /// 异步执行生成查询并将每一行映射为结果列表。
    /// </summary>
    /// <param name="query">要执行的 Lambda 查询。</param>
    /// <param name="first">读取第一映射段结果的函数。</param>
    /// <param name="second">读取第二映射段结果的函数。</param>
    /// <param name="map">将两个映射段合并为结果的函数。</param>
    /// <param name="bind">绑定生成查询参数的回调。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <typeparam name="TFirst">第一映射段的类型。</typeparam>
    /// <typeparam name="TSecond">第二映射段的类型。</typeparam>
    /// <typeparam name="TResult">合并后的查询结果类型。</typeparam>
    /// <returns>按 map 函数生成的结果列表。</returns>
    /// <remarks>
    /// 第二映射段的首列为数据库 NULL 时向 map 传入 default(TSecond)。
    /// </remarks>
    public static Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<SqlGeneratedRow, TFirst> first, Func<SqlGeneratedRow, TSecond> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var splitOrdinal = state.Ordinals[1].Values.Min();
        return state.Executor.ToGeneratedListAsync(state.Plan, reader => map(first(new SqlGeneratedRow(reader, state.Ordinals[0])),
            reader.IsDBNull(splitOrdinal) ? default : second(new SqlGeneratedRow(reader, state.Ordinals[1]))), timeout, cancellationToken);
    }

    /// <summary>先绑定列序号再执行生成查询，不为每行创建包装对象。</summary>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="read">将当前结果布局绑定为逐行读取器的工厂，每个结果集仅调用一次。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <returns>物化后的结果列表。</returns>
    public static List<TResult> ToBoundList<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        return state.Executor.ToGeneratedList(state.Plan, materialize, timeout);
    }

    /// <summary>先绑定列序号再异步执行生成查询。</summary>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="read">将当前结果布局绑定为逐行读取器的工厂，每个结果集仅调用一次。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <param name="cancellationToken">取消查询的令牌。</param>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <returns>物化后的结果列表。</returns>
    public static Task<List<TResult>> ToBoundListAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        return state.Executor.ToGeneratedListAsync(state.Plan, materialize, timeout, cancellationToken);
    }

    /// <summary>
    /// 应用终结 DTO 原型后执行生成式列表查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>物化后的结果列表。</returns>
    public static List<TResult> ToBoundListPrototype<TResult>(SqlLambdaQuery query, TResult prototype,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) where TResult : class =>
        ToBoundListPrototype(query, prototype, null, read, bind, timeout);

    /// <summary>
    /// 应用终结 DTO 原型后执行生成式列表查询，并使用编译期成员名称。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="projectionMembers">编译期确定的可读取标量成员名称，包含构造函数绑定成员。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>物化后的结果列表。</returns>
    public static List<TResult> ToBoundListPrototype<TResult>(SqlLambdaQuery query, TResult prototype,
        string[] projectionMembers,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) where TResult : class
    {
        SqlGeneratedQueryBridge.Prototype(query, typeof(TResult), prototype, projectionMembers);
        return ToBoundList(query, read, bind, timeout);
    }

    /// <summary>
    /// 应用终结 DTO 原型后异步执行生成式列表查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示物化结果列表的异步操作。</returns>
    public static Task<List<TResult>> ToBoundListPrototypeAsync<TResult>(SqlLambdaQuery query, TResult prototype,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) where TResult : class =>
        ToBoundListPrototypeAsync(query, prototype, null, read, bind, timeout, cancellationToken);

    /// <summary>
    /// 应用终结 DTO 原型后异步执行生成式列表查询，并使用编译期成员名称。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="projectionMembers">编译期确定的可读取标量成员名称，包含构造函数绑定成员。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示物化结果列表的异步操作。</returns>
    public static Task<List<TResult>> ToBoundListPrototypeAsync<TResult>(SqlLambdaQuery query, TResult prototype,
        string[] projectionMembers,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) where TResult : class
    {
        SqlGeneratedQueryBridge.Prototype(query, typeof(TResult), prototype, projectionMembers);
        return ToBoundListAsync(query, read, bind, timeout, cancellationToken);
    }

    /// <summary>
    /// 应用终结 DTO 原型后执行生成式单行查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>唯一结果或默认值；多行时抛出异常。</returns>
    public static TResult ToBoundSinglePrototype<TResult>(SqlLambdaQuery query, TResult prototype,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) where TResult : class =>
        ToBoundSinglePrototype(query, prototype, null, read, bind, timeout);

    /// <summary>
    /// 应用终结 DTO 原型后执行生成式单行查询，并使用编译期成员名称。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="projectionMembers">编译期确定的可读取标量成员名称，包含构造函数绑定成员。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>唯一结果或默认值；多行时抛出异常。</returns>
    public static TResult ToBoundSinglePrototype<TResult>(SqlLambdaQuery query, TResult prototype,
        string[] projectionMembers,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) where TResult : class
    {
        SqlGeneratedQueryBridge.Prototype(query, typeof(TResult), prototype, projectionMembers);
        return ToBoundSingle(query, read, bind, timeout);
    }

    /// <summary>
    /// 应用终结 DTO 原型后异步执行生成式单行查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果或默认值的异步操作；多行时抛出异常。</returns>
    public static Task<TResult> ToBoundSinglePrototypeAsync<TResult>(SqlLambdaQuery query, TResult prototype,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) where TResult : class =>
        ToBoundSinglePrototypeAsync(query, prototype, null, read, bind, timeout, cancellationToken);

    /// <summary>
    /// 应用终结 DTO 原型后异步执行生成式单行查询，并使用编译期成员名称。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="prototype">结果原型；不读取属性值。</param>
    /// <param name="projectionMembers">编译期确定的可读取标量成员名称，包含构造函数绑定成员。</param>
    /// <param name="read">生成读取器绑定工厂。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果或默认值的异步操作；多行时抛出异常。</returns>
    public static async Task<TResult> ToBoundSinglePrototypeAsync<TResult>(SqlLambdaQuery query, TResult prototype,
        string[] projectionMembers,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) where TResult : class
    {
        SqlGeneratedQueryBridge.Prototype(query, typeof(TResult), prototype, projectionMembers);
        return await ToBoundSingleAsync(query, read, bind, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用已冻结的生成计划执行标量查询。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>首行首列值；无结果时沿用执行器的标量语义。</returns>
    public static TResult Scalar<TResult>(SqlLambdaQuery query,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        var state = PreparePlan(query, typeof(TResult), null, bind);
        return state.Executor.Scalar<TResult>(state.Plan, timeout);
    }

    /// <summary>
    /// 异步使用已冻结的生成计划执行标量查询。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作。</returns>
    public static Task<TResult> ScalarAsync<TResult>(SqlLambdaQuery query,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = PreparePlan(query, typeof(TResult), null, bind);
        return state.Executor.ScalarAsync<TResult>(state.Plan, timeout, cancellationToken);
    }

    /// <summary>
    /// 使用已冻结的生成计划执行分页查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="pager">分页参数。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <returns>分页结果。</returns>
    /// <remarks>
    /// 计划与参数绑定已经静态生成；结果页仍复用 Provider 的分页执行器，以保持总数、回写和方言语义。
    /// </remarks>
    public static PagerList<TResult> ToPage<TResult>(SqlLambdaQuery query, IPager pager,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        var state = PreparePlan(query, typeof(TResult), null, bind, forPaging: true);
        return state.Executor.ToPage<TResult>(state.Plan, pager, timeout);
    }

    /// <summary>
    /// 异步使用已冻结的生成计划执行分页查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="pager">分页参数。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示分页结果的异步操作。</returns>
    /// <remarks>
    /// 计划与参数绑定已经静态生成；结果页仍复用 Provider 的分页执行器，以保持总数、回写和方言语义。
    /// </remarks>
    public static Task<PagerList<TResult>> ToPageAsync<TResult>(SqlLambdaQuery query, IPager pager,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = PreparePlan(query, typeof(TResult), null, bind, forPaging: true);
        return state.Executor.ToPageAsync<TResult>(state.Plan, pager, timeout, cancellationToken);
    }

    /// <summary>
    /// 执行已经绑定列序号的生成式单行查询；没有专用执行器时回退到列表执行器。
    /// </summary>
    public static TResult ToBoundSingle<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        if (state.Executor is ISqlGeneratedSinglePlanExecutor single)
            return single.ToGeneratedSingle(state.Plan, materialize, timeout);
        return state.Executor.ToGeneratedList(state.Plan, materialize, timeout).SingleOrDefault();
    }

    /// <summary>
    /// 异步执行已经绑定列序号的生成式单行查询；没有专用执行器时回退到列表执行器。
    /// </summary>
    public static async Task<TResult> ToBoundSingleAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        if (state.Executor is ISqlGeneratedSinglePlanExecutor single)
            return await single.ToGeneratedSingleAsync(state.Plan, materialize, timeout, cancellationToken).ConfigureAwait(false);
        return (await state.Executor.ToGeneratedListAsync(state.Plan, materialize, timeout, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();
    }

    /// <summary>执行生成式 First 终结；读取过程中最多消费一行。</summary>
    public static TResult First<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteTerminal(query, read, bind, timeout, SqlGeneratedTerminal.First);

    /// <summary>执行生成式 FirstOrDefault 终结；读取过程中最多消费一行。</summary>
    public static TResult FirstOrDefault<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteTerminal(query, read, bind, timeout, SqlGeneratedTerminal.FirstOrDefault);

    /// <summary>执行生成式 Single 终结，并在读取第二行时立即报告基数错误。</summary>
    public static TResult Single<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteTerminal(query, read, bind, timeout, SqlGeneratedTerminal.Single);

    /// <summary>执行生成式 SingleOrDefault 终结。</summary>
    public static TResult SingleOrDefault<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteTerminal(query, read, bind, timeout, SqlGeneratedTerminal.SingleOrDefault);

    /// <summary>异步执行生成式 First 终结。</summary>
    public static Task<TResult> FirstAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken) =>
        ExecuteTerminalAsync(query, read, bind, timeout, cancellationToken, SqlGeneratedTerminal.First);

    /// <summary>异步执行生成式 FirstOrDefault 终结。</summary>
    public static Task<TResult> FirstOrDefaultAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken) =>
        ExecuteTerminalAsync(query, read, bind, timeout, cancellationToken, SqlGeneratedTerminal.FirstOrDefault);

    /// <summary>异步执行生成式 Single 终结。</summary>
    public static Task<TResult> SingleAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken) =>
        ExecuteTerminalAsync(query, read, bind, timeout, cancellationToken, SqlGeneratedTerminal.Single);

    /// <summary>异步执行生成式 SingleOrDefault 终结。</summary>
    public static Task<TResult> SingleOrDefaultAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken) =>
        ExecuteTerminalAsync(query, read, bind, timeout, cancellationToken, SqlGeneratedTerminal.SingleOrDefault);

    /// <summary>
    /// 执行单来源基数终结，并在没有专用基数执行器时保留兼容回退。
    /// </summary>
    private static TResult ExecuteTerminal<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, SqlGeneratedTerminal terminal)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        if (state.Executor is ISqlGeneratedTerminalPlanExecutor generated)
            return generated.ToGeneratedTerminal(state.Plan, materialize, terminal, timeout);
        return ApplyTerminal(state.Executor.ToGeneratedList(state.Plan, materialize, timeout), terminal);
    }

    /// <summary>异步执行单来源基数终结，并在没有专用基数执行器时保留兼容回退。</summary>
    private static async Task<TResult> ExecuteTerminalAsync<TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TResult>> read,
        Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout, CancellationToken cancellationToken,
        SqlGeneratedTerminal terminal)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), null, bind);
        var materialize = read(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        if (state.Executor is ISqlGeneratedTerminalPlanExecutor generated)
            return await generated.ToGeneratedTerminalAsync(state.Plan, materialize, terminal, timeout, cancellationToken)
                .ConfigureAwait(false);
        var rows = await state.Executor.ToGeneratedListAsync(state.Plan, materialize, timeout, cancellationToken)
            .ConfigureAwait(false);
        return ApplyTerminal(rows, terminal);
    }

    /// <summary>在兼容执行器回退时应用基数语义。</summary>
    private static TResult ApplyTerminal<TResult>(IReadOnlyList<TResult> rows, SqlGeneratedTerminal terminal)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        return terminal switch
        {
            SqlGeneratedTerminal.First => rows.Count == 0
                ? throw new InvalidOperationException("Sequence contains no elements")
                : rows[0],
            SqlGeneratedTerminal.FirstOrDefault => rows.Count == 0 ? default : rows[0],
            SqlGeneratedTerminal.Single => rows.Count switch
            {
                0 => throw new InvalidOperationException("Sequence contains no elements"),
                1 => rows[0],
                _ => throw new InvalidOperationException("Sequence contains more than one element")
            },
            SqlGeneratedTerminal.SingleOrDefault => rows.Count switch
            {
                0 => default,
                1 => rows[0],
                _ => throw new InvalidOperationException("Sequence contains more than one element")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null)
        };
    }

    /// <summary>执行两个映射分段的同步基数终结。</summary>
    private static TResult ExecuteMappedTerminal<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        SqlGeneratedTerminal terminal)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var readFirst = first(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var readSecond = second(state.Ordinals[1]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var split = state.Ordinals[1].Values.Min();
        TResult Materialize(DbDataReader reader) => map(readFirst(reader),
            reader.IsDBNull(split) ? default : readSecond(reader));
        if (state.Executor is ISqlGeneratedTerminalPlanExecutor generated)
            return generated.ToGeneratedTerminal(state.Plan, Materialize, terminal, timeout);
        return ApplyTerminal(state.Executor.ToGeneratedList(state.Plan, Materialize, timeout), terminal);
    }

    /// <summary>执行两个映射分段的异步基数终结。</summary>
    private static async Task<TResult> ExecuteMappedTerminalAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken, SqlGeneratedTerminal terminal)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var readFirst = first(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var readSecond = second(state.Ordinals[1]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var split = state.Ordinals[1].Values.Min();
        TResult Materialize(DbDataReader reader) => map(readFirst(reader),
            reader.IsDBNull(split) ? default : readSecond(reader));
        if (state.Executor is ISqlGeneratedTerminalPlanExecutor generated)
            return await generated.ToGeneratedTerminalAsync(state.Plan, Materialize, terminal, timeout, cancellationToken)
                .ConfigureAwait(false);
        var rows = await state.Executor.ToGeneratedListAsync(state.Plan, Materialize, timeout, cancellationToken)
            .ConfigureAwait(false);
        return ApplyTerminal(rows, terminal);
    }

    /// <summary>执行两个映射分段的 First 终结。</summary>
    public static TResult First<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteMappedTerminal(query, first, second, map, bind, timeout, SqlGeneratedTerminal.First);

    /// <summary>执行两个映射分段的 FirstOrDefault 终结。</summary>
    public static TResult FirstOrDefault<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteMappedTerminal(query, first, second, map, bind, timeout, SqlGeneratedTerminal.FirstOrDefault);

    /// <summary>执行两个映射分段的 Single 终结。</summary>
    public static TResult Single<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteMappedTerminal(query, first, second, map, bind, timeout, SqlGeneratedTerminal.Single);

    /// <summary>执行两个映射分段的 SingleOrDefault 终结。</summary>
    public static TResult SingleOrDefault<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout) =>
        ExecuteMappedTerminal(query, first, second, map, bind, timeout, SqlGeneratedTerminal.SingleOrDefault);

    /// <summary>异步执行两个映射分段的 First 终结。</summary>
    public static Task<TResult> FirstAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) =>
        ExecuteMappedTerminalAsync(query, first, second, map, bind, timeout, cancellationToken, SqlGeneratedTerminal.First);

    /// <summary>异步执行两个映射分段的 FirstOrDefault 终结。</summary>
    public static Task<TResult> FirstOrDefaultAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) =>
        ExecuteMappedTerminalAsync(query, first, second, map, bind, timeout, cancellationToken, SqlGeneratedTerminal.FirstOrDefault);

    /// <summary>异步执行两个映射分段的 Single 终结。</summary>
    public static Task<TResult> SingleAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) =>
        ExecuteMappedTerminalAsync(query, first, second, map, bind, timeout, cancellationToken, SqlGeneratedTerminal.Single);

    /// <summary>异步执行两个映射分段的 SingleOrDefault 终结。</summary>
    public static Task<TResult> SingleOrDefaultAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken) =>
        ExecuteMappedTerminalAsync(query, first, second, map, bind, timeout, cancellationToken, SqlGeneratedTerminal.SingleOrDefault);

    /// <summary>为两个结果分段各绑定一次列序号，并保留外连接空分段语义。</summary>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="first">第一分段的读取器绑定工厂。</param>
    /// <param name="second">第二分段的读取器绑定工厂。</param>
    /// <param name="map">合并两个分段的映射函数。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <typeparam name="TFirst">第一分段类型。</typeparam>
    /// <typeparam name="TSecond">第二分段类型。</typeparam>
    /// <typeparam name="TResult">合并后的结果类型。</typeparam>
    /// <returns>映射后的结果列表。</returns>
    public static List<TResult> ToBoundList<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var readFirst = first(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var readSecond = second(state.Ordinals[1]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var split = state.Ordinals[1].Values.Min();
        return state.Executor.ToGeneratedList(state.Plan, reader => map(readFirst(reader),
            reader.IsDBNull(split) ? default : readSecond(reader)), timeout);
    }

    /// <summary>异步执行预先绑定的两个结果分段。</summary>
    /// <param name="query">待执行的 Lambda 查询。</param>
    /// <param name="first">第一分段的读取器绑定工厂。</param>
    /// <param name="second">第二分段的读取器绑定工厂。</param>
    /// <param name="map">合并两个分段的映射函数。</param>
    /// <param name="bind">生成的命令参数绑定器。</param>
    /// <param name="timeout">可选的命令超时秒数。</param>
    /// <param name="cancellationToken">取消查询的令牌。</param>
    /// <typeparam name="TFirst">第一分段类型。</typeparam>
    /// <typeparam name="TSecond">第二分段类型。</typeparam>
    /// <typeparam name="TResult">合并后的结果类型。</typeparam>
    /// <returns>映射后的结果列表。</returns>
    public static Task<List<TResult>> ToBoundListAsync<TFirst, TSecond, TResult>(SqlLambdaQuery query,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TFirst>> first,
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, TSecond>> second,
        Func<TFirst, TSecond, TResult> map, Action<DbCommand, IEnumerable<SqlParam>> bind, int? timeout,
        CancellationToken cancellationToken)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (map == null) throw new ArgumentNullException(nameof(map));
        cancellationToken.ThrowIfCancellationRequested();
        var state = Prepare(query, typeof(TResult), new[] { typeof(TFirst), typeof(TSecond) }, bind);
        var readFirst = first(state.Ordinals[0]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var readSecond = second(state.Ordinals[1]) ?? throw new InvalidOperationException("生成读取器绑定失败。");
        var split = state.Ordinals[1].Values.Min();
        return state.Executor.ToGeneratedListAsync(state.Plan, reader => map(readFirst(reader),
            reader.IsDBNull(split) ? default : readSecond(reader)), timeout, cancellationToken);
    }
}
