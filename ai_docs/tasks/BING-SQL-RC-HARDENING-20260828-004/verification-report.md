# 验证报告

## 证据层级

1. 仓库静态与本地合同：源码、unit/analyzer、runner self-test、CI 配置。
2. SQLite 真执行：本地文件数据库集成测试。
3. 受保护 Provider、远端 CI 与 FormalHost：仅接受本任务 current non-skip TRX/JSON 或 BenchmarkDotNet 原始制品。

## 当前状态

`PARTIAL`。Phase 0、Phase 2 和 Phase 5 已完成；本地 unit/analyzer/SQLite/runner/Release build 均通过。review-fix Round 1 已重新生成 Smoke 与 FormalHost after，并补齐 dirty-worktree provenance。缺少真实外部 Provider、远端 CI 与 FormalHost before 制品，且需要重新独立 review，故不得标记为任务整体 `COMPLETED`。

## 保护边界

八个受保护配置路径已通过 `git diff --quiet -- <path>` 验证未变更；未读取、输出或修改其内容。

## 已通过验证

- `dotnet build .\Bing.All.sln -c Release -nologo -v quiet -clp:ErrorsOnly`：0 errors，158 warnings。
- `Bing.Data.Sql.Tests`：net6.0/net8.0 均 1265 passed。
- `Bing.Data.Sql.Analyzers.Tests`：31 passed。
- `Bing.Dapper.Core.Tests`：net6.0/net8.0 均 134 passed。
- `Bing.Dapper.Sqlite.Tests.Integration`：net6.0/net8.0 均 151 passed。
- Provider runner `-SelfTest`：passed。
- SQL Server Startup 环境变量筛选：net6.0/net8.0 均 3 passed。
- `git diff --check`：passed。
- Benchmark review-fix：`Bing.Data.Sql.Benchmarks` Release build 通过（0 warnings、0 errors）；steady listener-on、subscribe-plus-query 和 cardinality filter 各只发现一个 benchmark；Smoke 发现八个可扩展 listener-off 方法并成功生成 Dry 制品。
- listener-off FormalHost after：`QueryToList` 的 RowCount `1/100/1000` 三个 case 成功完成；源码身份、SHA-256、运行命令和脱敏状态见 [制品索引](artifact-index.md)，结果不与 Dry 或历史制品比较。

## 未满足完成门槛

- 每个 MySQL/PostgreSQL/SQL Server 的 current non-skip TRX/JSON。
- 远端受保护 job 的 secret scope、trusted-lane 和实际运行证据。
- 与当前 FormalHost after 同 key 的 before artifact。
- Phase 6 独立 review 结论。
