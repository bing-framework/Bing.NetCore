# 迁移到 FusionCache

本文适用于把新业务或现有缓存读写路径迁移到 Bing.Caching.FusionCache。它新增异步单键契约 IHybridCache，不把 FusionCache 伪装成旧 ICache，也不改变既有 Provider 的实现和服务注册。

## 迁移前确认

- 应用宿主目标框架为 net8.0 或更高；FusionCache 2.9.0 不纳入现有 net6.0 宿主的兼容承诺。
- 先分类调用点：普通 cache-aside、可接受短时旧值、需要按标签批量失效的读模型可评估迁移；原子 TrySet、任意前缀/模式扫描、批量/流式操作继续使用旧 Provider。
- 为新缓存空间指定固定且包含应用/环境的受信 Namespace。不要从 HTTP 请求直接接受 Namespace、cache name 或失效标签。
- 选择稳定 DTO 与兼容的 System.Text.Json 设置。FusionCache 的 Redis L2 元数据封装不同于旧 Provider，不能复用旧物理键或猜测读取旧载荷。
- 确认回源服务能在异步工厂内部独立创建和释放 DI scope。超时、Eager Refresh 或后台完成都可能让工厂超过调用者请求生命周期。

注册和可运行示例见[包 README](../../framework/src/Bing.Caching.FusionCache/README.md)与[FusionCache console sample](../../samples/Bing.Samples.FusionCache/README.md)。

## 灰度发布

建议分阶段切换，且每个阶段的读路径使用一个明确的 Provider：

1. **保留旧读路径并发布双空间失效。** 在数据库事务提交成功后，应用同时使旧 Provider 的键/前缀失效，并使 FusionCache 新空间的键/标签失效。旧版实例仍在服务期间也必须发出这两个失效动作；如业务使用事务后事件，应让现有 outbox/事件投递负责重试，不能假定两个 Redis 操作和数据库提交是原子的。
2. **只读灰度的新空间。** 将一小部分新实例切到 FusionCache，使用新的键空间冷启动。不要先读 FusionCache miss 再从旧缓存回填，避免把两个空间的 TTL、序列化和标签关系混在一起。监测回源速率、L1/L2 命中、Fail-Safe 返回、Backplane 错误与 Redis 连接。
3. **逐步扩大流量。** 按业务风险分批提高新实例比例。必要时在切流前预热热门键，或限制回源并发并在负载高峰之外切换；评估缓存冷启动对数据库连接池、查询延迟和回源服务容量的影响。
4. **排空旧实例。** 确认所有旧读者退出后，等待旧请求和 in-flight 工厂结束，再停止双写旧空间失效。新空间的失效仍继续；新 Provider 已产生的数据在自然 TTL/清理策略内保留。

FusionCache 的冷启动类型、哪些情况会实际触发回源、队列上限和预热示例见[冷启动与回源排队清单](../../ai_docs/tasks/20261024-fusioncache-integration/cold-start.md)。该包不会在启动时扫描或预热整个缓存；上线预热应使用业务已知的热门键，并按数据库/下游连接池容量分批。

灰度过程中，两个空间内容独立；双空间失效只协调可见性，并不构成跨缓存原子提交。若某个变更只能让其中一侧失效，另一侧可能在 TTL 内返回旧数据。

## 回滚

切回旧 Provider **之前**先恢复数据库提交后的双空间失效，再处置旧缓存域，三种方式任选其一：

1. 使用旧 Provider 的业务键/前缀失效能力清除拟切回的域，确认完成后切流，接受冷启动回源。
2. 先失效旧域，再从权威数据库预热热门键；预热期间继续双空间失效，确认没有旧工厂晚到写回后切流。不能从新缓存复制载荷来代替权威数据。
3. 切换到全新的旧 Provider 命名空间，让回滚版本和失效发布者使用相同新身份，预热或冷启动后切流，原旧空间自然到期。

回滚版本及仍存活的新版本继续双空间失效，直到所有 FusionCache 读者、后台任务和在途工厂退出；仅切换读路径并不足以停止双空间失效。不要把 FusionCache Redis 键交给旧 Provider 解析，也不要用 FLUSHDB 清理共享 Redis。确认新实例已排空后，再由单独审核的运维流程处理新物理空间；否则保留其数据直至自然过期。

若新版本已写入数据库、旧版本只读旧缓存，回滚期间同样要保持数据库提交后的旧缓存失效。切换 Provider 不回滚已经完成的数据库写入或缓存副作用。

## 一致性与失效边界

- L1 是进程本地缓存；Redis L2 不会单独清除其他进程的 L1。要支持多实例失效，所有实例必须使用一致且隔离的 Backplane 身份并连接同一 Backplane。
- Backplane 通知和 Redis 故障恢复按最终一致性处理。失效通知传播期间可能读到旧 L1；Backplane/L2 故障、进程暂停或网络分区会扩大可见窗口。TTL 是回收旧值的最终边界，不是强一致性 SLA。
- Fail-Safe 可能在工厂失败或超时后返回过期值，必须按数据敏感度设置最大旧值窗口。访问控制、权限撤销、库存、幂等和认证状态等正确性路径不应仅依赖缓存失效。
- Remove、标签失效和 Clear 改变缓存可见性，不等同于物理擦除或数据库事务屏障。标签仅匹配写入时显式关联的条目，不会隐式匹配旧前缀。
- 标签/Clear 元数据使用独立的 `TagsDefaultEntryOptions` 长保留期；不要将业务条目 TTL 或 Fail-Safe 直接复制到该元数据配置。元数据 L2/Backplane 操作默认在前台执行并重新抛出异常，容量预算应与业务条目分别评估。
- in-flight 的旧工厂可能晚于显式 Set/Remove 完成并写回。本集成没有旧 Provider 的租约 fencing 或“显式修改永远胜过迟到工厂”保证；必须接受该窗口、采用版本化业务键，或继续让这类调用走旧 Provider。

## TTL 与原生配置边界

门面将正数基础 TTL 向上取整到毫秒；具体 L2 Provider 的存储精度可能不同。固定的 Microsoft.Extensions.Caching.StackExchangeRedis 10.0.1 使用整秒 Redis 过期时间，小于 1 秒的物理保留期可能立即消失。Redis 场景应使用秒级以上的 L2 保留期，并单独验证短 TTL；L1 毫秒级测试不代表 L2 具备相同精度。[官方实现](https://github.com/dotnet/aspnetcore/blob/v10.0.1/src/Caching/StackExchangeRedis/src/RedisCache.cs)

标签/Clear 元数据保留窗口必须覆盖业务条目可能保留的最大窗口，包含 L1/L2、Fail-Safe 与 L1 抖动。适配器在实例解析及每次 TTL 覆盖时校验：数据最长物理保留期不得超过已启用各层元数据的最短物理保留期；不满足就抛出 InvalidOperationException，不执行工厂或写入。元数据禁止跳过已启用层的读写。若把业务 TTL 延长到原生元数据默认窗口之外，需要同时通过原生 builder 延长元数据保留期。逻辑 Duration 较短但物理 FailSafeMaxDuration 足够长的元数据配置仍允许。

## 冷启动与回源排队

冷启动不是单一场景。新进程或扩容节点通常只有 L1 为空；如果 Redis L2 仍有对应条目，请求只会从 L2 读取，不会调用业务回源工厂。只有缓存各层都没有可用值，或显式失效/版本迁移使对应键不可见时，实际工厂才进入本集成的回源准入。

默认每个进程中的每个命名缓存实例最多执行 8 个回源工厂，最多额外排队 128 个；队列等待最多 30 秒，不含工厂自身执行时间。当前执行和排队请求最多登记 136 个；超过队列容量会因队列已满而失败，排队超过 30 秒会超时。可以通过 `BingFusionCacheOptions.MaxConcurrentFactories`、`MaxQueuedFactories` 和 `FactoryQueueTimeout` 调整。额度只覆盖本地实际回源工厂，不覆盖命中、同键合并等待者、L1/L2 查找、Redis 序列化或失效消息 IO。

额度按命名缓存实例、按进程隔离，不是整个集群共享的全局上限。若有 N 个节点、每个节点使用一个同名实例，理论上游并发预算最多是 `N × 8`；多个命名实例还会继续相加。可选 Redis 分布式锁可以减少跨节点同键重复回源，但锁超时或配置为不重抛时仍会继续执行工厂，因此不能把它当成严格互斥或容量上限。应按数据库连接池和回源服务的承载能力设置每节点额度，并为多个节点及多个实例计算总量。

同键请求先由 FusionCache 合并，只有获准执行的实际工厂占用额度。不同冷键各自执行工厂，会竞争同一实例的 8 个活跃名额；其余进入等待队列。固定 TTL 导致集中到期、批量失效后热门键同时重建，或首次切入新的命名空间，都可能使不同键并发回源。L1 淘汰但 L2 仍热通常只是 L2 读取；L2 丢失、过期或命名空间变更才会把相应键推到业务回源。Fail-Safe 可在允许的窗口内返回旧值，降低可见错误，但是否触发回源仍取决于条目状态和策略。

本地 Docker Redis 7.4 的一次 Release 测量以 64 个并发请求访问 64 个不同冷键，每个业务工厂含 5 ms 可取消延迟：64 个工厂都执行，峰值 8 个活跃、56 个排队；.NET 8 P95 为 126.75 ms，.NET 10 P95 为 124.17 ms。同一测试以 64 个请求访问同一个冷键时，仅 1 个工厂执行、峰值 1 活跃、0 排队。它证明了同键合并和不同键排队的行为，不是生产容量基线：每个 TFM 只测一轮，且该结果不覆盖生产数据库延迟、节点数、真实到期波形或共享连接池争用。[测试场景](../../framework/tests/Bing.Caching.FusionCache.Tests.Integration/FusionCacheResourceTest.cs)；[测量记录](../../ai_docs/tasks/20261024-fusioncache-integration/performance.md)。

该包没有启动时自动预热、后台全键扫描或定时全量刷新。可选的 FusionCache Eager Refresh 是访问驱动：条目接近到期且再次被访问时，才可能启动后台刷新；默认关闭，启用后实际刷新工厂仍消耗同一回源额度。标签失效和 Clear 也不主动枚举所有业务键或预热数据，之后的请求按需重建。

预热应从权威数据源已有的热门键清单中选择条目，分批并发并在每批完成后再继续；批次大小以数据库/下游连接池预算为准，并为实时流量预留容量。不要在启动时一次性创建覆盖全量键的并发任务。对大量条目使用 `JitterMaxDuration` 打散到期时间，并确保标签/Clear 元数据保留期仍长于包含抖动后的最大物理保留期。可按业务调整下例的批次大小：

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

排查冷启动时同时观察官方命中/未命中与 Bing 指标 `bing.cache.factory.active`、`bing.cache.factory.waiting`、`bing.cache.factory.rejected`、`bing.cache.factory.wait.duration`。等待指标表示正在申请本地回源许可的不同键工厂，不包含同键合并等待者；拒绝指标还应按 `reason` 区分队列已满、等待超时和取消。指标只带固定 cache name，不应加入业务 key/tag。更细的场景表和各类冷启动的实际影响见[任务清单](../../ai_docs/tasks/20261024-fusioncache-integration/cold-start.md)。

默认专属 L1 沿用 MemoryCache 的过期清理；需要固定容量时使用样例的 SizeLimit 配置。回源预算限制活跃及排队工厂，不等于缓存载荷字节上限。相同实例的工厂中允许不同键命中，但嵌套冷回源会被拒绝，防止持有额度后等待自身预算；在工厂外组合依赖。

默认前台操作会报告实际 IO 失败，已经成功的 L1/L2 副作用无法回滚。若通过原生配置开启断路器或容错，断路器打开期间可能跳过远端操作，成功返回不能解释为所有节点已同步。Host 停止也不会强制终止忽略取消的业务代码；在释放宿主前应由应用等待自身后台工作结束。

## 启动、关闭与锁故障

适配器强制 `WaitForInitialBackplaneSubscribe=true`，等待配置的 Backplane 首次 Subscribe 完成；记录订阅异常并使实例解析/宿主启动失败，不允许覆盖成异步启动。样例和测试使用同一策略。该就绪状态只说明初始订阅已完成，不承诺后续网络始终健康，也不意味着一次发布已经被所有读者处理。

`shutdown` 样例提供应用拥有的 `SampleFactoryLifetime`：在实际工厂内部登记工作，内部创建独立 scope；宿主停止后拒绝新工厂，等待已有工厂及 scope 完成，再释放容器。排空器最后注册，因此先执行 StopAsync。关闭预算耗尽会抛出取消异常，不能宣称已排空；应用应停止入口流量、给合作式工厂足够关闭时间。这只等待应用工厂，不是原生自动恢复队列的持久化或全部 IO 排空承诺。

Redis L2、Backplane、locker 分别使用专用连接并由 DI 管理组件。原生 Backplane 在缓存释放时解除订阅；官方 locker 会释放其 ConnectionMultiplexerFactory 返回的连接，因此不要向它借出共享外部连接。门面和订阅包装器不重复 Dispose 调用方拥有的组件。

固定 2.9.0 的真实 Redis 锁测试确认：`DistributedLockTimeout=80ms` 到期未获得锁会继续回源，即使 `ReThrowDistributedLockerExceptions=true`；锁超时不是严格互斥。锁专属连接被 Redis ACL 拒绝时，默认不重抛会继续工厂，工厂失败可按 Fail-Safe 返回旧值；显式重抛则抛出 `FusionCacheDistributedLockerException`，不执行工厂，现有旧值也不会掩盖这次锁获取错误。恢复权限后可再次获取锁。生产应显式选择等待预算与重抛策略。

崩溃测试先保存 Redis 已确认值 1，停止 Redis，在仍存活节点写入本地值 2 并产生进程内恢复工作，随后强制退出该节点；恢复 Redis 并启动新进程读到值 1。该窗口内值 2 丢失，恢复队列不是持久 outbox。暂停 Redis 的另一诊断还观察到：超时命令可能已在 TCP 中，恢复后仍被执行，不能把超时解释成远端绝无副作用。

## 诊断输出约束

Bing 自定义指标/活动只使用 cache name 等有限维度，不输出原始 key/tag。cache name 应来自固定配置，不能每请求/租户动态生成。官方 FusionCache trace、详细日志可能携带完整键，不能把演示 Console exporter 原样用于含身份信息的生产数据；应在 exporter 前删除或脱敏 `fusioncache.operation.key` / `fusioncache.operation.tag`，检查日志级别和采样，不把 key/tag 提升成指标维度。直接 MeterListener/ActivityListener 测试覆盖自定义 wait.duration、官方状态指标与活动来源；样例仅打印非敏感随机演示键。

## 验证

先运行内存和 Fail-Safe 演示，再用隔离的开发/测试 Redis 验证 L2、Backplane 与双实例失效。环境变量 BING_TEST_FUSIONCACHE_REDIS 只用于样例测试地址，不应指向生产 Redis。

```powershell
dotnet build samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -c Release -f net8.0
dotnet build samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -c Release -f net10.0
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- memory
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- failsafe
```

## 历史依赖安全补丁

本次在 Bing.Core 约束 System.Text.Json 最低版本为 6.0.10，修复 DependencyModel 6.0.0 引入的 [CVE-2024-43485](https://github.com/advisories/GHSA-8g4q-xg66-9fp4)。在 Bing.EasyCaching 约束 System.Drawing.Common 最低版本为 5.0.3，修复旧 Redis 性能计数器依赖链引入的 [CVE-2021-24112](https://github.com/dotnet/announcements/issues/176)。后者涉及 Linux/macOS 图形文件解析；发现包依赖不等于缓存路径已被证明可利用。

这些是保持 netstandard2.0/net6 兼容的最小历史补丁；5.x/6.x 分支已结束维护，本次审计无已知漏洞不等于长期安全支持。部署后请检查最终宿主依赖图和实际运行时版本。升级到新 Provider 后若仍引用旧 Bing.Core/Bing.EasyCaching 包，宿主仍需更新这些包，不能只更新 FusionCache 包。完整结果见[执行记录](../../ai_docs/tasks/20261024-fusioncache-integration/execution.md)。
