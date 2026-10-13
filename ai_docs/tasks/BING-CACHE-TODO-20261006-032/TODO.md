# 本轮 TODO

## Open Actionable

- [ ] FIX-007 的正式批量/扫描预算仍未闭环；本轮只完成扫描过滤路径优化和 1k 方向性证据。

## Completed

- [x] 计划和 Change Impact Analysis 已落盘。
- [x] `CacheKeyFormat` 增加预编码逻辑前缀匹配、规范 URI 转义校验、UTF-8 校验和哈希验证；非法键回退既有解析。
- [x] FreeRedis/CSRedis 异步 `GetByPrefixAsync` 使用只产生物理键的扫描枚举；Stream 和同步 API 保持原路径。
- [x] 共享键 net6/net8 精准测试、两个 Provider net6/net8 单节点特殊键/批量集成回归和 netstandard2.0 构建通过。

## Blocked External / Deferred

- [ ] Cluster、双节点分片、独立 Hybrid 总线及正式 2 轮预热/10 轮采样/10,000 项矩阵仍依赖外部环境。
- [ ] T02/T03 后双进程回源证据、100,000 项跨拓扑一致性和真实网络排队指标继续复用既有证据缺口。

## Directional Evidence

- [x] FreeRedis 1k、1 轮预热/3 轮样本/并发 8：扫描 P95 `12.9559 -> 11.6446 ms`，平均分配 `4,241,680 -> 4,107,386.7 B`；批量读平均分配约 `2,391,608 B`，未见改善。
- [x] CSRedis 1k、同参数：扫描 P95 `12.7918 -> 12.5809 ms`，平均分配 `2,035,882.7 -> 1,901,600 B`；批量读平均分配约 `1,642,152 B`，未见改善。
- [x] 原始产物：`diagnostic-prefixfast-v2-freeredis-1k.json`、`diagnostic-prefixfast-v2-csredis-1k.json`；仅作为方向性证据，未达到正式 2 轮预热/10 轮采样。
