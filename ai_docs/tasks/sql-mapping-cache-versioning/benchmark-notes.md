# 映射缓存性能正式基线

## 环境

- 日期：2026-09-18
- BenchmarkDotNet：0.14.0
- 作业：`FormalHost`，3 次启动、6 次预热、15 次测量
- 运行时：.NET 8.0.30，X64 RyuJIT AVX
- SDK：8.0.424
- 主机：Windows 10，Intel Xeon E5-2697 v2，24 物理核 / 48 逻辑核

## 版本发布与并发冷解析

| 配置数 | 发布 Mean / Allocated | 并发冷解析 Mean / Allocated |
| ---: | ---: | ---: |
| 1 | 1.747 us / 7.19 KB | 10.001 us / 10.8 KB |
| 10 | 3.683 us / 11.28 KB | 40.676 us / 29.55 KB |
| 100 | 21.791 us / 50.2 KB | 432.389 us / 652.59 KB |

发布完整快照随配置数近似线性增长，100 条配置仍为 21.8 us。并发冷解析包含解析器构造、配置快照、候选匹配和 100 个路由的首次映射构建；100 路由出现 Gen1 回收，适合保留为后续回归门槛。

## 缓存路径

| 配置数 | 命中 Mean / Allocated | 冷解析 Mean / Allocated | 满容量旁路 Mean / Allocated | LRU 淘汰 Mean / Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 305.9 ns / 264 B | 7.462 us / 9.25 KB | 1.721 us / 1.09 KB | 2.310 us / 1.25 KB |
| 10 | 934.6 ns / 984 B | 10.823 us / 14.59 KB | 1.671 us / 1.09 KB | 2.254 us / 1.25 KB |
| 100 | 7.440 us / 9.48 KB | 45.376 us / 68.66 KB | 1.682 us / 1.09 KB | 2.237 us / 1.25 KB |

## 结论

- 版本发布成本可接受，当前没有证据支持为发布路径增加更复杂的增量更新机制。
- AdmissionOnly 满容量旁路和 LRU 淘汰不随 `MappingConfigurationCount` 变化；当前容量管理锁不是本组数据中的扩展性瓶颈。
- 缓存命中仍先扫描候选配置。候选数从 1 增至 100 时，命中延迟增加约 24.3 倍，分配从 264 B 增至 9,712 B，应作为下一项独立优化任务。
- 后续优化应针对候选选择建立组合键索引，并保持当前优先级和回退语义；完成后用本基线复测，不在本任务内直接重写算法。

## 原始报告

- `BenchmarkDotNet.Artifacts/mapping-cache-versioning-20260918-rerun/results/Bing.Data.Sql.Benchmarks.SqlMetadataBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/mapping-cache-versioning-20260918-cache-paths/results/Bing.Data.Sql.Benchmarks.SqlMetadataBenchmarks-report-github.md`

首次尝试因缺少 `System.Threading.Tasks` 引用未执行样本，不计入正式结果；以上数据全部来自修复后的成功作业。
