<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-WEBAPICLIENT-SDK-CHECK-20260925-002
AI_EXECUTION_FINISHED_AT: 2026-09-25T14:58:13.868Z

# 实施执行报告

## 执行结论

已完成上一轮 WebApiClient SDK 接入的计划符合性检查和有证据的回归补齐。独立包的核心功能真实可用；注册前接口级 `HttpHost` 配置会保留，默认访问器、逐请求上下文、multipart 普通字段均通过真实 TestServer 验证。没有修改生产代码或公共 API。

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-CHECK-20260925-002`
- 基线：`BING-WEBAPICLIENT-SDK-20260925-001` 的独立基础包、测试及完成报告。
- 本轮范围：SDK 测试和 README；不修改动态 API 服务端协议，不发布包。

## 计划执行情况

| Task | 状态 | 结果 |
|---|---|---|
| WSDK-CHK-001 | COMPLETED | 接口级预配置 `HttpHost` 在注册后未被 BaseAddress 回退值覆盖。 |
| WSDK-CHK-001A | COMPLETED | 默认空访问器可解析并完成调用；同一客户端两次请求取得两组不同的上下文头。 |
| WSDK-CHK-001B | COMPLETED | multipart 普通字段和文件内容均到达服务端，上传和下载最终动词/路径有精确断言。 |
| WSDK-CHK-002 | COMPLETED | README 删除重复地址配置，加入接口级预配置示例。 |
| WSDK-CHK-003 | COMPLETED | 双框架 SDK 测试和多目标打包通过，MVC 证据复用上一轮未变更范围。 |

## 已完成事项

- 在真实动态 MVC TestServer 上新增 3 个端到端场景，并增强上传/下载断言；项目测试从 7 项增至 10 项。
- 验证生产 `AddBingWebApiClient<TApi>`、默认空访问器和上下文 handler 已正确实现，无需为制造差异修改生产代码。
- 对第三方示例补充 `ConfigureHttpApi<IOrdersSdk>` 的注册前配置方式。

## 部分/未完成事项

无计划内遗留；未执行全仓库测试、NuGet 发布、git add/commit/push 或 PR 创建。

## 修改文件

- `framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs`：新增/强化真实 HTTP 契约测试。
- `framework/src/Bing.DynamicApi.WebApiClient/README.md`：调整地址回退示例和说明。
- `ai_docs/tasks/BING-WEBAPICLIENT-SDK-CHECK-20260925-002/plan.md` 与本报告。

## API/数据/配置变化

无公共 API、数据结构、运行时配置、目标框架或服务端协议变化。

## 测试结果

- L1 `dotnet test framework/tests/Bing.DynamicApi.WebApiClient.Tests/Bing.DynamicApi.WebApiClient.Tests.csproj -c Release -f net8.0 --no-restore --filter FullyQualifiedName~BingWebApiClientEndToEndTest`：10/10 通过。
- L1 同命令 `-f net6.0`：10/10 通过。
- L2 双目标完整 SDK 测试：net8.0 10/10、net6.0 10/10 通过。
- L3 动态 MVC 服务端源未改，复用上一轮 `BING-WEBAPICLIENT-SDK-20260925-001/execution.md` 的 net8.0/net6.0 动态 API 29/29 回归证据；本轮未重复运行。

## Build/Typecheck/Lint/Format

- SDK 两目标的 Release 测试构建成功。
- `dotnet pack framework/src/Bing.DynamicApi.WebApiClient/Bing.DynamicApi.WebApiClient.csproj -c Release --no-restore --output .artifacts\\webapiclient-check\\combined`：成功；包包含 `lib/net6.0` 和 `lib/net8.0` 的 DLL/XML，另有符号包；未发布。
- `git diff --check`：通过。未运行独立 lint/formatter（当前修改无对应专用门禁）。

## 计划偏差

- 初次回归测试错误地使用了全局 `Configure<HttpApiOptions>`，而 WebApiClientCore 的选项是按接口配置；该实验首次断言失败。改为官方接口级 `ConfigureHttpApi<IOrdersSdk>` 后双框架通过，生产注册逻辑无需调整。
- 首次尝试的 `dotnet pack -f` 属命令行不支持的参数，未进入构建；随后使用完整多目标 pack 成功。

## 基线问题

NuGet 漏洞审计源不可达产生 `NU1900` 警告，不影响本地缓存资产构建和测试。其余动态 API 已有 staged 工作区改动保持原样。

## 已知问题

第三方 SDK 的路由和 DTO 是发布时确定的契约，服务端路由/版本调整需同步更新 SDK；首版不生成 DTO。WebApiClientCore 非成功状态及取消的异常形态仍按上一轮文档说明；不默认启用重试。

## 风险与回归关注点

下载 `HttpResponseMessage` 和异常中的响应由调用方释放。宿主预配置地址必须使用接口级 `ConfigureHttpApi<TApi>`；未命名的全局 `Configure<HttpApiOptions>` 不会作用于该 typed API。

## Reviewer 注意事项

核对新增注册前 HttpHost 测试与 README 示例一致；确认新断言读取真实请求表单字段而非仅验证文件；确认原生客户端和服务端生产文件均未被本轮修改。

## 生产符号 → 测试方法

| 生产符号/行为 | 测试项目 | 测试方法 |
|---|---|---|
| `AddBingWebApiClient<TApi>`：注册前接口级 `HttpHost` 优先于地址回退 | `Bing.DynamicApi.WebApiClient.Tests` | `RegistrationPreservesHttpHostConfiguredBeforeTheExtension` |
| `AddBingWebApiClient<TApi>`：默认空上下文访问器可解析调用 | 同上 | `RegistrationUsesEmptyContextAccessorWhenHostDoesNotProvideOne` |
| `BingWebApiClientRequestContextHandler.SendAsync`：逐请求读取上下文 | 同上 | `ContextAccessorIsReadForEveryRequest` |
| WebApiClientCore `[FormDataContent]` + `FormDataFile` 到 Bing MVC 上传绑定 | 同上 | `MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership` |
| 声明式上传 POST/下载 GET 版本化路由 | 同上 | `MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership` |
| 其他既有 SDK 行为 | 同上 | 本项目完整 10 项测试；原 7 项仍通过 |

## Git 状态

保留先前 staged 动态 API 改动；SDK 包和测试仍为工作区未跟踪文件，本轮未暂存、提交、推送或创建 PR。最终 `git diff --check` 通过。
