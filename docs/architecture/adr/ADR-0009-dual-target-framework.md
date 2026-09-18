# ADR-0009：双目标框架（netstandard2.0 与 net6.0 并存）

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `framework.props`（默认 `netstandard2.0`）、各工程的 `TargetFramework` 覆盖 |

## 背景

作为通用类库，希望**最大范围可复用**——包括仍在 .NET Framework 4.x 上的存量系统。`netstandard2.0` 是覆盖面最广的选择。但框架的一部分能力天然绑定新运行时生态：ASP.NET Core 中间件、EF Core 6、CAP 8 等，无法在 `netstandard2.0` 上实现。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 全部 `netstandard2.0`** | 覆盖面最大；一致性最好 | 与 ASP.NET Core / EF Core 6 / CAP 8 相关的能力全部做不了，等于砍掉半个框架 |
| **B. 全部 `net6.0`** | 一致性最好；可用全部新特性 | 直接排除 net framework 4.x 存量项目，而这些正是 `Bing.FreeSQL` 这类库的目标用户 |
| **C. 默认 `netstandard2.0`，例外覆盖 `net6.0`**（本方案） | 覆盖面与能力兼得 | 同一仓库两套 TFM；"这个包能不能被我的项目引用"要查才知道；每新增一个 net6-only 能力都在抬升消费者下限 |
| **D. 多目标 (`netstandard2.0;net6.0`)** | 两边都覆盖 | 每个工程都要 `#if` 分支，维护成本过高，且包体积翻倍 |

## 决策

采用 **方案 C**：

- **默认 `netstandard2.0`**（`framework.props`），最大化可复用性，可被 net framework / net core 引用；
- 与 ASP.NET Core 强耦合、或依赖 EF Core 6 / CAP 8 的工程**覆盖为 `net6.0`**：`Bing.AspNetCore.*`、`Bing.Permissions`、`Bing.Events` 等；
- 历史遗留：`Bing.Events.Cap.MySql` 为 `netstandard2.1`（CAP 2.6 源码镜像）。

## 后果

**正面**

- 老项目（`netstandard2.0` / net framework）也能用上 FreeSQL、`Bing.Data.Sql`、缓存等一批能力；
- 同时不妨碍新项目用全套 ASP.NET Core 与 EF Core 6 能力。

**负面**

- **"为什么这个包引用不到？"**——同一个仓库两套 TFM，引用前必须查清单，心智负担实打实。
- 新增一个只能用 net6 的特性，会**静默抬升**下游消费者的最低版本要求。
- 测试工程要同时跑 `net8.0;net6.0` 两个 TFM（`framework.tests.props`），CI 时间与矩阵复杂度增加。

**中性**

- `FreeSQL` 支是 `netstandard2.0`、EF Core 支是 `net6.0`——这直接决定了老工程选型时只能选前者，构成 [能力矩阵](../../getting-started/能力矩阵.md) 里最硬的一条约束。

## 相关文档

- [能力矩阵 §2](../../getting-started/能力矩阵.md)（各支 TFM 差异）
- [工程化文档](../../operations/工程化文档.md)（props 分层与 CI 多 TFM 矩阵）
- [设计思路文档 §4.9](../设计思路文档.md)
