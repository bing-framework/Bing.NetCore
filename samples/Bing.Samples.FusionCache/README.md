# Bing.Samples.FusionCache

该 console sample 展示 **IHybridCache / IHybridCacheProvider** 的单实例内存缓存、Redis L2 与 Backplane、多实例失效和 Fail-Safe。Redis 地址只从 **BING_TEST_FUSIONCACHE_REDIS** 读取；Redis 命令必须指向可丢弃的本地或测试服务，不要配置生产实例。

## 运行

在仓库根目录执行。memory、failsafe 不需要 Redis：

```powershell
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- memory
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net10.0 -- failsafe
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- shutdown
```

`memory --size-limit` 演示专属 DI singleton `MemoryCache` 的 L1 `SizeLimit`：每个业务条目配置为 1 个应用自定义单位，FusionCache 标签元数据为 0 并保留原生长 TTL。这个单位不是字节数，`1000` 也不是生产容量建议；不要把限额缓存共享给其他 named cache 或无关服务。

Redis 模式启用 L2 和 Backplane；multi 在两个独立 Host/DI 容器中建立同身份实例，先在第二实例预热，再验证第一个实例的写入通过 Backplane 传播：

```powershell
$env:BING_TEST_FUSIONCACHE_REDIS = 'localhost:6379'
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- redis
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- multi
```

可附加 --locker 使用官方 Redis distributed locker，--logging 输出 FusionCache ILogger 日志，--otel 通过官方 FusionCache OpenTelemetry instrumentation 输出 traces 与 metrics，并订阅 Bing 工厂准入仪表：

```powershell
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- multi --locker --logging --otel
```

--locker 需要 Redis；示例不打印连接配置。内存模式不注册 L2、Backplane 或分布式锁。

`shutdown` 在软超时返回旧值后立即停止宿主，演示 `SampleFactoryLifetime` 等待后台工厂的独立作用域释放。必须把排空器注册为最后一个 hosted service，在容器 Dispose 前等待 StopAsync 成功；超出关闭预算会报告取消，不会强行结束业务代码，也不保证持久化原生恢复队列。

Backplane 使用适配器默认的同步首次订阅，首次订阅失败会使宿主启动失败。`--locker` 的拿锁超时会降级继续回源；获取锁的 IO 错误是否重抛由 `ReThrowDistributedLockerExceptions` 控制，详见[迁移说明](../../docs/migrations/fusioncache-adoption.md)。

`--otel` 的官方 trace 可能包含原始缓存键；生产 exporter 前应脱敏/移除 `fusioncache.operation.key` / `fusioncache.operation.tag`，禁止把 key/tag 作为指标维度。示例使用非敏感随机键；自定义准入指标仅使用固定 cache name 等有限维度。

`--otel` 订阅 `ActivitySource` / `Meter` `Bing.Caching.FusionCache`。工厂活动名为 `cache.factory`；自定义指标为 `bing.cache.factory.waiting`（等待数）、`bing.cache.factory.active`（执行数）、`bing.cache.factory.rejected`（拒绝数）和 `bing.cache.factory.wait.duration`（等待毫秒数）。

## 构建

```powershell
dotnet build samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -c Release -f net8.0
dotnet build samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -c Release -f net10.0
```

除 warmup 使用固定热门产品清单外，sample 会为每次演示生成随机逻辑键，并为回源 reader 使用独立的异步 DI scope。要跨进程观察失效，可在两个应用实例中使用相同 Namespace、cache name、Redis、serializer 和身份前缀；此示例的 multi 用两个独立容器展示相同过程。

## 热门键分批预热与宿主启动

`warmup` 使用纯内存缓存，不需要 Redis。由 SampleWarmupHostedService 在 StartAsync 中预热业务提供的 12 个产品键；每批最多 4 个，等本批全部结束后再继续，默认总预算 5 秒。宿主启动完成后再次读取这些键，若意外回源会直接报错。

```powershell
# 正常预热：12 成功、0 失败；启动后再次访问不回源。
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- warmup

# 第三个产品回源失败，其他产品继续：11 成功、1 失败，宿主仍启动。
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net8.0 -- warmup --warmup-failure

# 总预算缩短为 50ms：报告取消和未开始数量，允许部分预热后启动。
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net10.0 -- warmup --warmup-timeout

# 在启动开始 150ms 后取消外部令牌：中止宿主启动，不输出启动成功。
dotnet run --project samples/Bing.Samples.FusionCache/Bing.Samples.FusionCache.csproj -f net10.0 -- warmup --warmup-cancel
```

失败/超时/取消开关用于可重复演示，取消时具体 started/cancelled/notStarted 数量随调度变化。不要组合这些开关；正式应用把取消令牌接到自身部署或启动流程。普通启动同时观察 ApplicationStopping。若使用 `--locker` 则会接入 Redis；共享空间中的已有命中不会执行模拟失败工厂，因此负面演示使用上述纯内存命令。

输出字段：requested（输入数）、started（已调用缓存数）、succeeded（正常返回，包含已有命中）、failed（单键异常）、cancelled（执行中因本轮令牌取消）、notStarted（尚未发起）、timedOut（预算令牌取消）、callerCancelled（外部取消）、factoryCalls（真实回源次数）。满足 `requested = succeeded + failed + cancelled + notStarted`。

接入路径：

1. 业务提供有限热门键清单，参考 [SampleWarmupHostedService.cs](SampleWarmupHostedService.cs)；本示例不扫描缓存、不查询全表、不记录无界失败明细。
2. 每次实际回源在委托内部创建并异步释放独立 DI scope。不要捕获 HTTP 请求的 scoped reader/DbContext；同一 scope 不跨产品并发共享。
3. [SampleCacheWarmup.cs](SampleCacheWarmup.cs) 只负责分批与聚合统计，继续调用 IHybridCache.GetOrCreateAsync，复用命中、同键合并和现有回源准入器。
4. 注册预热 hosted service，再将 SampleFactoryLifetime 排空器注册为最后一个 hosted service；默认顺序启动下，预热完成或预算耗尽后才返回 StartAsync。
5. 本示例采取“尽力预热”：单键失败或预算耗尽仍允许启动，未准备的数据由后续请求按需回源；调用方取消则中止启动。若少数数据是上线硬前提，业务应检查结果并抛错阻止启动，不能把尽力预热当成 readiness 保证。

为了让预热状态可解释，warmup 模式关闭 Fail-Safe、提前刷新、软超时以及超时后台完成；其他样例仍演示原有容错配置。预算依赖回源代码遵守 CancellationToken，无法强行终止忽略取消的数据库调用或用户代码，仍会等待当前批次收尾并由排空器保护作用域。

每批 4 个是本机演示值，不是为实时请求保留 4 个许可的保证：预热和实时请求共用命名实例的 8/128 准入预算，没有优先级。多实例/多节点额度会累加，部署时应错峰或由业务协调预热，并据下游容量调整批次。未新增通用预热框架、集群调度器或生产默认策略。更多场景见[冷启动清单](../../ai_docs/tasks/20261024-fusioncache-integration/cold-start.md)。
