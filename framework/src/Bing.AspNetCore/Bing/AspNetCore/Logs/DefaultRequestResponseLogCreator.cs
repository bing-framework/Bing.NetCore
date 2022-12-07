using Bing.Utils.Json;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应日志创建者
/// </summary>
public class DefaultRequestResponseLogCreator : IRequestResponseLogCreator
{
    /// <summary>
    /// 初始化一个<see cref="DefaultRequestResponseLogCreator"/>类型的实例s
    /// </summary>
    public DefaultRequestResponseLogCreator() => Log = new RequestResponseLog();

    /// <summary>
    /// 请求响应日志
    /// </summary>
    public RequestResponseLog Log { get; private set; }

    /// <summary>
    /// 输出Json字符串
    /// </summary>
    public string ToJsonString()
    {
        var jsonString = JsonHelper.ToJson(Log);
        return jsonString;
    }

}