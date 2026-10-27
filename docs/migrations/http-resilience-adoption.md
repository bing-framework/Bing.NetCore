# HTTP 故障恢复迁移

本迁移只针对 `Bing.DynamicApi.WebApiClient` 与 `Bing.DynamicApi.Client`。支付、OAuth、短信及其他 `Bing.Utils.Http` 外呼不由本次适配自动接管；在这些客户端中已有的业务重试也不因安装共享包而自动变化。

接入代码与配置细节见 [HTTP 故障恢复接入指南](../guides/http-resilience.md)，可运行示例见 [Bing.Samples.HttpResilience](../../samples/Bing.Samples.HttpResilience/README.md)。

## 从旧 HTTP 引擎迁移到中性 Provider

1. 增加 Bing.Resilience.Fences 包，并在宿主显式调用 AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary")。
2. HTTP 包已移除 Microsoft.Extensions.Http.Resilience/Polly 依赖；清理宿主为旧接入单独引用的相关包，检查实际 assets 与发布 deps。可选 Polly 组合单独安装 Bing.Resilience.Polly 并改用 AddPollyProvider，不能把固定旧版本当作永久免费承诺。
3. HTTP 两个现有入口保留；旧 Polly 异常捕获改为 Bing.Resilience 中性异常，原生 Meter 订阅改为 Bing.Resilience。不要再依赖 Microsoft ResilienceHandler 类型。
4. 配置需重启生效，Provider 切换会重建熔断采样和配额。默认仍为顺序 Retry，新增 RateLimit 和 Hedging 默认关闭。
5. Hedging 需独立 CanHedgeRequest、固定同服务 origin 池、安全 transport 和内侧认证/签名。头白名单及 Options 复制规则见[独立容错指南](../guides/resilience.md)。runtime 描述仍顺序访问主端点。

仅注册方法保持源码兼容，不保证旧异常类型、原生遥测或二进制兼容。最新交付证据见[执行报告](../../ai_docs/tasks/20261027-resilience-abstractions/execution.md)。

## 分阶段启用

1. **盘点现有尝试次数。** 按具名 SDK / remote 列出 `HttpClient.Timeout`、旧 `MaxRetries`、自定义 `IBingDynamicApiRetryPolicy`、`IBingDynamicApiRequestHandler`、SDK/handler 重试和调用方重试。计算冷缓存描述下载和业务请求的独立尝试数，不能只看其中一层。
2. **选择可重放的试点。** 选一个已确认无副作用、返回普通 JSON、无请求体的查询端点。核实远程 GET 无副作用；按精确路由编写 `CanRetryRequest`。上传、写请求、原始流和业务上无法证明幂等的 GET 均先排除。
3. **WebApiClient 先接入一个接口。** 保留 `AddBingWebApiClient<TApi>`，使用其返回 builder 追加 `AddBingHttpResilience`。业务方法传入完整调用 deadline 的 linked `CancellationToken`；把逐次签名 handler 放在恢复扩展之后。已有 `HttpHost` 优先于 `BingWebApiClientOptions.BaseAddress` 的回退值。
4. **运行时客户端按 remote 迁移。** 引入可选 `Bing.DynamicApi.Client.Resilience` 包。目标 `remote.MaxRetries` 明确设为 0，再调用 `AddBingDynamicApiClientResilience` 配置业务许可及 `OperationTimeout` / `DefinitionTimeout`。如果旧重试次数非零，选项验证会拒绝与新管线叠加；不要静默改写旧策略。
5. **迁移逐次请求处理。** `IBingDynamicApiRequestHandler` 在运行时管线模式下每个逻辑请求只运行一次。需要每次尝试更新签名、nonce 或认证数据时，把它移到恢复管线内侧的 `DelegatingHandler`。
6. **小流量观察后扩大白名单。** 检查逻辑请求与实际发送次数、取消、超时、429 的 `Retry-After`、熔断拒绝、并发拒绝、冷/热描述缓存及流释放。先设 `MaxRetryAttempts = 0` 可暂停重试，同时保留其他已配置的超时、熔断和并发策略；确认后再开放已审查的端点。

单一恢复管线是约束。不要在目标 builder 上再注册 `AddStandardResilienceHandler`、第二个 Bing 恢复 handler 或等效 Polly 重试 handler；共享模块会拒绝同一 client 的重复 Bing 注册，并在创建 handler 链时验证恢复 handler 数量。

## 异常判读

| 场景 | WebApiClientCore 2.1.6 | 运行时 DynamicApi.Client |
| --- | --- | --- |
| 最终非成功 HTTP 响应 | `HttpRequestException`，内层 `ApiResponseStatusException`。SDK 在抛出前不读取错误体；调用方如保留/读取 `ResponseMessage`，用完后释放。 | `BingRemoteCallException`，带 HTTP 状态和远程错误；错误体读取使用请求令牌，响应由客户端释放。 |
| 调用方取消 | 可能表现为 `HttpRequestException`，内层为 `TaskCanceledException` / `OperationCanceledException`。结合调用令牌和内层异常判断，不要当作瞬态网络错误。 | 保留取消语义；调用方令牌优先，不映射成内部超时。 |
| 单次超时 | 当前 SDK 将管线 `ResilienceTimeoutException` 包在外层 `HttpRequestException.InnerException`。 | 传播 中性的 `ResilienceTimeoutException`；它不同于 runtime `OperationTimeout`。 |
| HTTP 发送总预算 `RequestTimeout` 耗尽 | 传输层为中性超时（Kind=Execution）；WebApiClientCore 外层异常以 2.1.6 端到端调用形态为准。 | 传播传输管线超时异常；不会冒充完整操作预算。 |
| 完整操作 / 描述预算耗尽 | WebApiClient 没有自动完整调用预算；由调用方 linked token 和 SDK 取消链表示。 | `OperationTimeout` / `DefinitionTimeout` 映射为 `TimeoutException`，原取消异常在 `InnerException`。 |
| 熔断打开 | 当前 SDK 以 `HttpRequestException` 包装，`InnerException` 为 `ResilienceCircuitOpenException`。 | 传播 `ResilienceCircuitOpenException`。 |
| 并发拒绝 | 共享管线拒绝类型为 `ResilienceRejectedException`；不要假设所有 SDK 调用都使用同一外层包装。 | 传播并发拒绝异常。 |
| JSON 反序列化失败 | SDK 原解析异常，不属于 HTTP 重试。 | 原解析异常，不属于 HTTP 重试。 |

WebApiClientCore 的 `[RawReturn] Task<string>` 是原文返回，不会把 JSON 字符串反序列化为 C# 字符串；JSON 字符串内容中的引号会保留。该路径读取取消时，SDK 2.1.6 可能不释放响应流。需要可靠取消和资源所有权时改用 `[RawReturn] Task<HttpResponseMessage>`，由调用方用 `using` 释放并以 linked token 读取响应体。DTO JSON 读取取消会释放内容流。迁移不修改 WebApiClientCore 的行为。

## 回滚

- **先停止并行：** 将 Mode 改回 Retry，或撤销 CanHedgeRequest；如需停止所有自动重放，同时设置 Retry.MaxRetryAttempts=0。
- **先停止重试：** 对具名客户端将共享 `Retry.MaxRetryAttempts` 设为 0。超时、熔断和并发仍按其独立开关工作。
- **完全回滚 WebApiClient：** 移除该 builder 上的 `AddBingHttpResilience`，保留原 `AddBingWebApiClient` 和业务 `CancellationToken`。如签名 handler 仅为新管线逐次执行而加入，确认旧路径需求后再决定是否保留。
- **完全回滚运行时客户端：** 移除目标 remote 的 `AddBingDynamicApiClientResilience` 注册；恢复 `RetryMode.Legacy`，按原策略恢复 `MaxRetries` 和 `RetryDelay`，撤销适配器增加的 `OperationTimeout` 与 `DefinitionTimeout`。
- **重启宿主。** 管线在注册时捕获恢复选项，首版不热更新；配置回滚通过重建服务与 handler 生效。
- **核对后续调用。** 回滚只改变之后创建/执行的调用，不能撤销远端已完成的写入、付款或令牌操作；这些操作从一开始就不应加入自动重放白名单。

不涉及业务接口、服务端路由或持久化格式变更，因此无需数据库迁移。
