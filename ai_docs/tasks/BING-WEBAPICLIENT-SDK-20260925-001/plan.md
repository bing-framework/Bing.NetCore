# 第三方 WebApiClient SDK 基础包计划

## 任务信息

- Task ID：`BING-WEBAPICLIENT-SDK-20260925-001`
- 目标：增加独立的 `Bing.DynamicApi.WebApiClient` 包，供第三方 SDK 使用 WebApiClientCore 声明式接口调用 Bing 动态 API。
- 不替换 `Bing.DynamicApi.Client`，不修改动态 API 服务端协议，不发布 NuGet。

## 结论

可行。新增独立的 `Bing.DynamicApi.WebApiClient` 包，供对外 SDK 使用；现有 `Bing.DynamicApi.Client` 继续承担运行时读取 API 描述的调用。两者都通过 HTTP 调用动态端点，但第三方 SDK 使用发布时确定的声明式接口，因此无需每次部署后在运行时发现路由。

固定 `WebApiClientCore` 2.1.6，在 net6.0、net8.0 验证所需能力和兼容性。它本身仍基于 `HttpClient`；本方案主要获得声明式契约和扩展能力，不预设性能收益。

## 实施步骤

1. **兼容性验证**：固定 `WebApiClientCore` 2.1.6，在 net6.0、net8.0 下用现有 MVC TestServer 验证 `AddHttpApi<T>`、JSON、原样字符串、multipart、取消及错误状态。重点确认流响应的所有权和异常形态；这些结果决定示例采用的具体返回类型。上游对原样 `string`、`Stream` 和 `HttpResponseMessage` 提供 `RawReturn`，但 SDK 仍须通过测试明确释放责任。
2. **独立集成包**：新增 `Bing.DynamicApi.WebApiClient`，目标框架为 net6.0、net8.0。提供 `AddBingWebApiClient<TApi>(Action<BingWebApiClientOptions>)`，只调用一次 `AddHttpApi<TApi>`，返回该接口的 `IHttpClientBuilder`。选项包含绝对服务地址回退值和租户、关联 ID、语言的请求头名称；已有 WebApiClient `HttpHost` 保持不变，宿主可继续通过返回的 builder 配置主机、`HttpClient`、默认头和处理器。上下文访问器在每次发送时读取当前值。认证、日志、序列化和处理器配置继续使用 WebApiClient 与 `IHttpClientBuilder` 的扩展入口，不复制现有动态客户端的代理、元数据缓存和重试实现。
3. **对外契约示例**：在示例和测试中声明独立于 `IAppService` 的 SDK 接口与 DTO，显式标注最终 HTTP 动词、`api/app/v1/...` 路由及参数来源。普通响应使用 JSON，原样文本显式使用 `RawReturn`；下载示例返回由调用方释放的 `HttpResponseMessage`，上传使用 WebApiClient 的 multipart 文件类型。路由版本随该 SDK 接口发布；服务端改变路由或版本时同步更新接口并运行契约测试。首版不从 Bing 的运行时描述生成 C# 代码，因为该描述没有独立生成 DTO 所需的完整模型结构。
4. **验收与文档**：用真实 TestServer 覆盖 GET/POST、查询和路径、JSON、原样字符串、上传下载、取消、401/业务错误、上下文头及宿主注入认证；核对声明式路由与服务端最终端点一致。异常默认保留 WebApiClient 的公开异常形态，文档说明它与现有客户端的 `BingRemoteCallException` 不同。运行新项目双目标框架完整测试、受影响 MVC 回归、构建与打包检查，并提供第三方 DI 注册和资源释放示例；不执行发布。

## 已确定边界

- 首版交付可复用基础包与完整示例，不封装尚未指定的业务服务。
- 保留现有原生动态客户端；新依赖仅限独立包。
- 不改服务端协议。
- 重试默认不启用，由宿主仅为适合重放的请求配置。
- 不自动 git add、commit、push 或创建 PR；不修改包版本或发布包。

## 完成标准

- 新包及其测试项目均支持 net6.0 和 net8.0。
- 服务注册、动态上下文请求头和宿主处理器入口可通过公开 API 使用。
- TestServer 验证本计划的核心 HTTP 行为、资源所有权和异常形态。
- 接入文档、execution.md、最终生产符号到测试方法的追溯映射齐全。
- 受影响测试、构建、打包检查及 `git diff --check` 结果已记录。
