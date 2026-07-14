using Bing.Utils.Json;

namespace Bing.Logging.Hangfire;

/// <summary>
/// Hangfire日志上下文传输数据
/// </summary>
internal sealed class HangfireLogContextData
{
    public string TraceId { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public string SessionId { get; set; }
    public string Application { get; set; }
    public string Environment { get; set; }
    public string Ip { get; set; }
    public string Host { get; set; }
    public string Browser { get; set; }
    public string Url { get; set; }
    public string BusinessTraceId { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, object> Data { get; set; }

    public static string Serialize(LogContextSnapshot snapshot)
    {
        if (snapshot == null)
            return null;
        return new HangfireLogContextData
        {
            TraceId = snapshot.TraceId,
            UserId = snapshot.Identity.UserId,
            TenantId = snapshot.Identity.TenantId,
            SessionId = snapshot.Identity.SessionId,
            Application = snapshot.Client.Application,
            Environment = snapshot.Client.Environment,
            Ip = snapshot.Client.Ip,
            Host = snapshot.Client.Host,
            Browser = snapshot.Client.Browser,
            Url = snapshot.Client.Url,
            BusinessTraceId = snapshot.Business.BusinessTraceId,
            Tags = snapshot.Business.Tags.ToList(),
            Data = snapshot.Business.Data.ToDictionary(x => x.Key, x => x.Value)
        }.ToJson();
    }

    public static LogContextSnapshot Deserialize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            var data = JsonHelper.ToObject<HangfireLogContextData>(value);
            if (data == null)
                return null;
            return new LogContextSnapshot(
                data.TraceId,
                new LogIdentityContext(data.UserId, data.TenantId, data.SessionId),
                new LogClientContext(data.Application, data.Environment, data.Ip, data.Host, data.Browser, data.Url),
                new BusinessLogContext(data.BusinessTraceId, data.Tags, data.Data));
        }
        catch
        {
            return null;
        }
    }
}