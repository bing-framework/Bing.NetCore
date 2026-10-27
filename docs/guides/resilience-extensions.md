# 容错扩展接入指南

本任务提供三种 Provider：BuiltIn、Fences 和 Polly。业务代码只依赖 `Bing.Resilience.Abstractions`/中性 Core，运行时替换 Provider 只需要替换可选包和注册行。

## 基础注册

```csharp
services.AddBingResilience()
    .AddBuiltInProvider("builtin")
    .UseDefaultProvider("builtin");

services.AddBingResiliencePipeline<ProductDto>("catalog.read", options =>
{
    options.ExecutionTimeout = TimeSpan.FromSeconds(5);
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.ShouldHandle = args => args.Outcome.Exception is CatalogTransientException;
});
```

同一管线配置也可以使用 `AddFencesProvider("primary")` 或 `AddPollyProvider("primary")`。宿主必须明确选择 `UseDefaultProvider`，Provider 不支持声明的策略时启动校验失败。

## 注册中心适配

`Bing.Resilience.Discovery` 提供不绑定厂商 SDK 的 HTTP 适配器：`ConsulServiceEndpointSource`、`NacosServiceEndpointSource` 和 `KubernetesEndpointSliceSource`。三者都要求宿主配置 `TrustBaseAddress`、端点信任策略和访问凭据处理器；失败时只在 `MaxStale` 内复用快照，不能把未验证地址加入候选。

```csharp
services.AddSingleton<IServiceEndpointSource>(_ => new ConsulServiceEndpointSource(
    new ConsulEndpointOptions
    {
        ServiceName = "catalog", BaseAddress = new Uri("https://consul.example/"),
        TrustBaseAddress = uri => uri.Host == "consul.example",
        Scheme = "https", ContractRevision = "v1",
        ConsistencyGroup = "catalog", CredentialAudience = "catalog"
    }, transport, trustPolicy, TimeProvider.System));
```

适配器只负责把注册中心响应转换为 `ServiceEndpointSnapshot`；TLS、令牌、服务身份和业务契约仍由宿主验证。

## 持久操作管理端点

可选包 `Bing.Resilience.Operations.AspNetCore` 提供查询、取消 Replay 和 Saga 人工状态操作。必须显式提供访问范围解析与权限函数，端点不会自动开放匿名管理能力，也不会返回持久载荷。

```csharp
services.AddBingResilienceOperationsManagement();
// 在 UseAuthentication 之后映射；宿主需已注册 Durable Replay 与 Saga。
app.MapBingResilienceOperations(o =>
{
    o.EnableConsole = true; // 默认关闭；可选操作控制台。
    o.ResolveAccessScope = context => ValueTask.FromResult<string?>(context.User.FindFirst("tenant")?.Value);
    o.AuthorizeAsync = (context, permission) =>
        ValueTask.FromResult(context.User.IsInRole("resilience-operator"));
});
```

## 两个 HTTP 客户端

```csharp
services.AddBingWebApiClient<ICatalogSdk>(o => o.BaseAddress = origin)
    .AddBingHttpResilience("catalog", options =>
    {
        options.CanRetryRequest = request => request.Method == HttpMethod.Get;
        options.ProviderName = "builtin";
    });

services.AddBingDynamicApiClient(o => o.AddRemoteService("catalog", r => r.BaseUrl = origin.ToString()));
services.AddBingDynamicApiClient<ICatalogRuntime>("CATALOG");
services.AddBingDynamicApiClientResilience("catalog", options =>
{
    options.Http.ProviderName = "builtin";
    options.Http.CanRetryRequest = request => true;
    options.Definition.Enabled = true;
    options.Definition.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.Hedging;
    options.Definition.Endpoints.Add(new("primary", new Uri(origin, "api/dynamic-api/v1/definition")));
});
```

SDK 保持原异常包装和流所有权。运行时客户端使用大小写不敏感的 remote 查找，但恢复管线绑定实际具名 HttpClient；Runtime 模式要求旧 `MaxRetries=0`，描述下载拥有独立预算。

## 动态发现、写重放和合成响应

动态发现先发布带稳定 ID、incarnation、origin、契约版本和一致性组的不可变快照，再使用 `AddBingHttpDiscovery`。来源必须实现可信地址校验；撤销是安全状态，不能随 TTL 自动恢复。

写请求必须同时拥有宿主操作目录中的幂等写许可、可重建请求体和接收端同键协议。客户端会为首发和重发创建独立消息并在内侧重新签名。持久提交后的未知结果由 Durable Replay 确认，不能用普通 HTTP Retry 代替。

SyntheticResponse 只对显式白名单操作生效。只读缓存/默认响应必须是契约认可的有效 200；写入 202 必须带持久提交证明、同键同载荷和查询 Location。合成响应处于恢复调度之外但仍受总预算约束，远端失败仍进入熔断统计。

## 持久 Replay、Saga 和事务接入

`RelationalOperationStore.InitializeAsync` 只用于本地示例或迁移流程，不会在生产启动时自动建表。生产服务应在向后兼容迁移中创建 operations、attempts、audit 表，并保留历史处理器版本和加密密钥。

业务数据库应复用原连接和事务：

- EF Core：可选包 `Bing.Resilience.Operations.EntityFrameworkCore` 提供 `store.EnlistEntityFramework(context)`；必须先建立显式事务，再把桥接会话传给 `EnqueueInTransactionAsync` / `StartInTransactionAsync`。提交与回滚归宿主。
- Dapper：使用同一个 `DbConnection`/ `DbTransaction` 调 `Enlist`，不要让 Journal 自己打开第二条连接。
- FreeSQL：可选包 `Bing.Resilience.Operations.FreeSql` 提供 `store.EnlistFreeSql(unitOfWork)`；只使用 `GetOrBeginTransaction(false)` 已存在的事务，不隐式开启或提交。业务 SQL 必须绑定同一工作单元，不能先让 DbContext.SaveChanges 提交。
- CAP：不是可靠操作层依赖。只有在现有 UnitOfWork 已明确绑定同一事务时才可发布通知；CAP 不替代 Journal 的认领、租约、账本和幂等确认。

Worker 默认关闭内部 HTTP Retry/Hedging；每个持久尝试创建 `ResilienceDurableAttemptScope`，HTTP 层看到该范围后只发送一次。

## 运行中切换与分布式熔断

`AddRuntimeSwitching` 为每个完整切换组生成稳定句柄。已注册 Provider 之间可通过 `IResilienceRuntimeManager.SwitchAsync` 在运行中切换：新代完整构建后以 CAS 发布，已有调用继续使用旧代并排空，逻辑并发和 TokenBucket 不重置，已打开/隔离的本地熔断按 PreserveSafety 延续。Provider 包、注册代码和静态配置的变更仍通过重启生效；运行时切换不会迁移未声明的外部状态。

Redis Provider 接受宿主管理的 `CSRedisClient`，不使用静态 RedisHelper。分布式熔断需显式选择 FailClosed 或 FailOpenWithLocalCircuit，并提供稳定服务/端点键、版本和失联预算。Redis 调用的取消只取消等待；同一 attempt ID 可安全重接结果，不能把取消解释为脚本未执行。

## 回滚

关闭 Definition Hedging 时改为 SequentialRetry；写重放先撤销业务许可并保留幂等记录；暂停 Durable/Saga 新任务但保留 Journal、历史处理器和死信查询；Synthetic 按操作关闭即可。Provider 包与静态注册变更通过重启生效；已注册 Provider 之间使用 SwitchAsync 原子切换，旧代排空后释放。

完整边界接入与协议约束参见 [容错边界接入](resilience-boundaries.md)，生产迁移脚本参见 [Journal 迁移](../migrations/resilience-extensions/README.md)。
