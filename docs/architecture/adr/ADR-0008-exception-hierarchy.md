# ADR-0008：异常体系与统一错误

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Core`（`Warning` / `BusinessException` / `UserFriendlyException` / `IHasErrorCode` / `IHasHttpStatusCode`）、`Bing.ExceptionHandling`、`Bing.AspNetCore` |

## 背景

业务层需要表达「这个操作失败了，原因是余额不足」，也希望它能自动变成 HTTP 400 + 业务码，而不是 500 + 堆栈。同时：**生产环境绝不能把堆栈、SQL、内部消息返回给调用方**。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 抛通用 `Exception`，中间件 switch 类型映射** | 简单，无需自定义类型 | 无法携带结构化业务码；捕获粒度粗；所有异常都是 500 再靠字符串猜 |
| **B. 返回 Result 对象（`Result.Fail(...)`）** | 显式、类型安全 | 侵入每个方法签名；无法跨 await/事件边界；与 .NET 异常生态割裂 |
| **C. 自定义异常层次 + 转换器**（本方案） | 语义清晰；可携带业务码与 HTTP 状态码；与 .NET 异常机制一致；一处集中决定"暴露什么" | 需要记忆若干异常类型；中间件与过滤器两条输出路径并存 |

## 决策

采用 **方案 C**：

- `Bing.Core` 定义 **`Warning`** / **`BusinessException`** / **`UserFriendlyException`**，并实现 `IHasErrorCode`、`IHasHttpStatusCode` 等标记接口；
- `Bing.ExceptionHandling` 的 `DefaultExceptionToErrorInfoConverter` 识别异常类型 → 生成错误码与提示；
- Web 层用**中间件 + `BingExceptionFilter`** 输出统一结构 `{ code, message }`；
- `BingExceptionHandlingOptions` 控制**是否向调用方暴露堆栈**（生产默认不暴露）；
- 正常路径与异常路径输出**同一形状**（与 ADR-0004 的 `ApiResult` 对齐）。

```csharp
throw new BusinessException("1001", "余额不足");   // 即获得统一错误响应
```

## 后果

**正面**

- 业务层用一行 throw 就能得到正确的状态码与统一响应结构；
- 「哪些信息能外泄」集中在一处控制，而不是散落各处；
- 与 ADR-0004 配合，前后端只要解一层壳。

**负面**

- 需要记忆三种异常的语义差异（何时抛 `Warning` vs `BusinessException` vs `UserFriendlyException`）；
- **两条输出路径并存**（中间件 + 过滤器），排查时要确认是哪一个在生效；
- 生产环境默认不暴露堆栈 ⇒ **排错完全依赖日志**。若日志级别没配好或日志没落盘，线上问题就无从下手（见 [安全指南](../../operations/安全指南.md) 关于日志的建议）。

**中性**

- 统一响应结构的 **响应头** `_BingErrorFormat`（注意下划线前缀）可供调用方识别不同返回格式。

## 相关文档

- [架构文档 §10 异常与统一响应管道](../架构文档.md)
- [ADR-0004 统一 API 响应](ADR-0004-unified-api-response.md)
- [最佳实践 §8.1 / §8.2](../../guides/最佳实践.md)
