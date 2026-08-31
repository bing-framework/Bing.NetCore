namespace Bing.Authorization.Functions;

/// <summary>
/// 定义可被发现、刷新和持久化的授权功能元数据。
/// </summary>
public interface IFunction
{
    /// <summary>
    /// 获取或设置功能显示或标识名称。
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 获取或设置功能所属区域名称。
    /// </summary>
    string AreaName { get; set; }

    /// <summary>
    /// 获取或设置功能所属控制器名称。
    /// </summary>
    string ControllerName { get; set; }

    /// <summary>
    /// 获取或设置控制器操作方法名称。
    /// </summary>
    string ActionName { get; set; }

    /// <summary>
    /// 获取或设置该功能是否表示控制器类型。
    /// </summary>
    bool IsController { get; set; }

    /// <summary>
    /// 获取或设置该功能是否为 Ajax 请求入口。
    /// </summary>
    bool IsAjax { get; set; }

    /// <summary>
    /// 获取或设置功能访问控制类型。
    /// </summary>
    FunctionAccessType AccessType { get; set; }

    /// <summary>
    /// 获取或设置刷新功能元数据时是否保留当前访问控制类型。
    /// </summary>
    /// <remarks>值为 <c>true</c> 时，刷新流程忽略重新发现的访问控制类型。</remarks>
    bool IsAccessTypeChanged { get; set; }

    /// <summary>
    /// 获取或设置是否为该功能启用操作审计。
    /// </summary>
    bool AuditOperationEnabled { get; set; }

    /// <summary>
    /// 获取或设置是否为该功能启用实体数据审计。
    /// </summary>
    bool AuditEntityEnabled { get; set; }
    
    /// <summary>
    /// 获取或设置功能数据缓存时长，单位为秒。
    /// </summary>
    /// <remarks>仅在数据缓存启用时生效；<c>0</c> 或负数的具体语义由实现决定。</remarks>
    int CacheExpirationSeconds { get; set; }

    /// <summary>
    /// 获取或设置缓存过期时间是否按访问滑动续期。
    /// </summary>
    /// <remarks>值为 <c>true</c> 时每次访问续期；值为 <c>false</c> 时使用固定到期时间。</remarks>
    bool IsCacheSliding { get; set; }

    /// <summary>
    /// 获取或设置读取数据时是否优先路由至从库。
    /// </summary>
    /// <remarks>该值仅表达读取路由意图，实际是否可用由数据访问实现和当前数据源决定。</remarks>
    bool IsSlaveDatabase { get; set; }
}
