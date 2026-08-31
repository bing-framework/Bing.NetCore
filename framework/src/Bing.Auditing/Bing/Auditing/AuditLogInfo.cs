using System.Text;
using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 聚合一次应用操作的主体、请求、异常和实体变更审计信息。
/// </summary>
[Serializable]
public class AuditLogInfo : IHasExtraProperties
{
    /// <summary>
    /// 初始化 <see cref="AuditLogInfo"/> 的实例及各审计明细集合。
    /// </summary>
    public AuditLogInfo()
    {
        Actions = new List<AuditLogActionInfo>();
        Exceptions = new List<Exception>();
        EntityChanges = new List<EntityChangeInfo>();
        Comments = new List<string>();
    }

    /// <summary>
    /// 获取或设置写入审计日志的应用程序名称。
    /// </summary>
    /// <remarks>当你保存不同的应用审计日志到同一个数据库时，这个属性用来区分应用程序。</remarks>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 获取或设置发起操作的当前用户标识；未认证或无法解析当前用户时为 <c>null</c>。
    /// </summary>
    /// <remarks>该属性描述实际发起当前操作的主体，不应与代操作用户属性混淆。</remarks>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置发起操作的当前用户名。
    /// </summary>
    /// <remarks>当前用户已认证且能提供名称时设置；未登录或名称不可用时为 <c>null</c>。</remarks>
    public string UserName { get; set; }

    /// <summary>
    /// 获取或设置发起操作所属的当前租户标识；非多租户场景或无法解析租户时为 <c>null</c>。
    /// </summary>
    /// <remarks>该属性描述当前主体所在租户，不应与代操作租户属性混淆。</remarks>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置发起操作的当前租户名称。
    /// </summary>
    /// <remarks>多租户场景下当前租户的显示名称；无法解析或不适用时为 <c>null</c>。</remarks>
    public string TenantName { get; set; }

    /// <summary>
    /// 获取或设置被当前主体代操作的用户标识；未发生代操作时为 <c>null</c>。
    /// </summary>
    public string ImpersonatorUserId { get; set; }

    /// <summary>
    /// 获取或设置被当前主体代操作的租户标识；未发生代操作时为 <c>null</c>。
    /// </summary>
    public string ImpersonatorTenantId { get; set; }

    /// <summary>
    /// 获取或设置被当前主体代操作的用户名；未发生代操作时为 <c>null</c>。
    /// </summary>
    public string ImpersonatorUserName { get; set; }

    /// <summary>
    /// 获取或设置被当前主体代操作的租户名称；未发生代操作时为 <c>null</c>。
    /// </summary>
    public string ImpersonatorTenantName { get; set; }

    /// <summary>
    /// 获取或设置审计日志创建时的执行时间。
    /// </summary>
    /// <remarks>审计日志对象创建的时间。</remarks>
    public DateTime ExecutionTime { get; set; }

    /// <summary>
    /// 获取或设置请求总执行时长，单位为毫秒。
    /// </summary>
    /// <remarks>请求的总执行时间，以毫秒为单位，可以用来观察应用程序的性能。</remarks>
    public int ExecutionDuration { get; set; }

    /// <summary>
    /// 获取或设置已认证客户端的标识。
    /// </summary>
    /// <remarks>当前客户端的ID，如果客户端已经通过认证，客户端通常是使用HTTP API的第三方应用程序。</remarks>
    public string ClientId { get; set; }

    /// <summary>
    /// 获取或设置已认证客户端的名称。
    /// </summary>
    /// <remarks>当前客户端的名称，如果有的话。</remarks>
    public string ClientName { get; set; }

    /// <summary>
    /// 获取或设置客户端或用户设备的 IP 地址。
    /// </summary>
    /// <remarks>客户端/用户设备的IP地址。</remarks>
    public string ClientIpAddress { get; set; }

    /// <summary>
    /// 获取或设置关联同一逻辑操作中跨应用审计日志的关联标识。
    /// </summary>
    /// <remarks>当前关联ID，关联ID用于在单个逻辑操作中关联由不同应用程序（或微服务）写入的审计日志。</remarks>
    public string CorrelationId { get; set; }

    /// <summary>
    /// 获取或设置客户端浏览器的名称和版本信息。
    /// </summary>
    /// <remarks>当前用户的浏览器名称/版本信息，如果有的话。</remarks>
    public string BrowserInfo { get; set; }

    /// <summary>
    /// 获取或设置当前 HTTP 请求方法。
    /// </summary>
    /// <remarks>当前HTTP请求的方法（GET,POST,PUT,DELETE...等）。</remarks>
    public string HttpMethod { get; set; }

    /// <summary>
    /// 获取或设置 HTTP 响应状态码；非 HTTP 操作时为 <c>null</c>。
    /// </summary>
    /// <remarks>HTTP响应状态码。</remarks>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// 获取或设置请求的 URL。
    /// </summary>
    /// <remarks>请求的URL。</remarks>
    public string Url { get; set; }

    /// <summary>
    /// 获取或设置本次审计范围内的控制器或应用服务操作记录。
    /// </summary>
    /// <remarks>一个审计日志操作通常是WEB请求期间控制器操作或应用服务方法调用，一个审计日志可以包含多个操作。</remarks>
    public List<AuditLogActionInfo> Actions { get; set; }

    /// <summary>
    /// 获取或设置本次操作捕获的异常列表。
    /// </summary>
    /// <remarks>审计日志对象可能包含零个或多个异常，可以得到失败请求的异常信息。</remarks>
    public List<Exception> Exceptions { get; set; }

    /// <summary>
    /// 获取或设置本次审计范围内聚合的实体增删改记录；没有实体变更时为空集合。
    /// </summary>
    public List<EntityChangeInfo> EntityChanges { get; set; }

    /// <summary>
    /// 获取或设置附加到当前审计日志的自定义文本注释列表。
    /// </summary>
    /// <remarks>用于将自定义消息添加到审计日志条目的任意字符串值。审计日志对象可能包含零个或多个注释。</remarks>
    public List<string> Comments { get; set; }

    /// <summary>
    /// 返回适用于诊断输出的审计日志摘要。
    /// </summary>
    /// <returns>包含请求、操作、异常和实体变更摘要的文本。</returns>
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
