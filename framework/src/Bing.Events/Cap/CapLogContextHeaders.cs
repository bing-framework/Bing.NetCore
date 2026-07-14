using Bing.Logging;
using Bing.Utils.Json;

namespace Bing.Events.Cap;

/// <summary>
/// CAP日志上下文消息头转换器
/// </summary>
internal static class CapLogContextHeaders
{
    /// <summary>
    /// 写入日志上下文消息头
    /// </summary>
    public static void Write(IDictionary<string, string> headers, LogContextSnapshot snapshot)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));
        if (snapshot == null)
            return;
        Add(headers, Headers.TraceId, snapshot.TraceId);
        Add(headers, Headers.UserId, snapshot.Identity.UserId);
        Add(headers, Headers.TenantId, snapshot.Identity.TenantId);
        Add(headers, Headers.SessionId, snapshot.Identity.SessionId);
        Add(headers, Headers.Application, snapshot.Client.Application);
        Add(headers, Headers.Environment, snapshot.Client.Environment);
        Add(headers, Headers.ClientIp, snapshot.Client.Ip);
        Add(headers, Headers.Host, snapshot.Client.Host);
        Add(headers, Headers.Browser, snapshot.Client.Browser);
        Add(headers, Headers.Url, snapshot.Client.Url);
        Add(headers, Headers.BusinessTraceId, snapshot.Business.BusinessTraceId);
        if (snapshot.Business.Tags.Count > 0)
            Add(headers, Headers.Tags, snapshot.Business.Tags.ToJson());
        if (snapshot.Business.Data.Count > 0)
            Add(headers, Headers.Data, snapshot.Business.Data.ToJson());
    }

    /// <summary>
    /// 从日志上下文消息头创建快照
    /// </summary>
    public static LogContextSnapshot Read(IDictionary<string, string> headers, string fallbackTraceId)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));
        var identity = new LogIdentityContext(
            Get(headers, Headers.UserId),
            Get(headers, Headers.TenantId),
            Get(headers, Headers.SessionId));
        var client = new LogClientContext(
            Get(headers, Headers.Application),
            Get(headers, Headers.Environment),
            Get(headers, Headers.ClientIp),
            Get(headers, Headers.Host),
            Get(headers, Headers.Browser),
            Get(headers, Headers.Url));
        var business = new BusinessLogContext(
            Get(headers, Headers.BusinessTraceId),
            Parse<List<string>>(Get(headers, Headers.Tags)),
            Parse<Dictionary<string, object>>(Get(headers, Headers.Data)));
        return new LogContextSnapshot(Get(headers, Headers.TraceId) ?? fallbackTraceId, identity, client, business);
    }

    private static void Add(IDictionary<string, string> headers, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !headers.ContainsKey(name))
            headers[name] = value;
    }

    private static string Get(IDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static T Parse<T>(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;
        try
        {
            return JsonHelper.ToObject<T>(value);
        }
        catch
        {
            return default;
        }
    }
}