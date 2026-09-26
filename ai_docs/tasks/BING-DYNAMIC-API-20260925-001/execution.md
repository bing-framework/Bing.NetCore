<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-DYNAMIC-API-20260925-001
AI_EXECUTION_FINISHED_AT: 2026-09-25T20:57:51.3358285+08:00

# 实施执行报告

## 执行结论

已完成动态 API 服务端、运行时 API 描述、类型化动态客户端、测试与接入文档。实现采用显式 opt-in，不引入 ABP 运行时依赖；新动态端点返回未包装的原始结果，显式 Controller 行为保持不变。

Review Fix Round 1（开始时间：`2026-09-25T06:50:45.968Z`）已完成：处理 review.md 中 MUST_FIX / SHOULD_FIX 的 FIX-001 至 FIX-006，并完成受影响测试、构建及差异检查。Review Fix Round 2 已完成 FIX-007 泛型签名匹配修复；Review 报告保留为独立审查证据，待重新 Review。

## 任务信息

- Task ID：`BING-DYNAMIC-API-20260925-001`
- 计划：`ai_docs/tasks/BING-DYNAMIC-API-20260925-001/plan.md`
- 执行器：Codex
- 上一轮实施阶段完成时间：`2026-09-25T02:30:08.5190443+08:00`
- Review Fix Round 1 完成时间：以本文机器元数据 `AI_EXECUTION_FINISHED_AT` 为准。

## 计划执行情况

- 服务端程序集发现、筛选/禁用、MVC 动态 Controller 与 Action 生成：完成。
- ABP 风格路由、HTTP 动词推断、参数绑定、上传/下载和冲突检测：完成。
- 版本化 API 描述端点与类型化 HTTP 客户端：完成。
- 上下文头透传、请求处理器/序列化器/重试扩展、文档和示例：完成。
- 精准及完整受影响测试、生产项目构建和差异检查：完成。

## 已完成事项

- 在 `Bing.AspNetCore.Mvc` 增加显式程序集注册、服务筛选、禁用标记和 MVC 应用模型约定；Controller 通过服务接口从 DI 解析应用服务实例。
- 支持默认 `/api/app/v1/{kebab-case-service}` 路由、方法前缀动词推断、`Async` 后缀处理、HTTP 特性覆盖、路径/查询/Body 参数绑定、对象标识符以及 multipart/远程流。
- 注册时检测动态路由与显式 Controller、API 描述端点之间的冲突，并提供包含版本信息的定义端点及授权元数据。
- 增加共享 Contracts 和独立 Client 项目。客户端缓存 API 定义、选择配置版本、创建类型化代理，通过 `IHttpClientFactory` 调用 HTTP API，并处理 JSON、取消、上下文头、表单/流、远程错误和可配置重试（默认关闭）。
- 动态 Action 显式忽略既有 `ResultHandlerAttribute` 的结果包装；既有显式 API 的过滤器行为未改动。
- 更新 MVC/客户端接入文档、测试模块和解决方案项目清单。

## 部分/未完成事项

- 无计划内遗留事项。
- 未运行全解决方案测试或全解决方案构建；已运行受影响 MVC 和客户端测试项目的完整测试，并构建两个受影响生产项目。
- 未运行独立格式化器/Lint；仓库对应 .NET 项目没有在本任务验证中调用单独的格式化或 Lint 命令。

## 修改文件

- `Bing.All.sln`
- `framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/`
- `framework/src/Bing.AspNetCore.Mvc/README.md`
- `framework/src/Bing.AspNetCore.Mvc/references.props`
- `framework/src/Bing.AspNetCore/Bing/AspNetCore/Mvc/Filters/ResultHandlerAttribute.cs`
- `framework/src/Bing.DynamicApi.Contracts/`
- `framework/src/Bing.DynamicApi.Client/`
- `framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/`
- `framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/BingAspNetCoreMvcTestModule.cs`
- `framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj`
- `framework/tests/Bing.DynamicApi.Client.Tests/`
- `ai_docs/tasks/BING-DYNAMIC-API-20260925-001/execution.md`

## API/数据/配置变化

- 服务端新增 `AddBingDynamicApi(assembly, options)` 显式接入入口、动态 API 选项和 `DisableBingDynamicApiAttribute`。默认根路径为 `/api/app`、默认 API 版本为 `v1`；可以按宿主配置服务名、筛选条件与路由策略。
- 服务端 API 描述路由为 `/api/dynamic-api/{version}/definition`。描述来自 MVC 最终 Action 描述，包含服务契约、路由、动词、参数来源和响应信息，并遵循宿主授权策略。
- 新增独立 `Bing.DynamicApi.Contracts` 与 `Bing.DynamicApi.Client`，提供服务契约定义、远程调用异常、客户端注册/选项、序列化、请求处理器和重试策略扩展点。客户端支持显式配置 API 版本及宿主的身份凭据注入。
- 不涉及数据库、迁移或持久化配置变更；不引入 ABP 运行时包。

## 测试结果

- MVC：`dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj --no-restore -v q`，net6.0 与 net8.0 各 70 passed、1 skipped。
- 动态客户端：`dotnet test framework/tests/Bing.DynamicApi.Client.Tests/Bing.DynamicApi.Client.Tests.csproj --no-restore -v q`，net6.0 与 net8.0 各 19 passed；包含泛型参数/重载签名、真实 TestServer 调用及原样文本字符串响应测试。
- 核心覆盖包括发现/禁用、最终路由和参数元数据、契约与实现参数名差异、授权、DI 注册代理解析、原始响应、HTTP 特性、流、API 描述和版本、路由冲突，以及客户端缓存/调用/取消/远程错误/自定义序列化与请求处理/重试/版本选择。

## Build/Typecheck/Lint/Format

- `dotnet build framework/src/Bing.AspNetCore.Mvc/Bing.AspNetCore.Mvc.csproj --no-restore -v q`：通过，0 errors。
- `dotnet build framework/src/Bing.DynamicApi.Client/Bing.DynamicApi.Client.csproj --no-restore -v q`：通过，0 errors；Review Fix Round 3 再次构建通过。
- `dotnet sln Bing.All.sln list`：通过，确认新增项目已纳入解决方案。
- `git diff --check`：通过。
- 构建/测试输出包含 `NU1900`：当前环境无法访问 `api.nuget.org` 获取漏洞数据；不影响还原缓存依赖后的编译与测试。另有既存 `ASP0019` 警告位于未修改的 `RemoteStreamContentTestController.cs`。

## Review Fix Round 1

按 `review.md` 的 MUST_FIX + SHOULD_FIX 范围完成全部 6 项；没有修改 Review 报告本身。

| Finding | 修复记录 | 回归验证 |
| --- | --- | --- |
| FIX-001 | 新增 MVC `IActionDescriptorProvider`：动态 Controller 由 DI 按应用服务接口激活，同时在 descriptor 属性中保留实现类型供注册表/API 描述使用，避免接口型代理被错误强转为实现类。接口代理测试在修复前复现 `InvalidCastException`，修复后请求成功。 | `DynamicController_UsesAnInterfaceOnlyServiceProxy`（`Bing.AspNetCore.Mvc.Tests`）；原子类代理 `DynamicController_UsesTheScopedServiceInterfaceProxy` 继续通过。 |
| FIX-002 | 动态服务扫描排除显式 MVC Controller 类型（Controller 基类、Controller 命名约定及 Controller/ApiController 标记），防止动态约定删除或改写原有显式路由。 | `ExplicitControllerThatImplementsAppService_PreservesItsOriginalRoute`（`Bing.AspNetCore.Mvc.Tests`）。 |
| FIX-003 | 元数据定义由与调用者取消令牌隔离的共享任务获取；各等待者可独立取消，失败或取消的共享缓存项按实例移除，后续调用可重新获取。 | `Should_cancel_one_definition_waiter_without_cancelling_shared_fetch`、`Should_remove_cancelled_definition_fetch_and_allow_retry`（`Bing.DynamicApi.Client.Tests`）。 |
| FIX-004 | 将原始调用取消令牌通过请求选项传递至错误解析，并用令牌读取非成功响应正文；取消路径释放响应资源。 | `Should_cancel_remote_error_body_read_with_method_token`（`Bing.DynamicApi.Client.Tests`）；`Should_parse_remote_error_and_status_code` 继续通过。 |
| FIX-005 | MVC 应用模型阶段按最终绑定来源和键名检查重复参数；重复 Query/Form 名启动时报服务方法、来源及名称，Query 与 Header 等不同来源的同名参数允许。 | `Startup_RejectsDuplicateQueryBindingNames`、`Startup_RejectsDuplicateFormBindingNames`、`SameBindingNameFromDifferentSources_IsAllowed`（`Bing.AspNetCore.Mvc.Tests`）。 |
| FIX-006 | 新增客户端跨层集成测试：类型化代理从真实 MVC TestServer 获取最终 API 描述，再通过真实动态路由发送路径、查询和 JSON Body 并校验响应。客户端测试项目接入 TestHost 和 MVC 测试宿主依赖。 | `DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer`（`Bing.DynamicApi.Client.Tests`），net6.0/net8.0 均通过。 |

本轮最终验证：MVC 测试项目 net6.0/net8.0 各 70 passed、1 skipped；客户端测试项目 net6.0/net8.0 各 16 passed；`Bing.AspNetCore.Mvc` 与 `Bing.DynamicApi.Client` 生产项目构建通过；`git diff --check` 通过。恢复/构建/测试仍有 `NU1900` 漏洞源不可访问警告，无编译错误或测试失败。

## Review Fix Round 2

仅处理 `review.md` 中 SHOULD_FIX 的 FIX-007；未修改 Review 报告本身。

| Finding | 修复记录 | 回归验证 |
| --- | --- | --- |
| FIX-007 | 服务端 API 描述将 `TypeName`/`ReturnTypeName` 改为不含程序集版本信息的 `Type.ToString()` 完整类型表示；客户端先完整比较 `FullName`、`Name`、`ToString()`，再保留旧式简单/程序集限定类型名兼容检查，避免在泛型类型实参逗号处截断新格式。 | `DynamicClient_MatchesGenericAndOverloadedParameterTypesThroughTestServer` 覆盖 `List<int>`、`List<string>` 同名方法重载及 `E2EDynamicApiEnvelope<E2EDynamicApiInput>` 泛型 DTO，经真实 TestServer 获取描述并完成调用；客户端 net6.0/net8.0 各 17 passed。 |

本轮验证：MVC 测试项目 net6.0/net8.0 各 70 passed、1 skipped；客户端测试项目 net6.0/net8.0 各 17 passed；`Bing.AspNetCore.Mvc` 和 `Bing.DynamicApi.Client` 生产项目构建通过；`git diff --check` 通过。测试和构建包含环境既有 `NU1900`（无法访问 NuGet 漏洞源）警告；MVC 测试另有未修改文件中的 `ASP0019` 警告，无错误。

## Review Fix Round 3

仅处理 `review.md` 中 SHOULD_FIX 的 FIX-008；未修改 Review 报告本身。

| Finding | 修复记录 | 回归验证 |
| --- | --- | --- |
| FIX-008 | 客户端仅在目标返回类型为 `string` 且响应媒体类型为非 JSON 的 `text/*` 时，使用带调用取消令牌的文本读取；`application/json`、`text/json` 和 `+json` 继续交给宿主配置的 JSON 序列化器，缺失/二进制/其他媒体类型不被泛化为字符串。非成功状态仍走远程错误解析路径。 | `DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer` 通过真实 MVC TestServer/API 描述调用 `Task<string>`，断言返回原样 `plain:text-response`；`Should_use_custom_serializer_for_json_string_response` 断言 vendor `+json` 字符串响应仍调用自定义序列化器。 |

本轮验证：客户端聚焦用例 net6.0/net8.0 各 2 passed；客户端完整测试 net6.0/net8.0 各 19 passed；MVC 完整测试 net6.0/net8.0 各 70 passed、1 skipped；`Bing.DynamicApi.Client` 生产项目构建通过。`git diff --check` 通过；新增/修改 C# 文件未发现行尾空白。测试和构建包含环境既有 `NU1900`（无法访问 NuGet 漏洞源）警告，无编译错误或测试失败。

## 计划偏差

- 为保证契约接口上的参数名与绑定特性在 MVC 元数据及调用时保持一致，生成 Action 参数使用服务契约的 `ParameterInfo`，并合并实现方法上的绑定信息；路径值使用专用 binder 转换到目标类型。该调整是计划内参数绑定的实现细化。
- MVC 默认授权模型发现发生在动态 Action 约定之前；实现将契约上的授权元数据复制到动态 Endpoint metadata，避免动态端点意外匿名。与“描述端点遵循宿主授权策略”的计划一致。
- 未承诺 ABP 私有 API 描述/客户端协议兼容；计划仅要求能力与主要约定对齐，此处没有偏离。

## 基线问题

- 工作区初始基线未见与本任务重叠的用户改动；实现仅修改本计划相关文件。
- NuGet vulnerability feed 访问警告为环境限制，测试和构建均已成功完成。

## 已知问题

- 动态客户端面向 `Task`/`Task<T>` 异步契约；不支持的同步方法形态会在代理创建时明确拒绝。
- 重试默认关闭；宿主须自行配置重试策略，并仅对适用请求启用。
- API 定义及路由版本遵循 Bing 新增协议，不与 ABP 私有动态客户端协议互通。

## 风险与回归关注点

- 应用服务契约方法、实现方法和已注册 DI 服务需保持兼容；MVC Action 以契约为描述和参数元数据来源，同时由 DI 提供实际服务实例。
- 新动态端点不会经过 `ResultHandlerAttribute` 包装；调用方应使用原始响应及标准 HTTP 状态语义。显式 API 的包装行为通过完整 MVC 测试回归覆盖。
- 多程序集注册、根路径/版本定制及生产授权策略由宿主配置；上线前应在宿主自身测试授权、路由和客户端凭据处理器。

## Reviewer 注意事项

### 生产符号 → 测试方法

| 生产符号/行为 | 对应测试方法 | 测试项目 |
| --- | --- | --- |
| `AddBingDynamicApi`、MVC Controller 发现及禁用 | `DynamicService_IsInMvcControllerFeature`、`DynamicService_ActionsAreInMvcActionDescriptors` | `Bing.AspNetCore.Mvc.Tests` |
| 接口代理激活及显式 Controller 保护 | `DynamicController_UsesAnInterfaceOnlyServiceProxy`、`ExplicitControllerThatImplementsAppService_PreservesItsOriginalRoute` | `Bing.AspNetCore.Mvc.Tests` |
| 动词推断、完整路由、服务名规范化 | `RouteConvention_UsesExpectedVerbAndPath`、`NormalizeServiceName_RemovesServiceContractSuffix` | `Bing.AspNetCore.Mvc.Tests` |
| 服务契约参数元数据及路径绑定 | `GetAsync_UsesRouteIdAndReturnsUnwrappedJson`、`InterfaceParameterAttributes_ControlBindingAndMetadata`、`GetByIdAsync_BindsObjectIdentifierFromRoute` | `Bing.AspNetCore.Mvc.Tests` |
| DI 服务实例代理解析 | `DynamicController_UsesTheScopedServiceInterfaceProxy` | `Bing.AspNetCore.Mvc.Tests` |
| 契约授权元数据及描述端点授权 | `AuthorizationFromServiceContract_IsAppliedAndDefinitionIsNotAnonymous` | `Bing.AspNetCore.Mvc.Tests` |
| 请求 Body、显式 HTTP 特性及流绑定 | `CreateAsync_BindsComplexBodyAndReturnsOriginalObject`、`ExplicitHttpAttribute_OverridesDefaultMethodRoute`、`UploadAndDownload_UseMultipartAndRemoteStreamContent` | `Bing.AspNetCore.Mvc.Tests` |
| API 描述和版本路由 | `DefinitionEndpoint_DescribesFinalMvcRoutesAndBindings`、`UnknownVersion_ReturnsNotFound` | `Bing.AspNetCore.Mvc.Tests` |
| API 描述泛型类型身份与客户端重载匹配 | `DynamicClient_MatchesGenericAndOverloadedParameterTypesThroughTestServer` | `Bing.DynamicApi.Client.Tests` |
| `Task<string>` 原样文本响应及 JSON 字符串自定义序列化 | `DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer`、`Should_use_custom_serializer_for_json_string_response` | `Bing.DynamicApi.Client.Tests` |
| 动态路由与显式端点冲突检测 | `Startup_RejectsDynamicRouteCollisionWithExplicitController`、`Startup_RejectsExplicitControllerCollisionWithDefinitionEndpoint` | `Bing.AspNetCore.Mvc.Tests` |
| 同来源重复绑定键检测与跨来源同名兼容 | `Startup_RejectsDuplicateQueryBindingNames`、`Startup_RejectsDuplicateFormBindingNames`、`SameBindingNameFromDifferentSources_IsAllowed` | `Bing.AspNetCore.Mvc.Tests` |
| 客户端元数据缓存、HTTP 调度和上下文透传 | `Should_cache_definition_and_dispatch_route_query_body_and_context_headers` | `Bing.DynamicApi.Client.Tests` |
| 客户端取消、错误、表单/流和扩展点 | `Should_propagate_method_cancellation_to_http_send`、`Should_cancel_initial_definition_request`、`Should_cancel_one_definition_waiter_without_cancelling_shared_fetch`、`Should_remove_cancelled_definition_fetch_and_allow_retry`、`Should_cancel_remote_error_body_read_with_method_token`、`Should_parse_remote_error_and_status_code`、`Should_send_multipart_file_and_return_download_stream_metadata`、`Should_use_custom_serializer_and_request_handler` | `Bing.DynamicApi.Client.Tests` |
| 客户端读取真实 API 描述并调用动态端点 | `DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer` | `Bing.DynamicApi.Client.Tests` |
| 客户端版本选择及重试默认/配置行为 | `Should_select_configured_api_version_for_metadata_and_method_routes`、`Should_keep_retries_disabled_by_default`、`Should_retry_idempotent_get_only_when_configured`、`Should_use_host_supplied_retry_policy` | `Bing.DynamicApi.Client.Tests` |

## Git 状态

- 本任务文件变更尚未提交。
- 未自动执行 `git add`、`git commit`、`git push` 或创建 PR。
