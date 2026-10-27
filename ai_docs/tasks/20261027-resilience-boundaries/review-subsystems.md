<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261027-resilience-boundaries
AI_REVIEWED_AT: 2026-10-27T07:11:46Z

# 服务发现与管理接入独立审查

## 范围与结论

本次只审查 BND-02 注册中心适配器，以及 BND-03 Operations ASP.NET Core 接入、控制台和相关测试。对照 `plan.md` 的协议解析、分页、过期、身份/Origin 校验、授权、CSRF、CAS、Unknown 确认、重复补偿/审计及管理路径要求检查了源码和测试；未重跑已完成的全量回归。

初审记录的 3 项问题均已关闭：两项实现边界已有修复，一项管理端到端测试已补齐。当前 `execution.md` 仍标记 `IN_PROGRESS`；本报告只确认服务发现与管理接入范围，不代表完整任务已完成。

## Findings

### FIX-SD-01：无效协议记录可能沿用陈旧端点

- 状态：`CLOSED`
- 严重度：`SHOULD_FIX`
- 证据：[RegistryServiceEndpointSources.cs](../../../framework/src/Bing.Resilience.Discovery/RegistryServiceEndpointSources.cs:282)、[RegistryServiceEndpointSources.cs](../../../framework/src/Bing.Resilience.Discovery/RegistryServiceEndpointSources.cs:263)、[RegistryAdapterTests.cs](../../../framework/tests/Bing.Resilience.Extensions.Tests/RegistryAdapterTests.cs:148)
- 原触发：先成功获取端点快照；快照过 `ExpiresAt`、仍在 `MaxStaleUntil` 内时，注册中心返回格式错误、字段类型错误或 `port: 0`。
- 修复与验证：`ParsePage` 将 JSON/字段读取错误归一为 `ResilienceConfigurationException`；`Origin` 将非法端口归为同一中性配置错误，刷新失败时清空旧快照。`InvalidRegistryDocumentInvalidatesPreviouslyValidSnapshot` 覆盖三类响应；`TransportFailureUsesOnlyBoundedStaleSnapshot` 保留网络故障的有界 stale 行为。`review-fixes` 双框架证据均为 23/23 通过。

### FIX-OPS-01：有效的嵌套 ConsolePath 会遮蔽状态查询路由

- 状态：`CLOSED`
- 严重度：`SHOULD_FIX`
- 证据：[ResilienceOperationsEndpointExtensions.cs](../../../framework/src/Bing.Resilience.Operations.AspNetCore/ResilienceOperationsEndpointExtensions.cs:163)、[OperationsManagementTests.cs](../../../framework/tests/Bing.Resilience.Extensions.Tests/OperationsManagementTests.cs:140)
- 原触发：将 `ConsolePath` 配到 Replay/Saga 前缀之下，使静态 Console 或 session 路由落在单段 ID 查询路由上。
- 修复与验证：映射校验现在检查 Console 和 `/session` 是否会截获 Replay/Saga 的单段 ID，并在冲突时拒绝注册。`RegistrationRejectsMissingAuthorizationAndInvalidRoutes` 覆盖重叠配置；双框架 `review-fixes` 证据均为 23/23 通过。

### FIX-OPS-02：人工补偿的重复提交与审计缺少管理端到端断言

- 状态：`CLOSED`
- 严重度：`SHOULD_FIX`
- 证据：[OperationsManagementTests.cs](../../../framework/tests/Bing.Resilience.Extensions.Tests/OperationsManagementTests.cs:73)、[manual-compensation-repeat_net6.trx](evidence/tests/manual-compensation-repeat_net6.trx)、[manual-compensation-repeat_net8.trx](evidence/tests/manual-compensation-repeat_net8.trx)
- 原缺口：没有从真实管理路由提交人工补偿、重复提交同一版本并核对审计记录的端到端测试。
- 修复与验证：新增 `ManualCompensationRequiresCsrfUsesCasAndAuditsOnlyOnce`，使用 loopback Kestrel、SQLite 和带计数的 Saga handler；缺少 CSRF/错误版本不执行补偿，首次有效请求成功，旧版本重复请求返回 `409`，业务补偿执行一次且只写一条正确 actor/reason 的审计记录。该测试在 `net6.0`、`net8.0` 各 1/1 通过；完整管理测试类在两框架各 7/7 通过。

## 已核验边界

- 注册中心按 `Ordinal` 匹配服务名，限制响应字节、端点数量和 EndpointSlice 页数；分页要求来源版本一致，重复 continuation 会失败。请求认证每页重新执行；自动重定向和共享 Cookie 被拒绝，快照发布前统一执行端点信任校验。
- 配置的 `TimeToLive`、`MaxStale` 与失败退避有明确边界；取消单个等待者不会取消共享刷新。401/403、Consul ACL 截断、协议格式/字段类型错误及 Origin 错误会丢弃旧快照；网络故障仍使用有界 stale。
- 管理路由要求宿主认证、授权、访问范围及 actor resolver；变更请求在协调器调用前验证 antiforgery，并将版本交给协调器 CAS。跨范围/未知记录以 404 处理，CAS 冲突为 409。
- 状态 DTO 不包含业务载荷、幂等键或租约令牌。控制台通过 `textContent` 写入查询结果，CSP nonce 及 HTML 编码保护页面；响应由 `IResult` 直接生成，没有把可释放响应流交给调用方的所有权问题。受保护路由统一设置 `Cache-Control: no-store` 和 `X-Content-Type-Options: nosniff`。

## 验证限制

初审后按窄范围验证：主线 `review-fixes` TRX 在 `net6.0`、`net8.0` 各 23/23 通过；新增人工补偿测试两框架各 1/1 通过；完整 `OperationsManagementTests` 类两框架各 7/7 通过。未重跑整个扩展测试项目或全任务回归；包消费、性能及其他 BND 阶段仍由主线执行报告跟踪。
