# ADR-0005：工作单元 + AOP 自动提交

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Uow`（`IUnitOfWork` / `IUnitOfWorkManager`）、`Bing.Aop.AspectCore`（`UnitOfWorkAttribute` / `InterceptorBase`） |

## 背景

"写了数据但没提交"是新手最常见的错误之一，且**不报错**——请求正常返回，数据却没落库。传统做法是每个应用服务方法末尾手写 `await _unitOfWork.CommitAsync()`，缺点：

1. 遗漏是静默的；
2. 异常路径容易漏掉「不该提交」的判断（虽然事务本身会回滚，但显式代码容易写错）；
3. 样板代码占应用服务的一半。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 手动 `CommitAsync()`** | 完全显式、无魔法、无性能开销 | 易漏且不报错；样板重复 |
| **B. 中间件/过滤器在请求末尾统一提交** | 一处解决 | 粒度太粗——批量处理、后台任务等非请求场景覆盖不到；粒度不好控制（只读请求也走一套） |
| **C. AOP 拦截器 `[UnitOfWork]`**（本方案） | 粒度精确到方法；可用一处attribute 表达"这里要事务"；异常时不提交 | 依赖动态代理：仅对**接口/虚方法调用**生效，同类内部自调用不触发；有代理创建开销 |
| **D. 源生成器织入** | 编译期、零代理开销 | 无法覆盖第三方/接口动态调用，调试困难（当时生成器生态也不成熟） |

## 决策

采用 **方案 C**：`[UnitOfWork]`（基于 AspectCore 的 `InterceptorBase : AbstractInterceptorAttribute`）在应用服务方法**成功返回后**由 `IUnitOfWorkManager.CommitAsync()` 统一提交；`CrudAppServiceBase` 已内置该特性。

显式方式保留：`IUnitOfWork.Commit()` / `CommitAsync()` 仍可直接调用。

## 后果

**正面**

- 应用服务不再需要重复的提交样板；
- 事务边界用一行 attribute 表达，可读性反而更好；
- 异常路径无需额外处理（拦截器不执行提交）。

**负面**

- **必须启用 AOP**。`UnitOfWorkAttribute` 是 AOP 拦截器，**没调 `EnableAop(...)` 时它完全不生效——不报错，数据不落库**。这是本框架最危险的静默失败之一（入口还叫 `EnableAop` 而非 `AddAop`）。
- **仅对代理生效**：同一类内部方法互相调用不会触发拦截器。
- AspectCore 对 `EF DbContext`、标记 `[IgnoreAspect]` 的接口默认不代理——这是刻意为之，但会让"为什么这个类的 `[UnitOfWork]` 不生效"难以排查。
- 控制器需要属性注入时要额外 `AddControllersAsServices()`。

**中性**

- 与 ADR-0002 / ADR-0007 构成一致的「约定 + 切面」风格：减少样板，代价是需要理解的执行模型更多。

## 相关文档

- [最佳实践 §4 工作单元与事务](../../guides/最佳实践.md)
- [术语表 §8.2 工作单元 ≠ 数据库事务](../../getting-started/术语表.md)
- [ABP 迁移对照 §2.2](../../getting-started/abp-migration.md)（ABP 的 `[UnitOfWork]` 同名但无需显式启用 AOP）
