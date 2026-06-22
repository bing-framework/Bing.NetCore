using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// <see cref="NullCache"/> 单元测试 — 验证空缓存所有方法的默认行为（不抛异常、返回默认值/false/空集合）
/// </summary>
public class NullCacheTest
{
    private readonly ILocalCache _cache = NullCache.Instance;
    private readonly CacheKey _key = new("test:key");

    /// <summary>
    /// 测试目的：NullCache.Instance 为静态单例，不应为 null。
    /// </summary>
    [Fact]
    public void Instance_ShouldNotBeNull()
    {
        // Assert
        NullCache.Instance.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：多次获取 Instance 应返回同一引用。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSameReference()
    {
        // Assert
        NullCache.Instance.ShouldBeSameAs(NullCache.Instance);
    }

    /// <summary>
    /// 测试目的：Exists(CacheKey) 始终返回 false，表明缓存为空。
    /// </summary>
    [Fact]
    public void Exists_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.Exists(_key).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Exists(string) 始终返回 false。
    /// </summary>
    [Fact]
    public void Exists_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.Exists("test:key").ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：ExistsAsync(CacheKey) 始终返回 false。
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.ExistsAsync(_key)).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：ExistsAsync(string) 始终返回 false。
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.ExistsAsync("test:key")).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(CacheKey) 始终返回类型默认值（引用类型为 null，值类型为 0/false 等）。
    /// </summary>
    [Fact]
    public void Get_WithCacheKey_ShouldReturnDefault()
    {
        // Act
        var result = _cache.Get<string>(_key);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(string) 始终返回类型默认值。
    /// </summary>
    [Fact]
    public void Get_WithStringKey_ShouldReturnDefault()
    {
        // Act
        var result = _cache.Get<int>("test:key");

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(CacheKey, Func) 当 dataRetriever 不为 null 时，应调用并返回其结果。
    /// </summary>
    [Fact]
    public void Get_WithCacheKeyAndRetriever_ShouldCallRetriever()
    {
        // Act
        var result = _cache.Get<string>(_key, () => "hello");

        // Assert
        result.ShouldBe("hello");
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(CacheKey, null) 当 dataRetriever 为 null 时，应返回默认值，不抛异常。
    /// </summary>
    [Fact]
    public void Get_WithCacheKeyAndNullRetriever_ShouldReturnDefault()
    {
        // Act
        var result = _cache.Get<string>(_key, (Func<string>)null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(string, Func) 当 dataRetriever 不为 null 时，应调用并返回其结果。
    /// </summary>
    [Fact]
    public void Get_WithStringKeyAndRetriever_ShouldCallRetriever()
    {
        // Act
        var result = _cache.Get<int>("test:key", () => 42);

        // Assert
        result.ShouldBe(42);
    }

    /// <summary>
    /// 测试目的：Get&lt;T&gt;(string, null) 当 dataRetriever 为 null 时，应返回默认值。
    /// </summary>
    [Fact]
    public void Get_WithStringKeyAndNullRetriever_ShouldReturnDefault()
    {
        // Act
        var result = _cache.Get<string>("test:key", (Func<string>)null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetAsync(string, Type) 始终返回 null（Task 结果为 null）。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithStringKeyAndType_ShouldReturnNull()
    {
        // Act
        var result = await _cache.GetAsync("test:key", typeof(string));

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetAsync&lt;T&gt;(CacheKey) 始终返回默认值。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithCacheKey_ShouldReturnDefault()
    {
        // Act
        var result = await _cache.GetAsync<string>(_key);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetAsync&lt;T&gt;(string) 始终返回默认值。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithStringKey_ShouldReturnDefault()
    {
        // Act
        var result = await _cache.GetAsync<int>("test:key");

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：GetAsync&lt;T&gt;(CacheKey, Func&lt;Task&lt;T&gt;&gt;) 当 dataRetriever 不为 null 时，应调用并返回其结果。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithCacheKeyAndRetriever_ShouldCallRetriever()
    {
        // Act
        var result = await _cache.GetAsync<string>(_key, () => Task.FromResult("async-value"));

        // Assert
        result.ShouldBe("async-value");
    }

    /// <summary>
    /// 测试目的：GetAsync&lt;T&gt;(CacheKey, null) 当 dataRetriever 为 null 时，应返回默认值。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithCacheKeyAndNullRetriever_ShouldReturnDefault()
    {
        // Act
        var result = await _cache.GetAsync<string>(_key, (Func<Task<string>>)null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetAsync&lt;T&gt;(string, Func&lt;Task&lt;T&gt;&gt;) 当 dataRetriever 不为 null 时，应调用并返回其结果。
    /// </summary>
    [Fact]
    public async Task GetAsync_WithStringKeyAndRetriever_ShouldCallRetriever()
    {
        // Act
        var result = await _cache.GetAsync<int>("test:key", () => Task.FromResult(99));

        // Assert
        result.ShouldBe(99);
    }

    /// <summary>
    /// 测试目的：GetAll&lt;T&gt;(IEnumerable&lt;CacheKey&gt;) 始终返回空列表，不抛异常。
    /// </summary>
    [Fact]
    public void GetAll_WithCacheKeys_ShouldReturnEmptyList()
    {
        // Act
        var result = _cache.GetAll<string>(new[] { _key });

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetAll&lt;T&gt;(IEnumerable&lt;string&gt;) 始终返回空列表。
    /// </summary>
    [Fact]
    public void GetAll_WithStringKeys_ShouldReturnEmptyList()
    {
        // Act
        var result = _cache.GetAll<string>(new[] { "k1", "k2" });

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetAllAsync&lt;T&gt;(IEnumerable&lt;CacheKey&gt;) 始终返回空列表。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WithCacheKeys_ShouldReturnEmptyList()
    {
        // Act
        var result = await _cache.GetAllAsync<string>(new[] { _key });

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetByPrefix&lt;T&gt; 始终返回空列表。
    /// </summary>
    [Fact]
    public void GetByPrefix_ShouldReturnEmptyList()
    {
        // Act
        var result = _cache.GetByPrefix<string>("prefix:");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetByPrefixAsync&lt;T&gt; 始终返回空列表。
    /// </summary>
    [Fact]
    public async Task GetByPrefixAsync_ShouldReturnEmptyList()
    {
        // Act
        var result = await _cache.GetByPrefixAsync<string>("prefix:");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：TrySet&lt;T&gt;(CacheKey, value) 始终返回 false（不存储任何数据）。
    /// </summary>
    [Fact]
    public void TrySet_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.TrySet<string>(_key, "value").ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：TrySet&lt;T&gt;(string, value) 始终返回 false。
    /// </summary>
    [Fact]
    public void TrySet_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.TrySet<string>("test:key", "value").ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：TrySetAsync&lt;T&gt;(CacheKey, value) 始终返回 false。
    /// </summary>
    [Fact]
    public async Task TrySetAsync_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.TrySetAsync<string>(_key, "value")).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：TrySetAsync&lt;T&gt;(string, value) 始终返回 false。
    /// </summary>
    [Fact]
    public async Task TrySetAsync_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.TrySetAsync<string>("test:key", "value")).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Set&lt;T&gt;(CacheKey) 不应抛任何异常（空操作）。
    /// </summary>
    [Fact]
    public void Set_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert（调用不抛异常即通过）
        Should.NotThrow(() => _cache.Set<string>(_key, "value"));
    }

    /// <summary>
    /// 测试目的：Set&lt;T&gt;(string) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void Set_WithStringKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Set<string>("test:key", "value"));
    }

    /// <summary>
    /// 测试目的：SetAsync&lt;T&gt;(CacheKey) 不应抛任何异常，并返回 CompletedTask。
    /// </summary>
    [Fact]
    public async Task SetAsync_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.SetAsync<string>(_key, "value"));
    }

    /// <summary>
    /// 测试目的：SetAll&lt;T&gt;(IDictionary&lt;CacheKey, T&gt;) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void SetAll_WithCacheKeys_ShouldNotThrow()
    {
        // Arrange
        var items = new Dictionary<CacheKey, string> { [_key] = "value" };

        // Act & Assert
        Should.NotThrow(() => _cache.SetAll(items));
    }

    /// <summary>
    /// 测试目的：SetAll&lt;T&gt;(IDictionary&lt;string, T&gt;) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void SetAll_WithStringKeys_ShouldNotThrow()
    {
        // Arrange
        var items = new Dictionary<string, string> { ["k1"] = "v1" };

        // Act & Assert
        Should.NotThrow(() => _cache.SetAll(items));
    }

    /// <summary>
    /// 测试目的：Remove(CacheKey) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void Remove_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Remove(_key));
    }

    /// <summary>
    /// 测试目的：Remove(string) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void Remove_WithStringKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Remove("test:key"));
    }

    /// <summary>
    /// 测试目的：RemoveAll(IEnumerable&lt;CacheKey&gt;) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void RemoveAll_WithCacheKeys_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveAll(new[] { _key }));
    }

    /// <summary>
    /// 测试目的：RemoveAll(IEnumerable&lt;string&gt;) 不应抛任何异常。
    /// </summary>
    [Fact]
    public void RemoveAll_WithStringKeys_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveAll(new[] { "k1", "k2" }));
    }

    /// <summary>
    /// 测试目的：RemoveByPrefix 不应抛任何异常（空操作）。
    /// </summary>
    [Fact]
    public void RemoveByPrefix_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveByPrefix("prefix:"));
    }

    /// <summary>
    /// 测试目的：RemoveByPrefixAsync 不应抛任何异常。
    /// </summary>
    [Fact]
    public async Task RemoveByPrefixAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.RemoveByPrefixAsync("prefix:"));
    }

    /// <summary>
    /// 测试目的：RemoveByPattern 不应抛任何异常（空操作）。
    /// </summary>
    [Fact]
    public void RemoveByPattern_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveByPattern("*"));
    }

    /// <summary>
    /// 测试目的：RemoveByPatternAsync 不应抛任何异常。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.RemoveByPatternAsync("*"));
    }

    /// <summary>
    /// 测试目的：Clear() 不应抛任何异常（空操作）。
    /// </summary>
    [Fact]
    public void Clear_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Clear());
    }

    /// <summary>
    /// 测试目的：ClearAsync() 不应抛任何异常，返回 CompletedTask。
    /// </summary>
    [Fact]
    public async Task ClearAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.ClearAsync());
    }
}

/// <summary>
/// <see cref="CacheOptions"/> 单元测试 — 验证默认值与属性赋值行为
/// </summary>
public class CacheOptionsTest
{
    /// <summary>
    /// 测试目的：CacheOptions 默认构造后，Expiration 应为 null（不设过期时间）。
    /// </summary>
    [Fact]
    public void Default_Expiration_ShouldBeNull()
    {
        // Act
        var options = new CacheOptions();

        // Assert
        options.Expiration.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：设置 Expiration 后，应能正确读回。
    /// </summary>
    [Fact]
    public void SetExpiration_ShouldBeReadBack()
    {
        // Arrange
        var expiration = TimeSpan.FromHours(8);

        // Act
        var options = new CacheOptions { Expiration = expiration };

        // Assert
        options.Expiration.ShouldBe(expiration);
    }

    /// <summary>
    /// 测试目的：Expiration 可被设置为零（立即过期语义）。
    /// </summary>
    [Fact]
    public void SetExpiration_ToZero_ShouldBeReadBack()
    {
        // Act
        var options = new CacheOptions { Expiration = TimeSpan.Zero };

        // Assert
        options.Expiration.ShouldBe(TimeSpan.Zero);
    }
}

/// <summary>
/// <see cref="CacheNameAttribute"/> 单元测试 — 验证构造、Name 属性及 GetCacheName 静态方法逻辑
/// </summary>
public class CacheNameAttributeTest
{
    /// <summary>
    /// 测试目的：构造 CacheNameAttribute 并传入有效名称，Name 属性应等于传入值。
    /// </summary>
    [Fact]
    public void Constructor_WithValidName_ShouldStoreName()
    {
        // Act
        var attr = new CacheNameAttribute("user-cache");

        // Assert
        attr.Name.ShouldBe("user-cache");
    }

    /// <summary>
    /// 测试目的：构造 CacheNameAttribute 时传入 null，应抛出异常，Guard 会阻止无效状态。
    /// </summary>
    [Fact]
    public void Constructor_WithNullName_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<Exception>(() => new CacheNameAttribute(null));
    }

    /// <summary>
    /// 测试目的：GetCacheName&lt;TCacheItem&gt; 泛型版本应与 GetCacheName(Type) 非泛型版本返回相同结果。
    /// </summary>
    [Fact]
    public void GetCacheName_GenericAndNonGeneric_ShouldBeEqual()
    {
        // Act
        var fromGeneric = CacheNameAttribute.GetCacheName<AnnotatedCacheItem>();
        var fromType = CacheNameAttribute.GetCacheName(typeof(AnnotatedCacheItem));

        // Assert
        fromGeneric.ShouldBe(fromType);
    }

    /// <summary>
    /// 测试目的：有 [CacheNameAttribute] 标注的类型，GetCacheName 应返回特性中指定的名称。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenAttributePresent_ShouldReturnAttributeName()
    {
        // Act
        var name = CacheNameAttribute.GetCacheName<AnnotatedCacheItem>();

        // Assert
        name.ShouldBe("custom-name");
    }

    /// <summary>
    /// 测试目的：无 [CacheNameAttribute] 标注的类型，GetCacheName 应返回 FullName 去除 "CacheItem" 后缀的结果。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenNoAttribute_ShouldReturnFullNameWithoutCacheItemSuffix()
    {
        // Act
        var name = CacheNameAttribute.GetCacheName<UnannotatedCacheItem>();

        // Assert
        // FullName = "Bing.Caching.Tests.UnannotatedCacheItem", 去掉 "CacheItem" 后缀
        name.ShouldBe(typeof(UnannotatedCacheItem).FullName!.Replace("CacheItem", ""));
    }

    /// <summary>
    /// 测试目的：无后缀的普通类（不以 "CacheItem" 结尾），GetCacheName 应返回完整 FullName。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenNoAttributeAndNoSuffix_ShouldReturnFullName()
    {
        // Act
        var name = CacheNameAttribute.GetCacheName<PlainEntity>();

        // Assert
        name.ShouldBe(typeof(PlainEntity).FullName);
    }
}

// ─── 测试用辅助类型 ───────────────────────────────────────────────────

[CacheName("custom-name")]
internal class AnnotatedCacheItem { }

internal class UnannotatedCacheItem { }

internal class PlainEntity { }
