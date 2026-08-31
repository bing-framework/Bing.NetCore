namespace Bing.SecurityLog;

/// <summary>
/// 创建并保存当前操作的安全日志。
/// </summary>
public interface ISecurityLogManager
{
    /// <summary>
    /// 创建安全日志、执行补充操作并提交至安全日志存储器。
    /// </summary>
    /// <param name="saveAction">在保存前补充或调整安全日志字段的可选操作。</param>
    Task SaveAsync(Action<SecurityLogInfo> saveAction = null);
}
