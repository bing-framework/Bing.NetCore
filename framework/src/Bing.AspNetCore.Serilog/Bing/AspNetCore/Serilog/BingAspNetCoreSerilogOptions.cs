namespace Bing.AspNetCore.Serilog;

/// <summary>
/// AspNetCore 基于 Serilog 选项配置，用于设置 Enricher 信息
/// </summary>
public class BingAspNetCoreSerilogOptions
{
    /// <summary>
    /// Enricher 属性名称
    /// </summary>
    public AllEnricherPropertyNames EnricherPropertyNames { get; } = new AllEnricherPropertyNames();

    /// <summary>
    /// Enricher 属性名称
    /// </summary>
    public class AllEnricherPropertyNames
    {
        /// <summary>
        /// 租户标识。默认值：TenantId
        /// </summary>
        public string TenantId { get; set; } = "TenantId";

        /// <summary>
        /// 用户标识。默认值：UserId
        /// </summary>
        public string UserId { get; set; } = "UserId";

        /// <summary>
        /// 客户端标识。默认值：ClientId
        /// </summary>
        public string ClientId { get; set; } = "ClientId";

        /// <summary>
        /// 关联标识。默认值：CorrelationId
        /// </summary>
        public string CorrelationId { get; set; } = "CorrelationId";
    }
}