using Bing.Extensions;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.Configuration;

namespace Bing.EasyCaching;

/// <summary>
/// 在 EasyCaching 初始化期间聚合 Redis 服务端点的内部状态。
/// </summary>
internal class CachingOptions
{
    /// <summary>
    /// 保存跨配置调用累积的 Redis 服务端点列表；该状态为进程级静态集合，不会按配置对象隔离。
    /// </summary>
    private static List<ServerEndPoint> _redisEndPoints = new();

    /// <summary>
    /// 从配置节读取 Redis 服务端点并追加到进程级集合，不会清除此前已添加的端点。
    /// </summary>
    /// <param name="configuration">包含 Redis 配置节的配置对象。</param>
    /// <param name="section">Redis 配置节名称。</param>
    public static void AddRedisEndPoints(IConfiguration configuration, string section)
    {
        var config = configuration.GetSection($"{section}:DbConfig:Endpoints");
        var endpoints = config.GetChildren();
        foreach (var endpoint in endpoints)
        {
            var host = endpoint["Host"];
            var port = endpoint["Port"].ToIntOrNull() ?? 6379;
            _redisEndPoints.Add(new ServerEndPoint(host, port));
        }
    }

    /// <summary>
    /// 执行配置委托并将其定义的 Redis 服务端点追加到进程级集合，不会清除此前已添加的端点。
    /// </summary>
    /// <param name="setupAction">配置 Redis 选项的委托。</param>
    public static void AddRedisEndPoints(Action<RedisOptions> setupAction)
    {
        setupAction.CheckNull(nameof(setupAction));
        var options = new RedisOptions();
        setupAction(options);
        _redisEndPoints.AddRange(options.DBConfig.Endpoints);
    }

    /// <summary>
    /// 获取当前已累积的 Redis 服务端点列表。
    /// </summary>
    /// <returns>内部端点列表的可变引用；调用方修改会影响后续初始化，且该列表不应在并发配置期间直接修改。</returns>
    public static List<ServerEndPoint> GetRedisEndPoints() => _redisEndPoints;

    /// <summary>
    /// 清理初始化期间累积的 Redis 服务端点状态。
    /// </summary>
    /// <remarks>
    /// 当前实现会将内部列表设为 <c>null</c>，因此清理后不应再次调用添加或读取方法；该方法不恢复为可用的空集合。
    /// </remarks>
    public static void Clear()
    {
        _redisEndPoints = null;
    }
}
