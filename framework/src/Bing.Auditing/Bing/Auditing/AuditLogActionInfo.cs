using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 审计日志 - 操作信息
/// </summary>
/// <remarks>一个审计日志操作通常是WEB请求期间控制器操作或应用服务方法调用，一个审计日志可以包含多个操作。</remarks>
[Serializable]
public class AuditLogActionInfo : IHasExtraProperties
{
    /// <summary>
    /// 服务名称
    /// </summary>
    /// <remarks>执行的控制器/服务的名称。</remarks>
    public string ServiceName { get; set; }

    /// <summary>
    /// 方法名称
    /// </summary>
    /// <remarks>控制器/服务执行的方法的名称</remarks>
    public string MethodName { get; set; }

    /// <summary>
    /// 参数
    /// </summary>
    /// <remarks>传递给方法的参数的JSON格式化文本。</remarks>
    public string Parameters { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutionTime { get; set; }

    /// <summary>
    /// 执行持续时间
    /// </summary>
    /// <remarks>方法执行时长，以毫秒为单位，可以用来观察方法的性能。</remarks>
    public int ExecutionDuration { get; set; }
}
