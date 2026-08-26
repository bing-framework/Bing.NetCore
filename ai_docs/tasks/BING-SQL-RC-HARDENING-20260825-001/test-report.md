# 测试报告

- task-id: `BING-SQL-RC-HARDENING-20260825-001`
- 结果口径：未将未配置外部数据库环境标记为通过。

| 项目/命令范围 | TFM | 结果 |
| --- | --- | --- |
| `Bing.Data.Sql.Tests` Release | net6/net8 | `2496/2496` 通过 |
| `Bing.Data.Sql.Analyzers.Tests` Release | net6/net8 | `27/27` 通过 |
| `Bing.Dapper.Core.Tests` Release | net6/net8 | `262/262` 通过 |
| `Bing.Dapper.Sqlite.Tests` Release | net6/net8 | `222/222` 通过 |
| `Bing.Dapper.Sqlite.Tests.Integration` Release | net6/net8 | `292/292` 通过 |
| `SqliteMultipleQueryIntegrationTest` 专项 | net6/net8 | `14/14` 通过 |
| Data.Sql / Dapper.Core Release build | netstandard2.0 | 通过；保留既有 56 个 RS0026/RS0027 警告 |
| Data.Sql / Dapper.Core pack | netstandard2.0 | 通过 |
| `git diff --check` | - | 通过 |

## Round 2 Review Fix 验证

| 项目/命令范围 | TFM | 结果 |
| --- | --- | --- |
| `Bing.Dapper.SqlServer.Tests` 过滤 `ExecuteMultiple` | net6/net8 | `32/32` 通过 |
| `Bing.Data.Sql.Tests` 过滤 `SqlQueryApiContractTest` | net6/net8 | `30/30` 通过 |
| `Bing.Data.Sql.Analyzers.Tests` 过滤 `BingSql002AnalyzerTest` | net8 | `10/10` 通过 |
| MySQL Provider metadata contract | net6/net8 | `22/22` 通过 |
| PostgreSQL Provider metadata contract | net6/net8 | `10/10` 通过 |
| Oracle Provider metadata contract | net6/net8 | `6/6` 通过 |
| SQLite Provider metadata contract | net6/net8 | `8/8` 通过 |
| SQL Server Provider metadata contract | net6/net8 | `2/2` 通过 |

## Round 2 Benchmark

- Root FormalHost：`72` 个 case 完成；Job 配置和 artifact 见 `benchmark-report.md`。
- 历史 before artifact：不可追溯，性能 delta 记为 `N/A`。

## Round 3 Benchmark

- Root FormalHost before/after：`72/72` case 可比较；Mean 超过 10% 的 case 为 `18`，分配超过 10% 的 case 为 `0`。
- Join FormalHost before/after：`36/36` case 可比较；Mean 超过 10% 的 case 为 `6`，可用分配字段未发现超过 10% 的增长，另有 `1` 个 after 分配字段为 `NA`。
- before 基线来源：detached `HEAD=142380be` 独立 worktree；未覆盖当前工作区改动。
- 性能结论：保留 `PARTIAL`，存在时间回归候选及多峰/离群告警，未标记为无回归通过。

## 外部 Provider Gate

- MySQL：`GATE_SKIPPED`，未配置可安全使用的外部数据库环境。
- PostgreSQL：`GATE_SKIPPED`，未配置可安全使用的外部数据库环境。
- SQL Server：`GATE_SKIPPED`，未配置可安全使用的外部数据库环境。
- Oracle：`GATE_SKIPPED`，未配置可安全使用的外部数据库环境。
- Doris：`GATE_SKIPPED`，未配置可安全使用的外部数据库环境。

## 覆盖边界

SQLite Integration 已覆盖多结果顺序读取、重入拒绝、取消、提前释放、跨 sync/async Dispose、读失败恢复和重复释放。计划要求的 reader/事务/回调多异常组合以及完整 2～7 map null/throw/类型转换/取消矩阵仍未全部建立。
