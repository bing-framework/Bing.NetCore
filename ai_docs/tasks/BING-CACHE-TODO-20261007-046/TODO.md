# 本轮 TODO

## Open Actionable

- [ ] 无。性能预算属于 `VERIFIED_BOUNDARY`，本轮不重复已停止的同类基准实验。

## Completed

- [x] FreeRedis 双节点分片 List/Stream 和前缀 Stream 契约测试（代码已补，实机待门控）。
- [x] CSRedis 双节点分片 List/Stream 和前缀 Stream 契约测试（代码已补，实机待门控）。
- [x] 同步过期任务文档中的 `FIX-007` 状态。

## Blocked External

- [ ] 双节点 Redis 分片真实运行证据取决于 `BING_TEST_REDIS_SHARD_CONNECTION`。
- [ ] 三主三从 Cluster、故障转移、独立 Hybrid 总线和完整性能矩阵仍需专用环境。

## Verified Boundary / Deferred

- [ ] 批量读/扫描 P95 与分配预算仍未达标，保留为 `VERIFIED_BOUNDARY`；不通过放宽阈值关闭。
- [ ] 真实网络往返、客户端内部排队和严格三方 baseline 继续按前序任务的 `DEFERRED/BLOCKED_EXTERNAL` 记录。
