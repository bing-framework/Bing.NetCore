# ADR-0002：约定式依赖注入

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Core`（`ISingletonDependency` / `IScopedDependency` / `ITransientDependency` / `DependencyAttribute` / `DependencyModule`） |

## 背景

框架自身有几十个包、上百个需要注入的服务。若全部要求使用方显式 `services.AddScoped<IFoo, Foo>()`：

1. 注册代码比业务代码还长，且与「引用即启用」（ADR-0001）的目标相悖；
2. 新增一个类最常犯的错就是「忘了注册」，而故障表现为运行时解析失败，不是编译错误；
3. 每个包的 README 都得附一段注册清单，容易过期。

需要一种「**声明即注册**」的机制。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 全部显式注册** | 完全透明，IDE 可跳转，顺序可控 | 样板量大；易漏；违背"引用即启用" |
| **B. 按命名空间 / 后缀约定扫描**（如 `*Service`、`*.Repositories`） | 零标记代码 | 约定脆弱——改个后缀就失效；无法表达生命周期；第三方类误命中 |
| **C. 标记接口 + 扫描**（本方案） | 生命周期**写在类型上**，看一眼类定义就知道；零注册样板；可扩展 `[Dependency]` 精细控制 | 有一定"魔法"成分；需要一次程序集扫描；不小心实现多个接口会被注册多份 |
| **D. 依赖第三方容器**（如 Autofac 的 `RegisterAssemblyTypes`） | 功能强大，支持模块 | 引入额外容器依赖，与 `IServiceCollection` 生态割裂；替换成本不可逆 |

## 决策

采用 **方案 C**：

- 实现 `ISingletonDependency` / `IScopedDependency` / `ITransientDependency` 即自动注册；
- `DependencyModule`（`Level=Core`）在启动期扫描并注册；
- `[Dependency(Lifetime, TryAdd, ReplaceExisting, AddSelf)]` 用于精细控制，**优先级高于标记接口**；
- `[IgnoreDependency]` 用于排除（如只是顺手实现的标记接口）；
- 一个类型实现的**所有接口都会被注册**并指向同一实现；
- 逃生舱：`TryAdd`（已存在则跳过）、`ReplaceExisting`（覆盖）、`AddSelf`（额外注册实现类自身）。

## 后果

**正面**

- 新增服务零注册样板——"加了标记即被注入"；
- 生命周期信息内聚在类型定义上，比集中注册更不容易腐化；
- 与 ADR-0001 配合后，「引用 NuGet 包 → 自动生效」成立。

**负面**

- **隐式注册**：想知道 `IFoo` 解析到谁，得去找谁实现了 `IScopedDependency`，IDE 的 "Go to Implementation" 帮不上忙。
- **注册全部接口**的语义容易意外生效——一个类顺手实现 `IDisposable` 或某个标记接口就可能产生多余注册（用 `[IgnoreDependency]` 排除）。
- 默认行为是「注册」而非「替换」，想覆盖框架内置实现必须显式 `ReplaceExisting = true`，否则取决于注册顺序。
- 启动期有一次程序集扫描（框架用 `AssemblyManager` 缓存缓解，但 `DirectoryAssemblyFinder.FindAll()` **默认参数 `fromCache=false`**，会重复加载程序集——见 [性能指南](../../operations/性能指南.md)）。

**中性**

- 如果某天需要切换到纯显式注册，`[IgnoreDependency]` 提供了渐进迁移路径。

## 相关文档

- [架构文档 §9 约定式 DI 自动注册](../架构文档.md)
- [最佳实践 §2.2 / §2.3 / §2.4](../../guides/最佳实践.md)
- [能力矩阵](../../getting-started/能力矩阵.md)（哪些能力依赖它自动注册）
