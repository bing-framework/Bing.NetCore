# ADR-0001：模块化两阶段启动

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Core`（`IBingModule` / `BingModule` / `ModuleLevel` / `[DependsOnModule]`） |

## 背景

框架要打包成几十个可选 NuGet 包供不同项目按需引用。如果沿用传统做法——每个包给一份「请复制这 12 行到你的 `Startup.ConfigureServices`」文档——会带来三个问题：

1. 使用门槛高，漏复制一行就运行时报错，且错误发生在启动期难以定位；
2. 无法表达包之间的依赖（用了 A 就必须先用 B），文档里写「请先注册 XXX」很容易被忽略；
3. 框架自己也无法按能力裁剪——总不能装了一堆用不上的东西。

需要一个**自描述、自装配、可裁剪**的启动模型。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 手写 Startup 注册**（传统做法） | 完全显式，IDE 可跳转，无魔法 | 使用门槛高；依赖靠文档约定，极易漏；无法裁剪 |
| **B. 每个能力一个 `IServiceCollection` 扩展方法** | 一行调用，显式可控 | 调用顺序要用户自己保证；仍不表达依赖；包多了仍是一长串 |
| **C. 模块系统 + 自动拓扑排序**（ABP 式） | 依赖图自动推导顺序，语义最强 | 需要构建并求解依赖图；循环依赖要额外诊断；排序结果对用户不直观，调试时难以预测 |
| **D. 模块系统 + 显式 Level 排序**（本方案） | 实现简单（一次 `OrderBy`）；顺序对用户**显式可见**（写 `Level=Application` 就知道它在哪一层） | 顺序与依赖是两个独立维度，容易误解；开发者要自己想清楚该放哪一层 |

## 决策

采用 **方案 D**：以 `BingModule`（`ConfigureServices` 阶段 + `Initialize` 阶段）为装配单元，配 `[DependsOnModule]` 表达依赖、`ModuleLevel`（`Core=1` / `Framework=10` / `Application=20` / `Business=30`）表达顺序。

```csharp
services.AddBing().AddModule<AppModule>();   // 注册阶段（挂在 IBingBuilder，不是 IServiceCollection）
app.UseBing();                                // Web：初始化阶段
// 非 Web：serviceProvider.UseBing();
```

- `[DependsOnModule]` 递归展开，**只保证被依赖模块被加载**；
- 加载顺序由 `Level → Order → FullName` 三级排序决定，**不做拓扑排序**。

## 后果

**正面**

- 包可以「引用即生效」，用户零配置或一行 `AddModule<T>()`；
- 依赖关系由代码保证而非文档约定，漏依赖不可能发生；
- 可按能力裁剪——不想要的模块不引用即可。
- 排序规则只有三个字段，调试时打印 FullName 列表就能看懂。

**负面**

- **启动逻辑分散**：想知道 Redis 什么时候初始化，得去翻 `CacheModule`，而不是在 `Startup` 里一眼看完。
- **`[DependsOnModule]` 不管顺序**这一点与业界多数框架（ABP）相反，是本框架最容易误解的设计之一。已在 [最佳实践 §1.1](../../guides/最佳实践.md) 与 [术语表 §8.4](../../getting-started/术语表.md) 反复强调。
- 非 Web 场景（控制台 / WinForm）必须**额外**调 `serviceProvider.UseBing()`，漏了不报错、只是「什么都没发生」——静默失败。

**中性**

- 需要为非 Web 宿主保留一个全局 `IServiceProvider` 扩展重载，属于不得不做的妥协。

## 相关文档

- [架构文档 §8 模块系统与启动序列](../架构文档.md)
- [最佳实践 §1.1 / §1.2](../../guides/最佳实践.md)
- [术语表 §1 模块与启动](../../getting-started/术语表.md)
- [ABP 迁移对照 §2.1](../../getting-started/abp-migration.md)
