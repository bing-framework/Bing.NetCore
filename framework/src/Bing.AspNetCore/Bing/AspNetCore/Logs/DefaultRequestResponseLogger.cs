using Bing.Extensions;
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
    public void Log(IRequestResponseLogCreator logCreator)
    {
        if (_logger.IsEnabled(LogLevel.Trace) == false)
            return;
        var dict = new Dictionary<string, object>
        {
            { "RequestBody", logCreator.Log.RequestBody } ,
            { "ResponseBody", logCreator.Log.ResponseBody } ,
            { "RequestHeaders", logCreator.Log.RequestHeaders } ,
            { "ResponseHeaders", logCreator.Log.ResponseHeaders } ,
            { "ExceptionStackTrace", logCreator.Log.ExceptionStackTrace } ,

        };
        using (_logger.BeginScope(dict))
        {
            if (logCreator.Log.IsExceptionActionLevel.HasValue && logCreator.Log.IsExceptionActionLevel.SafeValue())
            {
                _logger.LogError("{RequestDateTime:yyyy-MM-dd HH:mm:ss.fff} | {Node} | {ClientIp} | {RequestMethod} | {RequestPath}{RequestQuery} | {ExceptionMessage} | {ResponseDateTime:yyyy-MM-dd HH:mm:ss.fff}",
                    logCreator.Log.RequestDateTimeUtc.SafeValue().ToLocalTime(),
                    logCreator.Log.Node,
                    logCreator.Log.ClientIp,
                    logCreator.Log.RequestMethod,
                    logCreator.Log.RequestPath,
                    logCreator.Log.RequestQuery,
                    logCreator.Log.ExceptionMessage,
                    logCreator.Log.ResponseDateTimeUtc.SafeValue().ToLocalTime());
            }
            else
            {
                _logger.LogTrace("{RequestDateTime:yyyy-MM-dd HH:mm:ss.fff} | {Node} | {ClientIp} | {RequestMethod} | {RequestPath}{RequestQuery} | {ResponseDateTime:yyyy-MM-dd HH:mm:ss.fff}",
                    logCreator.Log.RequestDateTimeUtc.SafeValue().ToLocalTime(),
                    logCreator.Log.Node,
                    logCreator.Log.ClientIp,
                    logCreator.Log.RequestMethod,
                    logCreator.Log.RequestPath,
                    logCreator.Log.RequestQuery,
                    logCreator.Log.ResponseDateTimeUtc.SafeValue().ToLocalTime());
            }
        }
    }
}
