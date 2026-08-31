namespace Bing.AspNetCore.Serilog;

/// <summary>
/// 配置 ASP.NET Core Serilog Enricher 写入的结构化属性名称。
/// </summary>
public class BingAspNetCoreSerilogOptions
{
    /// <summary>
    /// 获取用于配置 Serilog Enricher 属性名称的集合。
    /// </summary>
    public AllEnricherPropertyNames EnricherPropertyNames { get; } = new AllEnricherPropertyNames();

    /// <summary>
    /// 提供默认的 ASP.NET Core Serilog Enricher 属性名称配置。
    /// </summary>
    public class AllEnricherPropertyNames
    {
        /// <summary>
        /// 获取或设置租户标识对应的日志属性名称，默认值为 <c>TenantId</c>。
        /// </summary>
        public string TenantId { get; set; } = "TenantId";

        /// <summary>
        /// 获取或设置用户标识对应的日志属性名称，默认值为 <c>UserId</c>。
        /// </summary>
        public string UserId { get; set; } = "UserId";

        /// <summary>
        /// 获取或设置客户端标识对应的日志属性名称，默认值为 <c>ClientId</c>。
        /// </summary>
        public string ClientId { get; set; } = "ClientId";

        /// <summary>
        /// 获取或设置关联标识对应的日志属性名称，默认值为 <c>CorrelationId</c>。
        /// </summary>
        public string CorrelationId { get; set; } = "CorrelationId";
    }
}