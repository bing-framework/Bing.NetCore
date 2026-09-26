# WebApiClient SDK 接入计划检查与修复

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-CHECK-20260925-002`
- 目标：复核上一轮第三方 WebApiClient SDK 基础包是否满足已批准计划，并修复有证据的组合配置测试缺口。
- 范围：`Bing.DynamicApi.WebApiClient`、其 TestServer 测试和接入文档；不修改动态 API 服务端协议，不替换原生动态客户端。

## 计划检查结论

上一轮计划 `BING-WEBAPICLIENT-SDK-20260925-001` 已完成核心实现：

- 独立 `Bing.DynamicApi.WebApiClient` 包目标为 net6.0/net8.0，固定 WebApiClientCore 2.1.6。
- `AddBingWebApiClient<TApi>` 返回原生 `IHttpClientBuilder`，动态上下文头、认证处理器、JSON、原样文本、multipart、下载、取消和非成功状态已有真实 TestServer 覆盖。
- README 已说明声明式契约、版本路由、资源释放、异常形态和重试边界。
- 上一轮双目标框架测试和动态 MVC 回归测试均通过；WebApiClientCore 在 net6.0 使用可兼容资产、net8.0 使用 net8.0 资产，当前兼容性假设成立。

当前范围内 `OPEN_ACTIONABLE` 为四项测试/文档证据缺口：文档和实现承诺“宿主已经配置 `HttpHost` 时保留宿主值”，但测试只覆盖了通过返回 builder 在注册后覆盖 `HttpHost` 的场景；默认上下文访问器分支从未被解析调用；上下文只调用一次，错误的缓存实现仍可能通过；multipart 只验证文件内容，没有验证普通表单字段。它们都对应公开组合契约，必须补测；若测试失败再调整实现，否则仅补证据，不制造无关代码变化。

其他审查项分类：

- WebApiClientCore 2.1.6 的异常、RawReturn、取消和 FormDataFile 语义已通过源码/包兼容性核验：`VERIFIED_BOUNDARY`。
- 动态 API 服务端接口参数元数据和描述端点冲突属于另一任务的服务端范围：`DEFERRED`，不纳入本任务。
- 未提供完整运行时 DTO 模型，因此不从描述端点生成第三方 C# SDK：计划中的明确边界，`ACCEPTED_LIMITATION`。

## Phase 1：回归证据补齐

### WSDK-CHK-001：验证注册前 HttpHost 组合顺序

- 目标：证明基础包只提供地址回退，不覆盖宿主在注册前配置的 WebApiClientCore `HttpHost`。
- 现状/证据：生产代码使用 `httpApiOptions.HttpHost ??= baseAddress`；已有测试仅在扩展调用后执行 `ConfigureHttpApi`。
- 修改范围：`framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs`；如测试暴露真实顺序问题，再最小修改 `BingWebApiClientServiceCollectionExtensions.cs`。
- 实施步骤：先在 `AddBingWebApiClient` 前注册 `Configure<HttpApiOptions>`，再创建客户端并发送真实请求；断言请求仍到预配置宿主。保持同一 TestServer 和现有认证/上下文链路。
- 依赖：WebApiClientCore 2.1.6 的 `HttpApiOptions.HttpHost` 配置顺序。
- 验证：L0 编译/格式；L1 新增精准测试（net6.0、net8.0）。
- 风险：配置顺序若与假设不同，可能影响已有宿主的自定义地址；只允许修复该注册顺序，不扩展公共 API。
- 验收标准：新增测试在两个目标框架通过；既有 7 个端到端测试仍通过；无需修改实现时，在 execution.md 记录“已验证存在，无需改动”。

### WSDK-CHK-001A：验证默认访问器和上下文按请求读取

- 目标：覆盖不注册自定义访问器时的默认空实现，并证明同一个 typed API 连续请求会读取两次不同上下文。
- 现状/证据：测试夹具总是预先注册 `TestRequestContextAccessor`；生产 `TryAddSingleton` 默认分支没有实际解析证据；当前上下文测试只发送一次请求。
- 修改范围：`BingWebApiClientEndToEndTest.cs`，必要时补充测试服务端返回字段。
- 实施步骤：新增默认访问器解析/调用测试；在同一客户端中先发送第一组上下文，再修改 accessor 后发送第二组，分别断言服务端头值。
- 验证：L1 双目标框架精准测试。
- 风险：只涉及测试覆盖，不改变公共 API。
- 验收标准：默认访问器可构建且不会注入上下文头；两次请求的服务端头值分别与两次上下文一致。

### WSDK-CHK-001B：验证 multipart 普通字段

- 目标：证明 `[FormDataContent]` 的 DTO 字段与 `FormDataFile` 文件都实际发送并绑定。
- 现状/证据：服务端只读取文件内容，`Description` 字段即使缺失或名称错误测试也可能通过。
- 修改范围：测试服务端 `OrdersAppService.UploadAsync` 和对应断言/探针；不修改生产包。
- 实施步骤：从请求表单读取 `Description`，将其与文件内容一起返回或记录，断言两者均正确；同时补齐上传 POST 和下载 GET 的最终方法/路径断言。
- 验证：L1 双目标框架精准测试。
- 风险：仅增强契约测试断言；不改变动态 API 协议。
- 验收标准：字段、文件、HTTP 动词和路径均被直接断言。

### WSDK-CHK-002：同步接入文档证据

- 目标：让 README 示例明确区分“注册前宿主配置”和“注册后 builder 覆盖”，避免第三方误解。
- 现状/证据：README 当前示例在注册后重复设置相同地址，未展示回退语义的最小写法。
- 修改范围：`framework/src/Bing.DynamicApi.WebApiClient/README.md`，仅在测试确认后做最小文字/示例调整。
- 验证：UTF-8 读取检查、Markdown 差异检查、`git diff --check`；代码测试证据复用。
- 风险：仅文档变化，不改变运行时行为。
- 验收标准：示例能表达宿主可在注册前配置 HttpHost，`BaseAddress` 作为回退值；保留认证、上下文和资源释放说明。

## Phase 2：最终验证与追溯

### WSDK-CHK-003：受影响范围验证

- 目标：确认 SDK 包和动态 MVC 消费者没有回归。
- 验证顺序：
  1. L0：`git diff --check`、受影响项目编译。
  2. L1：WebApiClient SDK 测试 net6.0/net8.0。
  3. L2/L3：WebApiClient 测试项目完整双目标测试、动态 MVC 受影响测试。
  4. 包边界：Release pack/输出检查，复用上一轮未变更的兼容性证据。
- 验收标准：测试通过；任何外部 NuGet 漏洞索引不可达仅记录为环境警告，不伪装成代码失败；生成最终生产符号→测试方法追溯表。

## 变更影响分析

- ChangedFiles：SDK 测试、SDK README；实现文件仅在回归测试证明必要时修改。
- ChangedProjects：`Bing.DynamicApi.WebApiClient.Tests`；必要时 `Bing.DynamicApi.WebApiClient`。
- ChangedPublicContracts：无预期变化。
- ChangedRuntimePaths：仅可能涉及 WebApiClientCore HttpHost 配置组合顺序。
- ChangedProviders：无。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无版本/发布变更；保留上一轮包检查证据。
- ChangedDocsOnly：README 组合配置示例。
- AffectedDependents：第三方 SDK 注册者、WebApiClient 测试宿主；动态 MVC 仅做回归。
- RiskLevel：LOW。

## 完成标准

- 计划检查结论已写入本计划和 execution.md。
- 注册前 `HttpHost` 组合顺序有双目标框架回归证据。
- 文档与真实行为一致。
- 受影响测试、构建/打包检查和 diff 检查有记录。
- 不自动 git add、commit、push 或创建 PR。
