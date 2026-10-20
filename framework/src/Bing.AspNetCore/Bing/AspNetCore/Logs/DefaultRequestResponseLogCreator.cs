using Bing.Utils.Json;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应日志创建者
/// </summary>
public class DefaultRequestResponseLogCreator : IRequestResponseLogCreator
{
    /// <summary>
    /// 初始化一个 <see cref="DefaultRequestResponseLogCreator"/> 类型的实例。
    /// </summary>
    public DefaultRequestResponseLogCreator() => Log = new RequestResponseLog();

    /// <inheritdoc />
    public RequestResponseLog Log { get; private set; }

    /// <inheritdoc />
    public string ToJsonString()
    {
        var jsonString = JsonHelper.ToJson(Log);
        return jsonString;
    }

}