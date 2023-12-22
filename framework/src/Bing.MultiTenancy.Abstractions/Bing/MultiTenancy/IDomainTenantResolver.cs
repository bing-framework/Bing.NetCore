﻿namespace Bing.MultiTenancy;

/// <summary>
/// 域名租户解析器
/// </summary>
public interface IDomainTenantResolver
{
    /// <summary>
    /// 解析租户标识
    /// </summary>
    /// <param name="host">域名。范例：a.test.com</param>
    Task<string> ResolveTenantIdAsync(string host);
}
