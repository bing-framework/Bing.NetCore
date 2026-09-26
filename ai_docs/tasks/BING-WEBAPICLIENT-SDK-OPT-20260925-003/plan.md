# WebApiClient SDK 优化计划

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-OPT-20260925-003`
- 范围：`Bing.DynamicApi.WebApiClient` 的请求上下文隔离、选项校验、组合测试和接入文档。
- 不替换 WebApiClientCore，不改变现有注册方式，不修改动态 API 服务端协议。

## 目标

增强多租户安全性、配置失败提示和多接口组合能力。默认策略是：每次请求先清理本包管理的上下文头，再按当前上下文写入非空值；`null` 头名表示禁用。

## 实施任务

### OPT-001：请求上下文头隔离

- 修改 `BingWebApiClientRequestContextHandler`，每次请求先移除租户、关联 ID、语言三个已配置头。
- 当前上下文或字段为空时不保留旧的 `HttpClient.DefaultRequestHeaders` 值；非空值再写入。
- 保留自定义头名称和单项禁用行为。
- 通过连续请求、空上下文和并发请求测试验证无残留。

### OPT-002：配置项提前校验

- 在 `BingWebApiClientOptions` 中校验租户、关联 ID、语言请求头名称。
- `null` 合法并表示禁用；空字符串、空白或非法 HTTP 头名称在注册时抛出 `ArgumentException`。
- 保持 BaseAddress 既有 HTTP/HTTPS、绝对 URI、无查询和无片段校验。

### OPT-003：多 SDK 组合回归

- 增加两个不同 SDK 接口的真实 TestServer 注册测试。
- 验证地址、上下文头名称、处理器和请求头互不串扰。
- 验证同一上下文访问器可被多个 SDK 复用。
- 保留宿主认证处理器、默认请求头、builder 配置和资源释放测试。

### OPT-004：文档同步

- 说明上下文头优先级和清理行为。
- 说明宿主固定头应使用不同名称或独立处理器。
- 增加多 SDK 注册示例，保留 HttpHost、BaseAddress 回退、异常和资源释放说明。

## 验证

- L0：`git diff --check`、受影响项目 Release 编译。
- L1：新增请求头清理、头名校验和组合注册精准测试，分别运行 net6.0/net8.0。
- L2：WebApiClient SDK 测试项目双目标完整测试。
- L3：SDK 包多目标打包，检查包内 `lib/net6.0` 与 `lib/net8.0` 资产。
- 动态 MVC 回归复用上一任务中未变更服务端范围的证据，不重复无关全量测试。

## 不纳入本任务

- 不实现新的重试策略。
- 不替换 `Bing.DynamicApi.Client`。
- 不从运行时 API 描述生成第三方 DTO。
- 不改变 BaseAddress 必填语义。
- 不新增 Benchmark 或理论性能优化。

## 变更影响分析

- ChangedFiles：WebApiClient SDK 生产代码、测试、README、本任务执行记录。
- ChangedProjects：`Bing.DynamicApi.WebApiClient`、`Bing.DynamicApi.WebApiClient.Tests`。
- ChangedPublicContracts：仅异常校验行为更早失败；不改现有成功注册签名。
- ChangedRuntimePaths：请求处理器头清理和选项注册校验。
- ChangedProviders：无。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：仅验证，不改版本或发布设置。
- AffectedDependents：第三方 SDK 注册者和 WebApiClient typed API 使用者。
- RiskLevel：MEDIUM（多租户请求头语义改变）。

## 完成标准

- 头清理、配置校验和多 SDK 组合均有真实 TestServer 双目标测试。
- README 与实际行为一致。
- 测试、构建、打包和 diff 检查通过。
- 生成 execution.md 及生产符号到测试方法追溯表。
- 不自动 git add、commit、push、创建 PR 或发布包。
