# Bing.NetCore 文档导航

**简体中文** ｜ [English](README.en.md)（英文导航）
> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](ReleaseNotes.md)

> 本文是全部文档的**入口索引**。按「你是谁、想解决什么」组织，点击即可跳转。
>
> 若你只想快速开始：直接看《使用文档》第 2、3 章；若想理解框架全貌：看《架构文档》。

---

## 0. 目录结构

`docs/` **不再平铺**——除 5 个入口类文件外，全部文档按主题收进子目录。找文档时先按分组定位，再用本节表格精确定位。

```
docs/
├── README.md / README.en.md     导航 hub（中 / 英）
├── index.md                     docfx 站点首页（由根 README 派生，勿手编）
├── toc.yml / docfx.json         docfx 导航与配置
├── CHANGELOG.md                 文档体系自身的变更记录
├── ReleaseNotes.md              框架发行说明
├── 选型向导.html · cheatsheet.html   独立 HTML 工具页（不进 docfx，浏览器直接打开）
├── getting-started/             入门：使用 / 选型 / 查包 / 查词 / 查 API / 迁移过来 / 排错
├── architecture/                架构：结构与依赖 / 设计动机 / 子系统深挖 / 事件机制 / 功能缺口
│   └── adr/                     架构决策记录（10 份）
├── guides/                      指南：建模 / 扩展 / 配置 / 实践 / 横切能力 / 组件库
│   └── recipes/                 端到端配方（6 篇）
├── operations/                  运维与质量：日志可观测 / 安全 / 性能 / 测试 / 工程化 / 资产
├── sql/                         数据访问专项（10 篇）
├── testing/                     集成测试
├── migrations/                  跨版本升级
├── api/                         ← docfx 自动生成的 API 参考（生成物，勿手编）
├── ai-workflow/ · plans/        AI 协作资产（历史快照，见资产索引）
└── images/                      架构图（PNG + SVG 源文件）
```

> **两条硬约定**（已由 `eng/ci/check-docs.py` 的「顶层归位」检查兜住）：
> ① 新增文档**必须放进对应主题子目录**，不要在 `docs/` 顶层新建 `.md`；
> ② `docs/api/` 是 **docfx 的生成目录**，不要往里写手工内容——手工的 API 索引在 [getting-started/API速查索引.md](getting-started/API速查索引.md)。

---

## 1. 按角色选文档

| 你的角色 | 建议阅读顺序 |
| --- | --- |
| **初次接触 / 想跑起来** | [使用文档](getting-started/使用文档.md)（§2 安装 → §3 快速开始 → §3.3 注册入口速查） |
| **要基于框架做业务开发** | [使用文档](getting-started/使用文档.md)（§4 核心概念、§5 分层与 DDD、§6 数据访问、§7 Web 与认证、§8 横切能力） |
| **要理解框架结构与依赖** | [架构文档](architecture/架构文档.md)（§1 分层、§2 模块依赖图、§11 模块清单、§12 注册入口完整清单） |
| **要做架构决策 / 二次开发** | [设计思路文档](architecture/设计思路文档.md)（关键决策与权衡、扩展方式）→ [子系统深挖](architecture/子系统深挖.md) |
| **要选 ORM / 比较数据访问能力** | [能力矩阵](getting-started/能力矩阵.md)（EF Core / FreeSQL / Bing.Data.Sql+Dapper 三支 × 18 项能力对照 + 场景推荐） |
| **想知道框架还有哪些没做完** | [功能完成度与已知缺口](architecture/功能完成度.md)（逐条缺口清单：静默降级、被注释的能力、死代码、未验证的 Provider） |
| **读文档时被术语卡住** | [术语表](getting-started/术语表.md)（`IStore` 不是 `IRepository`、`Level` vs `[DependsOnModule]`、两套 `IMessageEventBus`…） |
| **想知道"怎么写才对"** | [最佳实践](guides/最佳实践.md)（12 组 ✅/❌ 对照 + 反模式速查表） |
| **从 ABP 迁移过来** | [ABP 迁移对照](getting-started/abp-migration.md)（同名 / 换名 / 无对应，含迁移步骤清单） |
| **排查问题 / 踩坑** | [FAQ 与排错](getting-started/FAQ与排错.md) |
| **性能问题 / 做优化** | [性能指南](operations/性能指南.md)（基于实测基准的缓存命中、Builder 复用、批量写入建议 + 已知陷阱） |
| **上线前安全自查** | [安全指南](operations/安全指南.md)（凭据管理、JWT 双校验路径、SQL 注入、日志脱敏、多租户隔离 + 检查清单） |
| **要建实体 / 聚合根 / 领域事件** | [领域建模指南](guides/领域建模指南.md)（继承体系选型、横切接口谁填充、⛔ 领域事件不会自动派发、`IStore` vs `IRepository`、真实建模示例） |
| **要给框架加东西（新模块 / 新 Provider / 换内置实现）** | [扩展指南](guides/扩展指南.md)（扩展点地图、三支 Provider 模板、⚠ `IConventionalRegistrar` 是空壳、自定义过滤器为何不生效） |
| **某个行为想调，不知道改哪** | [配置参考](guides/配置参考.md)（Options 全清单 + 默认值；⚠ 真正绑定配置节的只有 `JwtOptions`） |
| **要接日志 / 追踪 / 诊断面板** | [日志与可观测性](operations/日志与可观测性.md)（Serilog 装配、TraceId 贯通、SkyAPM、⚠ 请求日志无脱敏、框架无 HealthChecks） |
| **想知道某个决策为什么这样定** | [架构决策记录 ADR](architecture/adr/README.md)（10 份决策，含被否决的备选方案与负面后果） |
| **跑测试 / 写新测试** | [测试指南](operations/测试指南.md)（工程布局、门控变量、`Bing.Test.Shared`、RS00xx 门禁、eng 脚本） |
| **只知道包名，不知道装哪个** | [包索引](getting-started/包索引.md)（按「我要做 X」反查 67 个包） |
| **不确定组合，想要交互式推荐** | [选型向导](选型向导.html)（浏览器打开，几个问题 → 包清单 + 启动代码） |
| **想要一页纸速查 / 打印贴墙** | [Cheat Sheet](cheatsheet.html)（从零到 CRUD API 的全部代码） |
| **要端到端的完整场景代码** | [recipes 配方集](guides/recipes/README.md)（CRUD、CAP 事件、多租户、RBAC、支付、批量报表） |
| **要用边缘组件（支付 / OAuth / 缓存 / 日志 Sink / 链路追踪）** | [组件库文档](guides/组件库文档.md)（9 个包，含状态徽章与"入口不在 `IServiceCollection`"的提醒） |
| **深入某个横切能力（邮件 / 模板 / 锁 / 本地化 / 事件总线）** | [横切能力文档](guides/横切能力文档.md)（《使用文档》§8 的详解版） |
| **要发事件 / 用 CAP / 领域事件** | [事件与消息文档](architecture/事件与消息文档.md)（四套机制对照；⛔ 领域事件不会派发、⚠ `send:false` 事务外静默丢事件） |
| **跨版本升级** | [迁移指南](migrations/README.md)（已知破坏性变更 + 升级验收清单） |
| **维护仓库（构建 / CI / 发版 / 基准 / eng 脚本）** | [工程化文档](operations/工程化文档.md) + [贡献指南](../CONTRIBUTING.md) |
| **看懂 `ai_docs/` / `artifacts/` / `templates/`** | [资产索引](operations/资产索引.md)（性质、是否入库、时效性声明） |
| **看参考实现 / 跑示例** | [samples/README.md](../samples/README.md)（3 个最小工程）→ [modules/admin/README.md](../modules/admin/README.md)（完整分层业务） |
| **贡献代码** | [CONTRIBUTING.md](../CONTRIBUTING.md) |
| **查 API** | [API 速查索引](getting-started/API速查索引.md)（手工索引）＋ docfx 自动生成的完整站点（push 后自动部署，本地 `docfx ./docs/docfx.json --serve`） |

---

## 2. 核心文档（四份，互相引用）

| 文档 | 定位 | 主要内容 |
| --- | --- | --- |
| [使用文档](getting-started/使用文档.md) | **怎么用**（面向使用者） | 环境要求与 NuGet 安装、最小 Web API 上手、核心概念（模块系统 / 约定式 DI / 统一响应 / 异常 / 当前用户与租户）、分层与 DDD、EF Core / FreeSQL / Dapper 三种数据访问、Web 与 JWT / 多租户 / AOP、10 类横切能力、配置参考。**§3.3 是注册入口精简速查**。 |
| [架构文档](architecture/架构文档.md) | **长什么样**（面向理解结构） | 六层架构总览、**基于真实工程引用的模块依赖图**、组件职责边界、契约↔实现接口边界、部署与运行形态、模块启动序列、约定式 DI 自动注册、读路径与异常管道、**模块清单（51 个工程一句话职责）**、**注册入口完整清单（§12，含命名空间与全部重载 + 防误用清单）**。含 6 张架构/数据流图（`docs/images/`）。 |
| [设计思路文档](architecture/设计思路文档.md) | **为什么这样设计** | 设计目标、分层与模块依赖、≥10 项关键决策及权衡、四条数据流（写/读/事件/异常）、扩展方式、与 ABP 的对比、上手建议。 |
| [子系统深挖](architecture/子系统深挖.md) | **代码级实现细节** | CAP 事件总线两层发布与 Outbox 事务一致性；FreeSQL 为何仅 MySql Provider 及扩展其他数据库的模板；`Bing.Data.Sql` 方言/Provider 路由链（`SqlProviderRuntime` → `ActivatorUtilities`）。均标注文件、命名空间与成员签名。 |

> 这四份文档头部均已内置「配套文档」导航，可两两跳转。

---

## 3. 专项文档

| 文档 | 说明 |
| --- | --- |
| [能力矩阵](getting-started/能力矩阵.md) | **数据访问选型**：EF Core / FreeSQL / Bing.Data.Sql+Dapper 三支在 TFM、数据库覆盖、工作单元、`IStore`、软删除、审计、乐观锁、多租户、批量、跨库、存储过程、事务、迁移、异步等 18 个维度上的对照总表；含三分钟选型决策树、各支注册入口与实现位置、已知限制 9 条、按场景推荐、混用注意事项、三支都不行的事。 |
| [功能完成度与已知缺口](architecture/功能完成度.md) | **如实记录框架没做完的部分**（只描述、不改代码）：FreeSQL 异步 Provider **65 处占位且静默降级为同步阻塞**、Dapper 支**框架自认的 10 处** `ProviderImplementationGap`（附"Auto 策略会自动降级"这一可实际利用的事实）、Lambda 多源查询 6 处不支持、`Bing.EasyCaching` 注册扩展**整文件注释**、`admin` 参考实现**三块整段注释**、全仓 25 处被注释的注册调用、零引用的死包（`Bing.Events.Cap.MySql` / `Bing.EventBus.Abstractions`）、Oracle 与 Doris 未验证。含**自行复核方法与缺口的三种处置方式**。 |
| [术语表](getting-started/术语表.md) | **Glossary**：模块与启动、依赖注入、领域层、数据访问、事件消息、横切、Bing.Data.Sql 专有词的中文解释与对应类型；末尾 6 组**易混词辨析**（`IStore` vs `IRepository`、工作单元 vs 事务、本地 vs 消息事件、`Level` vs `[DependsOnModule]`、软删除过滤器 vs `IDataFilter`、两个 `AddMySqlUnitOfWork`）。 |
| [最佳实践](guides/最佳实践.md) | **怎么写才对**：12 组 ✅/❌ 对照（模块顺序、非 Web 宿主 `UseBing()`、DI 生命周期倒挂、实体注入仓储、循环提交、跨 ORM 事务、消息幂等、`[EventHandler]`、凭据硬编码、FreeSQL 异步退化、公开 API 基线…），规则分 🔴 必须 / 🟡 建议，末尾反模式速查表。 |
| [性能指南](operations/性能指南.md) | 基于 `artifacts/benchmarks/` **实测数据**（net8.0 / RyuJIT AVX2 / BDN 0.14.0）的性能建议：映射缓存命中 vs 冷启动（**17×**）、Builder 重复渲染（**约 60×**）、`Clone()` 开销、调试 SQL 渲染、批量写入的非线性（1000 实体 **127 ms / 463 MB**）；含缓存热路径地图、6 个已知陷阱、基准运行命令与优化清单。 |
| [安全指南](operations/安全指南.md) | 五个方面的安全边界：凭据管理（含仓库内现状清单与处置顺序）、**JWT 两条校验路径的差异**（标准中间件校验有效期 / 自定义校验器不校验 `exp`·`nbf`，`ThrowEnabled=true` 时完全无过期检查）、SQL 注入与 `BINGSQL002`、日志脱敏与外发、多租户隔离；附上线前检查清单。诚实列出框架**未提供**的防护（XSS / CSRF / 速率限制 / IP 白名单）。 |
| [领域建模指南](guides/领域建模指南.md) | **DDD 建模规范**：完整继承体系图（`DomainObjectBase` → `EntityBase` → `BasicAggregateRoot` → `AggregateRoot`）；`EntityBase` 与 `AggregateRoot` 的**确切差异**（谁自带 `IVersion`、谁不带软删除/审计）；横切接口"谁声明、谁填充"；⛔ **领域事件不会被自动派发**（`DispatchAsync` 无生产调用）；`IStore` 与 `IRepository` **两者都存在**且后者是超集；`IQueryStore` 读侧；应用服务与 DTO 映射；变更追踪源生成器；真实聚合根源码示例；10 条建模陷阱。 |
| [扩展指南](guides/扩展指南.md) | **二次开发**：扩展点地图；新增模块（两个基类差异、`[DependsOnModule]` 与 `ModuleLevel`、**排序规则是 `Level→Order→FullName` 而非拓扑**）；**新增数据访问 Provider 的三套模板文件清单**（EF 5 个 / FreeSQL 5 个 / Dapper 16 个 `.cs`）与 `SqlProviderProfile` 全字段；约定式 DI（`[Dependency]` 全参数、扫描范围与排除）；自定义分析器与源生成器；异常转换器、全局过滤器、租户解析器。含 3 条"别照这个扩展"的警告。 |
| [配置参考](guides/配置参考.md) | **Options 与配置节总表**：先分清三类配置来源（**真正绑定配置节的只有 `JwtOptions` 一个**，其余是代码配置或不绑定）；`AddBing()` 的 `BingOptions` **是空类**（没有 `EnableAop`）；约 30 个 Options 的类型 / 命名空间 / 配置节 / 关键默认值；`appsettings.json` 与 `skyapm.json` 真实结构；连接串键名与**未命中静默回退**；环境变量现状；6 条常见误区。 |
| [日志与可观测性](operations/日志与可观测性.md) | 五个层次（日志管道 / 结构化字段 / 请求留痕 / 链路标识 / 追踪面板）：`AddBingLogging` 与链式 `ILog`；Serilog 管道**手工装配**与级别映射（**未知级别静默变 `Warning`**）；`UseBingSerilogEnrichers` **在 `Bing.AspNetCore.Serilog` 而非 `Logging.Serilog`** 且**必须在 `UseAuthentication` 之后**；请求/响应日志**无任何脱敏**；`CorrelationId` / `TraceIdContext` 与 CAP 消费端 `bing-trace-id`；SkyAPM `AddSqlQuery`；MiniProfiler / CAP / Hangfire Dashboard；⚠ **框架无 HealthChecks**。 |
| [测试指南](operations/测试指南.md) | 测试工程布局（47 个工程 + xunit/Shouldly/Moq 技术栈）、三种 props 的作用、跑测试命令与 filter、集成测试门控变量表、`Bing.Test.Shared` 提供的能力清单、新增 Provider 测试的五件套模板、**RS0016/17/18/26 公开 API 门禁**（仅 7 个工程有基线）、三个 `eng/ci` 脚本与 ProviderEvidence CLI、产物目录解读。 |
| [abp-migration.md](getting-started/abp-migration.md) | **从 ABP 迁移对照**：模块、仓储、工作单元、审计并发、事件总线、横切服务的 ABP↔Bing 类型对照（🟢 同名 / 🔵 换名 / 🟡 同名异义 / 🔴 无对应），三个"最容易卡住"的差异，8 步迁移清单，不建议迁移的场景。 |
| [横切能力文档](guides/横切能力文档.md) | 《使用文档》§8 的**详解版**：邮件（Emailing / MailKit / 两套 `AddMailKit` 的命名空间陷阱）、文本模板（无注册入口，手动装配）、锁、本地化、**事件总线两套并存选型**（`Bing.EventBus` 遗留 vs `Bing.Events` 现行）、`Bing.Events.Cap.MySql` 现状、缓存三实现对比。 |
| [事件与消息文档](architecture/事件与消息文档.md) | **四套事件机制的完整对照**（本项目最易踩的区域之一）：① 进程内事件 `ISimpleEventBus`（已实现但**全仓无调用者**）② CAP 集成事件 `IMessageEventBus`（✅ 现行）③ DDD 领域事件（⛔ **派发器已注册但生产代码零调用**，`PublishEventsAsync` 是空钩子）④ 遗留总线 `Bing.EventBus*`（零生产引用）。含各自的抽象与注册入口、**完整发布链路**、Outbox 事务一致性落点、⛔ **`send:false` 在事务外静默丢事件**、CAP 重试/分组/序列化/TraceId、**类型名撞车对照表**（CS0104 / CS0433）、选型决策树、admin 一次登录的两条路径解剖、12 条陷阱、自行复核方法。 |
| [组件库文档](guides/组件库文档.md) | **9 个包的使用说明**（支付、**第三方 OAuth 登录 16 提供方**、业务基础类型、EasyCaching、FreeRedis、Exceptionless Sink、Mvc.UI、SkyAPM SQL 追踪、两个 Analyzers）。每个包标注状态徽章（✅ 可用 / 🔌 非标准入口 / 🧩 需手动装配 / ⚠ 入口缺失）。 |
| [迁移指南](migrations/README.md) | **升级导航**：7.0.0 已知破坏性变更（Dapper 事务/连接收敛、SQL Fluent 收敛）、目标框架与 SDK 事实、注册入口陷阱速查、覆盖边界声明、升级验收清单。 |
| [包索引](getting-started/包索引.md) | **按用途反查包**：从「我要做 X」找该装哪些包（15 类场景），以及全部 67 个可发包的一句话速览表（含 TFM、分层、上游依赖、反向下游包）。配合 [选型向导](选型向导.html) 使用效果最佳。 |
| [recipes 配方集](guides/recipes/README.md) | **端到端可运行场景代码**：① `crud-efcore`（EF Core + MySQL 完整 CRUD，含 DTO 映射、分页、软删除、`IVersion` 并发冲突处理）② `cap-event-outbox`（本地事件 + CAP 消息 + Outbox 事务一致性）③ `multi-tenancy`（租户解析链 + 三种租户 Store + ⚠ 自定义过滤器必须自己套用）④ `rbac-authorization`（JWT 签发校验 + 全局 Authorize + 权限树）⑤ `payment`（`AddPay` 配置与支付宝/微信下发-回调闭环）⑥ `bulk-and-report`（批量写入与报表查询的性能取舍）。每个配方含完整代码与验收清单。 |
| [选型向导](选型向导.html) | **交互式 HTML 工具**（浏览器打开即可用，无需部署）：回答几个问题（宿主类型 / 要不要全功能 ORM / 目标数据库 / 是否需要分布式事件 / 是否多租户…）→ 自动输出**该装哪几个包**与**可直接复制的 `Program.cs` / `AppModule` 代码**。纯粹的静态页面，数据固化自 [包索引](getting-started/包索引.md)。 |
| [Cheat Sheet](cheatsheet.html) | **一页纸速查**（浅色、适合 `Ctrl+P` 打印贴墙）：实体/聚合根模板、`IStore` 仓储写法、应用服务模板、注册入口总表、常用调用片段、十个最容易踩的坑。所有 API 均已按源码核准，可直接复制。 |
| [API 速查索引](getting-started/API速查索引.md) | **手工维护**的按模块 API 索引（「某个能力对应哪个命名空间 / 类型」）。与 docfx 生成物的分工：逐方法级参考由 docfx 生成在 `docs/api/`（**生成物，勿手编**），本文补其缺少的语义分组。docfx 本地预览：`docfx ./docs/docfx.json --serve`；注意元数据只覆盖 `framework/src`，`components/src` 不在内。 |
| [sql/](sql/README.md) 目录（10 篇） | `Bing.Data.Sql` 专项：Provider 能力矩阵、PostgreSQL 函数、Migration 指南、SqlQuery 用法与 Lambda 用法，以及引擎设计稿（变更 API 生命周期 / 批量执行 / 子句设计 / 结构化表引用与跨库查询）。 |
| [testing/](testing/database-integration-tests.md) 目录（2 篇） | 数据库集成测试的门控与运行方式（SQLite 本地集成、外部库通过环境变量门控）＋ 集成测试总览。 |
| [ReleaseNotes.md](ReleaseNotes.md) | 框架发行说明（版本历史）。 |
| [migrations/sql-transaction-api-vNext.md](migrations/sql-transaction-api-vNext.md) | SQL 事务 API 迁移到 7.0.0 的详细步骤（已删除 API、事务/连接改法、跨 ORM 边界、验收）。 |

---

## 4. 工程协作文档

| 文档 | 说明 |
| --- | --- |
| [工程化文档](operations/工程化文档.md) | **维护者手册**：解决方案与 props 分层（`common` / `framework` / `version` / `version.dev` / `asset/props`）、统一输出目录、**公开 API 门禁（RS0016/17/18/RS0026 与 PublicAPI 基线）**、测试与集成门控变量、基准测试命令、AppVeyor 5 个 lane 与 GitHub Actions、`eng/ci` 三个脚本与 ProviderEvidence CLI、**发版与 NuGet 打包手工流程（含发版清单）**、代码模板 `templates/`、产物目录 `artifacts/`。 |
| [adr/README.md](architecture/adr/README.md) | **架构决策记录（ADR）**：把关键决策拆成一决策一文件，记录背景 / **被否决的备选方案** / 决策 / 后果（**负面必写**）。目前 10 份（模块两阶段启动、约定式 DI、多 ORM 并存、统一响应、UoW+AOP、多租户解析链、横切约定注册、异常体系、双目标框架、Roslyn 生成器）。想在"当初为什么这么定"时查阅。 |
| [资产索引](operations/资产索引.md) | 非产品文档类资产的说明：`ai_docs/`（约 79 md 的 AI 任务档案，**历史快照需谨慎**）、`artifacts/`（不入库的过程产物）、`templates/`（CodeSmith 代码生成器，年代较早）、`docs/plans/` 与 `docs/ai-workflow/`。含"还能不能信"的时效性声明与维护约定。 |
| [CHANGELOG.md](CHANGELOG.md) | **文档体系自身的变更记录**（不是框架发行说明）：新增/重构了哪些文档、修正了哪些事实错误、建立了哪些维护约定。**改文档前先读它的 §1 维护约定。** |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | **贡献指南**：构建、运行测试（⚠ 路径为 `framework/tests/`）、代码规范（中文注释 / UTF-8）、公开 API 门禁处理、基准与 CI 脚本、新增 Provider 与改动的测试门槛、**文档维护约定表**。 |
| [../AGENTS.md](../AGENTS.md) | AI 工具（Copilot/Codex/Gemini/Claude 等）在本仓库的执行规范：默认中文、UTF-8 强制、`Bing.Data.Sql` 改动的测试覆盖门槛、提交前可追溯映射等。**贡献代码前必读**。 |
| [ai-workflow/README.md](ai-workflow/README.md) | AI 协作工作流（目标、协议、路由、各助手配置、排错）。 |
| [plans/chinese-comments-plan.md](plans/chinese-comments-plan.md) | 源码中文注释补齐计划与执行情况。 |

---

## 5. 配套资源

- **包级 README**：`framework/src/<工程>/README.md` 与 `components/src/<工程>/README.md`（共 67 份）。它们通过 `asset/props/package.props` 的 `PackageReadmeFile` **自动内嵌进 nupkg**，在 nuget.org 包页面直接展示。模板与结构规范见 [工程化文档 §7.1](operations/工程化文档.md)。
- **架构与数据流图**：`docs/images/`（6 张 PNG，附 SVG 源文件）——分层架构、写路径+事件 Outbox、模块启动序列、读路径、异常与统一响应管道、模块分层全景。
- **交互式工具（本地打开即可用）**：[选型向导](选型向导.html)（几个问题 → 包清单 + 可复制启动代码）、[一页纸 Cheat Sheet](cheatsheet.html)（打印版 API 速查）。两者都是零依赖静态 HTML，**不进 docfx 站点**（docfx 只收 `*.md` / `*.yml`），请在本地浏览器打开。
- **参考实现**：[`modules/admin/README.md`](../modules/admin/README.md)（12 个工程，领域 → 仓储 → 应用服务 → Web API 完整组织；两个宿主互斥，迁移/种子未接通，见文档第 5 节）。
- **示例**：[`samples/README.md`](../samples/README.md) —— `Bing.Samples.WebApi`（最小 Web 宿主骨架）、`Bing.Samples.Hangfire`（后台任务 + DI 打通 + 日志）、`Bing.Samples.Winform`（非 Web 宿主，⚠ 不在解决方案内）。
- **英文导航**：[`docs/README.en.md`](README.en.md) —— 中文文档的英文地图（**中文是权威版本**，英文只负责指路）。
- **文档一致性校验（改完文档请跑一遍）**：

  ```bash
  python eng/ci/check-docs.py              # 12 项检查：toc / YAML 结构 / 链接 / 顶层归位 / 包表 / index 同步 / 徽章 / 坏模式
  python eng/ci/sync-docs-index.py         # 改过根 README 后，重新派生 docs/index.md
  ```

  两者都已接入 CI：`.github/workflows/docs-lint.yml` 会在 **PR 阶段**跑（`docfx.yml` 只在 push 到主干时才跑，来不及拦 PR）。约定详见 [CHANGELOG.md §1](CHANGELOG.md)。
