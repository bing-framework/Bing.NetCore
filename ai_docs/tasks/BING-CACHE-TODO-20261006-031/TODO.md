# 本轮 TODO

## Open Actionable

- [ ] FIX-007 的整体批量/扫描预算尚未闭环；本轮只验证 List API 快路径能否消除流式收集开销。

## Completed

- [x] FreeRedis/CSRedis `GetAllAsync` 和 `GetByPrefixAsync` 直接复用原始批量响应，保留单节点、Cluster 和客户端分片路由、批预算及取消检查。
- [x] List 快路径与 Stream 共享物理键和解码语义，未改变 Stream 的 `CacheEntry<T>.Hit` 行为。
- [x] FreeRedis/CSRedis 首批消费后取消、特殊前缀流和大批量 List/Stream 一致性测试已补齐或复用。
- [x] EasyCaching List/Stream 重复键、缺失键、null 和首批取消单元测试已补齐。
- [x] FreeRedis/CSRedis net6/net8 单节点集成测试、EasyCaching net6/net8 单元测试、两个 Provider netstandard2.0 生产构建及 `git diff --check` 通过。
- [x] 1k 同端点方向性复测已保存：`current-listfast-freeredis-1k.json`、`current-listfast-csredis-1k.json`。

## Performance Evidence

- [ ] 相比 task 030 的当前候选，FreeRedis 批量读/扫描 P95 从 6.2166/13.1003 ms 降至 5.6873/12.5396 ms；CSRedis 从 5.8279/14.2732 ms 降至 5.4927/12.6621 ms。
- [ ] 相比固定 baseline，FreeRedis 批量读 P95 +20.2%、分配 +29.7%，扫描 P95 +24.8%、分配 +50.1%；CSRedis 批量读 P95 +22.0%、分配 +82.6%，扫描 P95 +24.1%、分配 +66.4%。阈值仍未全部满足，不能关闭 FIX-007。

## External / Deferred

- [ ] Cluster、双节点分片、独立 Hybrid 总线及正式 2 轮预热/10 轮采样/10,000 项矩阵仍需外部环境。
- [ ] T02/T03 后双进程回源证据、100,000 项跨拓扑一致性和真实命令/排队指标继续复用既有 TODO。
