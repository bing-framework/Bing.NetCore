namespace Bing.SecurityLog;

/// <summary>
/// 配置安全日志的启用状态和应用程序标识。
/// </summary>
public class BingSecurityLogOptions
{
    /// <summary>
    /// 初始化一个 <see cref="BingSecurityLogOptions"/> 类型的实例。
    /// </summary>
    public BingSecurityLogOptions() => IsEnabled = true;

    /// <summary>
    /// 获取或设置是否启用安全日志记录。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>true</c>。
    /// </remarks>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置写入安全日志的应用程序名称。
    /// </summary>
    /// <remarks>
    /// 用于区分共享日志存储中的来源。
    /// </remarks>
    public string ApplicationName { get; set; }
}
