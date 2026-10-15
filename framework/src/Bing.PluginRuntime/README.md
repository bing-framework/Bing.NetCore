# Bing.PluginRuntime

[![Bing.PluginRuntime](https://img.shields.io/nuget/v/Bing.PluginRuntime.svg)](https://www.nuget.org/packages/Bing.PluginRuntime/) [![Bing.PluginRuntime](https://img.shields.io/nuget/vpre/Bing.PluginRuntime.svg)](https://www.nuget.org/packages/Bing.PluginRuntime/) [![Bing.PluginRuntime](https://img.shields.io/nuget/dt/Bing.PluginRuntime.svg)](https://www.nuget.org/packages/Bing.PluginRuntime/)

该包提供可回收插件代的运行时宿主。候选代完成服务注册和模块初始化后才会切换为当前代；旧代会等待活动调用退出，再执行关闭和卸载。

## 核心类型

- `BingHotPluginHost`：加载、切换、调用和关闭插件代。
- `BingHotPluginHostStatus`：读取当前代与退役代快照。
- `BingHotPluginGenerationStatus`：描述单个插件代的状态与活动调用数。

完整注册、更新示例及清单格式见仓库的 `docs/modules/modularity-and-plugins.md`。

## 依赖与限制

- 依赖 `Bing.Core` 和 Microsoft 依赖注入抽象，支持 `net6.0`、`net8.0`。
- 插件按可信代码运行，不提供不可信代码沙箱。
- 卸载依赖插件代码不再被线程、静态事件、缓存或宿主对象引用；运行时只能报告回收状态，不能强制终止仍在执行的代码。
- ASP.NET Core 中间件管道不会随插件代自动替换。
