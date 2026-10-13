<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261007-063
AI_EXECUTION_FINISHED_AT: 2026-10-07T06:46:21+08:00

# 实施执行报告

## 执行结论

修复 EasyCaching 前缀流对 null 命中条目的错误丢弃。批量结果现在按物理键是否存在区分“扫描到但已消失”的 miss 和“值为 null 的命中”，分别产出 `Hit=false` 与 `Hit=true` 的 `CacheEntry<T>`。

## 计划执行情况

- [x] 修正 `ReadPhysicalBatchAsync` 的 null 命中判断。
- [x] 增加普通值、null 命中和缺失值的前缀流回归测试。
- [x] 保持 List API、顺序、取消和异常传播语义。
- [x] 完成 net6.0/net8.0 精准及完整 EasyCaching.Tests 回归。
- [x] 完成生产 netstandard2.0 构建和差异检查。

## Change Impact Analysis

```text
ChangedFiles: CacheManager.cs；CacheManagerTests.cs；本任务文档
ChangedProjects: Bing.EasyCaching、Bing.EasyCaching.Tests
ChangedPublicContracts: 无
ChangedRuntimePaths: EasyCaching Redis/Hybrid 前缀流批量读取
ChangedProviders: EasyCaching Redis、Hybrid；Memory 键流适配器复用同一路径
ChangedTFMs: 生产 netstandard2.0；测试 net6.0/net8.0
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 否
AffectedDependents: GetByPrefixStreamAsync 调用方、Hybrid 本地/分布式读取
RiskLevel: MEDIUM
```

## 修改内容

- `CacheManager.ReadPhysicalBatchAsync`：使用 `TryGetValue` 判断扫描到的物理键，`CacheValue.HasValue` 仅决定 `Hit`，不再决定是否产出条目。
- `CacheManagerTests.GetByPrefixStream_PreservesNullHitAndReportsMissingValue`：覆盖 `[null hit, missing]` 顺序、值和命中状态。

## 验证结果

- 精准 `GetByPrefix` 测试：net8.0 16 通过、2 跳过；net6.0 16 通过、2 跳过。
- EasyCaching.Tests 完整集合：net8.0 72 通过、54 跳过；net6.0 72 通过、54 跳过。
- `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --framework netstandard2.0 --no-restore`：0 警告、0 错误。
- `git diff --check`：通过；工作区已有 CRLF 转换提示不构成错误。
- 测试输出包含既有 `NU1801`/`NU1603` NuGet 警告，未导致失败；未执行 restore 或下载依赖。

## 结果边界

- `CLOSED`：EasyCaching 前缀流 null 命中和扫描竞态 miss 语义。
- `BLOCKED_EXTERNAL`：真实 Redis/Hybrid、Cluster、分片和跨实例总线回归仍按权威索引处理。
- `VERIFIED_BOUNDARY`：正式性能预算和 100,000 项性能矩阵未受本轮修改影响，沿用既有结论。

## Git 安全

未执行提交、推送、PR、发布或回滚工作区其他改动。
