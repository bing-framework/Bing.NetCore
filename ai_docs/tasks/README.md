# 任务文档索引

> 最新逐项核对与清理（2026-10-15，基点 `da0eccc0`）：见[任务状态与清理结果](STATUS-AUDIT.md)。用户确认后，将 006 与 V4 的 7 份完成快照合并后删除；独立任务目录由 24 个减至 22 个。全部 119 个任务 ID 均保留入口：97 个归入四份总览，22 个部分完成/待确认任务保留。

> 整理日期：2026-10-14。对照当前源码、职责级测试、原始计划和执行/Review 记录逐项核对 119 个任务；没有将历史测试记录标为本轮验证通过。

> 整理前记录：上一轮合并了 95 个任务，当时保留 24 个目录、110 份原任务 Markdown；本轮继续核对后仅追加合并 006 与 V4，没有将历史验收缺口改判为完成。当前共 111 份 Markdown，其中 22 个保留任务包含 103 份原文档；只修正一处指向 V4 Review 的历史引用，目标、背景和验收标准均保留。

## 目录规则

- 已完成的局部任务统一归入 `completed/`，保留任务 ID、实现证据、结论和限制，避免继续分散维护 plan/execution/review/TODO 快照。
- 未完成、待确认或仍被有效任务引用的目录完整保留原文；目标、背景、验收标准及已有证据不改写。
- `COMPLETED`、`PASS` 不是单独删除依据：发布、真实 Provider 和正式性能缺口必须单独保留。
- 后续新增任务沿用清晰的任务 ID，结束后归入模块总览；原始 JSON/checkpoint 放在 Git 忽略的 `artifacts/ai-tasks/`，见[产物说明](ARTIFACTS.md)。

## 已完成总览

| 文档 | 内容 |
| --- | --- |
| [缓存](completed/caching.md) | 91 个局部任务：输入、流式批量、分片、扫描、单节点/跨进程证据、故障门禁和 Hybrid outbox |
| [动态 API 与 SDK](completed/dynamic-api.md) | 动态服务端/原生客户端和三个 WebApiClient SDK 任务的最终实现与 Review |
| [SQL 元数据缓存](completed/sql-metadata-cache.md) | 映射缓存职责测试、失败 Lazy 清除、并发/容量/LRU 加固 |
| [SQL 查询 API 演进](completed/sql-query-api.md) | V4 原子状态与 Review 结论、V6 替代关系及历史验证边界 |

上一轮将 95 个任务的 380 份完成记录合并为三份总览；本轮新增 V4 演进总览并补入 006，追加删除 7 份被覆盖快照。精确文件和判断依据见[整理清单](CLEANUP.md)。原始 Markdown 本地备份：`artifacts/task-doc-audit/tasks-before-cleanup.zip`（Git 忽略，不随 clone 分发）。基线位置和原始产物迁移见 `ARTIFACTS.md`。

## 未完成、待确认和有效参考任务

以下 22 个任务保留，不能因后续任务完成局部功能就删除总体目标。RS0026 仅本地治理/Review 完成，真实 Provider 验收仍未闭环。006 的局部证据和 V4 的演进历史已归入上方总览。

| 任务入口 | 保留原因与当前边界 |
| --- | --- |
| [BING-CACHE-HARDENING-20261004-001](BING-CACHE-HARDENING-20261004-001/plan.md) | 有效总计划：功能已落地，正式预算及发布边界仍有效 |
| [BING-CACHE-PERF-20261006-007](BING-CACHE-PERF-20261006-007/plan.md) | 部分完成：批量读与扫描性能预算尚未闭环 |
| [BING-CACHE-PERF-20261006-008](BING-CACHE-PERF-20261006-008/plan.md) | 部分完成：性能候选已实现，正式 before/after 与预算仍缺 |
| [BING-CACHE-PERF-STREAM-20261004-002](BING-CACHE-PERF-STREAM-20261004-002/plan.md) | 部分完成：批量/扫描 P95、分配预算未达标，保留基准程序与目标 |
| [BING-CACHE-TODO-20261004-003](BING-CACHE-TODO-20261004-003/plan.md) | 有效后续验收：局部实现完成，整体性能和外部边界仍有效 |
| [BING-CACHE-TODO-20261005-004](BING-CACHE-TODO-20261005-004/plan.md) | 部分完成：基线可比性、正式矩阵和预算结论仍需保留 |
| [BING-CACHE-TODO-20261005-005](BING-CACHE-TODO-20261005-005/plan.md) | 实现完成、验证待确认：checkpoint/自适应拆分/回填代次存在；共享旧基线已恢复，对应 harness 构建与历史验收关系待核对 |
| [BING-CACHE-TODO-20261007-099](BING-CACHE-TODO-20261007-099/plan.md) | 部分完成：死信布局/迁移有实现，旧新二进制蓝绿发布待确认 |
| [BING-SQL-API-HARDENING-20260824-001](BING-SQL-API-HARDENING-20260824-001/plan.md) | 部分完成：Runtime 深拆、正式性能及外部 Provider 未闭环；后续任务仍引用 |
| [BING-SQL-QUERY-REFACTOR-20260824-001](BING-SQL-QUERY-REFACTOR-20260824-001/plan.md) | 部分完成：物理拆分、正式性能及外部 Provider 仍有缺口 |
| [BING-SQL-RC-FINAL-HARDENING-20260908-001](BING-SQL-RC-FINAL-HARDENING-20260908-001/plan.md) | 未闭环：受保护 CI、真实 MySQL 完整矩阵和 FormalHost 汇总缺证据 |
| [BING-SQL-RC-HARDENING-20260825-001](BING-SQL-RC-HARDENING-20260825-001/plan.md) | 部分完成：正式 before/after、Provider 生命周期故障矩阵仍有缺口 |
| [BING-SQL-RC-HARDENING-20260826-002](BING-SQL-RC-HARDENING-20260826-002/plan.md) | 部分完成：mutation gateway 已实现，CI、外部 Provider 和性能验收阻断 |
| [BING-SQL-RC-HARDENING-20260827-003](BING-SQL-RC-HARDENING-20260827-003/plan.md) | 未闭环：ReleaseEvidence、真实 Provider、远端 CI、FormalHost 待确认 |
| [BING-SQL-RC-HARDENING-20260828-004](BING-SQL-RC-HARDENING-20260828-004/plan.md) | 部分完成：本地 PASS 不能代替外部 Provider、CI、FormalHost 发布验收 |
| [BING-SQL-RC-HARDENING-20260904-001](BING-SQL-RC-HARDENING-20260904-001/plan.md) | 部分完成：发布正向证据链仍需复核，保留计划和 Review 问题 |
| [BING-SQL-RC-HARDENING-20260906-001](BING-SQL-RC-HARDENING-20260906-001/plan.md) | 部分完成：缺独立 Review、受保护 CI、MySQL 完整矩阵和取消证据 |
| [BING-SQL-RELEASE-HARDENING-20260831](BING-SQL-RELEASE-HARDENING-20260831/plan.md) | 部分完成：完整真实 Provider、资源、取消和发布门禁尚未闭环 |
| [BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903](BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/plan.md) | 部分完成：本地治理/Review PASS；原批准计划 T07–T09 的真实 Provider lane 证据仍缺，排除清理候选 |
| [bing-framework-docs](bing-framework-docs/plan.md) | 待确认：使用/设计文档已在主题目录存在；原路径调整与最终验收未闭环 |
| [sql-mapping-cache-versioning](sql-mapping-cache-versioning/plan.md) | 实现完成、验收证据待确认：正式基准原始报告在记录位置缺失；保留原任务，不以缺 Review 单独判未完成 |
| [sql-query-dev-v6-api-refactor](sql-query-dev-v6-api-refactor/plan.md) | 部分完成：本地 API 已实现，外部数据库兼容性和正式性能未闭环 |

## 全量任务对照清单

保留旧任务 ID 作为追溯键；原路径已合并的任务通过对应总览锚点查找，不创建重复空壳文档。

| 原任务 ID | 处置 | 当前入口/依据 |
| --- | --- | --- |
| `BING-CACHE-HARDENING-20261004-001` | 完整保留 | [有效总计划：功能已落地，正式预算及发布边界仍有效](BING-CACHE-HARDENING-20261004-001/plan.md) |
| `BING-CACHE-PERF-20261006-007` | 完整保留 | [部分完成：批量读与扫描性能预算尚未闭环](BING-CACHE-PERF-20261006-007/plan.md) |
| `BING-CACHE-PERF-20261006-008` | 完整保留 | [部分完成：性能候选已实现，正式 before/after 与预算仍缺](BING-CACHE-PERF-20261006-008/plan.md) |
| `BING-CACHE-PERF-20261006-021` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-PERF-20261006-021) |
| `BING-CACHE-PERF-20261007-062` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-PERF-20261007-062) |
| `BING-CACHE-PERF-20261007-064` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-PERF-20261007-064) |
| `BING-CACHE-PERF-STREAM-20261004-002` | 完整保留 | [部分完成：批量/扫描 P95、分配预算未达标，保留基准程序与目标](BING-CACHE-PERF-STREAM-20261004-002/plan.md) |
| `BING-CACHE-TODO-20261004-003` | 完整保留 | [有效后续验收：局部实现完成，整体性能和外部边界仍有效](BING-CACHE-TODO-20261004-003/plan.md) |
| `BING-CACHE-TODO-20261005-004` | 完整保留 | [部分完成：基线可比性、正式矩阵和预算结论仍需保留](BING-CACHE-TODO-20261005-004/plan.md) |
| `BING-CACHE-TODO-20261005-005` | 完整保留 | [实现完成、验证待确认：共享基线已恢复，原验收关系待核对](BING-CACHE-TODO-20261005-005/plan.md) |
| `BING-CACHE-TODO-20261006-006` | 已完成，合并去重 | [单节点与跨进程证据；总体预算仍保留于 002/004](completed/caching.md#BING-CACHE-TODO-20261006-006) |
| `BING-CACHE-TODO-20261006-009` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-009) |
| `BING-CACHE-TODO-20261006-010` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-010) |
| `BING-CACHE-TODO-20261006-011` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-011) |
| `BING-CACHE-TODO-20261006-012` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-012) |
| `BING-CACHE-TODO-20261006-013` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-013) |
| `BING-CACHE-TODO-20261006-014` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-014) |
| `BING-CACHE-TODO-20261006-015` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-015) |
| `BING-CACHE-TODO-20261006-016` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-016) |
| `BING-CACHE-TODO-20261006-017` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-017) |
| `BING-CACHE-TODO-20261006-018` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-018) |
| `BING-CACHE-TODO-20261006-019` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-019) |
| `BING-CACHE-TODO-20261006-020` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-020) |
| `BING-CACHE-TODO-20261006-022` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-022) |
| `BING-CACHE-TODO-20261006-023` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-023) |
| `BING-CACHE-TODO-20261006-024` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-024) |
| `BING-CACHE-TODO-20261006-025` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-025) |
| `BING-CACHE-TODO-20261006-026` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-026) |
| `BING-CACHE-TODO-20261006-027` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-027) |
| `BING-CACHE-TODO-20261006-028` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-028) |
| `BING-CACHE-TODO-20261006-029` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-029) |
| `BING-CACHE-TODO-20261006-030` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-030) |
| `BING-CACHE-TODO-20261006-031` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-031) |
| `BING-CACHE-TODO-20261006-032` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-032) |
| `BING-CACHE-TODO-20261006-033` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-033) |
| `BING-CACHE-TODO-20261006-034` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-034) |
| `BING-CACHE-TODO-20261006-035` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-035) |
| `BING-CACHE-TODO-20261006-036` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-036) |
| `BING-CACHE-TODO-20261006-037` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-037) |
| `BING-CACHE-TODO-20261006-038` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-038) |
| `BING-CACHE-TODO-20261006-039` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-039) |
| `BING-CACHE-TODO-20261006-040` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-040) |
| `BING-CACHE-TODO-20261006-041` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-041) |
| `BING-CACHE-TODO-20261006-042` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-042) |
| `BING-CACHE-TODO-20261006-043` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-043) |
| `BING-CACHE-TODO-20261006-044` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-044) |
| `BING-CACHE-TODO-20261006-045` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261006-045) |
| `BING-CACHE-TODO-20261007-046` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-046) |
| `BING-CACHE-TODO-20261007-047` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-047) |
| `BING-CACHE-TODO-20261007-048` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-048) |
| `BING-CACHE-TODO-20261007-049` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-049) |
| `BING-CACHE-TODO-20261007-050` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-050) |
| `BING-CACHE-TODO-20261007-051` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-051) |
| `BING-CACHE-TODO-20261007-052` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-052) |
| `BING-CACHE-TODO-20261007-053` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-053) |
| `BING-CACHE-TODO-20261007-054` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-054) |
| `BING-CACHE-TODO-20261007-055` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-055) |
| `BING-CACHE-TODO-20261007-056` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-056) |
| `BING-CACHE-TODO-20261007-057` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-057) |
| `BING-CACHE-TODO-20261007-058` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-058) |
| `BING-CACHE-TODO-20261007-059` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-059) |
| `BING-CACHE-TODO-20261007-060` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-060) |
| `BING-CACHE-TODO-20261007-061` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-061) |
| `BING-CACHE-TODO-20261007-063` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-063) |
| `BING-CACHE-TODO-20261007-065` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-065) |
| `BING-CACHE-TODO-20261007-066` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-066) |
| `BING-CACHE-TODO-20261007-067` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-067) |
| `BING-CACHE-TODO-20261007-068` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-068) |
| `BING-CACHE-TODO-20261007-069` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-069) |
| `BING-CACHE-TODO-20261007-070` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-070) |
| `BING-CACHE-TODO-20261007-071` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-071) |
| `BING-CACHE-TODO-20261007-072` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-072) |
| `BING-CACHE-TODO-20261007-073` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-073) |
| `BING-CACHE-TODO-20261007-074` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-074) |
| `BING-CACHE-TODO-20261007-075` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-075) |
| `BING-CACHE-TODO-20261007-076` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-076) |
| `BING-CACHE-TODO-20261007-077` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-077) |
| `BING-CACHE-TODO-20261007-078` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-078) |
| `BING-CACHE-TODO-20261007-079` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-079) |
| `BING-CACHE-TODO-20261007-080` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-080) |
| `BING-CACHE-TODO-20261007-081` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-081) |
| `BING-CACHE-TODO-20261007-082` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-082) |
| `BING-CACHE-TODO-20261007-083` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-083) |
| `BING-CACHE-TODO-20261007-084` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-084) |
| `BING-CACHE-TODO-20261007-085` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-085) |
| `BING-CACHE-TODO-20261007-086` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-086) |
| `BING-CACHE-TODO-20261007-087` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-087) |
| `BING-CACHE-TODO-20261007-088` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-088) |
| `BING-CACHE-TODO-20261007-089` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-089) |
| `BING-CACHE-TODO-20261007-090` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-090) |
| `BING-CACHE-TODO-20261007-091` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-091) |
| `BING-CACHE-TODO-20261007-092` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-092) |
| `BING-CACHE-TODO-20261007-093` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-093) |
| `BING-CACHE-TODO-20261007-094` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-094) |
| `BING-CACHE-TODO-20261007-095` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-095) |
| `BING-CACHE-TODO-20261007-096` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-096) |
| `BING-CACHE-TODO-20261007-097` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-097) |
| `BING-CACHE-TODO-20261007-098` | 已完成，合并去重 | [模块结论与证据](completed/caching.md#BING-CACHE-TODO-20261007-098) |
| `BING-CACHE-TODO-20261007-099` | 完整保留 | [部分完成：死信布局/迁移有实现，旧新二进制蓝绿发布待确认](BING-CACHE-TODO-20261007-099/plan.md) |
| `BING-DYNAMIC-API-20260925-001` | 已完成，合并去重 | [模块结论与证据](completed/dynamic-api.md#BING-DYNAMIC-API-20260925-001) |
| `bing-framework-docs` | 完整保留 | [待确认：主题文档已存在，路径调整与最终验收未闭环](bing-framework-docs/plan.md) |
| `BING-SQL-API-HARDENING-20260824-001` | 完整保留 | [部分完成：Runtime 深拆、正式性能及外部 Provider 未闭环；后续任务仍引用](BING-SQL-API-HARDENING-20260824-001/plan.md) |
| `BING-SQL-QUERY-REFACTOR-20260824-001` | 完整保留 | [部分完成：物理拆分、正式性能及外部 Provider 仍有缺口](BING-SQL-QUERY-REFACTOR-20260824-001/plan.md) |
| `BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001` | 已完成，合并去重 | [模块结论与证据](completed/sql-metadata-cache.md#BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001) |
| `BING-SQL-RC-FINAL-HARDENING-20260908-001` | 完整保留 | [未闭环：受保护 CI、真实 MySQL 完整矩阵和 FormalHost 汇总缺证据](BING-SQL-RC-FINAL-HARDENING-20260908-001/plan.md) |
| `BING-SQL-RC-HARDENING-20260825-001` | 完整保留 | [部分完成：正式 before/after、Provider 生命周期故障矩阵仍有缺口](BING-SQL-RC-HARDENING-20260825-001/plan.md) |
| `BING-SQL-RC-HARDENING-20260826-002` | 完整保留 | [部分完成：mutation gateway 已实现，CI、外部 Provider 和性能验收阻断](BING-SQL-RC-HARDENING-20260826-002/plan.md) |
| `BING-SQL-RC-HARDENING-20260827-003` | 完整保留 | [未闭环：ReleaseEvidence、真实 Provider、远端 CI、FormalHost 待确认](BING-SQL-RC-HARDENING-20260827-003/plan.md) |
| `BING-SQL-RC-HARDENING-20260828-004` | 完整保留 | [部分完成：本地 PASS 不能代替外部 Provider、CI、FormalHost 发布验收](BING-SQL-RC-HARDENING-20260828-004/plan.md) |
| `BING-SQL-RC-HARDENING-20260904-001` | 完整保留 | [部分完成：发布正向证据链仍需复核，保留计划和 Review 问题](BING-SQL-RC-HARDENING-20260904-001/plan.md) |
| `BING-SQL-RC-HARDENING-20260906-001` | 完整保留 | [部分完成：缺独立 Review、受保护 CI、MySQL 完整矩阵和取消证据](BING-SQL-RC-HARDENING-20260906-001/plan.md) |
| `BING-SQL-RELEASE-HARDENING-20260831` | 完整保留 | [部分完成：完整真实 Provider、资源、取消和发布门禁尚未闭环](BING-SQL-RELEASE-HARDENING-20260831/plan.md) |
| `BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903` | 完整保留 | [部分完成：本地治理完成，真实 Provider 验收仍缺](BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/plan.md) |
| `BING-WEBAPICLIENT-SDK-20260925-001` | 已完成，合并去重 | [模块结论与证据](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-20260925-001) |
| `BING-WEBAPICLIENT-SDK-CHECK-20260925-002` | 已完成，合并去重 | [模块结论与证据](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-CHECK-20260925-002) |
| `BING-WEBAPICLIENT-SDK-OPT-20260925-003` | 已完成，合并去重 | [模块结论与证据](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-OPT-20260925-003) |
| `sql-lambda-query-api-v4` | 历史完成，合并去重 | [V4 演进证据与 V6 替代边界](completed/sql-query-api.md#sql-lambda-query-api-v4) |
| `sql-mapping-cache-versioning` | 完整保留 | [实现完成、验收证据待确认：正式原始报告待定位](sql-mapping-cache-versioning/plan.md) |
| `sql-query-dev-v6-api-refactor` | 完整保留 | [部分完成：本地 API 已实现，外部数据库兼容性和正式性能未闭环](sql-query-dev-v6-api-refactor/plan.md) |

## 本轮验证范围

只修改任务文档与索引；验证 UTF-8、链接/锚点、119 个任务覆盖及精确删除集合。103 份保留原文档中 102 份逐字节一致，查询重构 plan.md 仅迁移一处历史 Review 引用。没有重跑构建、数据库集成、Redis/Cluster、性能矩阵或远端 CI；未据此改变原任务的发布与性能结论。
