# Bing.Core
[![NuGet](https://img.shields.io/nuget/v/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 框架地基：模块系统、约定式依赖注入、程序集查找、异常体系与选项绑定。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：179
>
> **上游 Bing 包**：无（本包是叶子/基础包）
>
> **三方依赖**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`NuGet.Versioning`、`Microsoft.Bcl.AsyncInterfaces`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

---

## 这是什么

`Bing.Core` 是整个 Bing.NetCore 框架的**根**，所有其它包最终都依赖它。它提供四件东西：

1. **模块系统** —— `IBingModule` / `BingModule` / `ModuleLevel` / `[DependsOnModule]`，支持兼容入口 `AddBing().AddModule<T>()`、依赖图入口 `AddBingApplication<T>()` 及启动期可信插件。
2. **约定式 DI** —— 实现 `ISingletonDependency` / `IScopedDependency` / `ITransientDependency` 即自动注册；`[Dependency(...)]` 可精细控制，`[IgnoreDependency]` 排除。
3. **异常体系** —— `Warning` / `BusinessException` / `ConcurrencyException` / `BingException`，统一携带错误码与 HTTP 状态码。
4. **基础设施** —— 程序集与类型查找器、选项批量绑定 `AddOptionsType`、`ILog`/`ILock` 等公共契约的默认实现。

## 安装

```bash
dotnet add package Bing.Core
```

## 快速上手

```csharp
using System;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

// .NET 6/8 控制台应用；顶级语句放在类型定义之前。
var services = new ServiceCollection();
await services.AddBingApplicationAsync<AppModule>(options =>
{
    options.AutoRegisterServices = false;
});

await using var provider = services.BuildBingServiceProvider();
await provider.UseBingAsync();
try
{
    Console.WriteLine(provider.GetRequiredService<Greeting>().Text);
}
finally
{
    await provider.ShutdownBingAsync();
}

public sealed class AppModule : BingModule
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton(new Greeting("Bing 已启动"));
}

public sealed record Greeting(string Text);
```

新应用建议使用应用入口；全部钩子为同步时可选 `AddBingApplication`、`UseBing` 和 `ShutdownBing`。维护旧应用时保留 `AddBing().AddModule<T>()`。同一个服务集合不能混用新旧入口；新应用入口只允许注册一次，旧构建器仍可在构建容器前追加模块。Web 应用使用对应 ASP.NET Core 包的 `app.UseBing()` / `await app.UseBingAsync()`，在运行 Web 宿主前装配管道，不要另建第二个根容器。

`AddBingApplication<TStartupModule>` 会把 `BingCoreModule`、`DependencyModule` 和启动模块加入依赖图，并按拓扑顺序配置模块。模块依赖始终先于依赖它的模块；没有依赖关系的就绪模块才按 `Level`、`Order` 和类型全名排序，`Level` 与 `Order` 只用于平局排序。`AdditionalModules` 和 `ExcludedModules` 可通过 `BingApplicationOptions` 配置。

新入口按全部模块依次执行 `PreConfigureServices`、`ConfigureServices`、`PostConfigureServices` 三轮服务配置。构建容器后，`UseBing` 或 `UseBingAsync` 再按全部模块执行 `OnPreApplicationInitialization`、`OnApplicationInitialization`、`OnPostApplicationInitialization` 三轮初始化；关闭时按模块执行序列的逆序关闭已开始的模块。`IPreConfigureServices`、`IPostConfigureServices`、`IOnPreApplicationInitialization`、`IOnPostApplicationInitialization` 和 `IBingModuleShutdown` 各自同时声明同步与异步方法。`BingModule` 提供默认实现，模块可重写虚方法，也可显式实现统一接口方法。使用异步钩子时分别调用 `AddBingApplicationAsync<TStartupModule>`、`UseBingAsync` 或 `ShutdownBingAsync`；同步入口会在任何阶段回调前拒绝实际需要异步执行的模块。`IBingAsyncModuleInitializer`、`IBingPreConfigureServices`、`IBingPostConfigureServices`、`AddServices` 和 `UseModule` 继续兼容。完整分派规则及迁移示例见[模块运行时迁移指南](../../../docs/migrations/modularity-runtime.md)。

`ServiceRegistrationMode` 默认 `Compatible`：约定 DI 扫描在 `DependencyModule` 的主配置阶段，选项类型注册和公开类型事件在 Post 之后。设置为 `BeforeConfigureServices` 后，这些注册在全部 Pre 完成、主配置开始前执行，且仍只触发一次。`AutoRegisterServices = false` 只关闭约定 DI 扫描。

启动期插件通过 `BingApplicationOptions.PluginSources` 添加。使用 `AddDirectory` 添加单个插件目录，或使用 `AddFolder` 发现父目录下的一级插件目录；每个目录必须包含 UTF-8 的 `bing-plugin.json`。插件依赖支持精确版本和版本区间，并在模块构造前完成路径、程序集和类型校验；版本区间只校验已部署版本，不自动下载或求解多个版本。详见 [可信插件指南](../../../docs/guides/modularity-plugins.md)。

插件来源、描述符与加载实现集中在源码的 `Modularity/PlugIns` 目录；热更新宿主位于 `Bing.PluginRuntime` 的对应目录。公开类型仍使用 `Bing.Core.Modularity` 命名空间，已有引用无需迁移。

## 异步生命周期方法

前后服务配置、前后初始化和关闭阶段分别使用 `IPreConfigureServices`、`IPostConfigureServices`、`IOnPreApplicationInitialization`、`IOnPostApplicationInitialization`、`IBingModuleShutdown`。每个接口都同时提供同步方法与返回 `Task`、接收 `CancellationToken` 的异步方法。`BingModule` 提供默认实现；直接实现接口的类型必须实现两种方法。

同一阶段只分派一次，虚方法重写优先于接口实现；前后配置最后仍可回退旧 `IBingPreConfigureServices` / `IBingPostConfigureServices` 接口。旧异步接口已合并删除，外部模块需迁移并重新编译。示例见[模块运行时迁移指南](../../../docs/migrations/modularity-runtime.md)。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddBing(Action<BingOptions>)` | `Microsoft.Extensions.DependencyInjection` | 框架总入口，返回 `IBingBuilder` |
| `IBingBuilder.AddModule<T>()` / `AddModules(params Type[])` | `Bing.Core.Builders` | 添加模块并递归拉入依赖，立即执行 `AddServices` |
| `AddBingApplication<TStartupModule>(Action<BingApplicationOptions>)` | `Microsoft.Extensions.DependencyInjection` | 按完整依赖图注册模块；同一服务集合不可与 `AddBing` 混用 |
| `AddBingApplicationAsync<TStartupModule>(Action<BingApplicationOptions>, CancellationToken)` | `Microsoft.Extensions.DependencyInjection` | 异步执行三轮服务配置；返回原服务集合 |
| `BuildBingServiceProvider(ServiceProviderOptions = null)` | `Microsoft.Extensions.DependencyInjection` | 构建根容器并预解析模块管理器，使容器释放覆盖尚未启动的模块 |
| `IBingModuleLoader` | `Bing.Core.Modularity` | 可替换的模块加载器；必须在调用应用注册入口前以实例注册 |
| `BingApplicationOptions.PluginSources` | `Bing.Core.Modularity` | 启动期可信插件来源集合 |
| `IBingPluginSource` / `BingPluginSourceList` | `Bing.Core.Modularity` | 提供插件目录或按父目录发现插件 |
| `IBingPluginContainer` / `BingPluginDescriptor` | `Bing.Core.Modularity` | 查看当前宿主已加载的插件描述 |
| `UseBing(this IServiceProvider)` | `Microsoft.Extensions.DependencyInjection` | 非 Web 场景：执行三轮初始化，兼容桥接 `UseModule` |
| `UseBingAsync` / `ShutdownBingAsync` | `Microsoft.Extensions.DependencyInjection` | 异步初始化和异步关闭 |
| `EnableBingHostedInitialization` | `Microsoft.Extensions.DependencyInjection` | Generic Host 的 Consumer/Task 启动初始化；停止时复用统一异步关闭 |
| `EnableAop(Action<IAspectConfiguration>)` | `Bing.DependencyInjection` | 启用 AOP（注意入口叫 `EnableAop`，没有 `AddAop`） |
| `AutowiredAttribute` | `Bing.Aspects` | 容器无关的属性注入标记；AspectCore 执行器由 `EnableAutowired()` 启用 |
| `AddLocalLock()` | `Bing.Locks` | 进程内锁 `ILock` → `LocalLock` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IBingModule` / `BingModule` | `Bing.Core.Modularity` | 模块契约与基类 |
| `IPreConfigureServices` / `IPostConfigureServices`、`IOnPreApplicationInitialization` / `IOnPostApplicationInitialization` | `Bing.Core.Modularity` | 同时提供同步与异步方法的前后配置和初始化接口 |
| `IBingModuleShutdown` | `Bing.Core.Modularity` | 同时提供同步与异步方法的模块关闭接口 |
| `ModuleLevel` | `Bing.Core.Modularity` | Core=1 / Framework=10 / Application=20 / Business=30 |
| `ISingletonDependency` 等三个标记接口 | `Bing.DependencyInjection` | 约定式 DI 生命周期 |
| `[Dependency]` / `[IgnoreDependency]` | `Bing.DependencyInjection` | 注册行为精细控制 |
| `[Autowired]` | `Bing.Aspects` | 标记由容器适配器设置的公开可写属性；不注入字段、索引器或非公开 setter |
| `Warning` / `BusinessException` | `Bing.Exceptions` / `Bing` | 异常基类 |

## 与其它包的关系

**依赖本包**：`Bing.Aop.AspectCore`、`Bing.AspNetCore.Abstractions`、`Bing.Biz.OAuthLogin`、`Bing.Caching`、`Bing.Data`、`Bing.ExceptionHandling`、`Bing.Localization.Abstractions`、`Bing.Logging`、`Bing.MultiTenancy.Abstractions`、`Bing.Security`、`Bing.TextTemplating`、`Bing.Uow`

**三方 NuGet**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`NuGet.Versioning`、`Microsoft.Bcl.AsyncInterfaces`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

## 注意事项

模块关闭兼容真实 Web 宿主的只读服务集合；插件静态引用的已部署私有 DLL 在临时解析期间预加载，无需额外加入服务扫描。部署与支持范围见[加载与关闭边界](../../../docs/guides/modularity-plugins.md#加载与关闭边界)。

`AddBingApplication` 的模块必须具有公共无参构造函数。自定义加载器应在调用应用注册入口前注册实例，例如 `services.AddSingleton<IBingModuleLoader>(new MyModuleLoader())`；框架会校验它返回的完整依赖图、执行顺序和模块集合。注册配置尚未结束时不能再次注册应用或提前构建 Bing 容器；注册失败后原服务集合不能重试。旧 `AddBing().AddModule<T>()` 仍立即执行 `AddServices`，使用新服务配置重写的模块需要迁移到应用入口。

自动 DI 只扫描本次选中的模块所属程序集；这是扫描边界，不是安全隔离。设置 `AutoRegisterServices = false` 可关闭约定式 DI 扫描。模块构造函数和 `AddServices` 可以创建仅支持 `IAsyncDisposable` 的资源，但同步注册失败路径会同步等待其释放；正常运行和关闭应使用 `UseBingAsync`、`ShutdownBingAsync` 及 `await using`。模块自身拥有的资源由框架释放，DI 容器中的服务仍由容器负责释放，模块不得代替容器释放这些服务。

需要扫描附加程序集时，模块可声明 `[BingModuleAssembly(typeof(SharedMarker))]`，应用可调用 `options.ServiceScanning.AdditionalAssemblies.Add(typeof(SharedMarker).Assembly)`。`ConventionalTypeFilter` 只筛选约定 DI 候选，不影响选项绑定、公开类型事件或手工注册。附加程序集不会自动成为模块，同一程序集只扫描一次；自动 DI 的最终范围包含模块程序集及显式附加程序集。

生命周期上下文可通过 `context.GetConfiguration()`、`context.GetHostEnvironment()` 读取当前作用域服务；注册期对应使用 `services.GetConfiguration()`、`services.GetHostEnvironment()`。注册期优先读取最后一个直接注册的单例实例，没有直接注册时从 `HostBuilderContext` 实例读取；不会执行服务工厂，也不会临时构建容器。直接注册若为工厂或类型，必需访问方法会报错，`OrNull` 方法返回 `null`。独立控制台应用可在注册模块前调用 `services.AddSingleton<IConfiguration>(configuration)`；只有确实需要环境时才注册 `IHostEnvironment` 实例。初始化的 Pre、主阶段与 Post 共用短期作用域，不应把其中解析的 scoped 服务保存在模块字段中。

此入口的插件是启动期受信代码，程序集加载到默认上下文后会保留到进程结束。`Bing.PluginRuntime` 另提供基于可回收加载上下文的整组插件切换；使用方式和回收约束见[插件指南](../../../docs/guides/modularity-plugins.md)。两种入口都不提供不可信插件沙箱。

配置完成后必须调用 `UseBing` 或 `UseBingAsync` 激活模块；退出前应调用 `ShutdownBing` 或 `ShutdownBingAsync`。Generic Host 会通过注册的 `IHostedService` 在停止时自动执行异步关闭。没有 Web 管道的 Consumer 或 Task 宿主可在模块注册完成后调用 `EnableBingHostedInitialization()`，由同一个 `IHostedService` 在 Host 启动阶段执行 `InitializeAsync`，停止阶段继续复用 `ShutdownAsync`；Web 宿主不要同时调用该入口和 `UseBing`。该入口只负责生命周期时机，不会移除 Kestrel、创建独立 Worker 进程或完成预热、排空和部署切流。非 Host 场景建议使用 `BuildBingServiceProvider`，它会预解析模块管理器，使 `using`/`await using` 能覆盖尚未初始化的模块；直接调用原生 `BuildServiceProvider` 且从未解析模块管理器时，DI 的惰性创建仍不保证模块自动释放。

旧入口保留单宿主兼容用的静态 `ServiceLocator`/配置定位器；新入口不绑定这些静态兼容定位器，因此不应在同一服务集合或多个宿主之间混用旧入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- [启动期可信插件](../../../docs/guides/modularity-plugins.md) —— 插件目录、清单和失败诊断
- [模块 DI 与资源所有权](../../../docs/guides/modularity-di-resources.md) —— 循环依赖、属性注入、释放与取消边界
- NuGet 包页面：https://www.nuget.org/packages/Bing.Core

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
