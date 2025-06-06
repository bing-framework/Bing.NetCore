﻿namespace Bing.MultiTenancy;

/// <summary>
/// 基础租户信息
/// </summary>
public class BasicTenantInfo
{
    /// <summary>
    /// 租户标识
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 初始化一个<see cref="BasicTenantInfo"/>类型的实例
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="name">租户名称</param>
    public BasicTenantInfo(string? tenantId, string? name = null)
    {
        TenantId = tenantId;
        Name = name;
    }
}
