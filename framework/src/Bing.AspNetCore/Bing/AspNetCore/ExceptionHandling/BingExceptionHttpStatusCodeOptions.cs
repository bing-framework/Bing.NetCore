using System.Net;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 配置异常错误码到 HTTP 状态码的映射及全局状态码策略。
/// </summary>
public class BingExceptionHttpStatusCodeOptions
{
    /// <summary>
    /// 获取错误码到 HTTP 状态码的映射字典。
    /// </summary>
    /// <remarks>该字典在构造时初始化，调用方可通过 <see cref="Map"/> 添加或覆盖映射。</remarks>
    public IDictionary<string, HttpStatusCode> ErrorCodeToHttpStatusCodeMappings { get; }

    /// <summary>
    /// 获取或设置是否对所有异常响应统一返回 HTTP 200，默认值为 <c>true</c>。
    /// </summary>
    public bool GlobalHttpStatusCode200 { get; set; } = true;

    /// <summary>
    /// 初始化 <see cref="BingExceptionHttpStatusCodeOptions"/> 的实例及空映射字典。
    /// </summary>
    public BingExceptionHttpStatusCodeOptions() => ErrorCodeToHttpStatusCodeMappings = new Dictionary<string, HttpStatusCode>();

    /// <summary>
    /// 添加或覆盖指定异常错误码对应的 HTTP 状态码映射。
    /// </summary>
    /// <param name="errorCode">异常错误码。</param>
    /// <param name="httpStatusCode">异常响应使用的 HTTP 状态码。</param>
    public void Map(string errorCode, HttpStatusCode httpStatusCode) => ErrorCodeToHttpStatusCodeMappings[errorCode] = httpStatusCode;
}