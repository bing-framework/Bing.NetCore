using System;

namespace Bing.MultiTenancy;

/// <summary>
/// 多租户
/// </summary>
public interface IMultiTenant : IMultiTenant<Guid?>
{
}

/// <summary>
/// 多租户
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IMultiTenant<TKey>
{
    /// <summary>
    /// 租户标识
    /// </summary>
    TKey TenantId { get; set; }
}