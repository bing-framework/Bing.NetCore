# 模块化与插件

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

`Bing.Core` 支持按依赖图加载模块，依次执行全局 `PreConfigureServices`、`ConfigureServices`、`PostConfigureServices`，容器建立后再执行 Pre、Main、Post 三阶段应用初始化，并在关闭时逆序清理。同步入口会在执行回调前拒绝只能异步执行的模块。

可信插件可在注册期加入同一模块图；`Bing.PluginRuntime` 还提供整组候选插件的可回收加载上下文和代际切换。

## 核心实现

- 应用注册：`framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingApplicationServiceCollectionExtensions.cs`
- 初始化与关闭：`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs`
- 生命周期契约：`framework/src/Bing.Core/Bing/Core/Modularity/I*.cs`
- 插件发现：`framework/src/Bing.Core/Bing/Core/Modularity/PlugIns/`
- 热更新宿主：`framework/src/Bing.PluginRuntime/Bing/Core/Modularity/PlugIns/`

## 用法

```csharp
await services.AddBingApplicationAsync<AppModule>(options =>
{
    options.AdditionalModules.Add(typeof(ReportingModule));
    options.PluginSources.AddFolder(Path.Combine(AppContext.BaseDirectory, "modules"));
});

await using var provider = services.BuildBingServiceProvider();
await provider.UseBingAsync();
await provider.ShutdownBingAsync();
```

模块通常继承 `BingModule`，按需重写同步或异步钩子。插件清单格式、版本区间和热更新流程见[可信插件指南](../guides/modularity-plugins.md)。

## 依赖与配置

- 基础模块只依赖 `Bing.Core` 和 Microsoft DI。
- `AutoRegisterServices` 控制约定扫描；`ServiceRegistrationMode` 控制扫描位于配置阶段前还是兼容位置。
- 同一 `IServiceCollection` 不能混用 `AddBing()` 与 `AddBingApplication<T>()`。

## 限制与注意事项

- 插件代码在进程内执行，必须可信；本功能不提供安全沙箱。
- 启动期插件进入默认加载上下文后不能卸载；热更新必须使用插件运行时的可回收上下文。
- Web 管道不能在运行中无损替换；热更新适合通过代际服务入口调用的能力。
- 初始化上下文中的作用域服务只能在本轮初始化使用，模块不得长期保存。
