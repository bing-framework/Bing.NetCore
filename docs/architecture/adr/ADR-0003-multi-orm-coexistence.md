# ADR-0003：多 ORM 后端并存 + 共享领域抽象

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Ddd.Domain`（`IStore` / `IQueryStore` / `IUnitOfWork`）、`Bing.EntityFrameworkCore.*`、`Bing.FreeSQL.*`、`Bing.Dapper.*`、`Bing.Data.Sql` |

## 背景

不同团队对 ORM 的偏好和约束差异极大：有的要求 LINQ 全能力与迁移，有的被限制只能用某个数据库，有的场景就是写复杂报表 SQL 用不上 ORM。框架若只绑定一种，会直接排除掉另一批用户；若完全不提供 ORM，又退化成工具库而非框架。

同时，上层业务代码希望**不依赖具体 ORM**——换数据访问实现时不改动应用层。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 只支持 EF Core** | 维护成本最低；能力最完整、最一致 | 排除 netstandard2.0 老工程；排除只想写 SQL 的场景；绑定 Microsoft 生态 |
| **B. 只提供领域抽象，不做实现** | 中立、轻 | 用户拿到接口还得自己写实现，等于没用 |
| **C. 单一抽象 + 一支实现 + 逃逸阀（Raw SQL）** | 比 A 灵活 | 复杂查询与批量写入场景仍很别扭（ORM 的通病） |
| **D. 三支并存 + 共享领域抽象**（本方案） | 覆盖面最广；可按场景混用 | 三套研发投入；能力天然不一致；用户要做选择题 |

## 决策

采用 **方案 D**：

1. **领域抽象与 ORM 无关**：`IStore` / `IQueryStore` / `IUnitOfWork` 定义在 `Bing.Ddd.Domain`，上层只依赖它们；
2. **三支实现并存**：EF Core（全功能 + 迁移）、FreeSQL（仅 MySql、`netstandard2.0`）、Dapper + `Bing.Data.Sql`（SQL 构建器 + 执行器，**不提供仓储与工作单元**）；
3. **`Bing.Data.Sql` 作为通用 SQL 引擎被三者复用**：EF 经 `IEfCoreSqlQueryFactory`、FreeSQL 直接用 `ISqlQuery`、Dapper 直接落到执行器；
4. 允许在同一应用中**组合使用**（如 EF 管写入、`Bing.Data.Sql` 管报表查询）。

## 后果

**正面**

- 覆盖三种截然不同的用户画像（全功能 ORM / 老框架兼容 / 纯 SQL 高性能）；
- 上层换 ORM 的成本被压到接近零（只要能力对齐）；
- 流利 SQL 引擎统一了跨库方言差异，跨库查询、存储过程、批量写入这些 EF 弱项都补齐了。

**负面**

- **三套实现的维护成本**，且能力天然无法对齐——这正是需要 [《能力矩阵》](../../getting-started/能力矩阵.md) 的原因（Dapper 支没有审计/乐观锁/UoW；FreeSQL 支没有迁移、异步会退化）。
- **命名陷阱**：EF Core 的运行时命名空间是 `Bing.Datas.EntityFramework.Core`，与包名 `Bing.EntityFrameworkCore` 不一致；PostgreSQL 入口是 `AddPgSqlUnitOfWork` 而目录叫 `PostgreSql`；两个不同支都有 `AddMySqlUnitOfWork`。
- **不共享事务**：三支各自的事务链路独立，混用时一致性要自行保证（或用 CAP 事件最终一致）。
- 用户在三支之间做选择需要额外信息，增加了上手成本。

**中性**

- `Bing.Events.Cap.MySql`（CAP 2.6 源码镜像，为 FreeSQL 换 MySqlConnector）是这条并行路线的历史遗留，目前**无工程引用**。

## 相关文档

- [能力矩阵](../../getting-started/能力矩阵.md)（选型依据）
- [子系统深挖](../子系统深挖.md)（三支的代码级实现）
- [最佳实践 §6.1 / §6.2](../../guides/最佳实践.md)
