using System.Collections.Generic;
using System.Net;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 异常Http状态码选项配置
/// </summary>
public class BingExceptionHttpStatusCodeOptions
{
    /// <summary>
    /// 错误码转Http状态码映射字典
    /// </summary>
    public IDictionary<string, HttpStatusCode> ErrorCodeToHttpStatusCodeMappings { get; }

    /// <summary>
    /// 全局状态码200
    /// </summary>
    public bool GlobalHttpStatusCode200 { get; set; } = true;

    /// <summary>
    /// 初始化一个<see cref="BingExceptionHttpStatusCodeOptions"/>类型的实例
    /// </summary>
    public BingExceptionHttpStatusCodeOptions() => ErrorCodeToHttpStatusCodeMappings = new Dictionary<string, HttpStatusCode>();

    /// <summary>
    /// 配置映射
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="httpStatusCode">Http状态码</param>
    public void Map(string errorCode, HttpStatusCode httpStatusCode) => ErrorCodeToHttpStatusCodeMappings[errorCode] = httpStatusCode;
}