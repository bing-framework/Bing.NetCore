namespace Bing.Authorization.Functions;

/// <summary>
/// 定义功能信息
/// </summary>
public interface IFunction
{
    /// <summary>
    /// 功能名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 区域名称
    /// </summary>
    string AreaName { get; set; }

    /// <summary>
    /// 控制器名称
    /// </summary>
    string ControllerName { get; set; }

    /// <summary>
    /// 控制器的功能名称
    /// </summary>
    string ActionName { get; set; }

    /// <summary>
    /// 是否控制器
    /// </summary>
    bool IsController { get; set; }

    /// <summary>
    /// 是否Ajax功能
    /// </summary>
    bool IsAjax { get; set; }

    /// <summary>
    /// 访问类型
    /// </summary>
    FunctionAccessType AccessType { get; set; }

    /// <summary>
    /// 访问类型是否可更改，如为true，刷新功能时将忽略功能类型
    /// </summary>
    bool IsAccessTypeChanged { get; set; }

    /// <summary>
    /// 是否启用操作审计
    /// </summary>
    bool AuditOperationEnabled { get; set; }

    /// <summary>
    /// 是否启用数据审计
    /// </summary>
    bool AuditEntityEnabled { get; set; }
    
    /// <summary>
    /// 数据缓存时间。单位：秒
    /// </summary>
    int CacheExpirationSeconds { get; set; }

    /// <summary>
    /// 是否相对过期时间，否则为绝对过期时间
    /// </summary>
    bool IsCacheSliding { get; set; }

    /// <summary>
    /// 是否从库读取数据
    /// </summary>
    bool IsSlaveDatabase { get; set; }
}
