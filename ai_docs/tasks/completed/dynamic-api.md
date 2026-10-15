# 动态 API 与 WebApiClient SDK 完成摘要

本文件汇总动态 API 主任务及其第三方 WebApiClient SDK 后续任务的完成证据。对应原始 Markdown 已合并进本摘要；历史仓库路径以代码文本列出，避免清理后产生断链。完整原始 Markdown 仅保留在本地备份 `artifacts/task-doc-audit/tasks-before-cleanup.zip`，删除清单见 [CLEANUP.md](../CLEANUP.md)。文中的测试数量、构建结果和制品状态均为原任务在 2026-09-25 至 2026-09-26 记录的历史结果，本文件未重新运行。

## 任务关系

| 任务 | 结论 | 作用 |
| --- | --- | --- |
| BING-DYNAMIC-API-20260925-001 | Review PASS；FIX-001 至 FIX-008 全部 CLOSED | 实现服务端动态 API、运行时描述和原生类型化客户端 |
| BING-WEBAPICLIENT-SDK-20260925-001 | execution COMPLETED | 增加独立第三方 WebApiClientCore SDK 包 |
| BING-WEBAPICLIENT-SDK-CHECK-20260925-002 | execution COMPLETED | 补齐 SDK 注册顺序、默认上下文和 multipart 合同 |
| BING-WEBAPICLIENT-SDK-OPT-20260925-003 | Review PASS；OPT-001 至 OPT-004 全部 PASS | 收紧请求头隔离、选项校验和多 SDK 组合行为 |

## BING-DYNAMIC-API-20260925-001

<a id="BING-DYNAMIC-API-20260925-001"></a>

### 目标与范围

在不引入 ABP 运行时依赖的前提下，打通应用服务、动态 HTTP API、运行时 API 描述和类型化客户端链路。任务采用显式 opt-in，不改变未启用动态 API 时的既有 Controller、路由和结果处理行为。

### 最终实现

- 在 Bing.AspNetCore.Mvc 中增加显式程序集注册、应用服务筛选、类/方法级禁用标记、MVC Controller/Action 生成和冲突检测。
- 默认动态路由为 /api/app/v1/{kebab-case-service}；方法前缀推断 HTTP 动词，Async 后缀不进入 URL；支持根路径、服务名、版本、方法路由和显式 HTTP 特性配置。
- 按绑定来源处理路径、查询、Header、Body、multipart 和远程流参数；重复的同来源绑定键在启动时拒绝，不同来源的同名参数可以共存。
- 通过 /api/dynamic-api/{version}/definition 暴露基于最终 MVC Action 描述的 API 定义，包含服务契约、路由、动词、参数来源和响应类型，并遵循宿主授权策略。
- 增加 Bing.DynamicApi.Contracts 与 Bing.DynamicApi.Client。原生动态客户端按服务和版本缓存定义，使用 IHttpClientFactory 创建代理，支持 Task/Task<T>、JSON、原样文本、表单、流、取消、上下文头、可替换序列化器、请求处理器和可配置重试。
- 动态 Action 显式跳过既有 ResultHandlerAttribute 的结果包装；既有显式 API 仍保持 ApiResult 和标准过滤器行为。

### 关键源码与测试

- 服务端入口：[BingDynamicApiServiceCollectionExtensions.cs](../../../framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiServiceCollectionExtensions.cs)、[BingDynamicApiRouteBuilder.cs](../../../framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiRouteBuilder.cs)、[BingDynamicApiApplicationModelConvention.cs](../../../framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiApplicationModelConvention.cs)、[BingDynamicApiActionDescriptorProvider.cs](../../../framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiActionDescriptorProvider.cs)。
- 动态描述与契约：[BingDynamicApiDescriptionController.cs](../../../framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiDescriptionController.cs)、[BingDynamicApiDefinition.cs](../../../framework/src/Bing.DynamicApi.Contracts/BingDynamicApiDefinition.cs)。
- 原生客户端：[BingDynamicApiClientProxyFactory.cs](../../../framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiClientProxyFactory.cs)、[BingDynamicApiDefinitionProvider.cs](../../../framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiDefinitionProvider.cs)、[BingDynamicApiClientServiceCollectionExtensions.cs](../../../framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiClientServiceCollectionExtensions.cs)。
- MVC 回归：[BingDynamicApiIntegrationTest.cs](../../../framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/BingDynamicApiIntegrationTest.cs)、[BingDynamicApiRouteBuilderTest.cs](../../../framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/BingDynamicApiRouteBuilderTest.cs)。
- 原生客户端回归：[BingDynamicApiEndToEndTest.cs](../../../framework/tests/Bing.DynamicApi.Client.Tests/BingDynamicApiEndToEndTest.cs)、[BingDynamicApiClientTest.cs](../../../framework/tests/Bing.DynamicApi.Client.Tests/BingDynamicApiClientTest.cs)、[BingDynamicApiCancellationTest.cs](../../../framework/tests/Bing.DynamicApi.Client.Tests/BingDynamicApiCancellationTest.cs)。

### Review 闭环与历史验证

- 独立 Review 状态为 PASS；FIX-001 至 FIX-008 均已关闭，没有遗留 MUST_FIX 或 SHOULD_FIX。
- FIX-001/002 覆盖接口代理激活和显式 Controller 路由保护；FIX-003/004 覆盖共享 API 定义取消、失败重试和远程错误正文取消；FIX-005 覆盖重复绑定键；FIX-006 覆盖真实 MVC TestServer 到动态客户端的完整调用；FIX-007 覆盖泛型参数和重载签名；FIX-008 覆盖原样文本和 JSON 字符串分流。
- 原任务记录 MVC 测试 net6.0/net8.0 各 70 passed、1 skipped；客户端完整测试各 19 passed；受影响生产项目构建通过。早期 Round 的 16/17 passed 等数字属于修复过程，不应与最终数字混用。
- NuGet 漏洞索引不可达产生 NU1900 环境告警；原任务未运行全解决方案测试、全解决方案构建或独立 Lint。

### 接口、异常、重试与限制

- 只支持 Task 或 Task<T> 形式的动态客户端契约；不支持的同步方法形态在代理创建时拒绝。
- 动态客户端非成功 HTTP 响应抛出 BingRemoteCallException，并保留 HTTP 状态和远程错误信息；错误正文读取使用调用取消令牌。
- 目标返回类型为 string 且响应为非 JSON text/* 时读取原样文本；application/json、text/json 和 +json 仍交给宿主配置的 JSON 序列化器。
- 重试默认关闭。宿主必须自行配置策略，并仅对可安全重放的请求启用。
- 动态端点不兼容 ABP 私有 API 描述/客户端协议；本任务只对齐主要能力和约定。
- 服务契约、实现方法和已注册 DI 服务必须保持兼容；多程序集注册、授权、版本和路由策略由宿主负责验证。

### 原始文件清单

- `ai_docs/tasks/BING-DYNAMIC-API-20260925-001/plan.md`：目标、路由/绑定/客户端方案和验收标准。
- `ai_docs/tasks/BING-DYNAMIC-API-20260925-001/execution.md`：实现范围、API 变化、测试/构建结果和 FIX 执行记录。
- `ai_docs/tasks/BING-DYNAMIC-API-20260925-001/review.md`：独立 Review、FIX-001 至 FIX-008 复核和最终 PASS。

## BING-WEBAPICLIENT-SDK-20260925-001

<a id="BING-WEBAPICLIENT-SDK-20260925-001"></a>

### 目标与最终实现

新增独立的 Bing.DynamicApi.WebApiClient 包，供第三方 SDK 使用 WebApiClientCore 2.1.6 的声明式接口调用动态 API。该包不替换 Bing.DynamicApi.Client，不修改动态 API 服务端协议，也不从运行时描述生成 DTO。

- 提供 IServiceCollection.AddBingWebApiClient<TApi>(Action<BingWebApiClientOptions>)，返回该 typed API 的 IHttpClientBuilder。
- BaseAddress 只作为回退值；宿主可以通过接口级 ConfigureHttpApi<TApi> 预配置 HttpHost、builder、HttpClient、默认头、认证和其他处理器。
- 支持 net6.0/net8.0、JSON、RawReturn 原样文本、multipart 上传、HttpResponseMessage 下载、取消、401/409 错误、宿主认证和逐请求上下文头。
- RawReturn 的 HttpResponseMessage 和内容资源由调用方释放；包不重新实现原生动态客户端的代理、定义缓存、错误包装或重试。

### 关键源码与测试

- [BingWebApiClientServiceCollectionExtensions.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientServiceCollectionExtensions.cs)
- [BingWebApiClientOptions.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientOptions.cs)
- [BingWebApiClientRequestContext.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientRequestContext.cs)
- [BingWebApiClientRequestContextHandler.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientRequestContextHandler.cs)
- [BingWebApiClientEndToEndTest.cs](../../../framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs)

### 历史验证与限制

- 原任务记录 SDK net6.0/net8.0 各 7/7、动态 MVC 筛选各 29/29、Release pack 成功；这些是原执行记录，未在本摘要任务中重跑。
- 依赖 WebApiClientCore 2.1.6，仅新包增加该依赖；不发布 NuGet 包。
- 路由和 DTO 是第三方 SDK 发布时确定的契约，服务端路由/版本变化需要同步更新接口。
- 非成功状态按 WebApiClientCore 公开形态返回 HttpRequestException，内层 ApiResponseStatusException 提供状态和响应；取消为外层 HttpRequestException、内层 TaskCanceledException。
- 重试默认不启用；宿主需自行保证重试请求可重放。

### 原始文件清单

- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-20260925-001/plan.md`：包边界、兼容性验证、注册 API 和完成标准。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-20260925-001/execution.md`：实现、测试、打包、资源所有权和异常边界。

## BING-WEBAPICLIENT-SDK-CHECK-20260925-002

<a id="BING-WEBAPICLIENT-SDK-CHECK-20260925-002"></a>

### 目标与最终实现

对基础 SDK 进行计划符合性检查并补齐四类组合证据，不修改生产代码或公共 API：

- 验证注册前接口级 HttpHost 优先于 BaseAddress 回退。
- 验证默认空上下文访问器可解析，同一 typed API 的连续请求逐次读取上下文。
- 验证 multipart 普通字段和文件字段均到达服务端，并精确断言上传/下载 HTTP 动词与路径。
- 更新 README，区分注册前宿主配置和注册后 builder 配置。

### 关键测试与历史验证

- 主要测试仍位于 [BingWebApiClientEndToEndTest.cs](../../../framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs)。
- 直接覆盖 RegistrationPreservesHttpHostConfiguredBeforeTheExtension、RegistrationUsesEmptyContextAccessorWhenHostDoesNotProvideOne、ContextAccessorIsReadForEveryRequest 和 MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership。
- 原任务记录双框架精准测试和完整 SDK 测试均 10/10 通过，pack 成功；MVC 29/29 为上一任务未变更服务端范围的复用证据，未在本任务重复运行。

### 限制

- 宿主预配置地址必须使用接口级 ConfigureHttpApi<TApi>；全局 Configure<HttpApiOptions> 不作用于该 typed API。
- 仍不生成 DTO、不替换原生动态客户端、不改变服务端协议、不默认启用重试。
- NuGet 漏洞审计源不可达产生 NU1900，仅作为环境警告。

### 原始文件清单

- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-CHECK-20260925-002/plan.md`：WSDK-CHK-001 至 WSDK-CHK-003 的检查与验收标准。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-CHECK-20260925-002/execution.md`：测试补齐、README 修改、历史验证和生产符号追溯。

## BING-WEBAPICLIENT-SDK-OPT-20260925-003

<a id="BING-WEBAPICLIENT-SDK-OPT-20260925-003"></a>

### 目标与最终实现

在不改变注册签名、动态 API 服务端协议或原生动态客户端的前提下，增强请求头隔离、配置失败提示和多 SDK 组合能力：

- BingWebApiClientRequestContextHandler 每次请求先移除本包管理的租户、关联 ID 和语言头，再按当前上下文写入非空值。
- BingWebApiClientOptions 在注册期校验头名；null 表示禁用，空白、控制字符和非 HTTP token 字符被拒绝。
- 两个 SDK 接口可共享 accessor，但各自的 Host、头名称、builder 默认头和处理器相互隔离；并发测试通过 TestServer 到齐屏障确认真实重叠。
- CorrelationIdHeaderName 等头名设为 null 时，本包不发送该上下文头，同名宿主固定头仍保持。
- README 同步头清理优先级、null 禁用、多 SDK 注册、HttpHost 回退、异常和资源释放说明。

### 关键源码与测试

- [BingWebApiClientRequestContextHandler.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientRequestContextHandler.cs)
- [BingWebApiClientOptions.cs](../../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientOptions.cs)
- [BingWebApiClientEndToEndTest.cs](../../../framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs)
- 主要合同包括 ContextHeadersClearDefaultValuesWhenContextIsMissingOrIncomplete、ContextHeadersRemainIsolatedForConcurrentRequests、MultipleSdkContractsKeepTheirHeaderConfigurationIndependent、RegistrationRejectsInvalidBaseAddressesAndNonInterfaceContracts、ContextHeaderNamesCanBeCustomizedOrDisabled。

### Review 闭环与限制

- 独立 Review 为 PASS，OPT-001 至 OPT-004 全部 PASS，未发现未解决 MUST_FIX/SHOULD_FIX。
- Review 复用的最近双框架 Release 测试记录为 net6.0 13/13、net8.0 13/13；此前 pack 已检查 net6.0/net8.0 资产。本摘要未重跑这些测试。
- 上下文为空时清理同名默认头是有意的多租户隔离行为；宿主固定头应使用不同名称或独立处理器。
- 本任务明确不新增重试策略、不生成第三方 DTO、不新增 Benchmark，也不进行 NuGet 发布。

### 原始文件清单

- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-OPT-20260925-003/plan.md`：OPT-001 至 OPT-004 的行为、测试和边界。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-OPT-20260925-003/execution.md`：实现、并发屏障、隔离测试、历史验证和追溯。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-OPT-20260925-003/review.md`：独立 PASS 结论及四项验收矩阵。

## 统一使用边界

### 原生动态客户端与第三方 SDK 的分工

- Bing.DynamicApi.Client 运行时读取服务端定义，按服务和版本缓存描述后创建类型化代理，适合服务契约与路由由服务端描述驱动的调用。
- Bing.DynamicApi.WebApiClient 是独立的声明式第三方 SDK 集成包，接口、DTO、路由和版本在 SDK 发布时确定，适合对外 SDK 使用者。
- 两者均通过 HTTP 调用动态端点，但不共享代理实现、元数据缓存或重试实现；第三方包不读取动态描述来生成 DTO。

### 资源、异常和安全边界

- 动态端点返回原始结果和标准 HTTP 状态，不经过 ResultHandlerAttribute；显式 Controller 仍沿用原有结果包装。
- 第三方 SDK 的 HttpResponseMessage、响应内容和下载资源由调用方释放；上传的 IRemoteStreamContent 依赖 Bing MVC 文件绑定配置。
- 请求上下文值应通过 ambient accessor 在 handler 发送阶段读取，不应缓存到单例字段。
- 测试和构建输出中的 NU1900 属于漏洞索引不可达告警；原任务没有将其误判为代码失败。

## 原始材料与验证声明

本摘要完整列出四个 task ID 及其历史 plan/execution/review 路径。对应原始 Markdown 已合并，完整内容仅保留在 `artifacts/task-doc-audit/tasks-before-cleanup.zip`；删除清单见 [CLEANUP.md](../CLEANUP.md)。所有数字、状态和 Review 结论均来自这些历史文件；本次只进行了 UTF-8 文档读取、路径核对和摘要写入，没有重新运行测试、构建、打包、CI 或发布。
