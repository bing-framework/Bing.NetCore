<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: sql-query-dev-v6-api-refactor
AI_REVIEWED_AT: 2026-08-22T22:40:20.8114024+08:00

# dev_v6 SQL 查询重构 Round 7 复审

## 验收摘要

结论：`PASS_WITH_ISSUES`。

Round 6 后没有新的 Review Fix 或相关代码变更。本轮仍优先复核最近一次开放的 `FIX-003`，结论保持 `RESOLVED`：活动发行说明已统一为连续非泛型 `From<TEntity>(alias, schema)` 与一元/二元 Lambda 合同；API Contract 已移除有限 `Func<>` 白名单，改为对全部公开 `Expression<TDelegate>` 读取委托 `Invoke` 的输入参数数。五输入和六输入 `Func` 使用相同机制被判定为超出二元上限，未来重新暴露任意 3+ 参数表达式会纳入同一公开 API 检查而失败。

没有新的 `MUST_FIX`。外部 Provider Gate 未配置、公开 Clone 的 FormalHost 基准证据不足仍是已知非阻塞风险，保留为 `SHOULD_FIX` / `NOT_VERIFIABLE`，不重新打开已解决的修复项。

## 上一轮 FIX 复核

| 上一轮项 | 本轮状态 | 实际证据 |
| --- | --- | --- |
| `FIX-003` 活动文档与高元数 Lambda 合同 | `RESOLVED` | `docs/ReleaseNotes.md` 已明确连续 `From<TEntity>(alias, schema)`、一元/二元 Lambda，并将 1～10 限定为测试覆盖范围。`SqlQueryApiContractTest` 直接遍历所有 `Expression<>` 的委托 `Invoke` 参数数，并对五、六输入 `Func` 执行负向检查。 |
| `FIX-008` Clone Benchmark/FormalHost | `PARTIAL` | 既有 `SHOULD_FIX`；本轮未改变基准或执行热路径，继续作为性能证据缺口。 |

## 计划验收矩阵

| 范围 | 状态 | 实际证据 |
| --- | --- | --- |
| P1 生命周期、冻结计划和 Clone | `PASS` | 前轮 Unit、真实 SQLite 生命周期与 Clone 隔离验证保持有效；本轮无实现变更。 |
| P2/P3 连续 From、方法级泛型和原子 Join | `PASS` | `SqlLambdaQuery.NonGeneric.cs` 当前全部公开 `Expression<Func<...>>` 入口为一元或二元；未发现公开 3+ 参数表达式。 |
| P5 Context/诊断 | `PASS` | 本轮无相关变更；前轮 Count/Data、ParentQueryContext 和诊断链路证据保持有效。 |
| P6 缓存与隔离 | `PASS` | 本轮无缓存或渲染路径变更；前轮动态过滤、Clone、Count/Data 责任级测试保持有效。 |
| P9 SQLite 真实执行 | `PASS` | 本轮仅修改文档/API Contract；既有 SQLite 真实执行证据未受影响。 |
| P10 文档、追溯和最终收口 | `PASS_WITH_ISSUES` | 活动 `docs/**/*.md` 已无旧固定元数 API 合同；顶层 dev_v6 追溯表保持连续来源/二元 Join 口径。外部 Provider Gate 和 FormalHost 基准仍非本轮可验证范围。 |

## Git 变更分析

- 工作区仍含大范围、未提交的 dev_v6 重构差异；本轮仅复核 Round 5 直接涉及的 `docs/ReleaseNotes.md`、`SqlQueryApiContractTest.cs` 与 `execution.md` 记录。
- `docs/ReleaseNotes.md` 的 SQL API 段已移除“类型化 From 支持 1～10 个实体来源”的发布口径，明确 1～10 仅为测试覆盖。
- `SqlQueryApiContractTest.cs` 已从有限 `Func<>` 筛选改为对所有 `Expression<TDelegate>` 委托的 `Invoke` 输入参数计数。
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt` 与 `framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt` 均无 Git Diff。
- `git diff --check`：PASS；仅出现既有 CRLF/LF 转换提示，无空白错误。

## 功能与 API 契约 Review

- `SqlLambdaQuery.NonGeneric.cs` 中公开的 Select、AppendSelect、Where、WhereIf、Join、GroupBy、OrderBy 和 Having 表达式入口均为一元或二元；没有公开 3+ 输入 Lambda。
- `LambdaQuery_WhenPublicApiInspected_ShouldUseMethodLevelUnaryOrBinaryExpressions` 会枚举 `SqlLambdaQuery` 所有公开方法参数中的 `Expression<>`，对其委托 `Invoke` 输入参数断言范围为 0～2；不再通过列举具体 `Func<>` 泛型定义漏检更高元数。
- 同一测试对 `Func<,,,,,>` 和 `Func<,,,,,,>` 断言输入参数数大于 2，证明五、六输入委托走同一参数计数逻辑，不会被静默筛除。
- 仍禁止公开 Lambda `.On()` 与 `.As()`，相关反射断言保持存在。

## 文档 Review

- `docs/ReleaseNotes.md` 当前 7.0.0 发行说明与实际兼容边界一致：来源由连续两参数 `From<TEntity>` 追加，公开 Join/Where/Select Lambda 最多绑定两个来源。
- 扫描活动 `docs/**/*.md`：未发现“类型化 From 支持 1～10 个实体来源”、十元 Lambda 宣称或三参数 Join/Select 示例。
- `ai_docs/sql-metadata-test-traceability.md` 顶部当前 dev_v6 映射明确来源数量不编码在公开查询类型中；文件内旧 V4 元数说明位于明确历史章节，未作为当前合同处理。

## 架构、性能与资源 Review

- 本轮未修改生产架构、执行、缓存、Builder、连接、事务或资源释放路径，未发现 Round 5 引入的功能或资源回归。
- `SqlLambdaQuery.Clone()` 的 FormalHost 性能基准与完整性能对比证据仍不足，延续为 `SHOULD_FIX`，不影响本轮 API/文档合同通过。

## 测试与验证

从当前工作区重新运行：

- `SqlQueryApiContractTest`，net6.0：`8/8` PASS。
- `SqlQueryApiContractTest`，net8.0：`8/8` PASS。
- 构建成功，保留既有 36 个 `CS0108`、`RS0026/RS0027` 警告；未观察到 `RS0016/RS0017/RS0018`。
- 修改的测试文件编辑器诊断：无错误。
- `git diff --check`：PASS。
- 外部 MySQL/PostgreSQL/Oracle Gate 未配置，保持 `NOT_VERIFIABLE`；本轮不涉及 Provider 运行路径。

## 剩余风险

- `FIX-008`：公开 Clone 的 FormalHost 基准和完整性能矩阵仍未补齐，严重程度 `LOW`，处理要求 `SHOULD_FIX`。
- 外部 Provider Integration Gate 未配置；这些真实数据库链路为 `NOT_VERIFIABLE`，执行报告已明确未使用猜测连接信息或绕过 Gate。
- 当前构建保留既有 36 个 `CS0108`、`RS0026/RS0027` 警告；本轮未新增警告，也未通过关闭 Analyzer 规避。
- `execution.md` 的历史实施段仍含旧测试数量和早期 `PARTIAL` 叙述；Round 5 的完成记录、当前源码及本轮实测为本次结论依据。

## 最终 Checklist

- [x] 已读取最新 plan、execution、旧 review、实际源码、文档、测试和 Git Diff。
- [x] 已逐项复核上一轮 `FIX-003`。
- [x] 已从当前源码运行 net6.0/net8.0 API Contract。
- [x] 已确认活动文档口径、公开表达式签名和高元数委托通用检查一致。
- [x] 已确认两个 Shipped API 基线无差异。
- [x] 已确认 `git diff --check` 通过。
- [x] 本轮未修改业务代码、测试代码、plan.md 或 execution.md。
- [x] 无开放 `MUST_FIX`。
