# AspectCore 3.0.0 迁移说明

本文记录 Bing.NetCore 从 AspectCore 2.x 升级到正式版 `3.0.0` 的实际影响。升级以正式 NuGet 包为准，不依赖上游仍标记为预发布的旧文档。

## 支持范围

`Bing.Aop.AspectCore` 及其直接依赖 AspectCore 的应用层工程现在以 `net6.0` 为目标框架，可被 `net6.0`、`net8.0` 宿主引用。`Bing.Core`、`Bing.Validation.Abstractions`、`Bing.Validation`、`ILazyServiceProvider` 和中立的 `AutowiredAttribute` 仍保持 `netstandard2.0`，不需要为了 AspectCore 升级而迁移。

因此，原来只能消费 `netstandard2.0` 的旧应用不能继续引用升级后的 `Bing.Aop.AspectCore` 及其受影响下游包。此限制是目标框架变化，不是属性注入 API 的变化；这类应用应继续使用兼容的旧 Bing 包版本，或整体迁移到 `net6.0` 及更高版本。

## 依赖和源码改动

所有生产工程、测试工程、循环依赖探针、Admin 宿主以及 WebApi/Hangfire 示例中的 AspectCore 直接引用统一为 `3.0.0`。应用通常仍使用原有入口：

```csharp
services.EnableAop();
hostBuilder.UseServiceContext();
```

只需要属性注入时仍可使用 `services.EnableAutowired()`。`AutowiredAttribute` 属于 `Bing.Core` 的中立契约，AspectCore 项目只提供执行器；现有源码无需改写。依赖旧 AspectCore 继承关系的外部二进制插件仍应重新编译，类型转发只覆盖已确认的旧限定名解析场景。

升级没有切换 DynamicProxy、DI 容器或 Source Generator，也没有接管 `ILazyServiceProvider` 的生命周期。属性注入仍使用当前作用域的服务提供程序，模块和工作单元不应把作用域服务保存到单例或静态字段。

## 中立特性迁移

以下类型现在只表达业务意图，不再继承 AspectCore 类型：

| 标记 | 契约程序集 | 执行器 |
| --- | --- | --- |
| `AutowiredAttribute`、`IgnoreAttribute` | `Bing.Core` | `Bing.Aop.AspectCore` |
| `NotNullAttribute`、`NotEmptyAttribute`、`ValidAttribute` | `Bing.Validation.Abstractions` | `Bing.Aop.AspectCore` |
| `UnitOfWorkAttribute`、`BingUnitOfWorkScope` | `Bing.Ddd.Application.Contracts` | `Bing.Ddd.Application.AspectCore` |

普通标记的源码用法和旧限定名解析保持兼容。依赖旧 `ParameterInterceptorBase`、`InterceptorBase`、AspectCore 接口或旧 `Scope` 枚举的外部代码需要迁移并重新编译；类型转发只覆盖特性的限定名，不承诺这些继承关系的二进制兼容。

参数校验默认关闭。需要执行 `NotNull`、`NotEmpty` 或 `Valid` 时使用：

```csharp
services.EnableAop(options => options.EnableBingParameterValidation());
```

工作单元执行器需要额外引用 `Bing.Ddd.Application.AspectCore` 并调用 `services.EnableBingUnitOfWork()`。

## 工作单元验证

Admin EFCore 和 FreeSQL 测试通过 `UseServiceContext()` 创建 AspectCore 容器，并验证继承的 `LazyServiceProvider` 能取得当前作用域的事务管理器和 CAP 发布器。新增的 SQLite 持久化夹具还覆盖成功写入、跨作用域回读和失败不持久化；当前环境中 EFCore 与 FreeSQL 测试均为 6/6 通过，两个作用域解析出的服务相互隔离。

SQLite 本地测试覆盖了框架现有 EFCore、FreeSQL 行为。MySQL/CAP 的外部事务联动需要受控测试环境；没有对应环境时不能用 SQLite 或 Mock 推断分布式事务已经验证。

Admin 的 EFCore 与 FreeSQL 均提供 `AdminMySqlCapIntegrationTest`。在专用 MySQL 测试库和 RabbitMQ 可用、依赖已还原后，使用 PowerShell 7 执行 `eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1`。脚本要求两项目各 3 项真实测试通过且无跳过；`--no-restore` 要求事先完成依赖还原。测试检查成功提交、业务失败及执行中取消时的业务记录和 CAP 出箱持久化，消费者收到消息仅作为投递证据。CAP 出箱并不保证消费者恰好执行一次，业务订阅仍需幂等处理。

## 资源边界

AspectCore 3.0 不会自动修复资源所有权。容器通常负责 Singleton/Scoped `IDisposable` 资源，瞬时资源、异步释放以及释放异常仍需遵循 Bing 模块的关闭约定。正常宿主退出应等待 `StopAsync`；模块或应用若使用异步关闭，应调用 `ShutdownBingAsync` 并等待完成。

## 验证记录

本次升级在仓库锁定 SDK `8.0.424` 不可用的环境中使用实际 `8.0.425` 验证。已通过：

- `Bing.Core.Tests`：`net8.0`/`net6.0` 各 612 项；
- `Bing.Aop.AspectCore.Tests`：`net8.0`/`net6.0` 各 102 项；
- WebApi Sample 自动冒烟：`/sample/lazy` 返回 200，实际 Lazy 业务值为 `lazy-service-ready`，宿主 `StopAsync` 完成后监听端口已释放；
- NuGet 最小消费者脚本：`netstandard2.0` 契约消费者编译、`net6.0` AOP 消费者编译及 `AutowiredAttribute` 类型转发运行验证通过，脚本位于 `ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/verify-nuget-consumer.ps1`。

Admin EFCore/FreeSQL 新增的真实 SQLite 写入、回读和失败不持久化测试均已通过（各 6/6）。`Bing.AspNetCore.Mvc.Tests`、完整 Admin 构建及 MySQL/CAP 外部事务仍需在依赖齐全的环境中顺序验证。

`Bing.Utils` 1.5.0 不在 Bing 源码工程中直接引用 AspectCore，但已安装的正式 `bing.utils.1.5.0.nupkg` 的 nuspec 为所有目标框架声明了 `AspectCore.Extensions.Reflection (>= 2.4.0)` 传递依赖。NuGet 还原中 AOP 3.0.0 图会将该下限统一到 3.0.0；若要求契约包完全没有任何 AspectCore 传递依赖，需要另行发布不含该依赖的 Bing.Utils 版本或拆分反射能力，不能通过本仓库的中立特性迁移单独消除。

Admin 构建和 WebApi/Hangfire 构建中的 `net6.0` 支持警告来自当前传递依赖或 SDK，不代表 AspectCore 3.0 API 编译失败。MySQL/CAP 外部基础设施和锁定 SDK `8.0.424` 仍是未验证项。
