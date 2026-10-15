# Admin 集群宿主配置与迁移

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

EFCore 与 FreeSQL Admin 的 CAP 模块从宿主配置读取 RabbitMQ 连接，不再使用源码中的固定地址或凭据。应用配置及环境变量按 ASP.NET Core 的配置规则合并。

| 配置项 | 环境变量 | 行为 |
| --- | --- | --- |
| `CAP:RabbitMQ:HostName` | `CAP__RabbitMQ__HostName` | 必填，缺失时注册失败 |
| `CAP:RabbitMQ:UserName` | `CAP__RabbitMQ__UserName` | 必填，无默认账户 |
| `CAP:RabbitMQ:Password` | `CAP__RabbitMQ__Password` | 必填，无默认密码；通过受控密钥注入 |
| `CAP:RabbitMQ:Port` | `CAP__RabbitMQ__Port` | 默认 5672，范围 1–65535 |
| `CAP:RabbitMQ:VirtualHost` | `CAP__RabbitMQ__VirtualHost` | 默认 `/` |
| `CAP:Dashboard:Enabled` | `CAP__Dashboard__Enabled` | 默认 false；仅 Web 角色显式配置 true 时注册，且必须配置 `CAP:Dashboard:Role` |
| `CAP:Dashboard:Role` | `CAP__Dashboard__Role` | CAP Dashboard 启用时必填；要求已认证用户携带匹配的 `role_code` 或标准角色声明 |
| `Exceptionless:ApiKey` | `Exceptionless__ApiKey` | 默认空；为空时不启用远程 Exceptionless 接收器，使用受控密钥注入 |
| `Exceptionless:ServerUrl` | `Exceptionless__ServerUrl` | 可选；配置 API Key 时用于指定自建 Exceptionless 地址 |
| `Inventory:Enabled` | `Inventory__Enabled` | 默认 false，显式启用整单库存、关键审计及 CAP 出箱业务 |
| `BING_CAP_CONSUMER_ENABLED` | 同名环境变量 | 默认 true；显式 false 时 Admin 仅运行 CAP 出箱发布，不启动 CAP 消费和接收重试处理器 |
| `OrderedEvents:Enabled` | `OrderedEvents__Enabled` | 默认 false；显式 true 时注册 Admin 有序事件存储和 CAP 事务适配器，业务消费者仍需单独注册 |
| `OrderedEvents:Metrics:Enabled` | `OrderedEvents__Metrics__Enabled` | 默认 false；仅允许在 `BING_HOST_ROLE=consumer` 且有序事件已启用时注册 Prometheus 指标端点 |
| `OrderedEvents:Metrics:Role` | `OrderedEvents__Metrics__Role` | 指标端点启用时必填；当前认证用户必须具备该角色 |
| `Auditing:DatabaseEnabled` | `Auditing__DatabaseEnabled` | 默认 false；显式 true 时将普通 HTTP 审计写入 `ConnectionStrings:DefaultConnection` 指定的 MySQL 审计表 |
| `Auditing:Query:Enabled` | `Auditing__Query__Enabled` | 默认 false；显式 true 时启用受保护的审计元数据查询入口，并要求数据库审计已启用 |
| `Auditing:Query:Role` | `Auditing__Query__Role` | 查询端点要求的独立角色；启用查询时必填 |
| `Auditing:Query:AllowCrossTenant` | `Auditing__Query__AllowCrossTenant` | 默认 false；设为 true 时必须同时配置跨租户角色，且查询仍需指定目标租户 |
| `Auditing:Query:CrossTenantRole` | `Auditing__Query__CrossTenantRole` | 跨租户查询所需的独立角色；仅在 AllowCrossTenant=true 时必填 |
| `BING_HOST_ROLE` | 同名环境变量 | 默认 `web`；`consumer` 跳过 HTTP 业务管道、Swagger、CAP Dashboard 和 Hangfire（EFCore），`task` 跳过 HTTP 业务管道、Swagger 和 CAP Dashboard 并启用 Hangfire（EFCore）；后台角色选择 Generic Host，不注册 Kestrel |

配置错误只指出配置项，不输出秘密值。不要把真实密码放入源码或提交的配置文件；部署通过密钥存储或受控环境提供。此前源码中的凭据如果实际使用过，应按运维流程轮换，删除代码不能撤销历史暴露。测试中的标记值不是真实部署凭据。

Dashboard 启用不等于完整的运维访问治理。Admin CAP Dashboard 和 Hangfire Dashboard 已在应用内默认拒绝匿名访问，并分别要求配置角色；仍需在宿主或可信网关完成身份认证、网络访问控制和审计，当前测试不替代真实网关验收。Consumer/Task 角色不注册 CAP Dashboard。

有序事件指标端点只在 Consumer 角色显式开启，配置示例：

```json
{
  "BING_HOST_ROLE": "consumer",
  "OrderedEvents": {
    "Enabled": true,
    "Metrics": {
      "Enabled": true,
      "Role": "metrics-reader"
    }
  }
}
```

请求需要通过宿主的认证策略并具备 `metrics-reader` 角色。端点只输出低基数聚合指标；生产环境还应在网关层限制来源，不能把它当作匿名健康检查或消息诊断接口。

## 请求日志与审计

EFCore Admin 的内置 HTTP 日志现在只采集请求方法和响应状态，旧完整请求响应采集默认关闭。旧中间件会读取查询和正文，单独设置 `WithRequest=false` 不能阻止这条读取路径，因此不能把它当成脱敏开关。业务审计使用 `UseBingHttpAuditing()`，默认不记录查询参数、正文和异常文本。

两 Admin 配置了本宿主的用户、名称、租户和客户端 Claim 类型。审计位于认证及路由之后，只读取已认证身份。需要查询字段时通过 `AllowedQueryParameters` 显式允许经过评估的字段；不要允许密码、令牌或登录凭据。其他业务日志和第三方日志仍需按实际输出检查，本次默认日志调整不承诺整个应用所有日志已经脱敏。

手工构造 `BingHttpAuditingMiddleware` 的消费者需迁移到不接收管理器的构造函数，并向 `InvokeAsync(HttpContext, IAuditingManager)` 传入当前请求的管理器。常规 `UseBingHttpAuditing()` 用法保持不变。

HTTP 审计在业务执行后保存，保存失败告警或 `ThrowOnStoreFailure` 抛错不能撤回响应和数据库提交。库存等关键操作必须在业务事务内保存审计，详见[数据库审计指南](auditing-persistence.md)。

两套 Admin 默认继续使用日志审计存储。启用 `Auditing:DatabaseEnabled=true` 后，宿主会使用独立的 MySQL 连接注册普通审计存储；未配置 `ConnectionStrings:DefaultConnection` 会在启动注册阶段失败。部署前必须执行[数据库审计迁移](auditing-persistence.md)，并按 `AllowedPropertyNames`、`MaxFieldLength`、`MaxPayloadBytes` 和 `MaxDetailCount` 限制投影内容。该连接不加入业务事务，不能把普通请求审计当作库存等关键操作的回滚依据；保留清理仍需由受控运维任务执行。

## 停止与部署边界

HTTP 闸门停止等待超时或取消会报告错误，不再正常返回“排空完成”。Generic Host 在不同运行时可能直接传播或聚合停止异常，部署脚本应检查失败及在途状态。后台角色的模块初始化由 `EnableBingHostedInitialization()` 在 Host 启动阶段完成，停止仍经过同一模块关闭管理器。超时只停止等待，不强制释放活动资源；在活动操作结束前不能释放根容器。

闸门的下游 HTTP 管道计数不覆盖请求作用域异步清理、`OnCompleted`、脱离请求的后台任务、CAP 或定时任务。这些阶段必须分别等待。两个 Admin 宿主现在识别 `BING_CAP_CONSUMER_ENABLED=false`，并保留出箱发布；`BING_HOST_ROLE=consumer` 会跳过 HTTP 业务管道、Swagger、CAP Dashboard 和 Hangfire；`BING_HOST_ROLE=task` 默认关闭 CAP 消费，只有显式设置 `BING_CAP_CONSUMER_ENABLED=true` 才接收 CAP，并启用两套宿主共用的 Hangfire 任务排空接线。后台角色通过 Generic Host 启动模块并关闭，不注册 Kestrel；Web 角色继续通过 `UseBing` 初始化。真正的 CAP 消费排空、Hangfire 原生任务拉取停止、定时任务排空、依赖预热和动态切流仍需单独部署验收。

## 库存重复请求

MySQL 在 `RepeatableRead` 下可能保留第一次查询的旧快照。库存存储初次查询仍为普通读，仅在唯一请求键冲突后使用 `FOR UPDATE` 当前读，返回另一事务已提交的首次结果。两个交错事务的 `RepeatableRead` 和 `ReadCommitted` 用例直接断言一次扣减及结果一致。

该单项修复不提交或释放调用方事务。新增整单入口已组合首次结果、关键审计、CAP 出箱和多 SKU 不足回滚，默认按当前作用域解析依赖，见[整单库存指南](inventory-orders.md)。双进程防超卖、整单重试和真实提交确认丢失仍需分别验收；提交结果未知时使用原请求查询权威数据库，查询不到不能证明旧事务已经回滚。

## CAP 消费取消策略

两套 Admin 的 `CapModule` 已调用 `EnableBingCapConsumerCancellationRetry()`。未被已有消费过滤器处理的 `OperationCanceledException` 会保留为普通失败的 `InnerException`，进入 CAP 持久化失败重试；CAP 8.0.1 原生执行器不会自动提供该保证。此策略不重发目标消息，提交后重试仍依赖业务同事务收据去重。

替换 Admin 的自定义 `ISubscribeInvoker` 时，应继承 `BingSubscribeInvoker` 以保留取消适配和原 `GetInstance` 行为。直接使用 `AddCapEventBus` 的外部应用需要显式选择启用；未知工厂注册不会被自动覆盖。业务取消、宿主停止、数据库最终提交及真实排空需分别确认，不能将收到取消视为业务回滚或停止完成。

## Hangfire Task 角色排空

EFCore Admin 的 `BING_HOST_ROLE=task` 会注册 `IBingHangfireRuntime`。发布或停止 Task 实例前，可以先拒绝新的 Hangfire 执行，再等待已经进入执行区的任务完成：

```csharp
var runtime = app.Services.GetRequiredService<IBingHangfireRuntime>();
runtime.BeginDrain();

var drained = await runtime.WaitForDrainAsync(
    TimeSpan.FromSeconds(30),
    cancellationToken);
if (!drained)
{
    // 保留旧实例和根容器，继续诊断在途任务；不要强制释放活动作用域。
    throw new InvalidOperationException("Hangfire 任务尚未完成排空。");
}

await app.StopAsync(cancellationToken);
```

该运行时只在 Task 角色生效。它通过 Hangfire 客户端和执行过滤器阻止排空期间的新任务写入或进入执行区，并统计已经进入应用执行区的任务；Generic Host 停止时也会等待该计数归零，关闭令牌到期或取消时报告未完成状态。实际 Hangfire Server 的轮询停止、队列中任务的重新领取、重试和存储状态迁移仍由 Hangfire 自身宿主负责。多个 Task 节点部署时应先在发布编排中停止目标实例的准入，再等待原生服务器停止并确认稳定队列和业务幂等键；不能仅凭 `WaitForDrainAsync` 的返回值宣称整个集群已排空。

等待超时或取消只表示本次观察没有完成，不能提前释放根容器或活动任务作用域。强制终止进程后的恢复依赖 Hangfire 存储、任务重试和业务幂等设计；它不属于托管清理保证。
