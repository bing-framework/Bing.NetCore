using System;
using System.Linq;
using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 测试辅助类型
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>带 CacheNameAttribute 的缓存项</summary>
[CacheName("my-custom-cache")]
public class AnnotatedCacheItem { }

/// <summary>无 CacheNameAttribute，FullName 为 "Bing.Caching.Tests.UserCacheItem"</summary>
public class UserCacheItem { }

/// <summary>无 CacheNameAttribute，FullName 不含 "CacheItem" 后缀</summary>
public class OrderData { }

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="CacheNameAttribute"/> 单元测试
/// </summary>
public class CacheNameAttributeTest
{
    // ═══════════════════════════════════════════════════════════
    // 构造 & 名称验证
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造时传入有效名称，Name 属性应正确读取。
    /// </summary>
    [Fact]
    public void Constructor_WithValidName_ShouldSetName()
    {
        // Arrange & Act
        var attr = new CacheNameAttribute("user-cache");

        // Assert
        attr.Name.ShouldBe("user-cache");
    }

    /// <summary>
    /// 测试目的：构造时传入 null 名称，应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WithNullName_ShouldThrowArgumentNullException()
    {
        Should.Throw<Exception>(() => new CacheNameAttribute(null!));
    }

    // ═══════════════════════════════════════════════════════════
    // GetCacheName<T> — 有 Attribute
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetCacheName&lt;T&gt; 对有 [CacheName] 特性的类型，应返回特性指定的名称。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenAttributePresent_ShouldReturnAttributeName()
    {
        var name = CacheNameAttribute.GetCacheName<AnnotatedCacheItem>();
        name.ShouldBe("my-custom-cache");
    }

    /// <summary>
    /// 测试目的：GetCacheName(Type) 对有特性的类型，应返回特性指定名称。
    /// </summary>
    [Fact]
    public void GetCacheName_ByType_WhenAttributePresent_ShouldReturnAttributeName()
    {
        var name = CacheNameAttribute.GetCacheName(typeof(AnnotatedCacheItem));
        name.ShouldBe("my-custom-cache");
    }

    // ═══════════════════════════════════════════════════════════
    // GetCacheName<T> — 无 Attribute，按 FullName 剥除 "CacheItem" 后缀
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：无 [CacheName] 的类型，名称应为 FullName 去除 "CacheItem" 后缀。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenNoAttribute_ShouldUseFallbackWithoutSuffix()
    {
        // UserCacheItem → FullName 去除 "CacheItem" → "Bing.Caching.Tests.User"
        var name = CacheNameAttribute.GetCacheName<UserCacheItem>();
        name.ShouldBe("Bing.Caching.Tests.User");
    }

    /// <summary>
    /// 测试目的：无 [CacheName] 且 FullName 不含 "CacheItem" 后缀，应直接返回完整 FullName。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenNoAttribute_AndNoSuffix_ShouldReturnFullName()
    {
        var name = CacheNameAttribute.GetCacheName<OrderData>();
        name.ShouldBe("Bing.Caching.Tests.OrderData");
    }
}

/// <summary>
/// <see cref="CacheKeyExtensions"/> 单元测试
/// </summary>
public class CacheKeyExtensionsTest
{
    // ═══════════════════════════════════════════════════════════
    // Validate — 正常路径
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：有效 CacheKey 调用 Validate() 不应抛任何异常。
    /// </summary>
    [Fact]
    public void Validate_WithValidKey_ShouldNotThrow()
    {
        var key = new CacheKey("user:1");
        Should.NotThrow(() => key.Validate());
    }

    // ═══════════════════════════════════════════════════════════
    // Validate — 异常路径
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：null CacheKey 调用 Validate() 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Validate_WithNullCacheKey_ShouldThrowArgumentNullException()
    {
        CacheKey key = null;
        Should.Throw<ArgumentNullException>(() => key.Validate());
    }

    /// <summary>
    /// 测试目的：Key 为空字符串的 CacheKey 调用 Validate() 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Validate_WithEmptyKey_ShouldThrowArgumentNullException()
    {
        var key = new CacheKey(string.Empty);
        Should.Throw<ArgumentNullException>(() => key.Validate());
    }

    /// <summary>
    /// 测试目的：Key 为纯空格的 CacheKey 调用 Validate() 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Validate_WithWhitespaceKey_ShouldThrowArgumentNullException()
    {
        var key = new CacheKey("   ");
        Should.Throw<ArgumentNullException>(() => key.Validate());
    }
}
