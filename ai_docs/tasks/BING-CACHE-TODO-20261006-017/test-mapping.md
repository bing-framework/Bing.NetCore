# 生产符号到测试方法

| 生产符号 | 测试方法 |
| --- | --- |
| `CacheCommandProbe.InvokeAsync` / 分类计数 | `Bing.Caching.Tests.CacheCommandProbeTest.Probe_TracksCallsByKindAndRestoresUnknownCompatibility` |
| FreeRedis/CSRedis/EasyCaching 已标注批量读/扫描路径 | `Bing.Caching.Tests.CacheCommandProbeTest`；本轮 FreeRedis/EasyCaching 单节点 smoke 的 `commandBreakdown` |
| CacheBench `PhaseReport.CommandBreakdown` | CacheBench 当前工程 `dotnet build --no-restore`；`probe-smoke-freeredis.json`、`probe-smoke-easyredis.json` |
