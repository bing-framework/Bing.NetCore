# 独立 .NET 容错抽象库实施计划

任务编号：20261027-resilience-abstractions。状态：用户批准，进入执行。
设计基线：[独立抽象设计](../20261026-http-resilience/resilience-abstraction-design.md)。
本计划对应用户 2026-10-27 批准的完整范围；原 HTTP 233 项回归仅为旧引擎历史证据。

## 1. 目标与边界

交付公共 API 不依赖引擎的独立 .NET 容错库，通过包引用和注册切换 Fences/Polly。包含通用管线、两个 HTTP 客户端，以及 Retry、Timeout、CircuitBreaker、Concurrency、TokenBucket RateLimit、DTO Fallback、ManualCircuitControl、通用及固定同服务地址池 HTTP Hedging。
不实现 BuiltIn、服务发现、写操作重放、热切换、跨节点熔断、补偿；不提交、推送、发布或升级全局依赖。

## 2. 包与公共契约

新增 Bing.Resilience.Abstractions（契约，无引擎/HTTP/Bing.Core 依赖）、Bing.Resilience（DI/注册/冻结/校验/生命周期/诊断）、Bing.Resilience.Fences、Bing.Resilience.Polly；重构 Bing.Http.Resilience 并接入 Bing.DynamicApi.Client.Resilience。目标框架 net6.0;net8.0。
Fences 固定 9.0.0，Polly 固定 8.4.2，局部必要 Core/Extensions/RateLimiting 和兼容 Microsoft 基础包，不改变全局基线。
免费组合移除 Microsoft.Extensions.Http.Resilience、Microsoft.Extensions.Resilience、Microsoft.Extensions.Http.Polly 及 Polly/Polly.* 传递依赖；Polly Provider 单独消费验证，Core 不引用 Provider，不建聚合 Provider 包。

公共入口：
- AddBingResilience().AddFencesProvider(name)/AddPollyProvider(name).UseDefaultProvider(name)。
- AddBingResiliencePipeline(name, configure) 及泛型版本。
- IResiliencePipelineProvider.GetPipeline(name) 及泛型版本；对应 ExecuteAsync 提供取消令牌回调及 ResilienceExecutionContext 回调。
- IResilienceProvider 能力声明和构建契约；不暴露原生 builder/context/outcome/异常，不设 object NativeOptions。
- 两个 HTTP 注册入口保留，增加 ProviderName、RateLimit、Hedging。
- ResilienceOutcome 支持成功 null；执行上下文每次独立，并行分支独立尝试上下文，长期策略不捕获 Scoped 服务。
- ResilienceTimeoutException（Execution/Attempt）、ResilienceCircuitOpenException、ResilienceRejectedException、ResilienceConfigurationException。内部谓词/事件和最终输出均中性化；原业务异常保留，调用方取消传播 OperationCanceledException。
- 手动熔断查询快照、隔离、恢复；恢复重置采样，不新增远程管理端点。

注册规则：显式默认 Provider；名称 Ordinal，runtime remote 保持 OrdinalIgnoreCase 并映射实际 client。按用途/名称/结果类型隔离，HTTP 按实际 client 而非 profile。重复、未知、能力缺失、组合无效明确失败。冻结配置，Host 启动验证，普通 DI 显式验证及首次解析兜底；不构建临时容器。切换和调整通过重启，不迁移运行状态。

## 3. 执行语义

顺序：总超时 → 逻辑并发 → 可选 Fallback → Retry 或 Hedging → 尝试速率 → 熔断 → 单次超时 → 回调。
Retry/Hedging 互斥；通用默认不启用策略，恢复动作要求明确谓词；MaxRetryAttempts=2 最多三次，零省略。
调用方取消和总预算耗尽不触发重试/降级。单次超时可选处理。并发覆盖退避/清理。
TokenBucket 默认关闭，容量/补充数/周期显式配置，默认队列零；每尝试计配额。
熔断按尝试采样，取消/局部拒绝不计远端失败，半开一个探测。
DTO Fallback 在总预算内；已有 HTTP 客户端外层仅降级不再 Retry/Hedging。
通用 Hedging 明确并行安全，默认额外一次、延迟200ms、最多两次；可恢复失败可提前触发。

### 既有 HTTP 行为

默认发送总预算8s、单次2s、重试2次、初始退避200ms、本地上限2s、指数抖动；熔断窗口30s/最低20/50%/打开15s；并发32/队列0。
仅业务许可的无体 GET/HEAD 非流，runtime进一步仅GET。408/429/500/502/503/504/选定网络错误恢复；429不计熔断。Retry-After不截短，超预算取消。
Legacy行为保留，新模式MaxRetries=0。完整调用10s、描述5s；共享下载不因一个等待者取消而取消，失败缓存移除可重下。
SDK异常包装、runtime错误转换及流所有权保留；恢复handler内侧每尝试认证/签名，旧runtime处理器逻辑调用一次。

### HTTP Hedging

显式选择Hedging模式且独立CanHedgeRequest许可。固定稳定端点ID+同服务origin，保留path/query；每调用从主端点起按顺序轮转，单池可重复。首分支也独立请求。
首个不满足恢复谓词的结果获胜（包括业务4xx）；全失败返回主分支结果；取消并等待其他分支，释放丢弃响应。竞速截止响应头，读取/解析归客户端。
写/带体/流只发送一次；描述始终顺序重试。逻辑并发和尝试配额client共享；Hedging端点熔断及端点并发隔离（32/0）。端点熔断/端点并发拒绝可消耗名额转下一候选；全局并发/速率拒绝结束。
原请求须匹配主origin；禁止池外路由。可转发头白名单，不复制Host/Authorization/Cookie/配置签名头，内侧重新生成。中性不可变Options复制；可变Options要求复制器，禁止并行共享。
关闭自动重定向和共享Cookie，启动校验transport，不静默覆盖自定义handler。已知Retry-After约束后续新分支，已发送分支继续。
失败分支取消不计熔断，不覆盖获胜结果；调用方取消优先。

### 资源与诊断

Provider内部非资源结果包装及执行级所有权记录，避免引擎和框架重复释放。最终响应归调用方，丢弃响应只释放一次，清理失败明确报告。
Core持有状态，handler更新不重置；宿主统一释放limiter/timer/订阅，旧handler不能释放共享状态。
Bing.Resilience Meter/ActivitySource：一次逻辑最终结果、实际策略事件；仅稳定管线/client/Provider/端点标签，不含URL/token/用户。原生遥测不作稳定契约。

## 4. 阶段

| Task | 目标/范围/实施 | 依赖 | 验证和验收 |
|---|---|---|---|
| RA-01 | 固定局部版本；两引擎双框架能力/依赖验证；现有行为和兼容基线 | 无 | restore/build及最小能力验证，不压制降级 |
| RA-02 | Abstractions/Core、API、冻结、注册/能力验证、异常、上下文、生命周期 | RA-01 | 独立.NET消费，无引擎类型泄露 |
| RA-03 | 两Provider全部策略/控制/释放/诊断，共用契约测试 | RA-02 | 同套契约双Provider双框架 |
| RA-04 | 自有HTTP handler、旧依赖移除、两客户端适配、统一配置复制 | RA-03 | 顺序重试/预算/缓存/流回归 |
| RA-05 | 地址池、复制、签名、端点隔离、Retry-After、清理 | RA-04 | 两客户端真实loopback |
| RA-06 | 解决方案、样例、迁移、打包消费、依赖审计、执行报告、独立复审 | 前述 | 全部必要证据通过 |

主代理负责架构/生产集成/最终验证；luna_worker补独立窄任务（测试、依赖、文档），明确文件所有权。
风险：引擎语义差异、Hedging请求复制、资源释放；RA-01/RA-03提前验证，不能忽略配置或缩小范围掩盖问题。

## 5. 验证与交付

先受影响最小测试，再受影响项目完整测试，最后打包消费；不运行无关外部数据库门禁。
- 契约：泛型/非泛型、null、重复/未知/能力、冻结、容器、API泄露。
- 策略：次数/取消/单次总预算/退避/TokenBucket/半开/控制/Fallback。
- Hedging：慢主快备/全失败/业务错误/端点熔断/并行上限/Retry-After/签名/头和Options隔离/取消晚到释放。
- 客户端：旧新重试、冷/热描述、等待者取消、重下、包装、体挂起、上传下载/流。
- 资源：一次释放、最终所有权、许可、handler更新、状态隔离、宿主释放、重复创建销毁。
- 两Provider单独pack，无ProjectReference net6/net8消费；免费assets/nupkg/deps无Polly。

测试沿用 dotnet test --nologo -m:1 -nr:false -v minimal --logger trx 双TFM；新任务独立证据目录不覆盖历史。
交付 plan.md/execution.md/test-map.md/迁移文档/通用及两客户端本地故障样例；新增生产符号和关键行为映射实际测试方法。
迁移说明Provider注册、异常/Meter/handler变化；停Hedging回顺序，MaxRetryAttempts=0停重放，移除适配恢复Legacy。新的代码/测试/包消费证据才可判定完成。
