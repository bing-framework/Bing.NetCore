using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// FreeRedis 缓存测试
/// </summary>
public class CacheTest
{
    /// <summary>
    /// 缓存服务
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<CacheTest> _logger;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public CacheTest(ICache cache, ILogger<CacheTest> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据
    /// </summary>
    [Fact]
    public void Test_Get()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = _cache.Get("a", () =>
            {
                data++;
                return data;
            });
        }
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据
    /// </summary>
    [Fact]
    public void Test_Get_Object()
    {
        TestCacheModel result = null;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = _cache.Get<TestCacheModel>("a-1", () =>
            {
                data++;
                return new TestCacheModel { Id = data, Name = "test-01" };
            });
        }
        Assert.Equal(1, result?.Id);
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据
    /// </summary>
    [Fact]
    public async Task Test_GetAsync()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = await _cache.GetAsync("a", async () =>
            {
                data++;
                return await Task.FromResult(data);
            });
        }
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据
    /// </summary>
    [Fact]
    public async Task Test_GetAsync_Object()
    {
        TestCacheModel result = null;
        for (var i = 0; i < 3; i++)
        {
            result = await _cache.GetAsync("a-1", typeof(TestCacheModel)) as TestCacheModel;
        }
        Assert.Equal(1, result?.Id);
    }

    /// <summary>
    /// 测试 - 添加缓存
    /// </summary>
    [Fact]
    public void Test_TryAdd()
    {
        Assert.False(_cache.Exists("b"));
        _cache.TryAdd("b", 1);
        Assert.True(_cache.Exists("b"));
    }

    /// <summary>
    /// 测试 - 移除缓存
    /// </summary>
    [Fact]
    public void Test_Remove()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = _cache.Get("c", () =>
            {
                data++;
                return data;
            });
            _cache.Remove("c");
        }
        Assert.Equal(3, result);
    }

    /// <summary>
    /// 测试 - 通过缓存键前缀移除缓存
    /// </summary>
    [Fact]
    public void Test_RemoveByPrefix()
    {
        var data = 0;
        for (var i = 0; i < 100; i++)
        {
            data++;
            var result = _cache.TryAdd($"test-prefix:{data}", data);
            _logger.LogInformation($"TryAdd: {result}");
        }

        _cache.RemoveByPrefix("test-prefix:");
        Assert.Equal(100, data);
    }

    /// <summary>
    /// 测试 - 清空缓存
    /// </summary>
    [Fact]
    public void Test_Clear()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = _cache.Get("d", () =>
            {
                data++;
                return data;
            });
            _cache.Clear();
        }
        Assert.Equal(3, result);
    }

    private class TestCacheModel
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }
}
