# Bing.NetCore — 文档导航

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 内容核对：2026-10-13｜ 版本历史见 [发行说明](ReleaseNotes.md)

**中文译文** ｜ [中文主入口](README.md)

本框架的参考文档以**简体中文**编写。本页面是该文档集的**导航地图**：说明每份文档回答什么问题，
帮助你直接跳转到正确文件（浏览器通常可以较好地翻译 Markdown，代码和类型名称显然无需翻译）。

如果只读一份文档，请阅读使用文档。如果只读一个*章节*，请阅读注册入口列表——本框架中最常见的困惑，
都来自猜错 `AddXxx` 名称。

**目录结构。** `docs/` 按主题组织（不再是平铺目录）。下表中的路径已包含对应子目录：

| 目录 | 内容 |
| --- | --- |
| `getting-started/` | 使用文档、ORM 能力矩阵、包索引、术语表、API 速查、ABP 迁移、问题排查 |
| `architecture/` | 架构、设计思路、子系统深挖、事件与消息、已知缺口、`adr/`（10 份决策记录） |
| `guides/` | 领域建模、扩展、配置、最佳实践、横切能力、组件包、`recipes/` |
| `modules/` | 跨包模块指南，覆盖核心启动、Web / MVC、动态 API、领域 / 工作单元、数据访问、缓存、事件、安全、可观测性、外围集成、工程测试和参考宿主 |
| `operations/` | 日志与可观测性、安全、性能、测试、工程化 / 发版、资产索引 |
| `sql/` · `testing/` · `migrations/` | 数据访问深挖、集成测试、跨版本升级 |
| `api/` | **由 docfx 生成，不要手工编辑。** 手工维护的索引位于 `getting-started/API速查索引.md` |

---

## 1. 从这里开始

| # | 文档 | 回答的问题 |
| --- | --- | --- |
| 1 | [使用文档](getting-started/使用文档.md) | **如何安装和使用？** 安装、最小宿主、核心概念（模块系统、约定式 DI、统一响应、异常、当前用户 / 租户）、DDD 分层、数据访问、Web / JWT / 多租户 / AOP、横切能力和配置。§3.3 是精简的注册入口速查。 |
| 2 | [架构文档](architecture/架构文档.md) | **代码结构如何组织，模块之间依赖什么？** 六层概览、根据*实际*项目引用生成的模块依赖图、契约与实现边界、部署形态、模块启动序列、约定式 DI 自动注册、读取路径与异常管道、51 个工程的模块清单（§11），以及完整注册入口列表（§12，包含命名空间、全部重载和误用清单）。`docs/images/` 中有 6 张图。 |
| 3 | [设计思路文档](architecture/设计思路文档.md) | **为什么这样设计？** 设计目标、至少 10 项关键决策及其权衡、四条数据流（写入 / 读取 / 事件 / 异常）、扩展点，以及与 ABP 的比较。 |
| 4 | [子系统深挖](architecture/子系统深挖.md) | **复杂内部机制如何工作？** CAP 事件总线（两级发布 + Outbox 事务一致性）的代码级分析、FreeSQL 仅提供 MySQL Provider 的原因，以及 `Bing.Data.Sql` 方言 / Provider 路由链。每项结论都带有文件、命名空间和成员签名。 |

## 2. 选择并串联

| 文档 | 回答的问题 |
| --- | --- |
| [能力矩阵](getting-started/能力矩阵.md) | **应该选择哪种 ORM？** 从 18 个维度比较 EF Core / FreeSQL / Dapper+`Bing.Data.Sql`（工作单元、`IStore`、软删除、审计、乐观并发、多租户、批量、跨库、存储过程、事务、迁移、异步等），并提供 3 分钟决策树和 9 项已知限制。 |
| [术语表](getting-started/术语表.md) | **这个词是什么意思？** 框架专用词汇和多义词，列出真实类型名及 6 组容易混淆的术语（`IStore` 与 `IRepository`、两种不同的 `AddMySqlUnitOfWork`、`Level` 与 `[DependsOnModule]` 等）。 |
| [abp-migration](getting-started/abp-migration.md) | **熟悉 ABP Framework，如何对应？** 逐类型映射（🟢 同名 / 🔵 改名 / 🟡 同名但含义不同 / 🔴 无对应）、三个陷阱和 8 步迁移清单。 |
| [最佳实践](guides/最佳实践.md) | **怎样正确编写？** 12 组 ✅/❌ 对照，覆盖模块排序、非 Web 宿主的 `UseBing()`、DI 生命周期倒置、跨 ORM 事务、消息幂等、硬编码凭据等。 |
| [FAQ 与排错](getting-started/FAQ与排错.md) | **为什么会出错？** 最常见的陷阱，以及每个问题的正确做法和源码证据。 |

## 3. 运维与维护

| 文档 | 回答的问题 |
| --- | --- |
| [安全指南](operations/安全指南.md) | **安全边界是什么？** 凭据管理（包括当前已提交内容的精确盘点）、两条 JWT 校验路径及其差异、SQL 注入与 `BINGSQL002` 分析器、日志脱敏和租户隔离，并明确说明框架**不提供**什么。 |
| [性能指南](operations/性能指南.md) | **哪里可以节省时间？** 数据取自 `artifacts/benchmarks/` 中的真实 BenchmarkDotNet 运行结果：映射缓存命中与冷启动（**17×**）、已构建 Builder 的重复渲染（**约 60×**）、批量写入的非线性。包含热点路径缓存图和 6 个陷阱。 |
| [测试指南](operations/测试指南.md) | **如何编写和运行测试？** 测试工程布局、`Bing.Test.Shared` 辅助工具、集成测试门控变量、公开 API 门禁（RS0016/17/18/26）和 `eng/ci` 脚本。 |
| [工程化文档](operations/工程化文档.md) | **仓库如何构建和发布？** 解决方案与 props 分层、统一输出目录、公开 API 基线、基准测试命令、AppVeyor 阶段和 GitHub Actions、手工发布 / NuGet 打包流程、代码模板和产物目录。 |
| [架构决策记录](architecture/adr/README.md) | **为什么这样决定？** 10 份决策记录，每份包含背景、**被否决的备选方案**、决策和后果（包括负面后果）。 |
| [迁移指南](migrations/README.md) | **如何跨版本升级？** 7.0.0 的已知破坏性变更（Dapper 事务 / 连接收敛、SQL Fluent API 收敛）、目标框架事实、注册入口陷阱、诚实的范围声明和升级验收清单。 |
| [ReleaseNotes](ReleaseNotes.md) | 框架发行历史。 |
| [资产索引](operations/资产索引.md) | `ai_docs/`、`artifacts/`、`templates/` 分别是什么，是否是当前内容，以及如何阅读。 |

## 4. 查找具体内容

| 资源 | 说明 |
| --- | --- |
| [包索引](getting-started/包索引.md) | **按任务反查**：“我想做 X → 安装这些包”，并提供全部 70 个可打包工程的一句话摘要表。 |
| [模块文档](modules/README.md) | 12 篇跨包模块文档，说明各模块的实现、用法、配置与限制。 |
| [文档与代码对照清单](operations/文档与代码对照清单.md) | 当前实现覆盖情况、过时或缺失的文档，以及明确标记为未实现或未验证的项目。 |
| [API 速查索引](getting-started/API速查索引.md) | 按模块手工维护的**命名空间 → 类型**索引。`docs/api/` 下的逐方法 API 参考由 **docfx 生成，不要手工编辑**。完整站点由 `.github/workflows/docfx.yml` 构建；本地可使用 `docfx ./docs/docfx.json --serve` 预览。元数据仅覆盖 `framework/src`。 |
| [选型向导](选型向导.html) | 交互式单页工具（本地打开）：回答几个问题，获取包清单和可复制的启动代码。 |
| [Cheat sheet](cheatsheet.html) | 从实体定义到可运行 CRUD API 的一页纸速查。 |
| [recipes/](guides/recipes/README.md) | 端到端场景代码：EF Core CRUD、CAP 事件 + Outbox、多租户、RBAC、支付、批量写入和报表。 |
| [组件库文档](guides/组件库文档.md) | 支付、第三方 OAuth、缓存适配器、日志 Sink、SQL 跟踪和分析器，并为每项明确标注状态。 |
| [横切能力文档](guides/横切能力文档.md) | 使用文档 §8 的扩展版：邮件、文本模板、锁、本地化，以及两个事件总线包并存的原因。 |
| 包级 `README.md` | 70 个包各自都有一份；它会嵌入 `.nupkg` 并显示在 nuget.org 上。 |

### 模块文档（12 篇）

| 模块 | 一句话简介 |
| --- | --- |
| [核心启动与依赖注入](modules/core.md) | 模块发现、依赖加载、约定注册、AOP 与异常基础设施。 |
| [Web 与 MVC](modules/web.md) | ASP.NET Core 启动、统一响应、异常处理与 Razor 扩展。 |
| [动态 API](modules/dynamic-api.md) | 将 `IAppService` 约定公开为 HTTP API，并提供运行时代理与 WebApiClientCore 两种客户端。 |
| [领域与工作单元](modules/domain.md) | DDD、应用服务、审计、验证、提交与对象映射。 |
| [数据访问](modules/data-access.md) | EF Core、FreeSQL、Dapper、SQL Builder 与各数据库 Provider。 |
| [缓存](modules/caching.md) | 统一缓存抽象、Redis 适配器、EasyCaching、命名空间、拓扑、分片和 Hybrid 协调。 |
| [事件与消息](modules/events.md) | 进程内发布、CAP 消息及领域事件分发的边界。 |
| [安全与多租户](modules/security.md) | JWT、授权、权限、安全服务及租户解析和切换。 |
| [日志与诊断](modules/observability.md) | Bing 日志、Serilog、Exceptionless、SkyAPM 与 MiniProfiler。 |
| [外围集成](modules/integrations.md) | 邮件、模板、本地化、锁、支付与 OAuth 的注册和限制。 |
| [工程与测试](modules/development.md) | 分析器、生成器、构建、测试和文档校验工具。 |
| [参考宿主](modules/reference-apps.md) | admin 分层参考实现及 WebAPI、Hangfire、WinForms 示例。 |

### 本次文档更新清单

逐文件的操作类型、内容摘要和验证边界见正式清单：[operations/本次文档更新清单.md](operations/本次文档更新清单.md)。

## 5. 仓库级文件

| 文件 | 内容 |
| --- | --- |
| [../README.en.md](../README.en.md) | 原英文主页的中文译文（本入口的上级入口）。 |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献指南——**仅中文**。硬性要求：所有文本文件使用 UTF-8，XML 文档注释使用中文。 |
| [../AGENTS.md](../AGENTS.md) | 本仓库 AI 编码工具的执行规则——**仅中文**。 |
| [CHANGELOG.md](CHANGELOG.md) | **文档系统自身**的变更记录（不是框架变更记录）。 |
| [samples/README.md](../samples/README.md) · [modules/admin/README.md](../modules/admin/README.md) | 可运行示例和完整分层参考实现——**仅中文**。 |

---

## 语言约定

所有参考文档、XML 文档注释和提交消息均以中文为权威语言。本文件已按本轮要求译为中文，并保留 `.en` 文件名以兼容原有链接；
两者不一致时，**以中文文档为准**。

欢迎贡献*新的*英文翻译，但英文翻译必须记录其对应的中文版本；悄然漂移的部分翻译不如没有翻译。
