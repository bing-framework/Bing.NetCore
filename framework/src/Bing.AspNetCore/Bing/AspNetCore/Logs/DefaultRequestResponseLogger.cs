using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应记录器
/// </summary>
public class DefaultRequestResponseLogger : IRequestResponseLogger
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<RequestResponseLog> _logger;

    /// <summary>
    /// 初始化一个<see cref="DefaultRequestResponseLogger"/>类型的实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public DefaultRequestResponseLogger(ILogger<RequestResponseLog> logger) => _logger = logger;

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="logCreator">请求响应日志创建者</param>
    public void Log(IRequestResponseLogCreator logCreator) => _logger.LogDebug(logCreator.ToJsonString());
}