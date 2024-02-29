﻿using Bing.Data;
using Bing.Helpers;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户配置
/// </summary>
[Serializable]
public class TenantConfiguration
{
    /// <summary>
    /// 初始化一个<see cref="TenantConfiguration" />类型的实例
    /// </summary>
    public TenantConfiguration()
    {
        IsActive = true;
    }

    /// <summary>
    /// 初始化一个<see cref="TenantConfiguration" />类型的实例
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">名称</param>
    public TenantConfiguration(string id, string name) : this()
    {
        Check.NotNull(name, nameof(name));
        Id = id;
        Name = name;
        ConnectionStrings = new ConnectionStringCollection();
    }

    /// <summary>
    /// 租户标识
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 连接字符串集合
    /// </summary>
    public ConnectionStringCollection ConnectionStrings { get; set; }

    /// <summary>
    /// 是否激活中
    /// </summary>
    public bool IsActive { get; set; }
}