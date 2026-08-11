using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using System.Collections;
using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 独立 SQL 查询描述的内部执行计划。
/// </summary>
/// <remarks>
/// 计划只保存查询输入，不保存连接、事务、诊断或执行状态；这些状态始终由创建它的根查询管理。
/// </remarks>
internal sealed class SqlQueryPlan
{
    /// <summary>
    /// 当前计划本次参数绑定完成后的输出参数访问器接收器。
    /// </summary>
    private Action<ISqlOutputParameterAccessor> _outputParametersReceiver;

    /// <summary>
    /// 当前计划执行成功后创建输出参数快照的回调。
    /// </summary>
    private Action _outputParametersCompletion;

    /// <summary>
    /// 使用 Fluent SQL Builder 创建查询计划。
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
    /// 使用原生 SQL 文本和参数源创建查询计划。
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
        Parameters = SnapshotParameters(parameters);
        SplitOn = NormalizeSplitOn(splitOn);
        CommandType = commandType;
    }

    /// <summary>
    /// 当前计划使用的 Fluent SQL Builder；原生文本计划为 null。
    /// </summary>
    public ISqlBuilder Builder { get; }

    /// <summary>
    /// 当前计划使用的原生 SQL 文本；Fluent 计划为 null。
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// 当前原生 SQL 文本计划的参数源；Fluent 计划为 null。
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    /// 获取 Dapper 多映射使用的分段列名称。
    /// </summary>
    public string SplitOn { get; }

    /// <summary>
    /// 获取当前计划对应的 ADO.NET 命令类型。
    /// </summary>
    public CommandType CommandType { get; }

    /// <summary>
    /// 指示当前计划是否使用 Fluent SQL Builder。
    /// </summary>
    public bool IsBuilderPlan => Builder != null;

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
    /// 通知本次计划已完成参数绑定。
    /// </summary>
    /// <param name="outputParameters">当前绑定器提供的输出参数访问器。</param>
    internal void NotifyParametersBound(ISqlOutputParameterAccessor outputParameters) =>
        _outputParametersReceiver?.Invoke(outputParameters);

    /// <summary>
    /// 通知当前计划已成功完成数据库执行。
    /// </summary>
    /// <remarks>
    /// 过程输出参数必须在事务提交和成功诊断之前复制，避免快照失败被误判为成功执行。
    /// </remarks>
    internal void NotifyExecutionCompleted() => _outputParametersCompletion?.Invoke();

    /// <summary>
    /// 创建 Fluent SQL Builder 查询计划。
    /// </summary>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <returns>仅包含该 Builder 的查询计划。</returns>
    public static SqlQueryPlan Create(ISqlBuilder builder, string splitOn = "Id") => new(builder, splitOn);

    /// <summary>
    /// 创建原生 SQL 文本查询计划。
    /// </summary>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由参数绑定器处理的参数源。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <param name="commandType">当前计划使用的 ADO.NET 命令类型。</param>
    /// <returns>包含 SQL 文本和参数源的查询计划。</returns>
    public static SqlQueryPlan Create(string commandText, object parameters, string splitOn = "Id",
        CommandType commandType = System.Data.CommandType.Text) => new(commandText, parameters, splitOn, commandType);

    /// <summary>
    /// 创建原生 SQL 查询参数快照。
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
/// SQL 描述的参数快照帮助器。
/// </summary>
/// <remarks>
/// 仅复制具有确定容器语义的字典、数组和集合；实体、动态参数和映射对象保留原引用，
/// 以维持调用方定义的转换、输出回写和 Provider 专用行为。
/// </remarks>
internal static class SqlParameterSnapshot
{
    /// <summary>
    /// 创建参数源快照。
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
        if (value is IEnumerable enumerable && value is not string && value is not ISqlParameterMap)
            return enumerable.Cast<object>().Select(SnapshotValue).ToArray();
        return value;
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
        var result = Array.CreateInstance(elementType, source.Length);
        for (var index = 0; index < source.Length; index++)
            result.SetValue(SnapshotValue(source.GetValue(index)), index);
        return result;
    }

    /// <summary>
    /// 复制增强参数及其可变值。
    /// </summary>
    /// <param name="parameter">原始增强参数。</param>
    /// <returns>执行专属增强参数。</returns>
    private static SqlParam CloneSqlParameter(SqlParam parameter) => SqlMutationParameter.Create(parameter)
        .CreateSqlParam();
}