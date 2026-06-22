using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// <see cref="CacheKey"/> 单元测试
/// </summary>
public class CacheKeyTest
{
    /// <summary>
    /// 测试目的：CacheKey 字符串构造后 Key 应等于传入字符串。
    /// </summary>
    [Fact]
    public void CacheKey_WithSimpleString_ShouldReturnSameKey()
    {
        // Arrange & Act
        var key = new CacheKey("user:profile");

        // Assert
        key.Key.ShouldBe("user:profile");
    }

    /// <summary>
    /// 测试目的：CacheKey 使用格式化参数时，Key 应正确替换占位符。
    /// </summary>
    [Fact]
    public void CacheKey_WithFormatParameters_ShouldFormatCorrectly()
    {
        // Arrange & Act
        var key = new CacheKey("user:{0}:orders:{1}", "u-001", 42);

        // Assert
        key.Key.ShouldBe("user:u-001:orders:42");
    }

    /// <summary>
    /// 测试目的：设置 Prefix 后，ToString()/Key 应返回 Prefix + Key 的组合。
    /// </summary>
    [Fact]
    public void CacheKey_WithPrefix_ShouldPrependPrefixToKey()
    {
        // Arrange & Act
        var key = new CacheKey("profile")
        {
            Prefix = "dev:"
        };

        // Assert
        key.Key.ShouldBe("dev:profile");
        key.ToString().ShouldBe("dev:profile");
    }

    /// <summary>
    /// 测试目的：无 Prefix 时，ToString 应直接返回键值，不含多余字符。
    /// </summary>
    [Fact]
    public void CacheKey_WithoutPrefix_ShouldReturnKeyOnly()
    {
        // Arrange & Act
        var key = new CacheKey("simple-key");

        // Assert
        key.ToString().ShouldBe("simple-key");
    }
}

/// <summary>
/// <see cref="NullCache"/> 单元测试。
/// NullCache 是一个纯内存空实现，用于测试和不需要实际缓存的场景。
/// </summary>
public class NullCacheTest
{
    private readonly ILocalCache _cache = NullCache.Instance;

    // ── Exists ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NullCache.Exists 对任何键都应返回 false（空缓存永远不存在数据）。
    /// </summary>
    [Fact]
    public void Exists_WithAnyKey_ShouldReturnFalse()
    {
        _cache.Exists("any_key").ShouldBeFalse();
        _cache.Exists(new CacheKey("any_key")).ShouldBeFalse();
    }

    // ── Get ────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NullCache.Get{T}() 应返回类型 T 的默认值，不抛异常。
    /// </summary>
    [Fact]
    public void Get_WithAnyKey_ShouldReturnDefault()
    {
        _cache.Get<string>("key").ShouldBeNull();
        _cache.Get<int>("key").ShouldBe(0);
        _cache.Get<DateTime>("key").ShouldBe(default(DateTime));
    }

    /// <summary>
    /// 测试目的：NullCache.Get{T} 带有 dataRetriever 时应直接调用 retriever 并返回其值。
    /// </summary>
    [Fact]
    public void Get_WithDataRetriever_ShouldInvokeRetriever()
    {
        // Arrange
        var callCount = 0;
        Func<string> retriever = () => { callCount++; return "fresh-value"; };

        // Act
        var result = _cache.Get<string>("key", retriever);

        // Assert
        result.ShouldBe("fresh-value");
        callCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：NullCache.Get{T} dataRetriever 为 null 时应返回默认值，不抛异常。
    /// </summary>
    [Fact]
    public void Get_WithNullRetriever_ShouldReturnDefault()
    {
        // Act & Assert
        Should.NotThrow(() =>
        {
            var result = _cache.Get<string>("key", (Func<string>)null);
            result.ShouldBeNull();
        });
    }

    // ── TrySet ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NullCache.TrySet 应始终返回 false（数据不被真正存储）。
    /// </summary>
    [Fact]
    public void TrySet_WithAnyValue_ShouldReturnFalse()
    {
        _cache.TrySet("key", "value").ShouldBeFalse();
        _cache.TrySet(new CacheKey("key"), 42).ShouldBeFalse();
    }

    // ── Remove ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NullCache.Remove 不抛异常（幂等，安全调用）。
    /// </summary>
    [Fact]
    public void Remove_WithAnyKey_ShouldNotThrow()
    {
        Should.NotThrow(() => _cache.Remove("nonexistent_key"));
        Should.NotThrow(() => _cache.Remove(new CacheKey("nonexistent_key")));
    }

    // ── Async ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：ExistsAsync 对任何键都应返回 false。
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithAnyKey_ShouldReturnFalse()
    {
        (await _cache.ExistsAsync("key")).ShouldBeFalse();
        (await _cache.ExistsAsync(new CacheKey("key"))).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：GetAsync{T}() 应返回默认值。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithAnyKey_ShouldReturnDefault()
    {
        (await _cache.GetAsync<string>("key")).ShouldBeNull();
        (await _cache.GetAsync<int>("key")).ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：GetAsync{T}() 带有 dataRetriever 时应直接调用并返回其值。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithDataRetriever_ShouldInvokeRetriever()
    {
        // Arrange
        var callCount = 0;
        Func<Task<string>> retriever = () => { callCount++; return Task.FromResult("async-value"); };

        // Act
        var result = await _cache.GetAsync<string>("key", retriever);

        // Assert
        result.ShouldBe("async-value");
        callCount.ShouldBe(1);
    }

    // ── Instance 单例 ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NullCache.Instance 应为单例，多次访问返回同一引用。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        ReferenceEquals(NullCache.Instance, NullCache.Instance).ShouldBeTrue();
    }
}

/// <summary>
/// <see cref="CacheOptions"/> 单元测试
/// </summary>
public class CacheOptionsTest
{
    /// <summary>
    /// 测试目的：默认 CacheOptions 的 Expiration 应为 null（无强制过期时间）。
    /// </summary>
    [Fact]
    public void Default_ExpirationShouldBeNull()
    {
        // Arrange & Act
        var options = new CacheOptions();

        // Assert
        options.Expiration.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：设置 Expiration 后应能正确读取。
    /// </summary>
    [Fact]
    public void Expiration_WhenSet_ShouldBeReadable()
    {
        // Arrange
        var expiry = TimeSpan.FromMinutes(30);
        var options = new CacheOptions { Expiration = expiry };

        // Assert
        options.Expiration.ShouldBe(expiry);
    }
}
