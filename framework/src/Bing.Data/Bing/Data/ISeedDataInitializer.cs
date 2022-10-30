﻿using System.Threading.Tasks;
using Bing.DependencyInjection;

namespace Bing.Data;

/// <summary>
/// 种子数据初始化
/// </summary>
[MultipleDependency]
public interface ISeedDataInitializer
{
    /// <summary>
    /// 种子数据初始化顺序
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 初始化种子数据
    /// </summary>
    Task InitializeAsync();
}