# P6/P7 缓存计划证据索引补全

## Completed

- [x] 将 Core `SingleTypeLocalMemoryBase` 的锁、快照、并发添加、更新和批量删除语义加入权威索引。
- [x] 记录 `LocalMemoryCacheBaseTest` net6.0/net8.0 各 5/5 通过。
- [x] 将 EasyCaching Memory/Redis/Hybrid 注册和通用 `ClientSharded` 拒绝边界加入权威索引。
- [x] 记录专用 `AddShardedRedisCaching`/`AddShardedHybridCaching` 入口及已有注册测试。
- [x] 明确真实双节点分片和独立总线仍属于 `BLOCKED_EXTERNAL`，不以本地注册测试替代。

## Open Actionable

- 无。

## Blocked Approval

- 无。

## Blocked External

- [ ] 双节点客户端分片和独立 Hybrid 总线的真实运行证据仍需要外部端点。

## Not Applicable

- 通用 `AddRedisCaching`/`AddHybridCaching` 不接受 `ClientSharded` 是显式入口边界，不是分片功能缺失。

## Verified Boundary / Deferred

- [ ] 正式批量/扫描性能预算、真实网络观测和严格三方 baseline 沿用权威索引分类，本轮不重复。
