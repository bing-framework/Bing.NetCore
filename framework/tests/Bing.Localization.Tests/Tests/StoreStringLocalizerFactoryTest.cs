﻿using Bing.Localization.Samples;
using Bing.Localization.Store;

namespace Bing.Localization.Tests;

/// <summary>
/// 数据存储本地化资源查找器工厂测试
/// </summary>
public class StoreStringLocalizerFactoryTest
{
    /// <summary>
    /// 模拟本地化资源存储器
    /// </summary>
    private readonly Mock<ILocalizedStore> _mockLocalizedStore;

    /// <summary>
    /// 本地化资源查找器工厂
    /// </summary>
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public StoreStringLocalizerFactoryTest()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(t => t.CreateLogger("Bing.Localization.Store.StoreStringLocalizer")).Returns(NullLogger<StoreStringLocalizer>.Instance);
        _mockLocalizedStore = new Mock<ILocalizedStore>();
        var mockMemoryCache = new Mock<IMemoryCache>();
        var mockCacheEntry = new Mock<ICacheEntry>();
        mockMemoryCache.Setup(t => t.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);
        _localizerFactory = new StoreStringLocalizerFactory(mockLoggerFactory.Object, _mockLocalizedStore.Object, mockMemoryCache.Object);
    }

    /// <summary>
    /// 测试 - 创建本地化资源查找器 - IStringLocalizer泛型版本返回类型
    /// </summary>
    [Fact]
    public void Test_Create_1()
    {
        var localizer = _localizerFactory.Create(typeof(TestType));
        var result = localizer["a"];
        _mockLocalizedStore.Verify(t => t.GetValue(It.IsAny<string>(), "TestType", It.IsAny<string>()));
    }

    /// <summary>
    /// 测试 - 创建本地化资源查找器 - IStringLocalizer泛型版本返回类型 - LocalizedType特性
    /// </summary>
    [Fact]
    public void Test_Create_2()
    {
        var localizer = _localizerFactory.Create(typeof(TestType2));
        var result = localizer["a"];
        _mockLocalizedStore.Verify(t => t.GetValue(It.IsAny<string>(), "test2", It.IsAny<string>()));
    }

    /// <summary>
    /// 测试 - 创建本地化资源查找器 - 类型字符串
    /// </summary>
    [Fact]
    public void Test_Create_3()
    {
        var localizer = _localizerFactory.Create("type", "");
        var result = localizer["a"];
        _mockLocalizedStore.Verify(t => t.GetValue(It.IsAny<string>(), "type", It.IsAny<string>()));
    }
}
