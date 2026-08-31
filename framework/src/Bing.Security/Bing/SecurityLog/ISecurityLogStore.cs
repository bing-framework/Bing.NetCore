namespace Bing.SecurityLog;

/// <summary>
/// 定义安全日志记录的存储目标。
/// </summary>
public interface ISecurityLogStore
{
    /// <summary>
    /// 异步保存已构建的安全日志记录。
    /// </summary>
    /// <param name="securityLogInfo">要保存的安全日志记录。</param>
    Task SaveAsync(SecurityLogInfo securityLogInfo);
}
