# ADR-0013：模块全局生命周期阶段

- 状态：已实现；当前工作树。
- 日期：2026-09-28。
- 范围：`Bing.Core` 模块配置、应用初始化与 `Bing.PluginRuntime` 候选代切换。

## 背景

原有模块注册有同步的 Pre / `AddServices` / Post 配置轮次，但应用只执行主初始化。旧模块主要使用 `AddServices`、`UseModule` 或可选接口，异步服务配置没有入口。约定 DI 扫描发生在 `DependencyModule.AddServices`，选项类型和公开类型事件在配置末尾；模块主配置无法按需观察全部自动注册结果。

## 备选方案

- 只扩展可选接口：旧模块兼容，但派生 `BingModule` 的常见写法仍需额外声明接口，阶段行为分散。
- 把扫描时机整体提前：实现简单，但会改变现有服务覆盖顺序和公开事件时机。
- 增加任意阶段贡献者：扩展性更强，但阶段排序和兼容约束超出本次需求。

## 决策

`BingModule` 增加服务配置、应用 Pre / 主阶段 / Post 初始化与关闭的同步、异步虚方法。应用入口按依赖优先的模块序列逐轮执行服务配置，初始化入口再按同一序列逐轮执行应用初始化。异步服务配置由 `AddBingApplicationAsync` 执行。异步入口优先采用有效的异步重写，其次同步重写；没有新重写时沿用原接口及 `AddServices` / `UseModule`。新重写替代对应旧回调，调用方可显式调用 `base` 保留旧行为。同步入口预检异步钩子，避免部分阶段已执行后才发现不兼容。

扫描时机默认 `BingServiceRegistrationMode.Compatible`，维持现有顺序；可选 `BeforeConfigureServices` 在全部 Pre 完成后一次性执行约定 DI 扫描、选项类型注册和公开类型事件，再执行主配置与 Post。`AutoRegisterServices` 仍只控制约定 DI。旧 `AddBing().AddModule<T>()` 保留立即执行 `AddServices`；新服务配置重写需迁移到应用入口，初始化阶段则共用模块管理器。

三个初始化阶段共用短期作用域和宿主上下文。任一阶段失败或取消时停止后续阶段，逆序关闭已开始的模块并释放框架持有的全部模块实例。候选热更新代完成 Post 后才切换服务入口。关闭仍只有一个逆序阶段。

## 后果与边界

- 依赖顺序覆盖 `Level` / `Order`；旧入口仍采用兼容排序。Web 管道继续在 `UseBing` / `UseBingAsync` 调用期间装配。
- 默认配置顺序不变；选择提前扫描的应用须检查服务替换和公开事件的时序依赖。Post 阶段可以替换此前的服务注册。
- 派生模块的重写与旧接口不会自动叠加。调用对应的 `base` 可明确续接旧 `AddServices`、`UseModule` 及相应的前后配置、异步初始化和关闭接口；前后配置接口映射到同一虚方法时不会递归调用。
- 新阶段属于固定框架生命周期，本决策不提供自定义阶段、运行中 Web 管道替换或不可信插件隔离。

## 关联

- [模块运行时迁移指南](../../migrations/modularity-runtime.md)
- [模块资源所有权](ADR-0011-module-runtime-ownership.md)
- [可回收的插件代](ADR-0012-hot-plugin-generations.md)
