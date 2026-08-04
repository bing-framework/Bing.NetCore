using Bing.Data.Sql.Builders;
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
    /// 创建原生 SQL 查询参数的浅快照。
    /// </summary>
    /// <param name="parameters">调用方提供的参数源。</param>
    /// <returns>字典参数的独立副本；其他参数对象保持原引用。</returns>
    internal static object SnapshotParameters(object parameters)
    {
        if (parameters is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.ToDictionary(item => item.Key, item => item.Value);
        if (parameters is IDictionary<string, object> dictionary)
            return new Dictionary<string, object>(dictionary);
        return parameters;
    }

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