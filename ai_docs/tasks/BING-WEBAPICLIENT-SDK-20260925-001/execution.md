<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-WEBAPICLIENT-SDK-20260925-001
AI_EXECUTION_FINISHED_AT: 2026-09-25T14:35:56.724Z

# 实施执行报告

## 执行结论

已完成第三方 `WebApiClientCore` SDK 集成基础包、端到端示例、接入文档和双框架验证。新包独立于运行时 `Bing.DynamicApi.Client`，未改动态 API 服务端协议，也未发布 NuGet 包。

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-20260925-001`
- 分支：`feat/dynamic-api`
- 依赖：`WebApiClientCore` 固定为 `2.1.6`
- 目标框架：`net6.0`、`net8.0`

## 计划执行情况

1. 兼容性验证：通过 TestServer 验证 JSON、原样文本、multipart 上传、`HttpResponseMessage` 下载所有权、取消和错误响应。
2. 独立集成包：完成 `Bing.DynamicApi.WebApiClient`；`AddBingWebApiClient<TApi>` 只调用一次 `AddHttpApi<TApi>` 并返回对应 builder，可配置认证、默认头及其他处理器；租户、关联 ID、语言可按请求上下文动态透传。
3. 对外契约示例：测试和 README 声明独立于 `IAppService` 的 SDK 接口/DTO，显式描述路由、动词、绑定和序列化。
4. 验收与文档：测试覆盖路径/查询、GET/POST JSON、文本、multipart、下载释放、取消、401/409、宿主认证、默认头、上下文头自定义及路由/主机配置组合；已补充注册和释放示例。

## 已完成事项

- 新增多目标框架包、请求上下文访问器/处理器及 `AddBingWebApiClient<TApi>` 注册入口。
- 地址选项验证绝对 HTTP(S) URI，并拒绝查询/片段；作为 WebApiClient `HttpHost` 回退值，保留宿主预先或通过 builder 配置的主机。测试验证只生成一个 `TApi` 注册，宿主 `HttpClient.BaseAddress` 和默认请求头未被覆盖。
- 上传按 WebApiClient multipart 契约使用表单 DTO 与 `FormDataFile`；TestServer 配置 Bing MVC 文件绑定器后，`IRemoteStreamContent` 绑定成功。
- `RawReturn` 的 `HttpResponseMessage` 由调用端释放；未在新包内重新实现代理、元数据缓存、错误包装或重试。
- 增加 package README 并确认 Release NuGet 包元数据包含 README 和 `WebApiClientCore 2.1.6` 依赖。

## 部分/未完成事项

无计划内遗留。按计划未执行全仓库测试、NuGet 发布、git add/commit/push 或 PR 创建。

## 修改文件

- `framework/src/Bing.DynamicApi.WebApiClient/`：包项目、选项、上下文契约与处理器、DI 注册和 README。
- `framework/tests/Bing.DynamicApi.WebApiClient.Tests/`：双框架 TestServer 端到端测试。
- `Bing.All.sln`：加入新包和测试项目。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-20260925-001/`：实施计划与本执行报告。

## API/数据/配置变化

- 新增 `IServiceCollection.AddBingWebApiClient<TApi>(Action<BingWebApiClientOptions>)`，返回该 typed API 的 `IHttpClientBuilder`。
- 新增 `BingWebApiClientOptions`、`BingWebApiClientRequestContext`、`IBingWebApiClientRequestContextAccessor`。
- 默认上下文头为 `X-Tenant-Id`、`X-Correlation-Id`、`Accept-Language`；头名称可配置或设为 `null` 禁用。
- 仅新包增加 `WebApiClientCore` 依赖；现有原生动态客户端使用者无新增依赖。

## 测试结果

- `dotnet test framework/tests/Bing.DynamicApi.WebApiClient.Tests/Bing.DynamicApi.WebApiClient.Tests.csproj -c Release --no-restore`：net8.0 通过 7/7；net6.0 通过 7/7。
- `dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net8.0 --no-restore --filter FullyQualifiedName~DynamicApi`：通过 29/29。
- 同一 MVC 动态 API 回归筛选在 net6.0：通过 29/29。
- 测试验证宿主 `HttpHost` 覆盖与 `HttpClient.BaseAddress`/默认请求头保留；认证处理器、动态上下文和上下文头自定义/禁用均有断言。

## Build/Typecheck/Lint/Format

- `dotnet pack framework/src/Bing.DynamicApi.WebApiClient/Bing.DynamicApi.WebApiClient.csproj -c Release --no-restore --verbosity minimal`：成功生成 `Bing.DynamicApi.WebApiClient.7.0.0.nupkg` 与符号包；未发布。
- `git diff --check`：通过。
- Release 构建无错误。构建输出包含 NuGet 漏洞审计源不可达的 `NU1900` 告警；完整 Release 测试构建还显示既有依赖项目的 `CS1572`/`CS0618` 警告，不属于本次新增包代码。
- 未运行全仓库测试、独立 lint 或 formatter；本次没有相关专用配置变更。

## 计划偏差

- `BaseAddress` 不再无条件写入 `HttpHost`，而是作为回退值；通过 `ConfigureHttpApi`/builder 配置的宿主主机设置优先。这样只注册一次 typed API，并保留宿主 `HttpClient` 配置，且以端到端断言验证。
- 新测试宿主额外调用 Bing MVC 的 `AddBing` 配置，以注册框架已有的 `IRemoteStreamContent` 文件模型绑定器；这是测试宿主配置对齐，不涉及生产服务端协议或实现变更。

## 基线问题

- 环境无法访问 NuGet 漏洞审计服务索引，出现 `NU1900`；包已从本地还原资产成功构建、测试和打包。
- Release 测试构建触及依赖项目既有 XML 参数注释和过时接口警告（`CS1572`、`CS0618`）。

## 已知问题

- SDK 路由和 DTO 是调用方发布时确定的契约；服务端路由/版本变化需同步更新 SDK。首版不从动态描述生成 DTO。
- 按 WebApiClientCore 当前验证结果，非成功状态表现为 `HttpRequestException`，内层 `ApiResponseStatusException` 提供状态和响应；取消表现为外层 `HttpRequestException`、内层 `TaskCanceledException`。包不转换这些异常。
- 重试不默认启用。宿主配置重试时需自行保证请求可重放。

## 风险与回归关注点

- `HttpResponseMessage`/内容资源由第三方调用方释放；README 和 TestServer 覆盖该契约。
- `IRemoteStreamContent` 上传依赖 Bing MVC 文件绑定配置；新增服务端时应沿用框架 MVC 初始化方式。
- 请求上下文访问器在 handler 发送阶段调用；访问请求作用域值时应使用 ambient accessor（例如 `IHttpContextAccessor`），不要把单请求值保存在单例字段中。

## Reviewer 注意事项

- 检查 `BaseAddress` 回退与宿主 `ConfigureHttpApi` 覆盖顺序、typed API 注册唯一性。
- 检查 README 示例里的发布路由、multipart 参数形态、`HttpResponseMessage` 释放和异常说明与测试的一致性。
- 检查新依赖仅位于 `Bing.DynamicApi.WebApiClient`。

## 生产符号 → 测试方法

| 生产符号/行为 | 测试项目 | 测试方法 |
|---|---|---|
| `BingWebApiClientOptions.GetValidatedBaseAddress`：URI 校验 | `Bing.DynamicApi.WebApiClient.Tests` | `BingWebApiClientEndToEndTest.RegistrationRejectsInvalidBaseAddressesAndNonInterfaceContracts` |
| `AddBingWebApiClient<TApi>`：单次 typed 注册、host builder 配置 | `Bing.DynamicApi.WebApiClient.Tests` | `RegistrationRejectsInvalidBaseAddressesAndNonInterfaceContracts`; `ContextHeadersAndHostAuthenticationHandlerAreAppliedPerRequest` |
| `BingWebApiClientRequestContextHandler.SendAsync`：默认/自定义/禁用头 | `Bing.DynamicApi.WebApiClient.Tests` | `ContextHeadersAndHostAuthenticationHandlerAreAppliedPerRequest`; `ContextHeaderNamesCanBeCustomizedOrDisabled` |
| 声明式 GET 路由、路径/查询和 JSON POST | `Bing.DynamicApi.WebApiClient.Tests` | `DeclaredRoutesAndJsonPayloadsMatchDynamicMvcEndpoints` |
| RawReturn 文本、multipart 和下载响应所有权 | `Bing.DynamicApi.WebApiClient.Tests` | `MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership` |
| 状态异常形态 | `Bing.DynamicApi.WebApiClient.Tests` | `ContextHeadersAndHostAuthenticationHandlerAreAppliedPerRequest`; `NonSuccessBusinessStatusUsesWebApiClientExceptionShape` |
| `CancellationToken` 透传 | `Bing.DynamicApi.WebApiClient.Tests` | `CancellationTokenCancelsTheInFlightDynamicApiRequest` |
| 服务端动态路由/MVC 文件绑定回归 | `Bing.AspNetCore.Mvc.Tests` | `BingDynamicApiIntegrationTest.UploadAndDownload_UseMultipartAndRemoteStreamContent`; 动态 API 筛选共 29 项通过 |

## Git 状态

- 保留了工作区已有的动态 API staged changes；本任务新增文件保持未暂存，`Bing.All.sln` 有本任务加入项目的未暂存改动。
- `git diff --check` 通过；未自动 git add、commit、push 或创建 PR。
