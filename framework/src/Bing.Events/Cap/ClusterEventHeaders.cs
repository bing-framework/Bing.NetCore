using System.Globalization;
using System.Text;
using Bing.Events.Messages;

namespace Bing.Events.Cap;

/// <summary>
/// 提供 CAP 消息中的业务事件元数据名称与解析。
/// </summary>
public static class ClusterEventHeaders
{
    /// <summary>
    /// 事件标识的最大字符数。
    /// </summary>
    private const int MaxEventIdLength = 128;

    /// <summary>
    /// 租户标识的最大字符数。
    /// </summary>
    private const int MaxTenantIdLength = 200;

    /// <summary>
    /// 业务流名称的最大字符数。
    /// </summary>
    private const int MaxStreamLength = 200;

    /// <summary>
    /// 实体标识的最大字符数。
    /// </summary>
    private const int MaxEntityKeyLength = 200;

    /// <summary>
    /// 消息契约版本的最大字符数。
    /// </summary>
    private const int MaxContractVersionLength = 100;

    /// <summary>
    /// 跟踪标识的最大字符数。
    /// </summary>
    private const int MaxTraceIdLength = 128;

    /// <summary>
    /// 连续序号消息头的最大字符数。
    /// </summary>
    private const int MaxSequenceLength = 20;

    /// <summary>
    /// 单条消息头集合的最大 UTF-8 字节数。
    /// </summary>
    private const int MaxHeaderBytes = 16 * 1024;

    /// <summary>
    /// 事件标识消息头。
    /// </summary>
    public const string EventId = "bing-event-id";

    /// <summary>
    /// 租户标识消息头。
    /// </summary>
    public const string TenantId = "bing-event-tenant";

    /// <summary>
    /// 业务流名称消息头。
    /// </summary>
    public const string Stream = "bing-event-stream";

    /// <summary>
    /// 实体标识消息头。
    /// </summary>
    public const string EntityKey = "bing-event-entity";

    /// <summary>
    /// 连续序号消息头。
    /// </summary>
    public const string Sequence = "bing-event-sequence";

    /// <summary>
    /// 消息契约版本消息头。
    /// </summary>
    public const string ContractVersion = "bing-event-version";

    /// <summary>
    /// 跟踪标识消息头。
    /// </summary>
    public const string TraceId = "bing-trace-id";

    /// <summary>
    /// 将可选事件元数据写入 CAP 消息头。
    /// </summary>
    public static void Write(IDictionary<string, string> headers, IClusterEventMetadata metadata)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrWhiteSpace(metadata.Id))
            throw new ArgumentException("事件标识不能为空。", nameof(metadata));
        ValidateWriteLength(metadata.Id, MaxEventIdLength, nameof(metadata.Id));
        ValidateWriteLength(metadata.TenantId, MaxTenantIdLength, nameof(metadata.TenantId));
        ValidateWriteLength(metadata.Stream, MaxStreamLength, nameof(metadata.Stream));
        ValidateWriteLength(metadata.EntityKey, MaxEntityKeyLength, nameof(metadata.EntityKey));
        ValidateWriteLength(metadata.ContractVersion, MaxContractVersionLength, nameof(metadata.ContractVersion));
        ValidateWriteLength(metadata.TraceId, MaxTraceIdLength, nameof(metadata.TraceId));
        if (metadata.Sequence.HasValue && (metadata.Sequence.Value <= 0 ||
            string.IsNullOrWhiteSpace(metadata.Stream) || string.IsNullOrWhiteSpace(metadata.EntityKey)))
            throw new ArgumentException("有序事件需要正序号、业务流与实体标识。", nameof(metadata));

        headers[EventId] = metadata.Id;
        SetIfPresent(headers, TenantId, metadata.TenantId);
        SetIfPresent(headers, Stream, metadata.Stream);
        SetIfPresent(headers, EntityKey, metadata.EntityKey);
        SetIfPresent(headers, ContractVersion, metadata.ContractVersion);
        SetIfPresent(headers, TraceId, metadata.TraceId);
        if (metadata.Sequence.HasValue)
            headers[Sequence] = metadata.Sequence.Value.ToString(CultureInfo.InvariantCulture);
        ValidateWriteHeaderSize(headers);
    }

    /// <summary>
    /// 从消息头读取可选事件元数据。
    /// </summary>
    public static ClusterEventMetadata Read(IReadOnlyDictionary<string, string> headers)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));
        if (!headers.TryGetValue(EventId, out var id))
            return null;
        ValidateReadHeaderSize(headers);
        if (string.IsNullOrWhiteSpace(id))
            throw new FormatException("事件标识消息头不能为空。");
        ValidateReadLength(id, MaxEventIdLength, EventId);

        long? sequence = null;
        if (headers.TryGetValue(Sequence, out var value))
        {
            if (value != null && value.Length > MaxSequenceLength)
                throw new FormatException($"消息头 {Sequence} 的值不能超过 {MaxSequenceLength} 个字符。");
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                throw new FormatException("事件序号消息头无效。");
            sequence = parsed;
        }
        var tenantId = Get(headers, TenantId);
        var stream = Get(headers, Stream);
        var entityKey = Get(headers, EntityKey);
        var contractVersion = Get(headers, ContractVersion);
        var traceId = Get(headers, TraceId);
        ValidateReadLength(tenantId, MaxTenantIdLength, TenantId);
        ValidateReadLength(stream, MaxStreamLength, Stream);
        ValidateReadLength(entityKey, MaxEntityKeyLength, EntityKey);
        ValidateReadLength(contractVersion, MaxContractVersionLength, ContractVersion);
        ValidateReadLength(traceId, MaxTraceIdLength, TraceId);
        var result = new ClusterEventMetadata(id, tenantId, stream, entityKey, sequence, contractVersion, traceId);
        if (sequence.HasValue && (string.IsNullOrWhiteSpace(result.Stream) || string.IsNullOrWhiteSpace(result.EntityKey)))
            throw new FormatException("有序事件缺少业务流或实体标识。");
        return result;
    }

    /// <summary>
    /// 写入非空消息头。
    /// </summary>
    private static void SetIfPresent(IDictionary<string, string> headers, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            headers[name] = value;
    }

    /// <summary>
    /// 读取可选消息头。
    /// </summary>
    private static string Get(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// 验证本地发布的元数据长度。
    /// </summary>
    private static void ValidateWriteLength(string value, int maxLength, string name)
    {
        if (value != null && value.Length > maxLength)
            throw new ArgumentException($"消息元数据不能超过 {maxLength} 个字符。", name);
    }

    /// <summary>
    /// 验证远端消息头的元数据长度。
    /// </summary>
    private static void ValidateReadLength(string value, int maxLength, string name)
    {
        if (value != null && value.Length > maxLength)
            throw new FormatException($"消息头 {name} 的值不能超过 {maxLength} 个字符。");
    }

    /// <summary>
    /// 验证远端消息头集合的总大小。
    /// </summary>
    private static void ValidateReadHeaderSize(IReadOnlyDictionary<string, string> headers)
    {
        if (GetHeaderBytes(headers) > MaxHeaderBytes)
            throw new FormatException($"消息头集合不能超过 {MaxHeaderBytes} 个 UTF-8 字节。");
    }

    /// <summary>
    /// 验证本地发布的消息头集合总大小。
    /// </summary>
    private static void ValidateWriteHeaderSize(IDictionary<string, string> headers)
    {
        if (GetHeaderBytes(headers) > MaxHeaderBytes)
            throw new ArgumentException($"消息头集合不能超过 {MaxHeaderBytes} 个 UTF-8 字节。", nameof(headers));
    }

    /// <summary>
    /// 计算消息头集合的 UTF-8 字节数。
    /// </summary>
    private static long GetHeaderBytes(IEnumerable<KeyValuePair<string, string>> headers)
    {
        long bytes = 0;
        foreach (var pair in headers)
        {
            bytes += Encoding.UTF8.GetByteCount(pair.Key ?? string.Empty);
            if (pair.Value != null)
                bytes += Encoding.UTF8.GetByteCount(pair.Value);
            if (bytes > MaxHeaderBytes)
                return bytes;
        }

        return bytes;
    }
}
