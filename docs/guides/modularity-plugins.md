# 启动期可信插件

`Bing.Core` 支持在 `AddBingApplication<TStartupModule>` 注册期间加载可信插件。插件会先完成清单、依赖和程序集校验，再把清单中的启动模块加入现有模块依赖图。

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
services.AddBingApplication<AppModule>(options =>
{
    options.PluginSources.AddFolder(Path.Combine(AppContext.BaseDirectory, "modules"));
    options.PluginSources.AddDirectory(Path.Combine(AppContext.BaseDirectory, "modules", "Contoso.Reporting"));
});

using var provider = services.BuildBingServiceProvider();
provider.UseBing();
```

也可以实现 `IBingPluginSource`，从配置中心或其他受信来源返回具体插件目录。来源只在注册阶段读取，服务集合构建完成后不会再次扫描。

应用可以通过 `IBingPluginContainer` 查看本次注册的插件描述符：

```csharp
var plugins = provider.GetRequiredService<IBingPluginContainer>().Plugins;
```

描述符提供插件 ID、版本、目录、入口程序集、启动模块、直接依赖和插件执行序号。`VersionText` 保存包含预发布和构建元数据的完整版本；原有 `Version` 仍返回三段或四段数值版本。依赖描述符的 `VersionRequirement` 保存原始约束；区间约束时原有 `Version` 为 `null`。

## 加载和失败行为

框架先读取全部清单并按插件依赖排序，再检查入口路径、程序集身份和启动模块类型。缺失依赖、版本约束不匹配、依赖环、同名不同版本程序集和非法路径都会在模块构造前失败。异常数据包含 `Bing.PluginId`、`Bing.PluginManifest`、`Bing.PluginPhase`，并在适用时包含 `Bing.PluginAssembly`、`Bing.PluginType` 和 `Bing.PluginDependencyPath`，便于记录具体插件、程序集、类型、依赖链和处理阶段。

此入口的插件程序集进入默认 `AssemblyLoadContext`，只在本次加载期间挂接依赖解析回调，成功或失败后都会退订。默认加载上下文不能卸载程序集，因此该入口的插件目录应在进程启动时保持稳定；运行中切换请使用下文的独立运行库。不可信代码隔离仍不属于当前能力。

插件模块仍使用 Bing 现有的 `PreConfigureServices`、`AddServices`、`PostConfigureServices`、同步/异步初始化和逆序关闭规则。`ExcludedModules` 排除插件启动模块时会直接报告冲突链，`AutoRegisterServices = false` 仍可关闭选中模块程序集的约定式 DI 扫描。

插件模块也可通过 `[BingModuleAssembly(typeof(AdditionalMarker))]` 声明同目录中的附加程序集。部署时将入口 DLL 与附加 DLL 一起放入插件目录；附加程序集参与当前插件代的服务、选项及类型事件扫描，但不自动作为启动模块。缺失 DLL 会在模块准备阶段失败，异常数据可定位插件 ID、清单和阶段。动态插件代的附加程序集必须由该代加载；旧代的 `Assembly`/`Type` 不可复用到新代。仅需缩小约定 DI 范围时使用 `options.ServiceScanning.ConventionalTypeFilter`，它不筛选选项绑定或公开事件。

## 运行中切换整组插件

需要运行中切换时，引用 `Bing.PluginRuntime`（目标框架为 `net6.0` 或 `net8.0`），使用 `BingHotPluginHost<TStartupModule>`。每次 `ReloadAsync` 都重新读取清单，创建独立 DI 容器和可回收 `AssemblyLoadContext`，初始化成功后才把新调用切到新一代。候选加载失败时旧代继续服务。

```csharp
var activePluginDirectory = Path.Combine(AppContext.BaseDirectory, "modules", "reporting-v1");
await using var plugins = new BingHotPluginHost<AppModule>(options =>
    options.PluginSources.AddDirectory(activePluginDirectory));

await plugins.ReloadAsync();
var report = await plugins.RunAsync((services, token) =>
    services.GetRequiredService<IReportService>().CreateAsync(token));

activePluginDirectory = Path.Combine(AppContext.BaseDirectory, "modules", "reporting-v2");
await plugins.ReloadAsync();
```

按版本部署到不同目录，先准备并校验新目录，再更新配置所读取的目录并调用 `ReloadAsync`。插件服务只在 `RunAsync` 回调和它的服务作用域内使用；不要向外返回插件实例、`Type`、`Assembly`、作用域或服务提供程序。返回宿主定义的 DTO、字符串等稳定数据。重新加载会等待旧回调结束，然后逆序停止模块、异步释放容器并请求卸载旧上下文；回调内等待同一宿主重新加载或关闭会立即报错。

可回收程序集的实际回收是协作式的：插件自建线程、事件订阅、静态缓存或调用方持有的对象都可能继续保留它。插件应在关闭钩子中停止后台任务并退订事件。`Bing.Core` 和 DI 契约使用宿主的程序集身份；自定义服务契约也应由宿主提供，不要把独立副本放入插件目录。动态切换作用于通过此宿主分派的服务调用；已经装配到 ASP.NET Core 管道中的中间件不会随插件代切换。
