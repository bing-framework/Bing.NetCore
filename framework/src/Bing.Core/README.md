# Bing.Core
[![NuGet](https://img.shields.io/nuget/v/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 框架地基：模块系统、约定式依赖注入、程序集查找、异常体系与选项绑定。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：179
>
> **上游 Bing 包**：无（本包是叶子/基础包）
>
> **三方依赖**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

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
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

// ① 定义一个模块
[DependsOnModule(typeof(AspNetCoreModule))]
public class AppModule : BingModule
{
    public override ModuleLevel Level => ModuleLevel.Application;
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }
}

// ② 宿主起来（Web）
services.AddBing().AddModule<AppModule>();
app.UseBing();

// ③ 宿主起来（控制台 / WinForm）
services.AddBing().AddModule<AppModule>();
serviceProvider.UseBing();
```

依赖图入口适合新应用：

```csharp
services.AddBingApplication<AppModule>(options =>
{
    options.AutoRegisterServices = true;
    options.ServiceRegistrationMode = BingServiceRegistrationMode.Compatible;
});

using var provider = services.BuildBingServiceProvider();
provider.UseBing();
// 应用退出前：provider.ShutdownBing();
```

`AddBingApplication<TStartupModule>` 会把 `BingCoreModule`、`DependencyModule` 和启动模块加入依赖图，并按拓扑顺序配置模块。模块依赖始终先于依赖它的模块；没有依赖关系的就绪模块才按 `Level`、`Order` 和类型全名排序，`Level` 与 `Order` 只用于平局排序。`AdditionalModules` 和 `ExcludedModules` 可通过 `BingApplicationOptions` 配置。

新入口按全部模块依次执行 `PreConfigureServices`、`ConfigureServices`、`PostConfigureServices` 三轮服务配置。构建容器后，`UseBing` 或 `UseBingAsync` 再按全部模块执行 `OnPreApplicationInitialization`、`OnApplicationInitialization`、`OnPostApplicationInitialization` 三轮初始化；关闭时按模块执行序列的逆序关闭已开始的模块。模块可直接重写 `BingModule` 的这些方法，也可继续使用已有的 `AddServices`、`UseModule` 与可选生命周期接口。异步配置钩子需调用 `AddBingApplicationAsync<TStartupModule>`，异步初始化钩子需调用 `UseBingAsync`；同步入口会在执行阶段钩子前拒绝需要异步入口的模块。完整分派规则见[模块运行时迁移指南](../../../docs/migrations/modularity-runtime.md)。

`ServiceRegistrationMode` 默认 `Compatible`：约定 DI 扫描在 `DependencyModule` 的主配置阶段，选项类型注册和公开类型事件在 Post 之后。设置为 `BeforeConfigureServices` 后，这些注册在全部 Pre 完成、主配置开始前执行，且仍只触发一次。`AutoRegisterServices = false` 只关闭约定 DI 扫描。

启动期插件通过 `BingApplicationOptions.PluginSources` 添加。使用 `AddDirectory` 添加单个插件目录，或使用 `AddFolder` 发现父目录下的一级插件目录；每个目录必须包含 UTF-8 的 `bing-plugin.json`。插件依赖采用 `System.Version` 精确匹配，并在模块构造前完成路径、程序集和类型校验。详见 [启动期可信插件](../../../docs/guides/modularity-plugins.md)。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddBing(Action<BingOptions>)` | `Microsoft.Extensions.DependencyInjection` | 框架总入口，返回 `IBingBuilder` |
| `IBingBuilder.AddModule<T>()` / `AddModules(params Type[])` | `Bing.Core.Builders` | 添加模块并递归拉入依赖，立即执行 `AddServices` |
| `AddBingApplication<TStartupModule>(Action<BingApplicationOptions>)` | `Microsoft.Extensions.DependencyInjection` | 按完整依赖图注册模块；同一服务集合不可与 `AddBing` 混用 |
| `AddBingApplicationAsync<TStartupModule>(Action<BingApplicationOptions>, CancellationToken)` | `Microsoft.Extensions.DependencyInjection` | 异步执行三轮服务配置；返回原服务集合 |
| `BuildBingServiceProvider(ServiceProviderOptions = null)` | `Microsoft.Extensions.DependencyInjection` | 构建根容器并预解析模块管理器，使容器释放覆盖尚未启动的模块 |
| `IBingModuleLoader` | `Bing.Core.Modularity` | 可替换的模块加载器；必须在构建容器前以实例注册 |
| `BingApplicationOptions.PluginSources` | `Bing.Core.Modularity` | 启动期可信插件来源集合 |
| `IBingPluginSource` / `BingPluginSourceList` | `Bing.Core.Modularity` | 提供插件目录或按父目录发现插件 |
| `IBingPluginContainer` / `BingPluginDescriptor` | `Bing.Core.Modularity` | 查看当前宿主已加载的插件描述 |
| `UseBing(this IServiceProvider)` | `Microsoft.Extensions.DependencyInjection` | 非 Web 场景：遍历模块执行 `UseModule` |
| `UseBingAsync` / `ShutdownBingAsync` | `Microsoft.Extensions.DependencyInjection` | 异步初始化和异步关闭 |
| `EnableAop(Action<IAspectConfiguration>)` | `Bing.DependencyInjection` | 启用 AOP（注意入口叫 `EnableAop`，没有 `AddAop`） |
| `AddLocalLock()` | `Bing.Locks` | 进程内锁 `ILock` → `LocalLock` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IBingModule` / `BingModule` | `Bing.Core.Modularity` | 模块契约与基类 |
| `ModuleLevel` | `Bing.Core.Modularity` | Core=1 / Framework=10 / Application=20 / Business=30 |
| `ISingletonDependency` 等三个标记接口 | `Bing.DependencyInjection` | 约定式 DI 生命周期 |
| `[Dependency]` / `[IgnoreDependency]` | `Bing.DependencyInjection` | 注册行为精细控制 |
| `Warning` / `BusinessException` | `Bing.Exceptions` / `Bing` | 异常基类 |

## 与其它包的关系

**依赖本包**：`Bing.Aop.AspectCore`、`Bing.AspNetCore.Abstractions`、`Bing.Biz.OAuthLogin`、`Bing.Caching`、`Bing.Data`、`Bing.ExceptionHandling`、`Bing.Localization.Abstractions`、`Bing.Logging`、`Bing.MultiTenancy.Abstractions`、`Bing.Security`、`Bing.TextTemplating`、`Bing.Uow`

**三方 NuGet**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

## 注意事项

`AddBingApplication` 的模块必须具有公共无参构造函数。自定义加载器应在服务集合构建前注册实例，例如 `services.AddSingleton<IBingModuleLoader>(new MyModuleLoader())`；框架会校验它返回的完整依赖图、执行顺序和模块集合。注册配置尚未结束时不能再次注册应用或提前构建 Bing 容器；注册失败后原服务集合不能重试。旧 `AddBing().AddModule<T>()` 仍立即执行 `AddServices`，使用新服务配置重写的模块需要迁移到应用入口。

自动 DI 只扫描本次选中的模块所属程序集；这是扫描边界，不是安全隔离。设置 `AutoRegisterServices = false` 可关闭约定式 DI 扫描。模块构造函数和 `AddServices` 可以创建仅支持 `IAsyncDisposable` 的资源，但同步注册失败路径会同步等待其释放；正常运行和关闭应使用 `UseBingAsync`、`ShutdownBingAsync` 及 `await using`。模块自身拥有的资源由框架释放，DI 容器中的服务仍由容器负责释放，模块不得代替容器释放这些服务。

需要扫描附加程序集时，模块可声明 `[BingModuleAssembly(typeof(SharedMarker))]`，应用可设置 `options.ServiceScanning.AdditionalAssemblies`。`ConventionalTypeFilter` 只筛选约定 DI 候选，不影响选项绑定、公开类型事件或手工注册。附加程序集不会自动成为模块，同一程序集只扫描一次。生命周期上下文可通过 `context.GetConfiguration()`、`context.GetHostEnvironment()` 读取当前作用域服务；注册期配置继续使用 `services.GetConfiguration()`，通用环境使用 `services.GetHostEnvironment()`。注册期只读取显式单例实例，不执行服务工厂。

需要扫描附加程序集时，模块可声明 `[BingModuleAssembly(typeof(SharedMarker))]`，应用可设置 `options.ServiceScanning.AdditionalAssemblies`。`ConventionalTypeFilter` 只筛选约定 DI 候选，不影响选项绑定、公开类型事件或手工注册。附加程序集不会自动成为模块，同一程序集只扫描一次。生命周期上下文可通过 `context.GetConfiguration()`、`context.GetHostEnvironment()` 读取当前作用域服务；注册期配置继续使用 `services.GetConfiguration()`，通用环境使用 `services.GetHostEnvironment()`。注册期只读取显式单例实例，不执行服务工厂。

此入口的插件是启动期受信代码，程序集加载到默认上下文后会保留到进程结束。`Bing.PluginRuntime` 另提供基于可回收加载上下文的整组插件切换；使用方式和回收约束见[插件指南](../../../docs/guides/modularity-plugins.md)。两种入口都不提供不可信插件沙箱。

配置完成后必须调用 `UseBing` 或 `UseBingAsync` 激活模块；退出前应调用 `ShutdownBing` 或 `ShutdownBingAsync`。Generic Host 会通过注册的 `IHostedService` 在停止时自动执行异步关闭。非 Host 场景建议使用 `BuildBingServiceProvider`，它会预解析模块管理器，使 `using`/`await using` 能覆盖尚未初始化的模块；直接调用原生 `BuildServiceProvider` 且从未解析模块管理器时，DI 的惰性创建仍不保证模块自动释放。

旧入口保留单宿主兼容用的静态 `ServiceLocator`/配置定位器；新入口不绑定这些静态兼容定位器，因此不应在同一服务集合或多个宿主之间混用旧入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- [启动期可信插件](../../../docs/guides/modularity-plugins.md) —— 插件目录、清单和失败诊断
- NuGet 包页面：https://www.nuget.org/packages/Bing.Core

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
