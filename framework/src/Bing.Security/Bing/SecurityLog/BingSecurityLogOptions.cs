namespace Bing.SecurityLog;

/// <summary>
/// 配置安全日志的启用状态和应用程序标识。
/// </summary>
public class BingSecurityLogOptions
{
    /// <summary>
    /// 初始化 <see cref="BingSecurityLogOptions"/> 的实例，并默认启用安全日志。
    /// </summary>
    public BingSecurityLogOptions() => IsEnabled = true;

    /// <summary>
    /// 获取或设置是否启用安全日志记录，默认值为 <c>true</c>。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置写入安全日志的应用程序名称，用于区分共享日志存储中的来源。
    /// </summary>
    public string ApplicationName { get; set; }
}
