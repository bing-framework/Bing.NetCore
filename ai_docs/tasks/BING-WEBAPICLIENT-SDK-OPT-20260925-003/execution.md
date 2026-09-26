<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-WEBAPICLIENT-SDK-OPT-20260925-003
AI_EXECUTION_FINISHED_AT: 2026-09-25T15:22:44.173Z

# 实施执行报告

## 执行结论

已完成 WebApiClient SDK 优化计划。请求上下文头现在按请求清理并重新写入，避免空上下文复用旧的默认头；三个上下文头名称在注册时进行 HTTP token 语法校验；多 SDK、并发上下文和配置隔离均有真实 TestServer 覆盖。2026-09-26 补强了三项验收证据：每个 SDK 使用独立自定义处理器、并发测试增加服务端到齐屏障、`null` 禁用头名时验证同名宿主默认头保留。没有改变现有注册签名、动态 API 服务端协议或原生动态客户端。

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-OPT-20260925-003`
- 基线：`BING-WEBAPICLIENT-SDK-CHECK-20260925-002` 的 WebApiClient SDK 包、测试和打包证据。
- 本轮风险：MEDIUM；上下文为空时清理同名默认头是有意的多租户隔离行为。

## 计划执行情况

| Task | 状态 | 结果 |
|---|---|---|
| OPT-001 | COMPLETED | handler 每次发送先移除已配置头，再写入当前上下文非空值；空上下文、空字段和由服务端屏障确认真实重叠的并发请求均有测试。 |
| OPT-002 | COMPLETED | 头名称按 HTTP token 字符规则注册期校验；`null` 保持禁用语义。 |
| OPT-003 | COMPLETED | 两个 SDK 接口共享 accessor 时，Host、上下文头名称、builder 默认头和各自注入的处理器互不串扰。 |
| OPT-004 | COMPLETED | README 已同步清理优先级和多 SDK 注册示例。 |

## 已完成事项

- `BingWebApiClientRequestContextHandler` 在每次请求中清理本包管理的租户、关联 ID、语言头，再按当前上下文写入。
- `BingWebApiClientOptions` 增加头名注册期校验，拒绝空白、控制字符和非 token 字符。
- 测试覆盖默认头清理、字段为空、8 路 AsyncLocal 并发（客户端同时放行，并由 TestServer 屏障等待 8 个请求全部到达）、非法头名和两个 SDK 独立配置场景；双 SDK 场景分别注入 marker handler 并断言各自头未泄漏到另一客户端。
- `CorrelationIdHeaderName = null` 的测试设置同名 `HttpClient.DefaultRequestHeaders`，确认上下文关联 ID 不发送，但宿主固定头 `host-correlation` 保留。
- README 明确默认头清理语义，并补充两个不同 SDK 头名称隔离示例。

## 部分/未完成事项

无计划内遗留。按计划未执行全仓库测试、NuGet 发布、git add/commit/push 或 PR 创建。

## 修改文件

- `framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientOptions.cs`
- `framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientRequestContextHandler.cs`
- `framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientServiceCollectionExtensions.cs`
- `framework/src/Bing.DynamicApi.WebApiClient/README.md`
- `framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs`
- 本任务 `plan.md` 与本报告。

## API/数据/配置变化

- 无新增公共 API，无签名变化。
- 非法上下文头名称现在在 `AddBingWebApiClient` 注册阶段抛出 `ArgumentException`，而不是首次请求时失败。
- 上下文头的运行时优先级变更为：本包先清理同名头，再写入当前上下文非空值；`null` 头名不由本包管理。

## 测试结果

- L1 net8.0：`BingWebApiClientEndToEndTest` 13/13 通过。
- L1 net6.0：`BingWebApiClientEndToEndTest` 13/13 通过。
- L2 net8.0：SDK 测试项目完整测试 13/13 通过。
- L2 net6.0：SDK 测试项目完整测试 13/13 通过。
- 2026-09-26 补充复验：同一 SDK 测试项目 `dotnet test ... -c Release -f net8.0 --no-restore` 与 `-f net6.0 --no-restore` 均通过，分别 13/13；仅有 NuGet 漏洞索引不可达的既有 `NU1900` 警告。
- 动态 MVC 服务端源未改，复用上一任务的 net6.0/net8.0 29/29 回归证据。

## Build/Typecheck/Lint/Format

- Release 测试构建成功。
- `dotnet pack framework/src/Bing.DynamicApi.WebApiClient/Bing.DynamicApi.WebApiClient.csproj -c Release --no-restore --output .artifacts\\webapiclient-opt\\combined` 成功。
- 包内确认存在：`lib/net6.0/Bing.DynamicApi.WebApiClient.dll`、`lib/net6.0/Bing.DynamicApi.WebApiClient.xml`、`lib/net8.0/Bing.DynamicApi.WebApiClient.dll`、`lib/net8.0/Bing.DynamicApi.WebApiClient.xml`。
- `git diff --check` 通过。
- 构建出现既有 `NU1900`（NuGet 漏洞索引不可达）警告，不影响本地还原、测试和打包。

## 计划偏差

- 头名校验最终采用 RFC token 字符规则，而不是依赖 `HttpRequestHeaders.Add`，避免把语法合法但不属于常见请求头集合的名称误判为非法。
- 多 SDK 测试复用同一个 `/headers` 动态端点，并让服务端回显 Host、两个 builder 默认头和 alternate 上下文头，未增加服务端生产协议。

## 基线问题

NuGet 审计索引在当前环境不可达，所有相关构建仅记录 `NU1900`；没有把该环境问题归类为代码失败。

## 已知问题

- `BaseAddress` 必填语义保持不变；仅预配置 `HttpHost` 而不提供回退地址仍属于后续 API 设计议题。
- 不默认启用重试；第三方宿主需自行判断请求是否可重放。
- WebApiClientCore 的非成功状态、取消和原始响应所有权仍按上一轮 README 说明。

## 风险与回归关注点

- 如果宿主依赖 `X-Tenant-Id` 等同名 `DefaultRequestHeaders` 作为固定头，需要改用不同头名或独立 handler；本包会清理其同名值。
- `IBingWebApiClientRequestContextAccessor` 仍应从线程安全的 ambient 上下文读取值，不能在单例字段中缓存单请求数据。

## 生产符号 → 测试方法

| 生产符号/行为 | 测试项目 | 测试方法 |
|---|---|---|
| `BingWebApiClientRequestContextHandler.SendAsync`：清理默认头、空上下文和空字段 | `Bing.DynamicApi.WebApiClient.Tests` | `ContextHeadersClearDefaultValuesWhenContextIsMissingOrIncomplete` |
| `BingWebApiClientRequestContextHandler.SendAsync`：并发上下文隔离 | 同上 | `ContextHeadersRemainIsolatedForConcurrentRequests` |
| 请求上下文头名称设为 `null` 时不管理该头，宿主同名默认头保留 | 同上 | `ContextHeaderNamesCanBeCustomizedOrDisabled` |
| `BingWebApiClientOptions.ValidateHeaderNames`：空白/非法头名注册期失败 | 同上 | `RegistrationRejectsInvalidBaseAddressesAndNonInterfaceContracts` |
| `AddBingWebApiClient<TApi>`：两个 typed SDK 的 Host、默认头、自定义头和自定义消息处理器隔离 | 同上 | `MultipleSdkContractsKeepTheirHeaderConfigurationIndependent` |
| 既有 JSON、RawReturn、multipart、认证、取消、HttpHost 回退 | 同上 | 本项目完整 13 项测试 |

## Git 状态

保留工作区已有动态 API staged changes；本任务新增/修改的 SDK 文件仍未暂存。本轮未自动 git add、commit、push、创建 PR 或发布包。
