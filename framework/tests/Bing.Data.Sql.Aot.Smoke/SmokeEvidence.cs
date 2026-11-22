using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bing.Data.Sql.Diagnostics;

/// <summary>
/// 记录实际 SQL 执行与物化结果的 AOT 冒烟证据。
/// </summary>
/// <remarks>
/// 记录实际执行前的 SQL/参数及完整结果对象；使用静态 JSON 元数据供 Native AOT 使用。
/// </remarks>
public sealed class SmokeEvidence : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>, IDisposable
{
    /// <summary>
    /// 保存冒烟证据收集的订阅句柄。
    /// </summary>
    private readonly List<IDisposable> _subscriptions = new();
    /// <summary>
    /// 获取执行操作的累计次数。
    /// </summary>
    public int Executions { get; private set; }
    /// <inheritdoc />
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
            _subscriptions.Add(listener.Subscribe(this, name => name == SqlQueryDiagnosticListenerNames.BeforeExecute));
    }
    /// <inheritdoc />
    public void OnNext(KeyValuePair<string, object> entry)
    {
        if (entry.Key != SqlQueryDiagnosticListenerNames.BeforeExecute || entry.Value is not DiagnosticsMessage message) return;
        var parameters = message.Parameters.Items.OrderBy(item => item.Name, StringComparer.Ordinal).Select(item =>
            new ParameterSnapshot(item.Name, item.Value?.GetType().FullName, Format(item.Value), item.DbType?.ToString(),
                item.Direction?.ToString(), item.Size, item.Precision, item.Scale)).ToArray();
        Console.WriteLine("EXECUTION: " + JsonSerializer.Serialize(new ExecutionSnapshot(message.Sql, parameters),
            SmokeJsonContext.Default.ExecutionSnapshot));
        Executions++;
    }
    /// <summary>
    /// 将冒烟证据值转换为稳定的文本格式。
    /// </summary>
    /// <param name="value">需要格式化或作为 SQL 参数的测试值。</param>
    /// <returns>规范化的证据文本；输入为 null 或 DBNull 时返回 null。</returns>
    private static string Format(object value) => value switch
    {
        null or DBNull => null,
        byte[] bytes => Convert.ToHexString(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
    /// <summary>
    /// 输出订单查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Orders(string name, List<Order> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListOrder));
    /// <summary>
    /// 输出汇总查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Summaries(string name, List<Summary> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListSummary));
    /// <summary>
    /// 输出二进制查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Binaries(string name, List<BinaryRow> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListBinaryRow));
    /// <summary>
    /// 输出名称查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Names(string name, List<NameResult> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListNameResult));
    /// <summary>
    /// 输出装箱汇总查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Boxed(string name, List<BoxedSummary> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListBoxedSummary));
    /// <summary>
    /// 输出转义名称查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Escaped(string name, List<EscapedSummary> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListEscapedSummary));
    /// <summary>
    /// 输出浮点汇总查询的冒烟证据。
    /// </summary>
    /// <param name="name">输出证据对应的测试场景名称。</param>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    public static void Floating(string name, List<FloatingSummary> rows) =>
        Console.WriteLine("RESULT " + name + ": " + JsonSerializer.Serialize(rows, SmokeJsonContext.Default.ListFloatingSummary));
    /// <inheritdoc />
    public void OnCompleted() { }
    /// <inheritdoc />
    public void OnError(Exception error) => throw error;
    /// <inheritdoc />
    public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
}
/// <summary>
/// 记录 SQL 参数在 AOT smoke 测试中的可观察属性。
/// </summary>
/// <param name="Name">名称。</param>
/// <param name="ClrType">参数值的 CLR 类型名称。</param>
/// <param name="Value">参数或响应携带的值。</param>
/// <param name="DbType">数据库参数类型。</param>
/// <param name="Direction">参数传递方向。</param>
/// <param name="Size">参数大小设置。</param>
/// <param name="Precision">参数精度设置。</param>
/// <param name="Scale">参数小数位数设置。</param>
public sealed record ParameterSnapshot(string Name, string ClrType, string Value, string DbType,
    string Direction, int? Size, byte? Precision, byte? Scale);
/// <summary>
/// 记录一次 SQL 执行语句及其参数快照。
/// </summary>
/// <param name="Sql">SQL 语句文本。</param>
/// <param name="Parameters">SQL 语句使用的参数集合。</param>
public sealed record ExecutionSnapshot(string Sql, ParameterSnapshot[] Parameters);

/// <summary>
/// 为 AOT smoke 测试提供静态 JSON 序列化元数据。
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ExecutionSnapshot))]
[JsonSerializable(typeof(List<Order>))]
[JsonSerializable(typeof(List<Summary>))]
[JsonSerializable(typeof(List<BinaryRow>))]
[JsonSerializable(typeof(List<NameResult>))]
[JsonSerializable(typeof(List<BoxedSummary>))]
[JsonSerializable(typeof(List<EscapedSummary>))]
[JsonSerializable(typeof(List<FloatingSummary>))]
[JsonSerializable(typeof(long))]
internal partial class SmokeJsonContext : JsonSerializerContext { }
