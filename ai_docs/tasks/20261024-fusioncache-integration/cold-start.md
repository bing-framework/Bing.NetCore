# FusionCache 冷启动与回源排队清单

本文把 FusionCache 集成中的冷启动按触发来源分类，说明哪些场景只增加缓存/Redis 读取，哪些场景会调用业务回源工厂。配置与结论基于当前代码和已有本地 Redis 测量，不代表生产容量承诺。

## 场景清单

| 冷启动或相近场景 | 缓存状态 | 是否通常调用业务工厂 | 主要影响 |
| --- | --- | --- | --- |
| 新进程启动、扩容新节点 | 新进程 L1 冷；共享 Redis L2 仍热 | 对 L2 有效的键不会；L2 未命中/过期的键会 | Redis L2 读量增加，Backplane 只传播失效，不会把其他进程的 L1 数据复制过来 |
| 新 Namespace、首次上线新缓存名或首次访问新键 | 对应新键空间的 L1/L2 都冷 | 会，每个不同未命中键都可能执行工厂 | 首批请求集中打到数据库或远程源；新版本键空间切换也属于此类 |
| 大量相近 TTL 同时到期 | 本地和/或 L2 条目集中变为不可用 | 会，按到期后实际被访问的不同键触发 | 形成到期波峰；访问量越集中，回源压力越高 |
| Remove、InvalidateTag、Clear 后重建 | 被失效的键不可见；失效元数据不是业务值 | 后续读取这些键时会 | 批量失效若紧接高访问流量，会集中重建；失效操作本身不预热 |
| L1 被容量策略或本地过期淘汰，L2 仍有值 | 当前进程 L1 冷，Redis L2 热 | 通常不会 | 增加 L2 读取和反序列化，不消耗回源许可；若 L2 的值也不可用则转为回源 |
| L2 淘汰、清空或丢失数据 | 各进程仍保留的 L1 可继续命中；L1 不可用的键失去 L2 后备 | 对 L1 也未命中的键会 | 可能同时触发多节点不同键回源；Redis 重启若数据恢复则不一定属于此类，需看实际数据是否丢失 |
| Redis/L2/回源故障期间降级 | 可能有 L1 或 Fail-Safe 旧值，也可能两层均不可读 | 视配置而定；无可用值时可能回源 | 这是故障降级/恢复压力，不等同于严格冷启动；Fail-Safe 默认关闭，启用时应限制旧值窗口 |
| 缓存键/schema 版本迁移 | 使用新版本键前缀或 Namespace 时，新空间没有旧值 | 通常会 | 相当于有计划的全空间冷启动；通过版本键分批迁移和预热。若保留旧键但只改变序列化格式，可能抛反序列化异常，不一定自动变成 miss |
| Eager Refresh（可选） | 条目仍有值但已接近过期，并且再次被访问 | 可能启动后台刷新工厂 | 不是启动预热或定时全量刷新；刷新工厂计入回源预算，默认关闭 |

表中的“会”指 FusionCache 判定 L1/L2 都没有可用值、Fail-Safe 等策略没有直接返回旧值后，业务工厂实际开始执行。Backplane 失效消息的延迟可能让其他进程短暂保留本地条目；TTL 是最终回收边界，不是强一致屏障。

## 排队与合并规则

- 默认限制按**每个进程、每个命名缓存实例**计算：最多 8 个活跃回源工厂，额外最多排队 128 个，排队等待最多 30 秒。配置在 `BingFusionCacheOptions.MaxConcurrentFactories`、`MaxQueuedFactories`、`FactoryQueueTimeout`。
- 当前活跃和等待许可的工厂最多登记 136 个。队列已满会立即拒绝；排队超过 30 秒会以超时失败。30 秒只限制许可等待，不含工厂执行时间；工厂自身硬超时单独由 FusionCache 策略配置。
- FusionCache 对同键并发执行合并：64 个请求同时访问同一个冷键时，已有测量只观察到 1 个实际工厂、峰值 1 活跃、0 排队。合并等待者不占 8/128 额度。
- 64 个不同冷键各自需要回源时，会有 64 个不同工厂争抢同一预算；已有测量观察到 8 活跃、56 排队，.NET 8 P95 126.75 ms、.NET 10 P95 124.17 ms。两组测量均为本地单轮测试，5 ms 工厂延迟，不外推生产数据库容量。
- 回源额度包围业务工厂，不包围缓存读取：L1/L2 命中、Redis L2 网络 IO、序列化、Backplane 失效 IO都不占回源许可。L2 故障仍可能令原本应命中的请求转向业务工厂。
- 每个节点有自己的准入状态。N 个节点使用一个命名实例时，最坏本地并发额度可达 `N × 8`，队列可达 `N × 128`；多个命名实例继续相加。按所有节点和实例的总量对照共享数据库连接池及回源服务，而非只看单节点默认值。
- 可选分布式锁可减少跨节点同键重复回源，但锁获取超时可能退化为继续执行工厂，不提供严格互斥。容量规划仍按每节点本地额度相加，并为热点、多键冷启动和故障时的重复回源预留余量。

## 触发来源与缓解

本包没有宿主启动时的无条件缓存扫描、全量预热或后台定时刷新。仓库现提供显式启用的 [warmup 宿主启动样例](../../../samples/Bing.Samples.FusionCache/README.md#热门键分批预热与宿主启动)，演示有限热门键分批预热、总预算、取消与失败统计；它不是缓存包自动注册的功能。缓存只由请求或调用方显式 `GetOrCreateAsync` 触发。可选 Eager Refresh 由接近过期条目的访问触发；若开启，刷新工厂也使用相同准入预算。当前默认 `EagerRefreshThreshold = null`。

处理突发冷启动时，可依次采取以下措施：

1. 从业务已有的热门键清单预热，不遍历全库或尝试恢复整个缓存。
2. 小批次并发，每批结束后再继续；批次大小按可用数据库连接和实时请求预留量决定。冷启动跨多个节点时，还要考虑每节点预算会相加。
3. 对大量写入启用 TTL jitter（`JitterMaxDuration`）以打散集中到期；确认标签/Clear 元数据保留期仍覆盖加入抖动后的最大物理 TTL。
4. 对 Redis L2 保持足够容量和持久化/恢复策略。批量失效应分业务域、分时间推进；Cache Clear 不应被当成无成本的预热替代。
5. 观测队列和回源的真实变化，再调整并发/队列上限。提高 `MaxConcurrentFactories` 会把压力转移到数据库和下游；提高队列上限只会允许更多请求等待，不会增加回源吞吐。

以下示例只按调用方提供的热门产品 ID 预热，每批最多 4 个并发操作；实际数据库查询请通过独立 DI scope 和取消令牌执行。它不会枚举数据库或缓存中的全部键：

```csharp
static async Task PrewarmAsync(
    IHybridCache cache,
    IEnumerable<string> hotProductIds,
    Func<string, CancellationToken, Task<string>> loadProduct,
    CancellationToken cancellationToken)
{
    const int batchSize = 4;

    foreach (var batch in hotProductIds.Chunk(batchSize))
    {
        await Task.WhenAll(batch.Select(id => cache.GetOrCreateAsync(
            $"product:{id}",
            token => loadProduct(id, token),
            cancellationToken: cancellationToken)));
    }
}
```

## 观测与证据

建议并列查看官方 FusionCache 命中/未命中事件和 Bing 自定义指标：

| 指标 | 含义 | 判断方式 |
| --- | --- | --- |
| `bing.cache.factory.active` | 正在执行业务回源工厂数 | 接近 8 说明该命名实例本地并发预算已用满 |
| `bing.cache.factory.waiting` | 等待本地回源许可的工厂数 | 持续升高表示不同冷键正在排队；同键合并等待者不计入 |
| `bing.cache.factory.rejected` | 回源准入拒绝数，含 `reason` 维度 | 区分 `full`、`timeout`、`cancelled`，识别超载或取消 |
| `bing.cache.factory.wait.duration` | 等待许可时间，单位毫秒 | 结合 P95/P99 和队列峰值判断请求延迟预算 |

指标标签仅使用固定 cache name 和拒绝原因，不包含 key/tag。官方详细日志或 trace 可能包含业务键，应在 exporter 侧脱敏。当前适配器的仪表说明见[包 README](../../../framework/src/Bing.Caching.FusionCache/README.md)。

本地既有 Redis 7.4 测量的完整工作负载、方法和限制记录在[性能报告](performance.md)，原始数据见[.NET 8](../../../output/fusioncache-validation/review-fix-1/performance-net8.json)和[.NET 10](../../../output/fusioncache-validation/review-fix-1/performance-net10.json) JSON；测试方法为[真实 Redis 资源测量](../../../framework/tests/Bing.Caching.FusionCache.Tests.Integration/FusionCacheResourceTest.cs)。该证据使用 64 个并发请求、每个工厂 5 ms 可取消延迟、每个 TFM 单轮测量；它没有覆盖生产流量分布、真实数据库查询或多节点合计压力。

实现入口：[工厂准入及指标](../../../framework/src/Bing.Caching.FusionCache/Bing/Caching/FusionCache/Internal/FusionCacheFactoryInvoker.cs)、[8/128/30 秒默认配置](../../../framework/src/Bing.Caching.FusionCache/Bing/Caching/FusionCache/BingFusionCacheOptions.cs)、[共享准入队列](../../../framework/src/Bing.Caching/Bing/Caching/CacheFactoryAdmission.cs)。Eager Refresh 计入工厂预算的测试见[ResilienceTest](../../../framework/tests/Bing.Caching.FusionCache.Tests/FusionCacheResilienceTest.cs)；分布式锁超时继续回源的真实 Redis 测试见[LockerTest](../../../framework/tests/Bing.Caching.FusionCache.Tests.Integration/FusionCacheLockerTest.cs)。
