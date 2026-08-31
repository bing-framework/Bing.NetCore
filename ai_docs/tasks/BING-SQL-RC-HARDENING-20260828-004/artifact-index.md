# 制品索引

## 状态

`PARTIAL`。修复后的本地 benchmark 制品已登记；外部 Provider TRX/JSON 和远端 CI artifact 尚未生成。

## 当前源码身份

- Source HEAD（基线）：`faba0eee924b7c992dc0aaad414099d92308f5f9`
- Source state：`dirty worktree`
- Benchmark source path：`framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
- Benchmark binary diff SHA-1：`22c00a6038eed6082eca70c2db6bc8f116c59d0e`
- Full worktree binary diff SHA-1：`c48331a4d321c5a3a15d8ab321bf6e20cc7ffdee`
- TFM/Provider：`net8.0` / SQLite temporary file database
- BenchmarkDotNet JSON：未生成。当前 BenchmarkDotNet 运行仅生成 log、GitHub Markdown、CSV 与 HTML，索引未将不存在的 JSON 伪报为证据。
- 敏感信息检查：对本节登记的八个制品按凭据键名模式进行只读扫描，未发现密码、令牌、密钥、连接字符串或数据库地址值；本索引不记录任何环境变量值。
- 报告链接：[Benchmark 报告](benchmark-report.md)；[验证报告](verification-report.md)。

## 当前制品

| 制品 | 相对路径 | SHA-256 | Provider/TFM/Job | 命令与结论 |
| --- | --- | --- | --- | --- |
| SQLite E2E Dry smoke log | `BenchmarkDotNet.Artifacts/rc28-e2e-smoke-review/Bing.Data.Sql.Benchmarks.SqliteDapperE2ESmokeBenchmarks-20260828-151558.log` | `4E6C89BC6FC554723A51B194FF597772A94D7157CCCD2C9B81ED11A11E0B5C05` | SQLite/net8.0/Dry | `dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build --no-restore -- --e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\rc28-e2e-smoke-review"`; smoke only |
| SQLite E2E Dry smoke GitHub report | `BenchmarkDotNet.Artifacts/rc28-e2e-smoke-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2ESmokeBenchmarks-report-github.md` | `DDA224970CA254A279FC80617CD09FFB960C9B0C3916837CC70E9FA8B432BD38` | SQLite/net8.0/Dry | Same command; smoke only |
| SQLite E2E Dry smoke CSV | `BenchmarkDotNet.Artifacts/rc28-e2e-smoke-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2ESmokeBenchmarks-report.csv` | `11DDCCF342F04B54BBA6D0E9560C8BC2C9E495AFC532B817F4A9A7E074150DE9` | SQLite/net8.0/Dry | Same command; smoke only |
| SQLite E2E Dry smoke HTML | `BenchmarkDotNet.Artifacts/rc28-e2e-smoke-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2ESmokeBenchmarks-report.html` | `99E6842BB6424319B1EE8B14B4A30950A17B842C1D9B8BDB7F86E2516FAF206E` | SQLite/net8.0/Dry | Same command; smoke only |
| SQLite E2E FormalHost after log | `BenchmarkDotNet.Artifacts/rc28-formal-after-review/Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks-20260828-151741.log` | `75925C889FCE9228EB8CD168AE4D6E1CE6092E863C5B6C0B0A8F9EF9E9545EF6` | SQLite/net8.0/FormalHost | `dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build --no-restore -- --filter "*SqliteDapperE2EBenchmarks.QueryToList*" --artifacts "BenchmarkDotNet.Artifacts\rc28-formal-after-review"`; after only, `NOT_COMPARABLE` |
| SQLite E2E FormalHost after GitHub report | `BenchmarkDotNet.Artifacts/rc28-formal-after-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks-report-github.md` | `8ECE885BB425930964ADE2466590876287A8B5FA07076079001EFE5992818EE6` | SQLite/net8.0/FormalHost | Same command; after only, `NOT_COMPARABLE` |
| SQLite E2E FormalHost after CSV | `BenchmarkDotNet.Artifacts/rc28-formal-after-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks-report.csv` | `94CC204C3DA72822C6AD165AD71148447328EB44D864D9A6A30AC5F36FACA653` | SQLite/net8.0/FormalHost | Same command; after only, `NOT_COMPARABLE` |
| SQLite E2E FormalHost after HTML | `BenchmarkDotNet.Artifacts/rc28-formal-after-review/results/Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks-report.html` | `6847C04B53B319D5115FD91DE3C5A2FFA438A3ED231F23D8AE394DCE929749DF` | SQLite/net8.0/FormalHost | Same command; after only, `NOT_COMPARABLE` |

## 已替代的历史制品

`rc28-e2e-smoke` 和 `rc28-formal-after` 由 benchmark 类型隔离重构前生成。它们仅保留为历史诊断，不代表当前 `22c00a6038eed6082eca70c2db6bc8f116c59d0e` 源码，也不得用于当前实现的性能结论。
