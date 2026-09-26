# 动态 API 实施计划

## 目标与现状

参照 ABP vNext 自动 API 控制器和动态 C# 客户端代理，在 Bing 中打通“应用服务 → HTTP API → API 描述 → 类型化客户端”链路，不引入 ABP 运行时依赖。

仓库已有 `IAppService`、自动 DI、MVC 测试宿主、多租户请求头和流绑定；现有 `IApiInterfaceService` 只枚举显式控制器，尚不能生成动态端点。现有全局 `ResultHandlerAttribute` 会包装结果，新动态端点必须明确避开该包装，保持原样响应。

## 实施方案

1. **服务端发现与注册**：在 `Bing.AspNetCore.Mvc` 增加显式程序集注册入口。仅扫描已注册程序集中的具体 `IAppService`；提供类型筛选及类/方法级禁用标记。通过 MVC `ControllerFeature` 和应用模型约定生成控制器与 Action，并确保 MVC 激活沿用应用服务的 DI/AOP 实例。未启用时，现有 Controller 和路由行为不变。
2. **路由与绑定**：默认 `/api/app/{kebab-case-service}`，按 ABP 的方法名前缀推断 GET、PUT、DELETE、POST、PATCH；`Async` 后缀不进入 URL。支持配置根路径、服务名、方法路由及显式 HTTP 特性覆盖。标识参数进入路径，简单参数进入查询，复杂输入进入请求体；上传使用 multipart，下载沿用 `IRemoteStreamContent`。启动时检测重复路由和无法唯一绑定的签名并报清晰错误，不静默覆盖显式 Controller。
3. **描述与客户端**：基于最终 MVC Action/ApiExplorer 信息提供版本化的 API 描述端点，记录服务契约、方法、路由、动词、参数来源和响应类型。元数据端点遵循宿主授权策略，不强制匿名。新增独立动态客户端组件：按共享服务接口注册代理，首次请求元数据并按远程服务及版本缓存；通过 `IHttpClientFactory` 发送请求，反序列化 `Task`/`Task<T>`，支持取消、普通 JSON、表单和流，失败时抛出携带 HTTP 状态与远程错误信息的 `BingRemoteCallException`。
4. **高级能力与接入**：客户端请求处理器支持宿主注入凭据，并透传当前租户键、关联 ID 和语言；API 版本在服务端路由及元数据中显式声明，客户端按配置选择版本。序列化、参数转换、请求处理器和重试策略提供扩展入口；重试默认关闭，由宿主为适用请求配置。新动态端点返回原始结果与标准 HTTP 状态，不经过 `ResultHandlerAttribute`；既有显式 API 的 `ApiResult` 行为不变。补充服务端和客户端接入文档及示例。

## 验证

- 单元测试覆盖发现/排除、动词与完整路由、显式特性覆盖、参数绑定、版本选择、冲突报错、DI/AOP 激活和 API 描述。
- 使用现有 ASP.NET Core 测试宿主做端到端测试：JSON CRUD、授权元数据、租户及关联 ID 透传、取消、上传下载、远程异常、可配置序列化与重试；确认显式 Controller 不回归。
- 先运行受影响项目的精准测试与完整测试，再在最终检查点构建受影响消费者；保留“生产符号 → 测试方法”的追溯记录。

## 已确定的默认值

- 显式 opt-in；ABP 风格且可配置的路由；运行时 API 描述；新端点原样响应；高级能力同步纳入。
- “对齐 ABP”指能力与主要约定对齐，不承诺与 ABP 私有协议或其客户端直接互通。
