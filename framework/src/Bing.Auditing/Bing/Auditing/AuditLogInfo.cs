using System.Text;
using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 审计日志信息
/// </summary>
[Serializable]
public class AuditLogInfo : IHasExtraProperties
{
    /// <summary>
    /// 初始化一个<see cref="AuditLogInfo"/>类型的实例
    /// </summary>
    public AuditLogInfo()
    {
        Actions = new List<AuditLogActionInfo>();
        Exceptions = new List<Exception>();
        EntityChanges = new List<EntityChangeInfo>();
        Comments = new List<string>();
    }

    /// <summary>
    /// 应用程序名称
    /// </summary>
    /// <remarks>当你保存不同的应用审计日志到同一个数据库时，这个属性用来区分应用程序。</remarks>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    /// <remarks>当前用户的ID，用户未登录为<c>null</c>。</remarks>
    public string UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    /// <remarks>当前用户的用户名，如果用户已经登录</remarks>
    public string UserName { get; set; }

    /// <summary>
    /// 租户ID
    /// </summary>
    /// <remarks>当前租户的ID，对于多租户应用。</remarks>
    public string TenantId { get; set; }

    /// <summary>
    /// 租户名称
    /// </summary>
    /// <remarks>当前的租户的名称，对于多租户应用。</remarks>
    public string TenantName { get; set; }

    /// <summary>
    /// 模仿用户ID
    /// </summary>
    public string ImpersonatorUserId { get; set; }

    /// <summary>
    /// 模仿租户ID
    /// </summary>
    public string ImpersonatorTenantId { get; set; }

    /// <summary>
    /// 模仿用户名
    /// </summary>
    public string ImpersonatorUserName { get; set; }

    /// <summary>
    /// 模仿租户名
    /// </summary>
    public string ImpersonatorTenantName { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    /// <remarks>审计日志对象创建的时间。</remarks>
    public DateTime ExecutionTime { get; set; }

    /// <summary>
    /// 执行持续时间
    /// </summary>
    /// <remarks>请求的总执行时间，以毫秒为单位，可以用来观察应用程序的性能。</remarks>
    public int ExecutionDuration { get; set; }

    /// <summary>
    /// 客户端ID
    /// </summary>
    /// <remarks>当前客户端的ID，如果客户端已经通过认证，客户端通常是使用HTTP API的第三方应用程序。</remarks>
    public string ClientId { get; set; }

    /// <summary>
    /// 客户端名称
    /// </summary>
    /// <remarks>当前客户端的名称，如果有的话。</remarks>
    public string ClientName { get; set; }

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    /// <remarks>客户端/用户设备的IP地址。</remarks>
    public string ClientIpAddress { get; set; }

    /// <summary>
    /// 关联ID
    /// </summary>
    /// <remarks>当前关联ID，关联ID用于在单个逻辑操作中关联由不同应用程序（或微服务）写入的审计日志。</remarks>
    public string CorrelationId { get; set; }

    /// <summary>
    /// 浏览器信息
    /// </summary>
    /// <remarks>当前用户的浏览器名称/版本信息，如果有的话。</remarks>
    public string BrowserInfo { get; set; }

    /// <summary>
    /// HTTP方法
    /// </summary>
    /// <remarks>当前HTTP请求的方法（GET,POST,PUT,DELETE...等）。</remarks>
    public string HttpMethod { get; set; }

    /// <summary>
    /// HTTP状态码
    /// </summary>
    /// <remarks>HTTP响应状态码。</remarks>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// 网址
    /// </summary>
    /// <remarks>请求的URL。</remarks>
    public string Url { get; set; }

    /// <summary>
    /// 操作信息日志列表
    /// </summary>
    /// <remarks>一个审计日志操作通常是WEB请求期间控制器操作或应用服务方法调用，一个审计日志可以包含多个操作。</remarks>
    public List<AuditLogActionInfo> Actions { get; set; }

    /// <summary>
    /// 异常列表
    /// </summary>
    /// <remarks>审计日志对象可能包含零个或多个异常，可以得到失败请求的异常信息。</remarks>
    public List<Exception> Exceptions { get; set; }

    /// <summary>
    /// 实体变更信息列表
    /// </summary>
    public List<EntityChangeInfo> EntityChanges { get; set; }

    /// <summary>
    /// 注释列表
    /// </summary>
    /// <remarks>用于将自定义消息添加到审计日志条目的任意字符串值。审计日志对象可能包含零个或多个注释。</remarks>
    public List<string> Comments { get; set; }

    /// <summary>
    /// 输出字符串
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"AUDIT LOG: [{HttpStatusCode?.ToString() ?? "---"}: {(HttpMethod ?? "-------"),-7}] {Url}");
        sb.AppendLine($"- UserName - UserId                 : {UserName} - {UserId}");
        sb.AppendLine($"- ClientIpAddress        : {ClientIpAddress}");
        sb.AppendLine($"- ExecutionDuration      : {ExecutionDuration}");

        if (Actions.Any())
        {
            sb.AppendLine("- Actions:");
            foreach (var action in Actions)
            {
                sb.AppendLine($"  - {action.ServiceName}.{action.MethodName} ({action.ExecutionDuration} ms.)");
                sb.AppendLine($"    {action.Parameters}");
            }
        }

        if (Exceptions.Any())
        {
            sb.AppendLine("- Exceptions:");
            foreach (var exception in Exceptions)
            {
                sb.AppendLine($"  - {exception.Message}");
                sb.AppendLine($"    {exception}");
            }
        }

        if (EntityChanges.Any())
        {
            sb.AppendLine("- Entity Changes:");
            foreach (var entityChange in EntityChanges)
            {
                sb.AppendLine($"  - [{entityChange.ChangeType}] {entityChange.EntityTypeFullName}, Id = {entityChange.EntityId}");
                foreach (var propertyChange in entityChange.PropertyChanges)
                {
                    sb.AppendLine($"    {propertyChange.PropertyName}: {propertyChange.OriginalValue} -> {propertyChange.NewValue}");
                }
            }
        }
        return sb.ToString();
    }
}
