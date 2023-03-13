namespace Bing.SecurityLog;

/// <summary>
/// 安全日志选项配置
/// </summary>
public class BingSecurityLogOptions
{
    /// <summary>
    /// 初始化一个<see cref="BingSecurityLogOptions"/>类型的实例
    /// </summary>
    public BingSecurityLogOptions() => IsEnabled = true;

    /// <summary>
    /// 是否已启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public string ApplicationName { get; set; }
}
