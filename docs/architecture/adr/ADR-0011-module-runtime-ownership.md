# ADR-0011：可选依赖图入口与模块资源所有权

- 状态：已实现；当前工作树，不变更包版本。
- 日期：2026-09-27。
- 范围：Bing.Core、Bing.AspNetCore 的启动时可信模块组合。

## 背景

旧入口立即执行 AddServices，注册顺序和 Web 初始化排序属于已有行为。直接改成拓扑排序会影响消费者。另一方面，循环依赖、重复初始化、静态选项订阅和缺少资源所有者需要在两种入口中共同修复。

## 决策

保留 AddBing().AddModule<T>()，增加 AddBingApplication<T>()。两者共用依赖校验、应用所属状态机和资源所有者，不得在同一集合混用。

新入口的完整模块图由 IBingModuleLoader 加载，IBingModuleContainer 暴露只读描述符。依赖先于使用方；仅就绪节点按 Level、Order、FullName（Ordinal）排序。配置按全部 PreConfigureServices、全部 AddServices、全部 PostConfigureServices 三轮执行。

异步初始化和关闭使用可选接口，不改变 IBingModule。异步入口优先异步钩子，每个模块只执行一条初始化路径。Generic Host 负责停止，初始化仍显式调用，以保证 Web 管道在宿主构建期间装配。

新增启动期可信插件来源。`BingPluginSourceList` 支持单目录和父目录一级发现；每个插件以 `bing-plugin.json` 声明 ID、三段或四段精确版本、入口程序集、启动模块和直接插件依赖。插件清单和插件依赖图先于模块构造完成校验，入口程序集在默认 `AssemblyLoadContext` 中加载，`IBingPluginContainer` 暴露按依赖顺序排列的只读描述符。

模块由框架创建并唯一释放，DI 服务仍归容器。启动失败清理已开始模块；逆序停止遇到单个异常继续清理；原启动异常保留清理错误。无 Host 场景建议使用 `BuildBingServiceProvider` 或显式关闭后释放容器。仅实现 IAsyncDisposable 的模块允许进入异步生命周期；同步配置或构造失败时同步等待其异步释放，正常关闭必须使用异步入口。

## 后果与边界

- 新入口按选中模块的程序集扫描，允许关闭自动 DI；同程序集内不提供模块级隔离。
- 旧静态定位器仅保留单宿主兼容语义，按所属者解绑；新入口不绑定它。
- 内置选项绑定使用集合所属注册器，不再订阅进程级事件。公开 RegisterType 事件仍由外部订阅者负责退订。
- 插件能力仅覆盖启动期受信代码；不提供运行时热插拔、不可信代码隔离或 collectible `AssemblyLoadContext` 卸载。默认加载上下文中的插件程序集保留到进程结束，已加载程序集不能在注册失败后撤回。
- 不承诺撤销 AddServices 任意外部副作用。原生 BuildServiceProvider 未激活管理器时仍不能依赖 DI 发现外部创建实例；应用应使用 BuildBingServiceProvider 或显式 ShutdownBing。
- 资源证据使用弱引用回收、释放次数以及后台任务/订阅停止；不以一次进程工作集变化推断不存在泄漏。

## 关联

- [旧入口决策](ADR-0001-module-two-phase-bootstrap.md)
- [迁移与示例](../../migrations/modularity-runtime.md)
- [执行与验证记录](../../../ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/execution.md)
