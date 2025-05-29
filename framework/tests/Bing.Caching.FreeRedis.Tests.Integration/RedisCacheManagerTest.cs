using Microsoft.Extensions.Logging;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// Redis 缓存管理器测试
/// </summary>
public class RedisCacheManagerTest
{
    /// <summary>
    /// 缓存服务
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<RedisCacheManagerTest> _logger;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public RedisCacheManagerTest(ICache cache, ILogger<RedisCacheManagerTest> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    #region Get

    /// <summary>
    /// 测试 - 获取缓存
    /// </summary>

    [Fact]
    public void Test_Get_1()
    {
        //变量定义
        var key = "r:Test_Get_1";
        var value = 1;

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存
        _cache.Set(key, value);

        //验证
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 获取缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_Get_2()
    {
        //变量定义
        var key = new CacheKey("r:Test_Get_2");
        var value = 1;

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存
        _cache.Set(key, value);

        //验证
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据 - 默认8小时过期
    /// </summary>
    [Fact]
    public void Test_Get_3()
    {
        var result = 0;
        var data = 0;
        for (int i = 0; i < 3; i++)
        {
            result = _cache.Get("r:Test_Get_3", () => {
                data++;
                return data;
            });
        }
        Assert.Equal(1, result);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_Get_4()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = _cache.Get(new CacheKey("r:Test_Get_4"), () => {
                data++;
                return data;
            });
        }
        Assert.Equal(1, result);

        _cache.Clear();
    }

    #endregion

    #region GetAll

    /// <summary>
    /// 测试 - 获取缓存 - 获取集合
    /// </summary>
    [Fact]
    public void Test_GetAll_1()
    {
        //变量定义
        var key = "r:Test_GetAll_11";
        var key2 = "r:Test_GetAll_12";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //验证
        var result = _cache.GetAll<int?>(new[] { key, key2 });
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 获取缓存 - 获取集合,设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_GetAll_2()
    {
        //变量定义
        var key = new CacheKey("r:Test_GetAll_21");
        var key2 = new CacheKey("r:Test_GetAll_22");

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //验证
        var result = _cache.GetAll<int?>(new[] { key, key2 });
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        _cache.Clear();
    }

    #endregion

    #region GetAsync

    /// <summary>
    /// 测试 - 获取缓存
    /// </summary>
    [Fact]
    public async Task Test_GetAsync_1()
    {
        //变量定义
        var key = "r:Test_GetAsync_1";
        var value = 1;

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存
        await _cache.SetAsync(key, value);

        //验证
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 获取缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_GetAsync_2()
    {
        //变量定义
        var key = new CacheKey("r:Test_GetAsync_2");
        var value = 1;

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存
        await _cache.SetAsync(key, value);

        //验证
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 从缓存中获取数据 - 默认8小时过期
    /// </summary>
    [Fact]
    public async Task Test_GetAsync_3()
    {
        var result = 0;
        var data = 0;
        for (var i = 0; i < 3; i++)
        {
            result = await _cache.GetAsync("r:Test_GetAsync_3", async () => {
                data++;
                return await Task.FromResult(data);
            });
        }
        Assert.Equal(1, result);

        await _cache.ClearAsync();
    }

    #endregion

    #region GetAllAsync

    /// <summary>
    /// 测试 - 获取缓存 - 获取集合
    /// </summary>
    [Fact]
    public async Task Test_GetAllAsync_1()
    {
        //变量定义
        var key = "r:Test_GetAllAsync_11";
        var key2 = "r:Test_GetAllAsync_12";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //验证
        var result = await _cache.GetAllAsync<int?>(new[] { key, key2 });
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 获取缓存 - 获取集合,设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_GetAllAsync_2()
    {
        //变量定义
        var key = new CacheKey("r:Test_GetAllAsync_21");
        var key2 = new CacheKey("r:Test_GetAllAsync_22");

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //验证
        var result = await _cache.GetAllAsync<int?>(new[] { key, key2 });
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        await _cache.ClearAsync();
    }

    #endregion

    #region GetByPrefix

    /// <summary>
    /// 测试 - 通过缓存键前缀获取数据
    /// </summary>
    [Fact]
    public void Test_GetByPrefix_1()
    {
        //变量定义
        var key = "jd:Test_GetByPrefix_11";
        var key2 = "jd:Test_GetByPrefix_12";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //验证
        var result = _cache.GetByPrefix<int?>("jd").OrderBy(t => t).ToList();
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        _cache.Clear();
    }

    #endregion

    #region GetByPrefixAsync

    /// <summary>
    /// 测试 - 通过缓存键前缀获取数据
    /// </summary>
    [Fact]
    public async Task Test_GetByPrefixAsync_1()
    {
        //变量定义
        var key = "ef:Test_GetByPrefixAsync_11";
        var key2 = "ef:Test_GetByPrefixAsync_12";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //验证
        var result = (await _cache.GetByPrefixAsync<int?>("ef")).OrderBy(t => t).ToList();
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        await _cache.ClearAsync();
    }

    #endregion

    #region TrySet

    /// <summary>
    /// 测试 - 设置缓存
    /// </summary>
    [Fact]
    public void Test_TrySet_1()
    {
        //变量定义
        var key = "Test_TrySet_1";
        var value = 1;

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存1
        _cache.TrySet(key, value);
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        //设置缓存2,无效
        _cache.TrySet(key, 2);
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_TrySet_2()
    {
        //变量定义
        var key = new CacheKey("Test_TrySet_2");
        var value = 1;

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存1
        _cache.TrySet(key, value);
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        //设置缓存2,无效
        _cache.TrySet(key, 2);
        result = _cache.Get<int?>(key);
        Assert.Equal(value, result);

        _cache.Clear();
    }

    #endregion

    #region TrySetAsync

    /// <summary>
    /// 测试 - 设置缓存
    /// </summary>
    [Fact]
    public async Task Test_TrySetAsync_1()
    {
        //变量定义
        var key = "Test_TrySetAsync_1";
        var value = 1;

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存1
        await _cache.TrySetAsync(key, value);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        //设置缓存2,无效
        await _cache.TrySetAsync(key, 2);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_TrySetAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_TrySetAsync_2");
        var value = 1;

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存1
        await _cache.TrySetAsync(key, value);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        //设置缓存2,无效
        await _cache.TrySetAsync(key, 2);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(value, result);

        await _cache.ClearAsync();
    }

    #endregion

    #region Set

    /// <summary>
    /// 测试 - 设置缓存
    /// </summary>
    [Fact]
    public void Test_Set_1()
    {
        //变量定义
        var key = "Test_Set_1";

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存1
        _cache.Set(key, 1);
        result = _cache.Get<int?>(key);
        Assert.Equal(1, result);

        //设置缓存2,覆盖
        _cache.Set(key, 2);
        result = _cache.Get<int?>(key);
        Assert.Equal(2, result);

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 设置缓存- 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_Set_2()
    {
        //变量定义
        var key = new CacheKey("Test_Set_2");

        //获取缓存,结果为空
        var result = _cache.Get<int?>(key);
        Assert.Null(result);

        //设置缓存1
        _cache.Set(key, 1);
        result = _cache.Get<int?>(key);
        Assert.Equal(1, result);

        //设置缓存2,覆盖
        _cache.Set(key, 2);
        result = _cache.Get<int?>(key);
        Assert.Equal(2, result);

        _cache.Clear();
    }

    #endregion

    #region SetAll

    /// <summary>
    /// 测试 - 设置缓存 - 设置集合
    /// </summary>
    [Fact]
    public void Test_SetAll_1()
    {
        //变量定义
        var key = "Test_SetAll_11";
        var key2 = "Test_SetAll_12";

        //设置缓存
        _cache.SetAll(new Dictionary<string, int> { { key, 1 }, { key2, 2 } });

        //验证
        Assert.Equal(1, _cache.Get<int>(key));
        Assert.Equal(2, _cache.Get<int>(key2));

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置集合,设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_SetAll_2()
    {
        //变量定义
        var key = new CacheKey("Test_SetAll_21");
        var key2 = new CacheKey("Test_SetAll_22");

        //设置缓存
        _cache.SetAll(new Dictionary<CacheKey, int> { { key, 1 }, { key2, 2 } });

        //验证
        Assert.Equal(1, _cache.Get<int>(key));
        Assert.Equal(2, _cache.Get<int>(key2));

        _cache.Clear();
    }

    #endregion

    #region SetAsync

    /// <summary>
    /// 测试 - 设置缓存
    /// </summary>
    [Fact]
    public async Task Test_SetAsync_1()
    {
        //变量定义
        var key = "Test_SetAsync_1";

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存1
        await _cache.SetAsync(key, 1);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(1, result);

        //设置缓存2,覆盖
        await _cache.SetAsync(key, 2);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(2, result);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_SetAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_SetAsync_2");

        //获取缓存,结果为空
        var result = await _cache.GetAsync<int?>(key);
        Assert.Null(result);

        //设置缓存1
        await _cache.SetAsync(key, 1);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(1, result);

        //设置缓存2,覆盖
        await _cache.SetAsync(key, 2);
        result = await _cache.GetAsync<int?>(key);
        Assert.Equal(2, result);

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置集合
    /// </summary>
    [Fact]
    public async Task Test_SetAllAsync_1()
    {
        //变量定义
        var key = "Test_SetAllAsync_11";
        var key2 = "Test_SetAllAsync_12";

        //设置缓存
        await _cache.SetAllAsync(new Dictionary<string, int> { { key, 1 }, { key2, 2 } });

        //验证
        Assert.Equal(1, await _cache.GetAsync<int>(key));
        Assert.Equal(2, await _cache.GetAsync<int>(key2));

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 设置缓存 - 设置集合,设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_SetAllAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_SetAllAsync_21");
        var key2 = new CacheKey("Test_SetAllAsync_22");

        //设置缓存
        await _cache.SetAllAsync(new Dictionary<CacheKey, int> { { key, 1 }, { key2, 2 } });

        //验证
        Assert.Equal(1, await _cache.GetAsync<int>(key));
        Assert.Equal(2, await _cache.GetAsync<int>(key2));

        await _cache.ClearAsync();
    }

    #endregion

    #region Exists

    /// <summary>
    /// 测试 - 缓存是否已存在
    /// </summary>
    [Fact]
    public void Test_Exists_1()
    {
        //变量定义
        var key = "Test_Exists_1";

        //缓存不存在
        Assert.False(_cache.Exists(key));

        //设置缓存
        _cache.Set(key, 1);

        //验证
        Assert.True(_cache.Exists(key));

        _cache.Clear();
    }

    /// <summary>
    /// 测试 - 缓存是否已存在 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_Exists_2()
    {
        //变量定义
        var key = new CacheKey("Test_Exists_2");

        //缓存不存在
        Assert.False(_cache.Exists(key));

        //设置缓存
        _cache.Set(key, 1);

        //验证
        Assert.True(_cache.Exists(key));

        _cache.Clear();
    }

    #endregion

    #region ExistsAsync

    /// <summary>
    /// 测试 - 缓存是否已存在
    /// </summary>
    [Fact]
    public async Task Test_ExistsAsync_1()
    {
        //变量定义
        var key = "Test_ExistsAsync_1";

        //缓存不存在
        Assert.False(await _cache.ExistsAsync(key));

        //设置缓存
        await _cache.SetAsync(key, 1);

        //验证
        Assert.True(await _cache.ExistsAsync(key));

        await _cache.ClearAsync();
    }

    /// <summary>
    /// 测试 - 缓存是否已存在 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_ExistsAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_ExistsAsync_2");

        //缓存不存在
        Assert.False(await _cache.ExistsAsync(key));

        //设置缓存
        await _cache.SetAsync(key, 1);

        //验证
        Assert.True(await _cache.ExistsAsync(key));

        await _cache.ClearAsync();
    }

    #endregion

    #region Remove

    /// <summary>
    /// 测试 - 移除缓存
    /// </summary>
    [Fact]
    public void Test_Remove_1()
    {
        //变量定义
        var key = "Test_Remove_1";

        //设置缓存
        _cache.Set(key, 1);
        Assert.True(_cache.Exists(key));

        //移除缓存
        _cache.Remove(key);

        //验证
        Assert.False(_cache.Exists(key));
    }

    /// <summary>
    /// 测试 - 移除缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_Remove_2()
    {
        //变量定义
        var key = new CacheKey("Test_Remove_2");

        //设置缓存
        _cache.Set(key, 1);
        Assert.True(_cache.Exists(key));

        //移除缓存
        _cache.Remove(key);

        //验证
        Assert.False(_cache.Exists(key));
    }

    /// <summary>
    /// 测试 - 移除缓存集合
    /// </summary>
    [Fact]
    public void Test_RemoveAll_1()
    {
        //变量定义
        var key = "Test_RemoveAll_11";
        var key2 = "Test_RemoveAll_12";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //移除缓存
        _cache.RemoveAll(new[] { key, key2 });

        //验证
        Assert.False(_cache.Exists(key));
        Assert.False(_cache.Exists(key2));
    }

    /// <summary>
    /// 测试 - 移除缓存集合 - 设置缓存键对象
    /// </summary>
    [Fact]
    public void Test_RemoveAll_2()
    {
        //变量定义
        var key = new CacheKey("Test_RemoveAll_21");
        var key2 = new CacheKey("Test_RemoveAll_22");

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //移除缓存
        _cache.RemoveAll(new[] { key, key2 });

        //验证
        Assert.False(_cache.Exists(key));
        Assert.False(_cache.Exists(key2));
    }

    #endregion

    #region RemoveAsync

    /// <summary>
    /// 测试 - 移除缓存
    /// </summary>
    [Fact]
    public async Task Test_RemoveAsync_1()
    {
        //变量定义
        var key = "Test_RemoveAsync_1";

        //设置缓存
        await _cache.SetAsync(key, 1);
        Assert.True(await _cache.ExistsAsync(key));

        //移除缓存
        await _cache.RemoveAsync(key);

        //验证
        Assert.False(await _cache.ExistsAsync(key));
    }

    /// <summary>
    /// 测试 - 移除缓存 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_RemoveAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_RemoveAsync_2");

        //设置缓存
        await _cache.SetAsync(key, 1);
        Assert.True(await _cache.ExistsAsync(key));

        //移除缓存
        await _cache.RemoveAsync(key);

        //验证
        Assert.False(await _cache.ExistsAsync(key));
    }

    /// <summary>
    /// 测试 - 移除缓存集合
    /// </summary>
    [Fact]
    public async Task Test_RemoveAllAsync_1()
    {
        //变量定义
        var key = "Test_RemoveAllAsync_11";
        var key2 = "Test_RemoveAllAsync_12";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //移除缓存
        await _cache.RemoveAllAsync(new[] { key, key2 });

        //验证
        Assert.False(await _cache.ExistsAsync(key));
        Assert.False(await _cache.ExistsAsync(key2));
    }

    /// <summary>
    /// 测试 - 移除缓存集合 - 设置缓存键对象
    /// </summary>
    [Fact]
    public async Task Test_RemoveAllAsync_2()
    {
        //变量定义
        var key = new CacheKey("Test_RemoveAllAsync_21");
        var key2 = new CacheKey("Test_RemoveAllAsync_22");

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //移除缓存
        await _cache.RemoveAllAsync(new[] { key, key2 });

        //验证
        Assert.False(await _cache.ExistsAsync(key));
        Assert.False(await _cache.ExistsAsync(key2));
    }

    #endregion

    #region RemoveByPrefix

    /// <summary>
    /// 测试 - 通过缓存键前缀移除缓存
    /// </summary>
    [Fact]
    public void Test_RemoveByPrefix_1()
    {
        //变量定义
        var key = "Test_RemoveByPrefix_11";
        var key2 = "Test_RemoveByPrefix_12";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //移除缓存
        _cache.RemoveByPrefix("Test_RemoveByPrefix");

        //验证
        Assert.False(_cache.Exists(key));
        Assert.False(_cache.Exists(key2));
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <summary>
    /// 测试 - 通过缓存键前缀移除缓存
    /// </summary>
    [Fact]
    public async Task Test_RemoveByPrefixAsync_1()
    {
        //变量定义
        var key = "Test_RemoveByPrefixAsync_11";
        var key2 = "Test_RemoveByPrefixAsync_12";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //移除缓存
        await _cache.RemoveByPrefixAsync("Test_RemoveByPrefixAsync");

        //验证
        Assert.False(await _cache.ExistsAsync(key));
        Assert.False(await _cache.ExistsAsync(key2));
    }

    #endregion

    #region RemoveByPattern

    /// <summary>
    /// 测试 - 通过缓存键模式移除缓存
    /// </summary>
    [Fact]
    public void RemoveByPattern()
    {
        //变量定义
        var key = "RemoveByPattern_1";
        var key2 = "RemoveByPattern_2";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //移除缓存
        _cache.RemoveByPattern("*2");

        //验证
        Assert.True(_cache.Exists(key));
        Assert.False(_cache.Exists(key2));
    }

    #endregion

    #region RemoveByPatternAsync

    /// <summary>
    /// 测试通过缓存键模式移除缓存
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync()
    {
        //变量定义
        var key = "RemoveByPatternAsync_1";
        var key2 = "RemoveByPatternAsync_2";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //移除缓存
        await _cache.RemoveByPatternAsync("*2");

        //验证
        Assert.True(await _cache.ExistsAsync(key));
        Assert.False(await _cache.ExistsAsync(key2));
    }

    #endregion

    #region Clear

    /// <summary>
    /// 测试 - 清空缓存
    /// </summary>
    [Fact]
    public void Test_Clear()
    {
        //变量定义
        var key = "Test_Clear_1";
        var key2 = "Test_Clear_2";

        //设置缓存
        _cache.Set(key, 1);
        _cache.Set(key2, 2);

        //清空缓存
        _cache.Clear();

        //验证
        Assert.False(_cache.Exists(key));
        Assert.False(_cache.Exists(key2));
    }

    #endregion

    #region ClearAsync

    /// <summary>
    /// 测试 - 清空缓存
    /// </summary>
    [Fact]
    public async Task Test_ClearAsync()
    {
        //变量定义
        var key = "Test_ClearAsync_1";
        var key2 = "Test_ClearAsync_2";

        //设置缓存
        await _cache.SetAsync(key, 1);
        await _cache.SetAsync(key2, 2);

        //清空缓存
        await _cache.ClearAsync();

        //验证
        Assert.False(await _cache.ExistsAsync(key));
        Assert.False(await _cache.ExistsAsync(key2));
    }

    #endregion
}
