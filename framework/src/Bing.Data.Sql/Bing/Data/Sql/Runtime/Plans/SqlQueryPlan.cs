using System.ComponentModel;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using System.Collections;
using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 独立 SQL 查询描述的执行计划。
/// </summary>
/// <remarks>
/// 计划保存独立查询输入、查询上下文和本次执行标识，但不持有连接、事务、诊断 Scope 或执行资源；
/// 这些资源始终由创建它的根查询执行器管理。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlQueryPlan
{
    /// <summary>
    /// 当前计划使用的不可变查询 IR。
    /// </summary>
    private readonly SqlQueryIr _queryIr;

    /// <summary>
    /// 计划创建阶段冻结的 SQL 与参数快照。
    /// </summary>
    private readonly SqlBuilderExecutionSnapshot _executionSnapshot;

    /// <summary>
    /// 原生文本计划创建阶段冻结的参数源。
    /// </summary>
    private readonly object _parameters;

    /// <summary>
    /// 仅用于派生 Count/Page 结构的隔离规划源。
    /// </summary>
    /// <remarks>
    /// 该字段不属于 <see cref="SqlQueryIr" />，也不参与普通执行；派生计划创建时会先复制它，
    /// 以便统一 IR 保持纯文本快照语义，同时保留 CTE、Union、Group 和 Provider 分页规则。
    /// </remarks>
    private readonly SqlQueryPlanningSource _planningSource;
    /// <summary>
    /// 当前计划本次参数绑定完成后的输出参数访问器接收器。
    /// </summary>
    private Action<ISqlOutputParameterAccessor> _outputParametersReceiver;

    /// <summary>
    /// 当前计划执行成功后创建输出参数快照的回调。
    /// </summary>
    private Action _outputParametersCompletion;

    /// <summary>
    /// 计划开始执行时调用的生命周期回调。
    /// </summary>
    private Action _executionStarted;

    /// <summary>
    /// 计划执行完成时调用的生命周期回调。
    /// </summary>
    private Action _executionCompleted;

    /// <summary>
    /// 当前计划所属的查询上下文标识。
    /// </summary>
    private string _queryContextId;

    /// <summary>
    /// 当前计划继承的父查询上下文标识。
    /// </summary>
    private string _parentQueryContextId;

    /// <summary>
    /// 当前计划本次执行的标识。
    /// </summary>
    private string _executionId;

    /// <summary>
    /// 当前计划阶段名称。
    /// </summary>
    private string _phase = "Data";

    /// <summary>
    /// 初始化一个 <see cref="SqlQueryPlan"/> 类型的实例。
    /// </summary>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="builder"/> 为 null 时抛出。</exception>
    private SqlQueryPlan(ISqlBuilder builder, string splitOn)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        SplitOn = NormalizeSplitOn(splitOn);
        CommandType = System.Data.CommandType.Text;
    }

    /// <summary>
    /// 初始化包含 Builder 派生源和执行快照的查询计划。
    /// </summary>
    private SqlQueryPlan(ISqlBuilder builder, SqlBuilderExecutionSnapshot executionSnapshot, string splitOn)
        : this(builder, splitOn)
    {
        _executionSnapshot = executionSnapshot ?? throw new ArgumentNullException(nameof(executionSnapshot));
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlQueryPlan"/> 类型的实例。
    /// </summary>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由参数绑定器处理的参数源。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <param name="commandType">当前计划使用的 ADO.NET 命令类型。</param>
    /// <exception cref="ArgumentException">当 <paramref name="commandText"/> 为空白时抛出。</exception>
    private SqlQueryPlan(string commandText, object parameters, string splitOn, CommandType commandType)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("SQL 文本不能为空。", nameof(commandText));
        CommandText = commandText;
        _parameters = SnapshotParameters(parameters);
        SplitOn = NormalizeSplitOn(splitOn);
        CommandType = commandType;
    }

    /// <summary>
    /// 初始化一个脱离 Builder 的不可变查询计划。
    /// </summary>
    private SqlQueryPlan(SqlQueryIr queryIr, string splitOn, ISqlBuilder planningBuilder)
    {
        if (queryIr == null)
            throw new ArgumentNullException(nameof(queryIr));
        _queryIr = queryIr;
        _executionSnapshot = queryIr.Snapshot;
        _planningSource = planningBuilder == null || _executionSnapshot.CanDeriveAnyPage
            ? null : new SqlQueryPlanningSource(planningBuilder, _executionSnapshot);
        CommandText = _executionSnapshot.Sql;
        SplitOn = NormalizeSplitOn(splitOn);
        CommandType = queryIr.CommandType;
    }

    /// <summary>
    /// 获取当前计划使用的 Fluent SQL Builder。
    /// </summary>
    /// <remarks>
    /// 原生文本计划为 null。
    /// </remarks>
    internal ISqlBuilder Builder { get; }

    /// <summary>
    /// 获取当前计划使用的原生 SQL 文本。
    /// </summary>
    /// <remarks>
    /// Fluent 计划为 null。
    /// </remarks>
    public string CommandText { get; }

    /// <summary>
    /// 获取当前原生 SQL 文本计划的参数源。
    /// </summary>
    /// <remarks>
    /// Fluent 计划为 null。对于字典、数组和 <see cref="SqlParam" /> 集合，
    /// 每次读取都会返回冻结参数的独立副本，避免调用方通过公开对象修改计划状态。
    /// </remarks>
    public object Parameters => _queryIr != null
        ? _executionSnapshot.CreateParameterSnapshot()
        : SnapshotParameters(_parameters);

    /// <summary>
    /// 获取 Dapper 多映射使用的分段列名称。
    /// </summary>
    public string SplitOn { get; }

    /// <summary>
    /// 获取当前计划对应的 ADO.NET 命令类型。
    /// </summary>
    public CommandType CommandType { get; }

    /// <summary>
    /// 获取指示当前计划是否使用 Fluent SQL Builder。
    /// </summary>
    public bool IsBuilderPlan => Builder != null;

    /// <summary>
    /// 将 Fluent Builder 计划冻结为文本执行计划，仅在派生能力未覆盖时保留隔离规划来源。
    /// </summary>
    /// <remarks>
    /// 返回计划拥有独立的 SQL、参数和分页规划来源；原计划仍可继续用于创建派生计划。
    /// 文本计划调用此方法时直接返回自身。
    /// </remarks>
    /// <returns>可脱离可变 Builder 执行的不可变文本计划。</returns>
    public SqlQueryPlan Freeze()
    {
        if (IsBuilderPlan == false)
            return this;

        var planningBuilder = Builder.Clone();
        var snapshot = ExecutionSnapshot;
        if (snapshot == null && planningBuilder is SqlBuilderBase sqlBuilder)
        {
            var rendered = sqlBuilder.CreateRenderSnapshot();
            planningBuilder = rendered.Builder;
            snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(rendered);
        }
        else if (snapshot == null)
        {
            var sql = planningBuilder.ToSql();
            snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(planningBuilder, sql);
        }
        var frozen = Create(SqlQueryIr.Create(snapshot, CommandType, Builder.Pager), SplitOn,
            planningBuilder);
        CopyExecutionMetadataTo(frozen);
        return frozen;
    }

    /// <summary>
    /// 将执行上下文和生命周期委托复制到脱离 Builder 的计划。
    /// </summary>
    /// <param name="target">目标文本计划。</param>
    private void CopyExecutionMetadataTo(SqlQueryPlan target)
    {
        target.SetContext(QueryContextId, ParentQueryContextId, Phase);
        target._outputParametersReceiver = _outputParametersReceiver;
        target._outputParametersCompletion = _outputParametersCompletion;
        target._executionStarted = _executionStarted;
        target._executionCompleted = _executionCompleted;
        target.IsGeneratedExecution = IsGeneratedExecution;
        target.GeneratedParameterBinder = GeneratedParameterBinder;
    }

    /// <summary>
    /// 获取计划创建时冻结的 Builder 输出。
    /// </summary>
    /// <remarks>执行器优先使用此快照，Builder 仅保留给派生计划生成逻辑使用。</remarks>
    internal SqlBuilderExecutionSnapshot ExecutionSnapshot => _executionSnapshot;

    /// <summary>
    /// 获取当前计划的不可变查询 IR。
    /// </summary>
    internal SqlQueryIr QueryIr => _queryIr;

    /// <summary>
    /// 获取仅供 Count/Page 派生使用的 Builder 副本。
    /// </summary>
    /// <remarks>
    /// 普通执行路径不会读取该规划源；每次访问都会创建独立 Builder，调用方不能修改计划内部状态。
    /// </remarks>
    internal ISqlBuilder PlanningBuilder => _planningSource?.CreateBuilder();

    /// <summary>
    /// 创建一次派生规划使用的独立 Builder。
    /// </summary>
    /// <returns>计划包含规划源时返回其 Builder 副本，否则返回 null。</returns>
    internal ISqlBuilder CreatePlanningBuilder() => _planningSource?.CreateBuilder();

    /// <summary>
    /// 获取本次执行是否使用编译期生成的读取器与参数绑定器。
    /// </summary>
    public bool IsGeneratedExecution { get; internal set; }

    /// <summary>
    /// 获取本次执行使用的静态生成参数绑定器。
    /// </summary>
    public Action<System.Data.Common.DbCommand, IEnumerable<SqlParam>> GeneratedParameterBinder { get; internal set; }

    /// <summary>
    /// 获取当前计划的内部 Builder。
    /// </summary>
    /// <returns>当前计划使用的 Builder。</returns>
    internal ISqlBuilder GetBuilder() => Builder;

    /// <summary>
    /// 获取当前计划的查询上下文标识。
    /// </summary>
    public string QueryContextId => _queryContextId;

    /// <summary>
    /// 获取父查询上下文标识。
    /// </summary>
    public string ParentQueryContextId => _parentQueryContextId;

    /// <summary>
    /// 获取当前执行标识。
    /// </summary>
    public string ExecutionId => _executionId;

    /// <summary>
    /// 获取当前计划阶段。
    /// </summary>
    public string Phase => _phase;

    /// <summary>
    /// 设置计划的查询上下文，并用于派生 Count/Data 计划的关系传递。
    /// </summary>
    /// <param name="queryContextId">查询上下文标识。</param>
    /// <param name="parentQueryContextId">父查询上下文标识。</param>
    /// <param name="phase">计划阶段。</param>
    internal void SetContext(string queryContextId, string parentQueryContextId = null, string phase = "Data")
    {
        _queryContextId = queryContextId;
        _parentQueryContextId = parentQueryContextId;
        _phase = string.IsNullOrWhiteSpace(phase) ? "Data" : phase;
    }

    /// <summary>
    /// 从来源计划复制查询上下文，并设置当前派生计划阶段。
    /// </summary>
    /// <param name="source">来源计划。</param>
    /// <param name="phase">派生计划阶段。</param>
    internal void CopyContextFrom(SqlQueryPlan source, string phase)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SetContext(source.QueryContextId, source.ParentQueryContextId, phase);
    }

    /// <summary>
    /// 在本次计划参数绑定完成后接收输出参数访问器。
    /// </summary>
    /// <param name="receiver">接收绑定参数访问器的回调。</param>
    /// <param name="completion">数据库执行成功后创建输出参数快照的回调。</param>
    /// <remarks>
    /// 仅过程结果终结入口使用该回调；计划对象不保留任何执行后的可变状态。
    /// </remarks>
    internal void SetOutputParametersReceiver(Action<ISqlOutputParameterAccessor> receiver, Action completion = null)
    {
        _outputParametersReceiver = receiver;
        _outputParametersCompletion = completion;
    }

    /// <summary>
    /// 设置计划执行生命周期回调。
    /// </summary>
    /// <param name="started">执行开始回调。</param>
    /// <param name="completed">执行完成回调。</param>
    internal void SetLifecycleCallbacks(Action started, Action completed)
    {
        _executionStarted = started;
        _executionCompleted = completed;
    }

    /// <summary>
    /// 通知计划即将执行。
    /// </summary>
    public void NotifyExecutionStarted()
    {
        _executionId = Guid.NewGuid().ToString();
        _executionStarted?.Invoke();
    }

    /// <summary>
    /// 通知计划执行已结束。
    /// </summary>
    public void NotifyExecutionFinished() => _executionCompleted?.Invoke();

    /// <summary>
    /// 通知本次计划已完成参数绑定。
    /// </summary>
    /// <param name="outputParameters">当前绑定器提供的输出参数访问器。</param>
    public void NotifyParametersBound(ISqlOutputParameterAccessor outputParameters) =>
        _outputParametersReceiver?.Invoke(outputParameters);

    /// <summary>
    /// 通知查询计划执行已完成。
    /// </summary>
    /// <remarks>
    /// 过程输出参数必须在事务提交和成功诊断之前复制，避免快照失败被误判为成功执行。
    /// </remarks>
    public void NotifyExecutionCompleted() => _outputParametersCompletion?.Invoke();

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="fragment">待冻结的 SQL 查询片段。</param>
    /// <returns>不持有 Builder 的冻结文本查询计划。</returns>
    public static SqlQueryPlan Create(ISqlQueryFragment fragment) => Create(fragment, "Id");

    /// <summary>
    /// 从冻结查询片段创建不持有 Builder 的 SQL 查询计划。
    /// </summary>
    /// <param name="fragment">待冻结的 SQL 查询片段。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <returns>包含冻结 SQL 和参数的文本查询计划。</returns>
    public static SqlQueryPlan Create(ISqlQueryFragment fragment, string splitOn)
    {
        if (fragment == null)
            throw new ArgumentNullException(nameof(fragment));
        var snapshot = fragment.Freeze() ?? throw new InvalidOperationException("查询片段冻结结果不能为空。");
        return new SqlQueryPlan(snapshot.Sql, snapshot.Parameters, splitOn, System.Data.CommandType.Text);
    }

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <returns>仅包含该 Builder 的查询计划。</returns>
    public static SqlQueryPlan Create(ISqlBuilder builder) => new(builder, "Id");

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <returns>仅包含该 Builder 的查询计划。</returns>
    public static SqlQueryPlan Create(ISqlBuilder builder, string splitOn) => new(builder, splitOn);

    /// <summary>
    /// 创建带执行快照的 Fluent 查询计划。
    /// </summary>
    internal static SqlQueryPlan Create(ISqlBuilder builder, SqlBuilderExecutionSnapshot executionSnapshot,
        string splitOn) => new(builder, executionSnapshot, splitOn);

    /// <summary>
    /// 从不可变查询 IR 和可选规划来源创建查询计划。
    /// </summary>
    /// <param name="queryIr">不可变查询 IR。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <param name="planningBuilder">查询派生使用的隔离 Builder 来源。</param>
    /// <returns>脱离 Builder 的查询计划。</returns>
    internal static SqlQueryPlan Create(SqlQueryIr queryIr, string splitOn, ISqlBuilder planningBuilder = null) =>
        new(queryIr, splitOn, planningBuilder);

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由参数绑定器处理的参数源。</param>
    /// <returns>包含 SQL 文本和参数源的查询计划。</returns>
    public static SqlQueryPlan Create(string commandText, object parameters) =>
        new(commandText, parameters, "Id", System.Data.CommandType.Text);

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由参数绑定器处理的参数源。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <returns>包含 SQL 文本和参数源的查询计划。</returns>
    public static SqlQueryPlan Create(string commandText, object parameters, string splitOn) =>
        new(commandText, parameters, splitOn, System.Data.CommandType.Text);

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由参数绑定器处理的参数源。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <param name="commandType">当前计划使用的 ADO.NET 命令类型。</param>
    /// <returns>包含 SQL 文本和参数源的查询计划。</returns>
    public static SqlQueryPlan Create(string commandText, object parameters, string splitOn, CommandType commandType) =>
        new(commandText, parameters, splitOn, commandType);

    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="parameters">调用方提供的参数源。</param>
    /// <returns>常见可变参数容器的独立副本；不具备通用克隆语义的对象保持原引用。</returns>
    internal static object SnapshotParameters(object parameters) => SqlParameterSnapshot.Create(parameters);

    /// <summary>
    /// 规范化 Dapper 多映射分段列名称。
    /// </summary>
    /// <param name="splitOn">调用方指定的分段列名称。</param>
    /// <returns>可供 Dapper 使用的分段列名称。</returns>
    private static string NormalizeSplitOn(string splitOn)
    {
        if (string.IsNullOrWhiteSpace(splitOn))
            throw new ArgumentException("多映射分段列不能为空。", nameof(splitOn));
        return splitOn;
    }
}

/// <summary>
/// 创建 SQL 查询参数快照。
/// </summary>
/// <remarks>
/// 仅复制具有确定容器语义的字典、数组和集合；实体、动态参数和映射对象保留原引用，
/// 以维持调用方定义的转换、输出回写和 Provider 专用行为。
/// </remarks>
internal static class SqlParameterSnapshot
{
    /// <summary>
    /// 创建 SQL 查询计划。
    /// </summary>
    /// <param name="parameters">原始参数源。</param>
    /// <returns>可安全用于后续执行的参数源。</returns>
    internal static object Create(object parameters)
    {
        if (parameters is IEnumerable<SqlParam> sqlParameters)
            return sqlParameters.Where(parameter => parameter != null).Select(CloneSqlParameter).ToArray();
        if (parameters is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.ToDictionary(item => item.Key, item => SnapshotValue(item.Value));
        if (parameters is IDictionary<string, object> dictionary)
            return dictionary.ToDictionary(item => item.Key, item => SnapshotValue(item.Value));
        if (parameters is IDictionary nonGenericDictionary)
            return SnapshotDictionary(nonGenericDictionary, parameters);
        return parameters;
    }

    /// <summary>
    /// 复制可证明可变的参数值容器。
    /// </summary>
    /// <param name="value">原始参数值。</param>
    /// <returns>容器副本或原始不可泛化克隆对象。</returns>
    internal static object SnapshotValue(object value)
    {
        if (value is Array array)
            return SnapshotArray(array);
        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.ToDictionary(item => item.Key, item => SnapshotValue(item.Value));
        if (value is IDictionary<string, object> dictionary)
            return dictionary.ToDictionary(item => item.Key, item => SnapshotValue(item.Value));
        if (value is IDictionary nonGenericDictionary)
            return SnapshotDictionary(nonGenericDictionary, value);
        if (value is IEnumerable enumerable && value is not string && value is not ISqlParameterMap)
            return enumerable.Cast<object>().Select(SnapshotValue).ToArray();
        return value;
    }

    /// <summary>
    /// 复制字符串键字典；无法证明键为字符串时保留原容器。
    /// </summary>
    private static object SnapshotDictionary(IDictionary source, object original)
    {
        var snapshot = new Dictionary<string, object>();
        foreach (DictionaryEntry item in source)
        {
            if (item.Key is not string name)
                return original;
            snapshot[name] = SnapshotValue(item.Value);
        }
        return snapshot;
    }
    /// <summary>
    /// 复制一维数组及其嵌套数组元素。
    /// </summary>
    /// <param name="source">原始数组。</param>
    /// <returns>独立数组副本。</returns>
    private static Array SnapshotArray(Array source)
    {
        if (source.Rank != 1)
            return (Array)source.Clone();
        var elementType = source.GetType().GetElementType();
        // 值类型和字符串元素不需要递归快照，批量复制可避免逐项反射和装箱。
        if (elementType.IsValueType || elementType == typeof(string))
            return (Array)source.Clone();
        var result = Array.CreateInstance(elementType, source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var snapshot = SnapshotValue(source.GetValue(index));
            if (snapshot is not null && !elementType.IsInstanceOfType(snapshot))
            {
                // 递归快照可能改变元素容器类型；改用 object[] 保留完整快照值。
                var fallback = new object[source.Length];
                for (var copied = 0; copied < index; copied++)
                    fallback[copied] = result.GetValue(copied);
                fallback[index] = snapshot;
                for (var remaining = index + 1; remaining < source.Length; remaining++)
                    fallback[remaining] = SnapshotValue(source.GetValue(remaining));
                return fallback;
            }
            result.SetValue(snapshot, index);
        }
        return result;
    }

    /// <summary>
    /// 复制 SQL 参数。
    /// </summary>
    /// <param name="parameter">原始增强参数。</param>
    /// <returns>执行专属增强参数。</returns>
    internal static SqlParam CloneSqlParameter(SqlParam parameter)
        => CloneSqlParameter(parameter, parameter?.Name);

    /// <summary>
    /// 复制 SQL 参数。
    /// </summary>
    /// <param name="parameter">源参数。</param>
    /// <param name="name">目标参数名。</param>
    /// <returns>带目标名称的执行专属参数。</returns>
    internal static SqlParam CloneSqlParameter(SqlParam parameter, string name)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        return CloneSqlParameterCore(parameter, name, parameter.Value, parameter.OriginalValue, parameter.IsProviderValue);
    }

    /// <summary>复制参数元数据并绑定新值，避免复制将被覆盖的默认值。</summary>
    internal static SqlParam CloneSqlParameterWithValue(SqlParam parameter, object value)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        return CloneSqlParameterCore(parameter, parameter.Name, value, null, false);
    }

    private static SqlParam CloneSqlParameterCore(SqlParam parameter, string name, object value,
        object originalValue, bool isProviderValue)
    {
        return new SqlParam(name, SnapshotValue(value), parameter.DbType, parameter.Direction,
            parameter.Size, parameter.Precision, parameter.Scale)
        {
            OriginalValue = SnapshotValue(originalValue),
            IsProviderValue = isProviderValue,
            EntityType = parameter.EntityType,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            DatabaseType = parameter.DatabaseType,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel,
            StorageKind = parameter.StorageKind,
            ConverterKind = parameter.ConverterKind,
            CustomConverterName = parameter.CustomConverterName
        };
    }
}
