# 模块运行时迁移指南

本文说明从旧的立即注册入口迁移到新的依赖图入口时，注册、初始化和关闭行为的变化。

2026-09-28 修复了真实 Web 宿主只读集合清理，以及已部署插件私有 DLL 的方法体延迟加载。现有调用方式无需调整；辅助 DLL 不必为了加载而加入服务扫描。具体支持范围见[加载与关闭边界](../guides/modularity-plugins.md#加载与关闭边界)。

## 入口对照

旧入口保留用于兼容已有应用：

```csharp
services.AddBing().AddModule<AppModule>();
app.UseBing();
```

`AddModule<T>` 会递归纳入依赖，并在调用处立即执行每个新增模块的 `AddServices`。`AddModules(params Type[])` 扫描可发现的模块类型后再加入它们。新入口按显式启动模块建立完整依赖图：

```csharp
services.AddBingApplication<AppModule>(options =>
{
    options.AutoRegisterServices = true;
    options.AdditionalModules.Add(typeof(ExtraModule));
    options.ExcludedModules.Add(typeof(OptionalModule));
});
```

新入口默认根模块包含 `BingCoreModule`、`DependencyModule` 和 `AppModule`。同一个 `IServiceCollection` 不能重复注册，也不能混用 `AddBing` 与 `AddBingApplication`。

异步服务配置使用 `AddBingApplicationAsync`，该方法返回原服务集合。注册一开始便占用集合，配置回调和模块配置期间不能重入注册或提前调用 `BuildBingServiceProvider`；失败后也不能在同一集合重试。

```csharp
await services.AddBingApplicationAsync<AppModule>(options =>
{
    options.ServiceRegistrationMode = BingServiceRegistrationMode.BeforeConfigureServices;
}, cancellationToken);
```

新入口还支持启动期可信插件：

```csharp
services.AddBingApplication<AppModule>(options =>
{
    options.PluginSources.AddFolder(Path.Combine(AppContext.BaseDirectory, "modules"));
});
```

插件目录必须包含 `bing-plugin.json`，入口程序集和启动模块由清单明确声明。插件依赖可使用三段或四段 `System.Version` 精确匹配，也可使用 NuGet 版本区间；清单、路径、程序集身份和模块类型全部通过预检后，启动模块才会进入模块图。此入口的插件程序集进入默认加载上下文，不支持卸载。需要整组插件在运行中切换时，使用独立的 `Bing.PluginRuntime` 和 `BingHotPluginHost<TStartupModule>`；两种入口都只接受受信代码。

依赖拓扑排序只适用于 `AddBingApplication<TStartupModule>`。旧入口保留历史注册时机：`AddModule<T>` 调用时立即执行 `AddServices`，运行时继续使用原有的 `Level`、`Order` 和兼容入口的注册行为，不会改用新入口的依赖图调度规则。

## 依赖与排序

在新入口中，`[DependsOnModule]` 表示必须先配置和启动的模块，依赖拓扑优先于排序属性。没有依赖关系的就绪模块才按 `Level`、`Order`、类型全名排序；`Level` 和 `Order` 只在同一轮的可选模块之间解决平局。

自定义加载器必须在构建容器前注册实例：

```csharp
services.AddSingleton<IBingModuleLoader>(new MyModuleLoader());
services.AddBingApplication<AppModule>();
```

加载器返回的模块目录会被校验，必须包含完整依赖集合、正确的执行序号和所有必需模块。

依赖图错误会提供从根模块到失败节点的完整路径，包括进入循环之前的前缀。诊断程序可读取异常的 `Data["Bing.ModuleDependencyPath"]`（`Type[]`）；依赖提供程序抛出的原始异常也保留，并附加该路径。旧入口的依赖校验失败同样使构建器失效，不能捕获后继续向原集合注册模块。

## 服务扫描边界

新入口的约定式 DI 默认扫描本次选中模块所属的程序集，也支持模块声明和应用选项指定的附加程序集。该边界用于控制扫描范围，不提供安全隔离；需要关闭扫描时设置 `AutoRegisterServices = false`。模块可以持有仅支持 `IAsyncDisposable` 的资源；同步注册失败时框架会同步等待异步释放，正常运行和关闭应使用异步入口。

`ServiceRegistrationMode` 提供两种扫描时机：

| 模式 | 约定 DI 扫描 | 选项类型注册、公开类型事件 |
| --- | --- | --- |
| `Compatible`（默认） | `DependencyModule` 的主配置阶段 | 全部 Post 配置完成后 |
| `BeforeConfigureServices` | 全部 Pre 配置完成后、主配置开始前 | 与约定 DI 扫描同一时机 |

两种模式使用相同的冻结扫描集合：最终选中模块的程序集、模块声明的附加程序集及应用额外程序集；公开类型事件每类型每轮注册只触发一次。`AutoRegisterServices = false` 仅关闭约定 DI 自动注册，不改变选项绑定和公开事件；配置阶段较晚加入的选项类型仍由既有补扫机制处理。`BeforeConfigureServices` 适合主配置钩子需要读取约定注册结果的模块；已有应用保持默认值即可维持原顺序。

新入口的短期初始化作用域只在模块初始化或关闭回调期间有效。模块和 Web 中间件不得长期保存该作用域、`BingModuleInitializationContext.ServiceProvider` 或由它解析的短期服务；需要长期工作的对象应通过合适的 DI 生命周期或独立资源管理。

`ModuleDependencyGraph` 允许只实现 `IAsyncDisposable` 的模块。此类模块必须使用 `UseBingAsync`、`ShutdownBingAsync` 和 `await using`；同步注册或构造失败路径会同步等待 `DisposeAsync`，以确保原始异常返回前不遗留模块资源。主初始化仍可实现 `IBingAsyncModuleInitializer.InitializeAsync`；前后阶段和关闭钩子使用下文的统一接口。

模块实例由框架拥有并释放；通过 DI 注册的服务仍由 DI 容器拥有和释放，模块不要手动释放从容器解析的服务。

插件描述符可通过 `IBingPluginContainer` 读取。插件来源、描述符和临时程序集解析回调按当前服务集合隔离；解析回调在加载完成或异常时退订。注册失败会释放已经创建的模块，但无法撤回已经进入默认加载上下文的插件程序集。

插件依赖原有的普通版本字符串继续表示精确匹配；迁移到区间时使用 `"[1.0.0,2.0.0)"` 一类 NuGet 区间写法。`BingPluginDependencyDescriptor.VersionRequirement` 保留完整约束，区间场景的 `Version` 为 `null`。预发布插件版本的完整文本可从插件描述符的 `VersionText` 读取；数值 `Version` 属性保持原有三段或四段形态。区间只校验已部署插件，不自动下载或选择其他版本。运行中切换仍须经 `Bing.PluginRuntime` 的独立容器入口。

模块资源按实例的引用身份管理。即使两个模块覆写 `Equals` 后被判定相等，框架也会分别释放它们，并保证每个实例最多释放一次。

## 生命周期

`IPreConfigureServices`、`IPostConfigureServices`、`IOnPreApplicationInitialization`、`IOnPostApplicationInitialization` 和 `IBingModuleShutdown` 各自同时声明同步方法和异步方法。异步方法接收对应的服务集合或上下文及 `CancellationToken`，返回 `Task`。`BingModule` 提供默认实现，继承它的模块只需重写实际使用的方法；直接实现这些接口的类型必须实现同步和异步两种方法。仅继承基类默认实现的模块不会被误判为必须异步执行。

下面是一个完整示例。它假设项目引用 `Bing.Core` 和 `Microsoft.Extensions.DependencyInjection`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

public sealed class CacheService
{
    public Task WarmUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CacheModule : BingModule, IPreConfigureServices,
    IOnPostApplicationInitialization
{
    void IPreConfigureServices.PreConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<CacheService>();
    }

    Task IPreConfigureServices.PreConfigureServicesAsync(
        IServiceCollection services, CancellationToken cancellationToken)
    {
        services.AddSingleton<CacheService>();
        return Task.CompletedTask;
    }

    void IOnPostApplicationInitialization.OnPostApplicationInitialization(
        BingModuleInitializationContext context)
    {
    }

    Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
        BingModuleInitializationContext context, CancellationToken cancellationToken) =>
        context.ServiceProvider.GetRequiredService<CacheService>()
            .WarmUpAsync(cancellationToken);
}

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        await services.AddBingApplicationAsync<CacheModule>();
        await using var provider = services.BuildServiceProvider();
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
    }
}
```

新入口对前置配置、主配置、后置配置、前置初始化、主初始化、后置初始化分别选择唯一回调：有效异步虚方法重写优先，其次是同步虚方法重写；前后阶段没有重写时，再从统一接口选择对应方法。主配置和主初始化分别默认桥接 `AddServices` 与 `UseModule`；前后服务配置还可回退到旧 `IBingPreConfigureServices` / `IBingPostConfigureServices`。基类桥接期间不会重复调用接口实现。同步入口在实际需要异步回调时，会在任何阶段回调执行前拒绝启动并给出迁移提示。

`AddBingApplication` 按已确定的依赖优先模块序列完成三轮服务配置，再由 `UseBing` / `UseBingAsync` 按同一序列完成三轮应用初始化：

```text
全模块 PreConfigureServices
→ 全模块 ConfigureServices
→ 全模块 PostConfigureServices
→ 构建容器
→ 全模块 OnPreApplicationInitialization
→ 全模块 OnApplicationInitialization
→ 全模块 OnPostApplicationInitialization
→ 初始化完成
```

例如模块 A 是模块 B 的依赖，初始化顺序为 `Pre(A), Pre(B), Initialize(A), Initialize(B), Post(A), Post(B)`，即使 B 的 `Level` / `Order` 更靠前也一样。旧入口保留原有模块排序，但初始化也按全局 Pre / 主阶段 / Post 三轮执行。三个初始化阶段共享一次短期作用域和宿主上下文；应用只在 Post、作用域释放和启动日志处理都成功后进入已初始化状态。Web 管道仍在调用 `UseBing` / `UseBingAsync` 时装配。

`BingModule` 可直接重写同步钩子，异步钩子则以 `Async` 为后缀并接收 `CancellationToken`。例如：

```csharp
public sealed class AppModule : BingModule
{
    public override void PreConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AppSettings>();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services); // 明确保留旧 AddServices 行为
        services.AddSingleton<AppService>();
    }

    public override Task OnPostApplicationInitializationAsync(
        BingModuleInitializationContext context, CancellationToken cancellationToken)
    {
        return context.ServiceProvider.GetRequiredService<AppService>()
            .WarmUpAsync(cancellationToken);
    }
}
```

前后服务配置也可分别实现 `IPreConfigureServices`、`IPostConfigureServices`，前后初始化可分别实现 `IOnPreApplicationInitialization`、`IOnPostApplicationInitialization`，关闭可实现 `IBingModuleShutdown`。每个接口均同时包含同步方法和以 `Async` 结尾的异步方法；异步方法参数沿用对应上下文并接收 `CancellationToken`。直接实现接口的类型必须提供两种方法，继承 `BingModule` 的类型则可使用基类默认实现，只重写所需虚方法。以下示例展示统一接口的显式实现：

```csharp
public sealed class ExplicitModule : BingModule, IOnPostApplicationInitialization
{
    void IOnPostApplicationInitialization.OnPostApplicationInitialization(
        BingModuleInitializationContext context) { }

    Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
        BingModuleInitializationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

所有接口均位于 `Bing.Core.Modularity`。删除独立异步阶段及关闭接口属于破坏性契约调整：依赖旧接口的外部模块需要迁移到对应统一接口，并重新编译模块及已编译插件。

每阶段只分派一次，顺序与上文一致。有效重写包括继承自中间基类的重写；派生类的同名隐藏方法不算重写。重写取代该阶段的接口回调，显式调用对应的 `base` 方法可续接基类桥接的接口实现。关闭阶段使用 `IBingModuleShutdown.Shutdown` / `ShutdownAsync`；`BingModule` 将其桥接到 `OnApplicationShutdown` / `OnApplicationShutdownAsync`。

旧 `AddBing().AddModule<T>()` 继续在调用处执行 `AddServices`，不会执行新的前后服务配置阶段；实际重写服务配置钩子或独立实现前后配置接口方法的模块会收到迁移提示。仅继承 `BingModule` 的默认接口实现不会触发提示。初始化前后命名接口仍可在旧初始化入口使用。同步注册发现实际需要异步配置的模块时，在任何配置回调执行前报错；同步 `UseBing` 发现实际需要异步初始化的模块时，也会在任何初始化回调执行前报错。需要异步关闭的模块应等待 `ShutdownBingAsync` 完成。初始化任一阶段失败或取消后，后续阶段不再执行；已经开始初始化的模块逆序关闭，所有框架拥有的模块实例释放一次。清理失败会附加到原始异常的诊断数据中。

模块异步初始化抛出 `OperationCanceledException` 时，`UseBingAsync` 返回已取消的 `Task`，并保留该异常实例、取消令牌、消息及 `Data` 中的模块阶段和清理诊断。调用方可检查 `Task.IsCanceled`，或捕获原始 `OperationCanceledException` 获取诊断。调用前令牌已取消的路径仍按原入口规则处理。

注册完成后必须显式激活模块：

```csharp
using var provider = services.BuildBingServiceProvider();
provider.UseBing();

// 应用退出前
provider.ShutdownBing();
```

含 `IBingAsyncModuleInitializer` 或异步初始化重写的模块必须使用 `UseBingAsync`。ASP.NET Core 中，`app.UseBingAsync()` 会把 `IApplicationBuilder` 放入 `BingModuleInitializationContext.HostContext`：

```csharp
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using System.Threading;
using System.Threading.Tasks;

public sealed class AppModule : BingModule, IBingAsyncModuleInitializer
{
    public Task InitializeAsync(BingModuleInitializationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var app = (IApplicationBuilder)context.HostContext;
        app.Use(async (httpContext, next) =>
        {
            httpContext.Response.Headers["X-Module"] = "App";
            await next();
        });
        return Task.CompletedTask;
    }
}
```

应用关闭前先等待 `ShutdownBingAsync()`，再释放根容器：

```csharp
await provider.ShutdownBingAsync();
await provider.DisposeAsync();
```

这里的 `provider` 为 `BuildBingServiceProvider` 返回的容器。也可使用 `await using` 管理其释放；模块异步关闭完成不等于容器里的服务都支持同步释放。

Generic Host 不会替应用自动调用 `UseBing` 或 `UseBingAsync`；应用仍须显式激活模块。它只会通过 Bing 注册的 `IHostedService` 在停止阶段自动调用异步关闭。无 Host 场景建议使用 `BuildBingServiceProvider`，该入口会预解析管理器，使根容器的 `using`/`await using` 能覆盖尚未初始化的模块；若直接调用原生 `BuildServiceProvider` 且从未解析模块管理器，DI 仍无法保证自动释放。已初始化应用应在释放根容器前等待 `ShutdownBing` 或 `ShutdownBingAsync`。

`BingHotPluginHost` 用异步应用注册构建候选代。候选代的 Pre、主阶段、Post 都完成后才切换服务入口；任一阶段失败时旧代仍继续服务，候选代进行关闭与资源清理。插件代仍按整组切换，不支持替换已装配的 Web 管道。

运行中切换可读取 `BingHotPluginHost.Status`，其中当前代和退役代共享稳定的代 ID，退役代提供活动调用数与 `Draining` / `Closing` 状态。`ReloadAsync` 保留原异常类型；失败时通过 `BingHotPluginReloadFailureInfo.FromException(error)` 读取失败阶段、代 ID 和 `Switched`。旧代关闭失败可能发生在新代已切换之后，应依据 `Switched` 判断当前入口。原有只调用 `ReloadAsync` 的代码无需改签名。

旧入口仍支持单宿主的静态兼容定位器；新入口不绑定该 locator。需要多个宿主或可测试的应用边界时，应使用新入口并通过 `BingModuleInitializationContext.ServiceProvider` 获取当前服务。

## 当前应用的配置、环境与附加程序集

初始化与关闭钩子可使用当前上下文的 `GetConfiguration()`、`GetConfigurationOrNull()`、`GetHostEnvironment()` 和 `GetHostEnvironmentOrNull()`。这些方法通过当前短期作用域解析，不缓存服务；模块不要长期保存其中解析到的 scoped 服务。注册阶段继续使用 `services.GetConfiguration()` 或可选版本，通用环境使用 `services.GetHostEnvironment()` 或可选版本。

注册期配置和通用环境读取均采用最后一个显式单例实例注册。只有没有直接 `IConfiguration` / `IHostEnvironment` 注册时，才回退到最后一个已注册的 `HostBuilderContext` 实例，分别读取其 `Configuration` / `HostingEnvironment`。配置只需实现 `IConfiguration`。若最后一个直接服务注册是工厂、实现类型或非单例注册，可选方法返回 null，必需方法提示改为显式实例或运行期 DI；不会跳过该注册回退到旧值。宿主上下文也必须是显式单例实例，注册期不会执行服务工厂。若上下文没有环境，通用环境可选方法返回 null、必需方法抛出缺失异常，不会生成 Development 环境。已有 Web 专用环境方法仍遵循原有行为。依赖旧优先级或提前执行工厂副作用的模块需要迁移。

模块可声明附加程序集，并允许应用补充扫描范围：

```csharp
[BingModuleAssemblyAttribute(typeof(SharedServicesMarker))]
public sealed class ReportingModule : BingModule { }

services.AddBingApplication<ReportingModule>(options =>
{
    options.ServiceScanning.AdditionalAssemblies.Add(typeof(SharedContractsMarker).Assembly);
    options.ServiceScanning.ConventionalTypeFilter = type =>
        type.Namespace != null && type.Namespace.StartsWith("Contoso.", StringComparison.Ordinal);
});
```

框架只读取最终选中模块的声明，并按模块序列和应用附加顺序合并程序集，以实际程序集身份去重。范围在服务配置前冻结，`BingModuleDescriptor.Assembly` 仍是模块本体程序集，`Assemblies` 是包含附加声明的只读集合。附加程序集参与约定 DI、选项绑定与公开类型事件扫描，但不会自动成为模块；`ConventionalTypeFilter` 只筛选约定 DI。`AutoRegisterServices = false` 时筛选器不会运行，选项与公开事件仍按原时机执行。筛选器抛错会使注册失败，须丢弃该服务集合。

插件模块可通过同一特性声明插件目录内的附加 DLL。插件清单仍只列入口程序集；附加 DLL 必须随插件部署，并由同一插件代解析。不得将旧可回收插件代的 `Assembly` 对象放入新应用的 `AdditionalAssemblies`。附加扫描只是注册策略，不隔离可信插件代码；持有旧代的类型、程序集或描述符也会影响卸载。
