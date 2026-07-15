using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// <see cref="CacheKey"/> ToString / Prefix 行为补充测试，以及
/// <see cref="CacheKeyExtensions.Validate"/> 边界测试，
/// <see cref="ILocalCache"/> / <see cref="IRedisCache"/> 类型层次测试
/// </summary>
public class CacheKeyAndExtensionsTest
{
    // ═══════════════════════════════════════════════════════════
    // CacheKey — ToString / Prefix 组合
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：无参构造后 Key / Prefix 均为空，ToString() 返回空字符串，
    /// 确保默认状态不携带意外值。
    /// </summary>
    [Fact]
    public void CacheKey_DefaultConstructor_KeyAndPrefixShouldBeEmpty()
    {
        // Act
        var key = new CacheKey();

        // Assert
        key.Key.ShouldBe(string.Empty);
        key.Prefix.ShouldBeNull();
        key.ToString().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：直接对 Key 属性赋值后，ToString() 应返回 Prefix+Key，
    /// 确保 setter 路径与构造器路径行为一致。
    /// </summary>
    [Fact]
    public void CacheKey_SetKey_ToStringShouldReturnPrefixPlusKey()
    {
        // Arrange
        var key = new CacheKey { Key = "my-key", Prefix = "ns:" };

        // Assert
        key.ToString().ShouldBe("ns:my-key");
        key.Key.ShouldBe("ns:my-key");
    }

    /// <summary>
    /// 测试目的：仅设置 Key 不设置 Prefix 时，ToString() 应只返回 Key，
    /// 确保 Prefix 为 null 时不拼接额外字符。
    /// </summary>
    [Fact]
    public void CacheKey_WithKeyOnly_NoPrefix_ToStringShouldBeKeyOnly()
    {
        // Arrange
        var key = new CacheKey("order:{0}", 123);

        // Assert — Prefix 为 null，ToString() = "" + "order:123"
        key.ToString().ShouldBe("order:123");
    }

    // ═══════════════════════════════════════════════════════════
    // CacheKeyExtensions.Validate
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Validate(null) 应抛出 ArgumentNullException，
    /// 防止 null CacheKey 传入缓存操作导致运行时错误。
    /// </summary>
    [Fact]
    public void Validate_NullCacheKey_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((CacheKey)null).Validate());
    }

    /// <summary>
    /// 测试目的：Key 为空白字符串时，Validate 应抛出 ArgumentNullException，
    /// 防止空键被写入缓存存储层。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceKey_ShouldThrowArgumentNullException(string rawKey)
    {
        // Arrange
        var key = new CacheKey { Key = rawKey };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => key.Validate());
    }

    /// <summary>
    /// 测试目的：Key 有效时，Validate 应不抛异常，
    /// 确保正常场景不被拦截。
    /// </summary>
    [Fact]
    public void Validate_ValidKey_ShouldNotThrow()
    {
        // Arrange
        var key = new CacheKey("valid-key");

        // Act & Assert
        Should.NotThrow(() => key.Validate());
    }

    /// <summary>
    /// 测试目的：Key 由 Prefix 拼接后非空时，Validate 应不抛异常，
    /// 确保前缀参与组合后的键依然被认为有效。
    /// </summary>
    [Fact]
    public void Validate_KeyWithPrefix_ShouldNotThrow()
    {
        // Arrange — Prefix 拼入后 Key getter 返回 "ns:item"
        var key = new CacheKey { Key = "item", Prefix = "ns:" };

        // Act & Assert
        Should.NotThrow(() => key.Validate());
    }

    // ═══════════════════════════════════════════════════════════
    // ILocalCache / IRedisCache — 类型层次
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ILocalCache 应继承自 ICache，
    /// 确保本地缓存实现可被 ICache 接口统一引用。
    /// </summary>
    [Fact]
    public void ILocalCache_ShouldExtendICache()
    {
        // Assert
        typeof(ICache).IsAssignableFrom(typeof(ILocalCache)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IRedisCache 应继承自 ICache，
    /// 确保 Redis 缓存实现可被 ICache 接口统一引用。
    /// </summary>
    [Fact]
    public void IRedisCache_ShouldExtendICache()
    {
        // Assert
        typeof(ICache).IsAssignableFrom(typeof(IRedisCache)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ILocalCache 与 IRedisCache 是不同类型，
    /// 确保两者可在 DI 容器中分别注册和解析。
    /// </summary>
    [Fact]
    public void ILocalCache_And_IRedisCache_ShouldBeDifferentTypes()
    {
        // Assert
        typeof(ILocalCache).ShouldNotBe(typeof(IRedisCache));
    }
}
