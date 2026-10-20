# 管理参考实现与示例宿主

## 功能说明

`modules/admin` 包含 EF Core 与 FreeSQL 两个 Web 宿主，以及共享的服务、领域、数据、事件与基础设施工程。`samples` 展示 Web API、Hangfire 和 WinForms 宿主接入，不等同于生产产品。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `modules/admin/src/Bing.Admin/`、`modules/admin/src/Bing.Admin.FreeSQL/` | Startup 与模块装配，两种 ORM 宿主。 |
| `modules/admin/src/Bing.Admin.Service/`、`modules/admin/src/Bing.Admin.Service.Shared/` | 业务应用服务、接口与 DTO。 |
| `modules/admin/src/Bing.Admin.Data*`、`modules/admin/src/Bing.Admin.*.Domain/` | 数据实现、仓储和领域模型。 |
| `modules/admin/src/Bing.Admin.EventHandlers/`、`modules/admin/src/Bing.Admin.Infrastructure/` | 事件处理与公共基础设施。 |
| `samples/Bing.Samples.WebApi/`、`samples/Bing.Samples.Hangfire/`、`samples/Bing.Samples.Winform/` | 三种宿主示例。 |

管理宿主经 `AddBing().AddModule<T>()` 装配服务和管道。当前缓存模块显式注入 `CSRedisClient` 与 `CacheConfiguration`；FreeSQL 宿主另外保留静态初始化，供 Redis 分布式锁使用。

## 对外接口与用法

```powershell
dotnet run --project samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
```

管理宿主构建/启动命令及十二个工程的逐项职责见[admin README](../../modules/admin/README.md)；示例接口与端口以各工程启动配置和[samples README](../../samples/README.md)为准。

## 依赖与配置项

Web/Admin 目标框架为 `net6.0`，WinForms 为 Windows 目标框架。管理宿主需要数据库、Redis，以及启用模块对应的 RabbitMQ、日志和追踪服务。缓存读取 `Caching:Namespace` 与 `ConnectionStrings:RedisConnection`，Redis 连接串不能叠加缓存隐藏前缀。

## 已知限制与注意事项

- Identity/权限、迁移与种子数据仍有被注释的注册路径；完整接通：待实现/待确认。
- 两个管理宿主使用不同 ORM，实现选择需保持一致，不能认为它们自动共用事务。
- 示例 WebApi 是启动骨架，不包含完整业务 Controller；WinForms 未纳入主解决方案。
- 示例外部服务地址与历史凭据需由运行者替换；生产就绪性：待实现/待确认。
