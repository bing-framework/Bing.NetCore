# ADR-0004：统一 API 响应（结果过滤器）

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.AspNetCore.Mvc`（`ApiResult` / `ResultHandlerAttribute` / `IgnoreResultHandler` / `IResultFactory`） |

## 背景

前后端分离下，前端需要一个**稳定、可预测**的响应外壳（`code` / `message` / `data`）。若由业务代码自己包装，会出现三种典型失效：

1. 有的 Controller 包了有的没包，前端要写兼容逻辑；
2. 异常路径由中间件包，正常路径由 Controller 包，两处格式容易不一致；
3. 加一个字段（如 `operationTime`）要改遍所有 Controller。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 每个 Action 手写 `return Ok(ApiResult.Ok(x))`** | 完全显式 | 样板量大；必然有人漏；加字段要全局改 |
| **B. 中间件改写响应体** | 与 MVC 解耦 | 必须处理流式响应、压缩、状态码已提交等情况，**很脏且易出错** |
| **C. Action 过滤器（OnActionExecuted）包装** | 简单直观 | 拿不到 MVC 的格式化器上下文，处理 `IActionResult` 与文件内容类型时很别扭 |
| **D. 结果过滤器（Result Filter）**（本方案） | 工作在 MVC 管道结果阶段，能拿到完整上下文；异常路径由独立的异常管道产出同结构结果 | 仅适用于 MVC 管道，非 MVC 端点（如 Minimal API）不适用 |

## 决策

采用 **方案 D**：用全局 `ResultHandlerAttribute`（ASP.NET Core **结果过滤器**）把任意 Action 返回值包装成

```json
{ "code": "...", "message": "...", "data": { }, "operationTime": "..." }
```

配套设计：

- **`[IgnoreResultHandler]`**：单个 Action 的退出点（返回文件流、原始 `IActionResult` 时用）；
- **`IResultFactory`**：允许替换结果的形状；
- **`IRemoteStreamContent`** 等专用格式化器走独立通路，避免被外壳污染；
- 异常路径由 [ADR-0008](ADR-0008-exception-hierarchy.md) 的异常管道产出**相同结构**。

## 后果

**正面**

- 业务 Controller 完全无需关心包装，正常路径与异常路径格式统一；
- 加字段只需改一处（`IResultFactory` / `ApiResult`）；
- 返回结构对前端稳定，前端可以写死一层解包。

**负面**

- **"我的返回值为什么被包了一层？"**——隐式行为会让第一次接触的人困惑，尤其在 Swagger 里看到的类型与实际返回不一致。
- 需要返回非标准结构时（文件下载、第三方回调协议、SSE）必须记得加 `[IgnoreResultHandler]`，否则协议被破坏。
- 只覆盖 MVC 管道——如果项目用 Minimal API 为主，这套机制不生效。

**中性**

- 与 `[UnitOfWork]`（ADR-0005）同为「约定优于配置」的体现，取舍逻辑一致。

## 相关文档

- [架构文档 §10 读路径与异常管道](../架构文档.md)
- [ADR-0008 异常体系与统一错误](ADR-0008-exception-hierarchy.md)
- [使用文档 §7](../../getting-started/使用文档.md)
