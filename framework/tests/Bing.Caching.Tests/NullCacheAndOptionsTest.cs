using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 验证空缓存的读取、写入和删除行为。
/// </summary>
public class NullCacheComprehensiveTest
{
    /// <summary>
    /// 提供空操作缓存实现。
    /// </summary>
    private readonly ILocalCache _cache = NullCache.Instance;
    /// <summary>
    /// 供空缓存行为测试使用的键。
    /// </summary>
    private readonly CacheKey _key = new("test:key");

    /// <summary>
    /// 验证空缓存单例可用。
    /// </summary>
    [Fact]
    public void Instance_ShouldNotBeNull()
    {
        // Assert
        NullCache.Instance.ShouldNotBeNull();
    }

    /// <summary>
    /// 验证空缓存始终返回同一实例。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSameReference()
    {
        // Assert
        NullCache.Instance.ShouldBeSameAs(NullCache.Instance);
    }

    /// <summary>
    /// 验证键对象查询始终未命中。
    /// </summary>
    [Fact]
    public void Exists_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.Exists(_key).ShouldBeFalse();
    }

    /// <summary>
    /// 验证字符串键查询始终未命中。
    /// </summary>
    [Fact]
    public void Exists_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.Exists("test:key").ShouldBeFalse();
    }

    /// <summary>
    /// 验证键对象异步查询始终未命中。
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.ExistsAsync(_key)).ShouldBeFalse();
    }

    /// <summary>
    /// 验证字符串键异步查询始终未命中。
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.ExistsAsync("test:key")).ShouldBeFalse();
    }

    /// <summary>
    /// 验证键对象读取返回类型默认值。
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
    /// 验证字符串键读取返回类型默认值。
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
    /// 验证键对象读取调用并返回回源结果。
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
    /// 验证未提供回源委托时键对象读取返回默认值。
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
    /// 验证字符串键读取调用并返回回源结果。
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
    /// 验证未提供回源委托时字符串键读取返回默认值。
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
    /// 验证运行时类型异步读取返回空值。
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
    /// 验证键对象异步读取返回类型默认值。
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
    /// 验证字符串键异步读取返回类型默认值。
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
    /// 验证键对象异步读取调用并返回回源结果。
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
    /// 验证未提供回源委托时键对象异步读取返回默认值。
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
    /// 验证字符串键异步读取调用并返回回源结果。
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
    /// 验证预先取消时异步操作保持取消状态且不调用回源委托。
    /// </summary>
    [Fact]
    public async Task AsyncOperations_WithPreCanceledToken_ShouldReturnCanceledTasksAndSkipRetrievers()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancellationToken = cancellationTokenSource.Token;
        var retrieverCalls = 0;
        var tasks = new Task[]
        {
            _cache.ExistsAsync(_key, cancellationToken),
            _cache.ExistsAsync("test:key", cancellationToken),
            _cache.GetAsync("test:key", typeof(string), cancellationToken),
            _cache.GetAsync<string>(_key, cancellationToken),
            _cache.GetAsync<string>("test:key", cancellationToken),
            _cache.GetAsync(_key, () =>
            {
                retrieverCalls++;
                return Task.FromResult("value");
            }, cancellationToken: cancellationToken),
            _cache.GetAsync("test:key", () =>
            {
                retrieverCalls++;
                return Task.FromResult("value");
            }, cancellationToken: cancellationToken),
            _cache.GetAsync(_key, _ =>
            {
                retrieverCalls++;
                return Task.FromResult("value");
            }, cancellationToken: cancellationToken),
            _cache.GetAsync("test:key", _ =>
            {
                retrieverCalls++;
                return Task.FromResult("value");
            }, cancellationToken: cancellationToken),
            _cache.GetAllAsync<string>(new[] { _key }, cancellationToken),
            _cache.GetAllAsync<string>(new[] { "test:key" }, cancellationToken),
            _cache.GetByPrefixAsync<string>("prefix:", cancellationToken),
            _cache.TrySetAsync(_key, "value", cancellationToken: cancellationToken),
            _cache.TrySetAsync("test:key", "value", cancellationToken: cancellationToken),
            _cache.SetAsync(_key, "value", cancellationToken: cancellationToken),
            _cache.SetAsync("test:key", "value", cancellationToken: cancellationToken),
            _cache.SetAllAsync(new Dictionary<CacheKey, string> { [_key] = "value" }, cancellationToken: cancellationToken),
            _cache.SetAllAsync(new Dictionary<string, string> { ["test:key"] = "value" }, cancellationToken: cancellationToken),
            _cache.RemoveAsync(_key, cancellationToken),
            _cache.RemoveAsync("test:key", cancellationToken),
            _cache.RemoveAllAsync(new[] { _key }, cancellationToken),
            _cache.RemoveAllAsync(new[] { "test:key" }, cancellationToken),
            _cache.RemoveByPrefixAsync("prefix:", cancellationToken),
            _cache.RemoveByPatternAsync("*", cancellationToken),
            _cache.ClearAsync(cancellationToken)
        };

        foreach (var task in tasks)
        {
            task.IsCanceled.ShouldBeTrue();
            await Should.ThrowAsync<OperationCanceledException>(async () => await task);
        }

        retrieverCalls.ShouldBe(0);
    }

    /// <summary>
    /// 验证流式读取在首次消费前响应取消。
    /// </summary>
    [Fact]
    public async Task StreamOperations_WithPreCanceledToken_ShouldThrowBeforeFirstMoveNext()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancellationToken = cancellationTokenSource.Token;

        var cacheKeyEnumerator = _cache.GetAllStreamAsync<string>(new[] { _key }, cancellationToken).GetAsyncEnumerator();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => await cacheKeyEnumerator.MoveNextAsync());
        }
        finally
        {
            await cacheKeyEnumerator.DisposeAsync();
        }

        var stringKeyEnumerator = _cache.GetAllStreamAsync<string>(new[] { "test:key" }, cancellationToken).GetAsyncEnumerator();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => await stringKeyEnumerator.MoveNextAsync());
        }
        finally
        {
            await stringKeyEnumerator.DisposeAsync();
        }

        var prefixEnumerator = _cache.GetByPrefixStreamAsync<string>("prefix:", cancellationToken).GetAsyncEnumerator();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => await prefixEnumerator.MoveNextAsync());
        }
        finally
        {
            await prefixEnumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证键对象批量读取返回空列表。
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
    /// 验证字符串键批量读取返回空列表。
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
    /// 验证键对象异步批量读取返回空列表。
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
    /// 验证前缀读取返回空列表。
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
    /// 验证异步前缀读取返回空列表。
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
    /// 验证键对象条件写入始终失败。
    /// </summary>
    [Fact]
    public void TrySet_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.TrySet<string>(_key, "value").ShouldBeFalse();
    }

    /// <summary>
    /// 验证字符串键条件写入始终失败。
    /// </summary>
    [Fact]
    public void TrySet_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        _cache.TrySet<string>("test:key", "value").ShouldBeFalse();
    }

    /// <summary>
    /// 验证键对象异步条件写入始终失败。
    /// </summary>
    [Fact]
    public async Task TrySetAsync_WithCacheKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.TrySetAsync<string>(_key, "value")).ShouldBeFalse();
    }

    /// <summary>
    /// 验证字符串键异步条件写入始终失败。
    /// </summary>
    [Fact]
    public async Task TrySetAsync_WithStringKey_ShouldReturnFalse()
    {
        // Act & Assert
        (await _cache.TrySetAsync<string>("test:key", "value")).ShouldBeFalse();
    }

    /// <summary>
    /// 验证键对象写入为空操作。
    /// </summary>
    [Fact]
    public void Set_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert（调用不抛异常即通过）
        Should.NotThrow(() => _cache.Set<string>(_key, "value"));
    }

    /// <summary>
    /// 验证字符串键写入为空操作。
    /// </summary>
    [Fact]
    public void Set_WithStringKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Set<string>("test:key", "value"));
    }

    /// <summary>
    /// 验证键对象异步写入为空操作。
    /// </summary>
    [Fact]
    public async Task SetAsync_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.SetAsync<string>(_key, "value"));
    }

    /// <summary>
    /// 验证键对象批量写入为空操作。
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
    /// 验证字符串键批量写入为空操作。
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
    /// 验证键对象删除为空操作。
    /// </summary>
    [Fact]
    public void Remove_WithCacheKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Remove(_key));
    }

    /// <summary>
    /// 验证字符串键删除为空操作。
    /// </summary>
    [Fact]
    public void Remove_WithStringKey_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Remove("test:key"));
    }

    /// <summary>
    /// 验证键对象批量删除为空操作。
    /// </summary>
    [Fact]
    public void RemoveAll_WithCacheKeys_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveAll(new[] { _key }));
    }

    /// <summary>
    /// 验证字符串键批量删除为空操作。
    /// </summary>
    [Fact]
    public void RemoveAll_WithStringKeys_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveAll(new[] { "k1", "k2" }));
    }

    /// <summary>
    /// 验证前缀删除为空操作。
    /// </summary>
    [Fact]
    public void RemoveByPrefix_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveByPrefix("prefix:"));
    }

    /// <summary>
    /// 验证异步前缀删除为空操作。
    /// </summary>
    [Fact]
    public async Task RemoveByPrefixAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.RemoveByPrefixAsync("prefix:"));
    }

    /// <summary>
    /// 验证模式删除为空操作。
    /// </summary>
    [Fact]
    public void RemoveByPattern_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.RemoveByPattern("*"));
    }

    /// <summary>
    /// 验证异步模式删除为空操作。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.RemoveByPatternAsync("*"));
    }

    /// <summary>
    /// 验证缓存清理为空操作。
    /// </summary>
    [Fact]
    public void Clear_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _cache.Clear());
    }

    /// <summary>
    /// 验证异步缓存清理为空操作。
    /// </summary>
    [Fact]
    public async Task ClearAsync_ShouldNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.ClearAsync());
    }
}

/// <summary>
/// 验证缓存过期选项的默认值和赋值行为。
/// </summary>
public class CacheOptionsComprehensiveTest
{
    /// <summary>
    /// 验证过期选项默认不设置过期时间。
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
    /// 验证过期时间可被正确读取。
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
    /// 验证过期时间支持零值。
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
/// 验证缓存名称特性的解析行为。
/// </summary>
/// <remarks>
/// 覆盖构造、Name 属性和 GetCacheName 静态方法。
/// </remarks>
public class CacheNameAttributeComprehensiveTest
{
    /// <summary>
    /// 验证缓存名称特性保留传入名称。
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
    /// 验证缓存名称为空时构造失败。
    /// </summary>
    [Fact]
    public void Constructor_WithNullName_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<Exception>(() => new CacheNameAttribute(null));
    }

    /// <summary>
    /// 验证泛型和运行时类型解析返回相同缓存名称。
    /// </summary>
    [Fact]
    public void GetCacheName_GenericAndNonGeneric_ShouldBeEqual()
    {
        // Act
        var fromGeneric = CacheNameAttribute.GetCacheName<AnnotatedComprehensiveCacheItem>();
        var fromType = CacheNameAttribute.GetCacheName(typeof(AnnotatedComprehensiveCacheItem));

        // Assert
        fromGeneric.ShouldBe(fromType);
    }

    /// <summary>
    /// 验证缓存名称优先使用类型特性中的名称。
    /// </summary>
    [Fact]
    public void GetCacheName_WhenAttributePresent_ShouldReturnAttributeName()
    {
        // Act
        var name = CacheNameAttribute.GetCacheName<AnnotatedComprehensiveCacheItem>();

        // Assert
        name.ShouldBe("custom-name");
    }

    /// <summary>
    /// 验证缓存名称默认移除 CacheItem 后缀。
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
    /// 验证无约定后缀时缓存名称保留完整类型名。
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

/// <summary>
/// 带自定义缓存名称的测试实体。
/// </summary>
[CacheName("custom-name")]
internal class AnnotatedComprehensiveCacheItem { }

/// <summary>
/// 未声明缓存名称的测试实体。
/// </summary>
internal class UnannotatedCacheItem { }

/// <summary>
/// 用于验证默认缓存名称的实体。
/// </summary>
internal class PlainEntity { }
