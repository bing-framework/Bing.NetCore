# 可信插件：启动期加载与整组热更新

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

`Bing.Core` 支持在 `AddBingApplication<TStartupModule>` 注册期间加载可信插件。插件会先完成清单、依赖和程序集校验，再把清单中的启动模块加入现有模块依赖图。

## 选择入口

| 场景 | 入口 | 生效时机 |
| --- | --- | --- |
| 项目引用或 NuGet 模块 | `AddBingApplication<AppModule>` | 应用注册时构建完整模块图 |
| 随应用启动的可信目录插件 | 在应用选项中添加 `PluginSources` | 当前进程启动时加载，不支持卸载 |
| 运行中切换整组可信插件 | `BingHotPluginHost<AppModule>` | `ReloadAsync` 完成候选初始化后切换新调用 |

服务配置含异步钩子时使用 `AddBingApplicationAsync`；初始化或关闭含异步钩子时使用 `UseBingAsync`、`ShutdownBingAsync`。热更新宿主已使用这些异步入口。三种方式都执行进程内可信代码，不能用于隔离不可信上传文件。

## 目录结构

`AddFolder` 读取父目录的一级子目录；`AddDirectory` 读取一个具体插件目录。每个插件目录必须包含入口程序集和 `bing-plugin.json`：

```text
modules/
  Contoso.Foundation/
    bing-plugin.json
    Contoso.Foundation.dll
  Contoso.Reporting/
    bing-plugin.json
    Contoso.Reporting.dll
    Contoso.Reporting.Extensions.dll
```

清单使用 UTF-8 JSON：

```json
{
  "id": "Contoso.Reporting",
  "version": "1.2.0",
  "entryAssembly": "Contoso.Reporting.dll",
  "startupModules": [
    "Contoso.Reporting.ReportingModule"
  ],
  "dependencies": [
    {
      "id": "Contoso.Foundation",
      "version": "1.0.0"
    }
  ]
}
```

`id` 按不区分大小写判重。插件自身版本使用三段或四段非负数字，可附加 SemVer 预发布标识和构建元数据，例如 `1.2.0-beta.2+build.7`。依赖的普通版本字符串仍表示**精确匹配**，例如 `"1.2.0"` 只接受 `1.2.0`；需要区间时使用 NuGet 区间语法，例如 `"[1.2.0,2.0.0)"`、`"(,2.0.0]"` 或 `"[1.2.0,)"`。区间上下界也必须使用三段或四段版本。稳定版区间不会匹配预发布插件；要接受预发布版本，须在至少一个区间边界中明确写出预发布标识。构建元数据不参与版本先后比较。

插件图只从已配置目录加载，每个 ID 只能有一个已安装版本；区间用于验证这一版本是否满足依赖，不会联网查找或自动选择另一版本。入口程序集只能是当前插件目录内的相对 DLL，启动模块必须是入口程序集中的公开 `BingModule` 派生类型，并且具有公共无参构造函数。

## 注册插件

```csharp
// AppModule 是应用定义的、具有公共无参构造函数的 BingModule。
// services 为当前宿主的 IServiceCollection。
await services.AddBingApplicationAsync<AppModule>(options =>
{
    options.PluginSources.AddFolder(Path.Combine(AppContext.BaseDirectory, "modules"));
});

// 无 Generic Host 的独立应用由调用方拥有根容器。
await using var provider = services.BuildBingServiceProvider();
await provider.UseBingAsync();
try
{
    // 在这里运行应用；需要 scoped 服务时另建服务作用域。
}
finally
{
    await provider.ShutdownBingAsync();
}
```

以上示例使用 `Bing.Core.Modularity`、`Microsoft.Extensions.DependencyInjection` 和 `System.IO` 命名空间。只加载单个目录时，将 `AddFolder(...)` 换成 `AddDirectory(...)`；不同来源可组合，但无需重复添加已被父目录发现的插件。使用 Web/Generic Host 时复用宿主服务集合和根容器，仍显式调用 `UseBingAsync`，宿主停止会自动执行异步关闭。

也可以实现 `IBingPluginSource`，从配置中心或其他受信来源返回具体插件目录。来源只在注册阶段读取，服务集合构建完成后不会再次扫描。

应用可以通过 `IBingPluginContainer` 查看本次注册的插件描述符：

```csharp
var plugins = provider.GetRequiredService<IBingPluginContainer>().Plugins;
```

描述符提供插件 ID、版本、目录、入口程序集、启动模块、直接依赖和插件执行序号。`VersionText` 保存包含预发布和构建元数据的完整版本；原有 `Version` 仍返回三段或四段数值版本。依赖描述符的 `VersionRequirement` 保存原始约束；区间约束时原有 `Version` 为 `null`。

## 加载和失败行为

框架先读取全部清单并按插件依赖排序，再检查入口路径、程序集身份和启动模块类型。缺失依赖、版本约束不匹配、依赖环、同名不同版本程序集和非法路径都会在模块构造前失败。异常数据包含 `Bing.PluginId`、`Bing.PluginManifest`、`Bing.PluginPhase`，并在适用时包含 `Bing.PluginAssembly`、`Bing.PluginType` 和 `Bing.PluginDependencyPath`，便于记录具体插件、程序集、类型、依赖链和处理阶段。

此入口的插件程序集进入默认 `AssemblyLoadContext`，只在本次加载期间挂接依赖解析回调，成功或失败后都会退订。默认加载上下文不能卸载程序集，因此该入口的插件目录应在进程启动时保持稳定；运行中切换请使用下文的独立运行库。不可信代码隔离仍不属于当前能力。

插件模块使用与普通模块相同的生命周期：全模块 `PreConfigureServices` → `ConfigureServices` → `PostConfigureServices`，随后全模块 `OnPreApplicationInitialization` → `OnApplicationInitialization` → `OnPostApplicationInitialization`，关闭按模块序列逆序执行。模块可重写同名虚方法，也可实现 `IPreConfigureServices`、`IPostConfigureServices`、`IOnPreApplicationInitialization`、`IOnPostApplicationInitialization`、`IBingModuleShutdown`；每个统一接口都同时提供同步方法和接收 `CancellationToken` 的异步方法。主配置默认桥接旧 `AddServices`；重写优先于接口，同一阶段不会重复执行。详细分派规则见[模块迁移指南](../migrations/modularity-runtime.md)。`ExcludedModules` 排除插件启动模块时会直接报告冲突链，`AutoRegisterServices = false` 只关闭约定式 DI 扫描。

框架源码中的插件契约与加载实现位于 `Modularity/PlugIns` 目录，热更新实现位于 `Bing.PluginRuntime` 的对应目录。公开类型仍使用 `Bing.Core.Modularity` 命名空间，插件项目不需要修改现有 `using`。

插件模块也可通过 `[BingModuleAssembly(typeof(AdditionalMarker))]` 声明同目录中的附加程序集。部署时将入口 DLL 与附加 DLL 一起放入插件目录；附加程序集参与当前插件代的服务、选项及类型事件扫描，但不自动作为启动模块。缺失 DLL 会在模块准备阶段失败，异常数据可定位插件 ID、清单和阶段。动态插件代的附加程序集必须由该代加载；旧代的 `Assembly`/`Type` 不可复用到新代。仅需缩小约定 DI 范围时使用 `options.ServiceScanning.ConventionalTypeFilter`，它不筛选选项绑定或公开事件。

`ServiceRegistrationMode.BeforeConfigureServices` 可把约定 DI、选项类型注册和公开类型事件统一安排在全部 Pre 之后、主配置之前，以便主配置或 Post 使用手工注册覆盖默认服务。默认 `Compatible` 保留原时机。直接向目录放入 DLL 不会自动扩大扫描范围；应通过模块声明或 `options.ServiceScanning.AdditionalAssemblies.Add(...)` 明确指定附加程序集。

模块配置中可调用 `services.GetConfiguration()` / `services.GetHostEnvironment()`，生命周期钩子中使用 `context.GetConfiguration()` / `context.GetHostEnvironment()`。注册期仅接受直接单例实例，或无直接注册时的 `HostBuilderContext` 实例回退，不执行工厂；可选依赖用对应 `OrNull` 方法。独立插件宿主不会自动复制主应用的配置与服务，需通过下文构造函数的 `configureServices` 参数显式注册。

插件模块应将原独立异步阶段接口迁移为对应统一接口，例如显式实现时使用 `IPreConfigureServices.PreConfigureServicesAsync`、`IPostConfigureServices.PostConfigureServicesAsync`、`IOnPreApplicationInitialization.OnPreApplicationInitializationAsync`、`IOnPostApplicationInitialization.OnPostApplicationInitializationAsync` 或 `IBingModuleShutdown.ShutdownAsync`。直接实现统一接口时必须同时实现同步和异步方法；继承 `BingModule` 时可依赖默认实现。此项为破坏性契约调整，旧接口的外部插件需要修改源码并重新编译。热更新宿主等待候选代完成全部 Post 初始化后再切换；初始化失败时保留旧代并清理、卸载候选代。

## 常见失败与处理

| 现象 | 处理方式 |
| --- | --- |
| 依赖缺失、版本冲突或循环 | 结合 `Bing.PluginDependencyPath` 检查整条链；部署全部必需插件并调整清单约束 |
| 入口程序集或类型无效 | 检查清单路径、相对 DLL 路径、类型完整名称、可见性及公共无参构造函数 |
| 提示需要异步入口 | 注册和初始化分别改用异步入口并等待完成，不以阻塞等待包装同步入口 |
| 注册失败后重试仍被拒绝 | 新建服务集合重新注册；失败的注册记录不会恢复为可用状态 |
| 配置或环境无法读取 | 在应用注册前提供单例实例，或在运行期从生命周期上下文获取；不要提前构建容器 |
| 热更新后程序集仍未回收 | 检查后台任务、事件订阅、静态缓存及回调返回值是否保留旧代对象 |

记录原始异常及其 `Data` 中的诊断字段；这些字段可能包含本机路径，不应直接作为公开 HTTP 错误响应。任一阶段失败都不能撤销插件自行产生的外部副作用。默认加载上下文已加载的程序集也无法回滚；更换其二进制后应重启进程。

## 加载与关闭边界

框架在临时解析会话内预加载已部署、由入口或附加程序集静态引用可达的私有托管依赖，包括只在方法体中使用的 DLL 及其传递依赖。配置、初始化和运行期服务调用不再依赖尚未退订的解析事件。预加载不增加约定 DI 扫描范围；只需要调用其实现的辅助 DLL 不必声明为 `BingModuleAssembly`。未引用的目录 DLL 不会仅因在目录中就被加载或扫描。

请完整部署私有依赖。未部署且尚未被实际使用的可选引用不做强制预加载；实际调用缺失依赖时仍会失败，配置或初始化失败可通过 `Bing.ModulePhase` 和 `Bing.ModuleType` 定位。字符串形式的 `Assembly.Load`、运行期才出现的新 DLL 和本机 DLL 解析不属于此静态依赖加载能力。不要通过长期挂接全局解析事件绕过这些边界，否则可能保留插件代。

真实 Web 宿主构建后可能将服务集合锁为只读。模块关闭不需要移除这类集合中的描述符：框架仍逆序释放模块并清空内部运行状态及插件容器；可变集合继续移除框架拥有的模块实例描述符。只读集合中的描述符随宿主容器一起结束生命周期，调用方不要长期保留已经关闭的宿主。初始化失败保留原始异常，清理异常附加在 `Bing.ModuleCleanupErrors` 中。

修复与回归证据见[执行报告](../../ai_docs/tasks/BING-MODULE-CONTEXT-SCANNING-20260928/execution.md)。计划范围和后续能力见[审查报告](../../ai_docs/tasks/BING-MODULE-CONTEXT-SCANNING-20260928/review.md)。

## 运行中切换整组插件

需要运行中切换时，引用 `Bing.PluginRuntime`（目标框架为 `net6.0` 或 `net8.0`），使用 `BingHotPluginHost<TStartupModule>`。每次 `ReloadAsync` 都重新读取清单，创建独立 DI 容器和可回收 `AssemblyLoadContext`，初始化成功后才把新调用切到新一代。候选加载失败时旧代继续服务。

```csharp
var activePluginDirectory = Path.Combine(AppContext.BaseDirectory, "modules", "reporting-v1");
await using var plugins = new BingHotPluginHost<AppModule>(options =>
    options.PluginSources.AddDirectory(activePluginDirectory),
    configureServices: services => services.AddSingleton<IConfiguration>(configuration));

await plugins.ReloadAsync();
var report = await plugins.RunAsync((services, token) =>
    services.GetRequiredService<IReportService>().CreateAsync(token));

activePluginDirectory = Path.Combine(AppContext.BaseDirectory, "modules", "reporting-v2");
await plugins.ReloadAsync();
```

此片段中的 `configuration` 为宿主已创建的 `IConfiguration`，`IReportService` 为宿主共享契约，其 `CreateAsync` 返回稳定 DTO 或字符串；需要引入 `Microsoft.Extensions.Configuration`。`configureServices` 每代执行一次，仅注册当前代需要的服务；不要把主应用的根容器当作插件容器使用。

按版本部署到不同目录，先准备并校验新目录，再更新配置所读取的目录并调用 `ReloadAsync`。插件服务只在 `RunAsync` 回调和它的服务作用域内使用；不要向外返回插件实例、`Type`、`Assembly`、作用域或服务提供程序。返回宿主定义的 DTO、字符串等稳定数据。重新加载会等待旧回调结束，然后逆序停止模块、异步释放容器并请求卸载旧上下文；回调内等待同一宿主重新加载或关闭会立即报错。

候选代的 Post 初始化完成后即切换新调用，再等待旧调用退出。重新加载失败时保留原异常类型，并在异常数据中附加 `BingHotPluginReloadFailureInfo`。`Switched` 说明新代是否已经发布；`Phase` 区分候选构建、旧代排空和旧代关闭；`AttemptId`、`PreviousGenerationId`、`CurrentGenerationId` 可关联同版本的多次切换。候选构建失败时 `Switched=false`，旧代继续服务；旧代关闭失败时 `Switched=true`，新代已经对新调用可用。

```csharp
try
{
    await plugins.ReloadAsync();
}
catch (Exception error)
{
    var failure = BingHotPluginReloadFailureInfo.FromException(error);
    if (failure != null)
        Console.Error.WriteLine($"{failure.Phase}: switched={failure.Switched}, " +
            $"current={failure.CurrentGenerationId}, attempt={failure.AttemptId}");
    throw;
}
```

`plugins.Status` 是不持有插件程序集或服务对象的只读快照。`Current` 表示对新调用开放的代，`Retiring` 包含等待调用退出或正在关闭的代；每代有稳定 `Id`、`ActiveCalls` 和 `State`（`Active`、`Draining`、`Closing`）。例如重新加载迟迟未完成时，可检查 `plugins.Status.Retiring` 中 `State=Draining` 且 `ActiveCalls>0` 的代。快照不会自动更新，重新读取 `Status` 可获取新状态；插件自身的后台任务不计入 `ActiveCalls`。

取消令牌用于等待本宿主其他重新加载，以及构建候选代和初始化；切换后旧代清理不受调用方取消影响。当前没有内置旧调用排空超时；回调应遵守取消和超时约定，避免长期阻塞重新加载或关闭。该入口不会监视文件变更，部署完成后需由调用方触发重新加载。

可回收程序集的实际回收是协作式的：插件自建线程、事件订阅、静态缓存或调用方持有的对象都可能继续保留它。插件应在关闭钩子中停止后台任务并退订事件。`Bing.Core` 和 DI 契约使用宿主的程序集身份；自定义服务契约也应由宿主提供，不要把独立副本放入插件目录。动态切换作用于通过此宿主分派的服务调用；已经装配到 ASP.NET Core 管道中的中间件不会随插件代切换。
