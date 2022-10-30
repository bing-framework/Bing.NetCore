namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应记录器
/// </summary>
public interface IRequestResponseLogger
{
    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="logCreator">请求响应日志创建者</param>
    void Log(IRequestResponseLogCreator logCreator);
}