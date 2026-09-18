# 贡献指南

> 参与 Bing.NetCore 开发前，请先阅读 **[AGENTS.md](AGENTS.md)**——它定义了 AI 工具与人类贡献者在本仓库必须遵守的规范（默认中文、UTF-8 强制、`Bing.Data.Sql` 的测试覆盖门槛等）。本文是它的可读补充。

---

## 1. 环境要求

- **构建 SDK**：由仓库根目录 [`global.json`](global.json) 固定（当前 **.NET SDK 8.0.424**）。请先 `dotnet --version` 确认一致，不要用系统默认的其它版本。
- **目标框架**：类库默认 `netstandard2.0`（最大化可复用性）；Web / 应用层为 `net6.0`。新增工程请沿用所在层的约定（见 `common.props`、`framework.props`、`framework.tests.props`）。
- **IDE**：Visual Studio 2022 / VS Code / JetBrains Rider（项目由 JetBrains 提供开源授权）。
- **编码**：所有文本文件统一 **UTF-8**。详见 `AGENTS.md` 的「UTF-8 规则（强制）」。

## 2. 构建

```bash
dotnet build Bing.All.sln
```

单工程构建：

```bash
dotnet build framework/src/Bing.Core/Bing.Core.csproj
```

版本号集中在 `version.props` / `version.dev.props`，不要在各 `.csproj` 里硬编码版本。

## 3. 运行测试

> ⚠ 测试工程在 **`framework/tests/`**，不是根目录的 `tests/`（后者只放 `runsettings` 模板）。

```bash
# 单元测试
dotnet test framework/tests/<测试工程>/<测试工程>.csproj

# 全量（较慢）
dotnet test Bing.All.sln
```

**测试工程组织**：

- 约 30 个工程，位于 `framework/tests/`，`*.Tests`（单元）与 `*.Tests.Integration`（集成）**成对出现**；
- 共享基建：`Bing.Test.Shared` / `Bing.TestShare(.MySql)`；
- `components/tests/Bing.Biz.Payments.Tests` **独立于 `Bing.All.sln`**，需单独跑；
- 框架为 xunit（V2/V3）+ VSTest runner + Shouldly / Moq / Coverlet。

**集成测试的门控规则（重要）**：

- SQLite 本地集成测试默认可跑，涉及数据库 Provider / 连接 / 执行器 / 查询工厂路径的改动**必须运行**。
- 外部数据库（SqlServer / MySql / PostgreSQL / Oracle / Doris）的测试通过环境变量 `RUN_INTEGRATION_TESTS` 或 Provider 专用环境变量（`RUN_MYSQL_INTEGRATION_TESTS` 等）门控，**不得使用生产数据库**。
- 连接串配置键为 `ConnectionStrings__<Provider>Connection`（双下划线）；`tests/runsettings/` 下有 `integration.*.runsettings.example` 模板，复制为 `integration.runsettings.local`（已忽略）后填自己的值。
- 详见 [docs/testing/database-integration-tests.md](docs/testing/database-integration-tests.md) 与 [docs/integration-testing.md](docs/testing/integration-testing.md)。

## 3.1 公开 API 与分析器门禁

`common.props` 把下列工程的 `RS0016 / RS0017 / RS0018 / RS0026` 提升为**编译错误**：

```
Bing.Data.Sql、Bing.Dapper.Core、Bing.Dapper.MySql、Bing.Dapper.Oracle、
Bing.Dapper.PostgreSql、Bing.Dapper.SqlServer、Bing.Dapper.Sqlite
```

处理规则：

| 规则 | 含义 | 动作 |
| --- | --- | --- |
| `RS0016` | 公开 API 未在基线声明 | 追加到 `PublicAPI.Unshipped.txt` |
| `RS0017` | 公开 API 被删除/改名 | 在该行前加 `*REMOVED*` |
| `RS0018` | 不应公开的 API | 收紧可见性 |
| `RS0026` | 可选参数规范问题 | 拆成显式重载（7.0.0 做过一轮治理） |

发版时把 `PublicAPI.Unshipped.txt` 并入 `PublicAPI.Shipped.txt` 并清空 Unshipped。分析器诊断号（如 `BINGSQL002`）另有一份基线：`Bing.Data.Sql.Analyzers/AnalyzerReleases.Unshipped.md`。

> ⚠ 其余工程**没有** API 门禁，公开 API 变化不会被拦截，请自行把关并在 [迁移指南](docs/migrations/README.md) 记录。

## 4. 代码规范

- **语言**：注释、日志、异常消息默认使用**简体中文**；标识符用英文。
- **编码**：读写文件必须显式指定 `UTF-8`（C# 用 `Encoding.UTF8`，Python/Node 同理）。禁止依赖 Windows ANSI/GBK。
- **命名与结构**：新增类型遵循所在模块的既有风格；跨层契约放领域层（如 `Bing.Ddd.Domain`），实现放基础设施/数据访问层。
- **注释**：配合 [docs/plans/chinese-comments-plan.md](docs/plans/chinese-comments-plan.md) 的中文注释补齐计划，新增公共 API 请补 XML 文档注释与中文说明。

## 5. 改动 `Bing.Data.Sql` 的额外门槛

`Bing.Data.Sql` 是跨 ORM 的通用 SQL 引擎，改动风险最高。`AGENTS.md` 要求：

- 修改公共接口、默认实现、Provider 分支、实体映射、缓存键、SQL 输出或跨库校验时，**必须同步增加直接单元测试**（禁止只依赖综合测试间接覆盖）。
- **SQL 输出测试必须断言完整 SQL 字符串**，不得只用 `Contains` 断言片段。
- 修改映射缓存、格式化热路径或 Builder 克隆逻辑时，需更新 `Bing.Data.Sql.Benchmarks` 基线或说明不适用原因。
- 提交前维护「最终生产符号 → 测试方法」的可追溯映射。

## 6. 新增数据库 Provider

框架采用「**Provider = 薄壳**」设计，新增一个数据库支持通常只需复制现有 Provider 工程：

- **FreeSQL**：复制 `Bing.FreeSQL.MySql` → 换 `FreeSql.Provider.*` 包 → `DataType.MySql` 改为目标库 → 重命名 `Add*UnitOfWork` 与 `IMap`。核心库 `Bing.FreeSQL` 无需改动。
- **Dapper / `Bing.Data.Sql`**：复制 `Bing.Dapper.SqlServer` 等 → 提供 `XxxDialect`（引用符）、`XxxBuilder`、`XxxSqlProvider`（`Key` 如 `bing.sqlserver`）→ 通过 `AddSqlProviderRuntime(new SqlProviderRuntime(...))` 注册。
- **EF Core**：复制对应 Provider → 提供 `Add*UnitOfWork` 扩展。

详细模板见 [docs/子系统深挖.md](docs/architecture/子系统深挖.md) 第 2.6 与第 3.5 节。

## 7. 提交与 PR

- 提交信息简明描述改动范围；一个 PR 聚焦一件事。
- 若改动影响公共 API、注册入口或架构分层，**同步更新文档**（见下节）。
- 破坏性变更请在 [docs/ReleaseNotes.md](docs/ReleaseNotes.md) 与（必要时）`docs/migrations/` 下补充说明。项目声明：出于成本考虑不保证 API 兼容，请在 Release Notes 中显式提示。
- **发版与 NuGet 打包**是手工流程（`framework/Publish.bat` / `components/Publish.bat`），完整发版清单见 [工程化文档 §7](docs/operations/工程化文档.md)。

## 7.1 基准测试与 CI 脚本

- 基准工程：`framework/tests/Bing.Data.Sql.Benchmarks`，支持 `--ci-smoke` / `--e2e-smoke` / `--filter` / `--artifacts`。
- CI 编排脚本在 `eng/ci/`（`Invoke-ProviderIntegrationTests.ps1`、`Invoke-SqliteContractTests.ps1`、`Invoke-ReleaseCandidateValidation.ps1`），证据校验 CLI 在 `eng/Bing.ProviderEvidence.Cli`。
- 用法、CI lane 划分与产物目录说明见 [工程化文档](docs/operations/工程化文档.md)。

## 8. 文档维护约定

文档与代码同等重要。改动时请一并维护：

| 改动内容 | 需要同步的文档 |
| --- | --- |
| 新增/变更注册入口（`AddXxx`/`UseXxx`） | [架构文档 §12](docs/architecture/架构文档.md)（完整清单）**与** [使用文档 §3.3](docs/getting-started/使用文档.md)（精简版）——两处需保持一致 |
| 新增/删除工程 | [架构文档 §2](docs/architecture/架构文档.md)（真实模块依赖图）、§11（模块清单）；**并创建该工程的 `README.md`**（会被打进 NuGet 包并在 nuget.org 展示，规范见 [工程化文档 §7.1](docs/operations/工程化文档.md)） |
| 关键决策变更 | [设计思路文档](docs/architecture/设计思路文档.md) |
| CAP / FreeSQL / `Bing.Data.Sql` 实现变更 | [子系统深挖](docs/architecture/子系统深挖.md) |
| 数据访问能力变化（新增 Provider / 某支新增或失去某项能力） | [能力矩阵](docs/getting-started/能力矩阵.md)（三支 × 18 项能力对照） |
| **新增/删除了实体基类、横切接口、仓储契约，或改了聚合根与领域事件的机制** | [领域建模指南](docs/guides/领域建模指南.md)（含继承体系与"谁填充字段"的归属） |
| **新增扩展点、改了模块基类/`[DependsOnModule]`/排序规则、新增 Provider 模板** | [扩展指南](docs/guides/扩展指南.md)（三支新增 Provider 的文件清单与注册入口） |
| **新增/重命名 Options、新增配置节、改了默认值** | [配置参考](docs/guides/配置参考.md)（并核对"是否真的绑定配置节"） |
| **新增日志 Sink / Enricher、改动请求日志或 TraceId 贯通** | [日志与可观测性](docs/operations/日志与可观测性.md)（⚠ 请求日志无脱敏，改动前先读该文 §4） |
| **补齐了某个已知缺口 / 新引入一处占位实现或未接线工程** | [功能完成度与已知缺口](docs/architecture/功能完成度.md)（该文如实登记"哪些没做完"；补上缺口或新增缺口都要更新它） |
| 新增自造术语或重命名概念 | [术语表](docs/getting-started/术语表.md) |
| 发现新的反模式 / 规范 | [最佳实践](docs/guides/最佳实践.md) |
| ABP 对应关系变化 | [ABP 迁移对照](docs/getting-started/abp-migration.md) |
| 性能相关改动（热路径、缓存、批量）或更新了基准 | [性能指南](docs/operations/性能指南.md)（并在必要时按 AGENTS.md 更新 `Bing.Data.Sql.Benchmarks` 基线） |
| 安全相关改动或发现新的安全风险 | [安全指南](docs/operations/安全指南.md) |
| 新增/调整关键架构决策 | [ADR](docs/architecture/adr/README.md)（**新建 ADR 文件并更新索引**；已废弃的决策改状态为 🔴 而非删除） |
| 测试工程、门控、CI 脚本、门禁规则变更 | [测试指南](docs/operations/测试指南.md) |
| 邮件 / 文本模板 / 锁 / 本地化 / 事件总线实现变更 | [横切能力文档](docs/guides/横切能力文档.md)（使用文档 §8 的详解版） |
| **事件相关改动**（`Bing.Events` / `Bing.EventBus*` / 领域事件派发 / CAP 发布路径 / Outbox 事务语义 / `[EventHandler]` 与订阅） | [事件与消息文档](docs/architecture/事件与消息文档.md)（四套机制的完整对照；**改动 `Send` 语义、派发时机或 UoW 提交路径时务必同步**——这两处失效都是静默的） |
| 边缘包（支付 / OAuth / 缓存适配 / 日志 Sink / 链路追踪 / Analyzers） | [组件库文档](docs/guides/组件库文档.md)（注意状态徽章：✅ / 🔌 / 🧩 / ⚠） |
| 构建配置、CI、发版、基准、eng 脚本变更 | [工程化文档](docs/operations/工程化文档.md) |
| 跨版本破坏性变更 | [迁移指南](docs/migrations/README.md) + [ReleaseNotes](docs/ReleaseNotes.md) |
| 新增易踩坑点 | [FAQ 与排错](docs/getting-started/FAQ与排错.md) |
| 新增文档 | [docs/README.md](docs/README.md)（文档导航索引）**与** `docs/toc.yml`（docfx 站点导航） |
| 新增 `ai_docs/` / `artifacts/` / `templates/` 资产 | [资产索引](docs/operations/资产索引.md) |
| **改动仓库根 `README.md`** | ⚠ 必须重新派生 `docs/index.md`：`python eng/ci/sync-docs-index.py`。**不要手工编辑 `docs/index.md`**，它是派生产物 |
| **改动 `docs/` 文档体系本身**（新增/重构文档、约定变化） | [docs/CHANGELOG.md](docs/CHANGELOG.md)（记录"文档体系"的演进，**不是**框架发行说明） |
| **提交前（任何文档改动）** | 运行 `python eng/ci/check-docs.py`，须全绿。已接入 CI：`.github/workflows/docs-lint.yml` 会在 PR 阶段拦截 |

### 8.1 文档自查脚本

```bash
python eng/ci/check-docs.py                # 12 项一致性校验：toc 链接与结构 / YAML 注释误用 /
                                           # README、README.en、docs hub 链接 / 全 docs 递归交叉链接 /
                                           # docs 顶层归位 / 包表覆盖 / index.md 同步 /
                                           # 包级 README 与徽章 / 坏模式回归
python eng/ci/sync-docs-index.py           # 从根 README 派生 docs/index.md
python eng/ci/sync-docs-index.py --check   # 只校验派生结果是否一致（幂等，CI 用）
```

两条硬门槛（CI 会拦）：

1. **包表覆盖必须 67/67** —— `framework/src` + `components/src` 下的每个可发包都要出现在根 README 的 NuGet 包表里；
2. **包级 README 必须有徽章行** —— 否则 nuget.org 包页面没有版本/下载量展示。

架构图源文件位于 `docs/images/`（SVG + PNG）。修改图请改 SVG 后重新导出 PNG（中文字体需显式指定 `Microsoft YaHei`）。
