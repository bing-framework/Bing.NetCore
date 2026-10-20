# 日志、追踪与性能诊断

## 功能说明

`Bing.Logging` 提供链式日志 API 与上下文，Serilog 包负责日志输出，Exceptionless Sink 负责外发；Web Enricher、SQL SkyAPM 诊断及 MiniProfiler 提供不同层次的可观测能力。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Logging/Bing/Logging/` | `ILog`、日志工厂、上下文与注册。 |
| `framework/src/Bing.Logging.Serilog/`、`framework/src/Bing.Logging.Sinks.Exceptionless/` | 日志实现、配置与 Sink。 |
| `framework/src/Bing.AspNetCore.Serilog/` | 请求中用户、租户和关联 ID 的日志丰富器。 |
| `framework/src/Bing.Extensions.SkyApm.Diagnostics.Sql/` | SQL 诊断订阅与 SkyAPM 适配。 |
| `framework/src/Bing.MiniProfiler/` | MiniProfiler 注册与诊断面板接入。 |

日志调用经 `ILog` 与工厂转交底层记录器；Enricher 读取请求上下文。SQL 追踪从诊断事件收集信息，不能把它视作数据库健康检查或所有代码路径的性能测量。

## 对外接口与用法

```csharp
using Bing.Logging;
using Serilog;

services.AddBingLogging();
services.AddLogging(builder => builder.AddSerilog());
```

宿主另行配置 `Log.Logger`。Web 上下文使用 `UseBingSerilogEnrichers`；Exceptionless 入口是 `WriteTo.Exceptionless`，SQL SkyAPM 入口在 `SkyApmExtensions` 上。完整用法见[日志使用说明](../operations/日志使用说明.md)。

## 依赖与配置项

依赖 Serilog、所选 Sink、SkyAPM 或 MiniProfiler。日志级别通过 `Logging:LogLevel` 与 `ConfigLogLevel` 对接；Sink 地址、输出路径、追踪服务及面板权限由宿主配置。认证之后才能读取已认证用户来丰富日志。

## 已知限制与注意事项

- 请求响应日志不自动保证脱敏；外发内容需遵守业务日志策略。
- 这些包不提供统一 HealthChecks 服务。
- SQL 诊断与 profiler 结果只代表被观测路径，不是整个系统性能基线。
- 外部追踪/日志服务可用性和生产面板配置：待实现/待确认。

更多说明见[日志与可观测性](../operations/日志与可观测性.md)。
