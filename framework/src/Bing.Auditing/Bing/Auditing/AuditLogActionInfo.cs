using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 表示一次控制器或应用服务调用的审计操作信息。
/// </summary>
/// <remarks>一个审计日志可以包含多个操作记录。</remarks>
[Serializable]
public class AuditLogActionInfo : IHasExtraProperties
{
    /// <summary>
    /// 获取或设置执行控制器或应用服务的名称。
    /// </summary>
    /// <remarks>执行的控制器/服务的名称。</remarks>
    public string ServiceName { get; set; }

    /// <summary>
    /// 获取或设置执行的方法名称。
    /// </summary>
    /// <remarks>记录被审计的控制器或应用服务方法名称。</remarks>
    public string MethodName { get; set; }

    /// <summary>
    /// 获取或设置传入方法参数的 JSON 格式化文本。
    /// </summary>
    /// <remarks>参数以 JSON 文本保存；敏感数据是否脱敏由产生审计信息的上游组件负责。</remarks>
    public string Parameters { get; set; }

    /// <summary>
    /// 获取或设置操作开始执行的时间。
    /// </summary>
    public DateTime ExecutionTime { get; set; }

    /// <summary>
    /// 获取或设置操作执行时长，单位为毫秒。
    /// </summary>
    /// <remarks>方法执行时长，以毫秒为单位，可以用来观察方法的性能。</remarks>
    public int ExecutionDuration { get; set; }
}
