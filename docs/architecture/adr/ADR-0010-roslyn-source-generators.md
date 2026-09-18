# ADR-0010：Roslyn 源生成器与分析器

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Ddd.Domain.Extensions.Analyzers`（变更跟踪生成器）、`Bing.Data.Sql.Analyzers`（`BINGSQL002` 与迁移诊断） |

## 背景

两处重复劳动适合在**编译期**消灭：

1. **DDD 变更跟踪**：`partial` 领域类需要一个个字段写对比逻辑（`AddChanges(TOther)`），样板量大且容易漏字段；
2. **SQL API 安全与迁移**：插值字符串拼 SQL 是注入的主要来源，靠 Code Review 抓不可靠；跨版本 API 变更也需要 IDE 提示。

同时有个硬约束：**不希望这些能力带来运行期依赖或反射开销**。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 运行时反射** | 实现简单，无需构建期工具链 | 每次调用都有反射开销；无编译期类型安全；错误延后到运行时 |
| **B. 代码生成模板**（T4 / CodeSmith） | 仓库里已有历史产物（`templates/`） | 生成的代码要签入仓库，容易与源码不同步；生成器本身不在构建链路，改了要手工跑 |
| **C. Roslyn 源生成器 + 分析器**（本方案） | 编译期完成、**运行期零依赖零反射**；代码不入库（每次构建现生成）；分析问题在 IDE 里即时可见 | 调试要理解生成代码；分析器必须随包以 `analyzers/dotnet/cs` 形式发布；新诊断要维护 `AnalyzerReleases.*.md`；拖慢编译 |

## 决策

采用 **方案 C**，两个包：

| 包 | 能力 |
| --- | --- |
| `Bing.Ddd.Domain.Extensions.Analyzers` | 为 `partial` 领域类**编译期自动生成** `AddChanges(TOther)`，自动跳过 `Version` / `IsDeleted` 等框架字段 |
| `Bing.Data.Sql.Analyzers` | ① **`BINGSQL002`**：插值 SQL 未参数化时告警（Warning，`Bing.Data.Sql.Security` 分类，默认启用）；② API 迁移诊断 |

安全路径：`SqlInterpolated(...)` 与传入参数对象不会触发 `BINGSQL002`——分析器指导你往参数化方向写。

## 后果

**正面**

- 变更跟踪**零手写样板**，且新字段自动加入对比（不会漏）；
- 运行期**无反射开销**，符合性能敏感场景；
- 注入风险在**编写时**就被 IDE 提示，而不是靠 Review 或线上事故暴露；
- 生成代码不进版本库，杜绝"生成物与源码不同步"。

**负面**

- 调试时看不到/读不懂生成的代码，`AddChanges` 出现意外行为时要理解生成器逻辑；
- 分析器必须正确打包到 `analyzers/dotnet/cs` 目录，打包配置错了就静默失效；
- 新增分析器规则必须同步更新 `AnalyzerReleases.Unshipped.md`，否则构建报错；
- 源生成器会增加编译时间。

**中性**

- 仓库里的 `templates/`（CodeSmith 模板，约 50 个 `.cst`）是**更早一代**的代码赶工方案，与 Roslyn 生成器并存。建议标注其历史属性——见 [资产索引](../../operations/资产索引.md)。

## 相关文档

- [组件库文档](../../guides/组件库文档.md)（两个分析器包的状态与用法）
- [安全指南](../../operations/安全指南.md)（`BINGSQL002` 与注入防护）
- [工程化文档](../../operations/工程化文档.md)（`AnalyzerReleases` 与分析器发布）
