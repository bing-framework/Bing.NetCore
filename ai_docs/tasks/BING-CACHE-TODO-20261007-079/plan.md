# BING-CACHE-TODO-20261007-079：Redis Cluster 三主三从实机门禁

## 目标

利用本机已有 Redis 7.4 镜像创建任务专用的三主三从 Redis Cluster，补齐 FreeRedis、CSRedis 的跨槽批量、全主节点扫描、Clear、拓扑变化和故障转移实机证据。所有容器、端口和键空间仅供本任务使用，收口时精确删除。

## 范围

- 创建 6 个临时 Redis 节点：主节点 `7000`、`7001`、`7002`，副本节点 `7003`、`7004`、`7005`；对应 Cluster bus 端口为 `17000`–`17005`。
- 启用 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_CLUSTER_TESTS=true`，运行 FreeRedis/CSRedis `ClusterCacheTest` 的正常跨槽批量、List/Stream、前缀扫描和命名空间 Clear 门禁。
- 在独立可丢弃集群上按测试已有门控运行故障转移、拓扑变化、槽迁移和扫描在途失败测试；每个 Provider、TFM 和门控结果单独记录。
- 测试结束验证原有 `redis:6379` 服务仍存在，并删除本任务专用网络和 6 个容器。

## 不在本任务内

- 不开启 10,000/100,000 项大规模矩阵，既有性能边界沿用索引结论。
- 不修改公共缓存接口、键格式、Provider 注册方式或集群拓扑算法，除非门禁复现新的可执行生产缺陷；若需修改，先更新 Change Impact Analysis。
- 不把三主无副本或单节点结果当作三主三从故障转移证据。

## 验收

- Cluster `CLUSTER NODES` 明确包含 3 个在线 master、3 个在线 replica，槽位覆盖完整且无重复。
- FreeRedis 与 CSRedis 正常 Cluster 门禁在 net6.0/net8.0 无失败；故障、迁移和扫描在途门禁的 Skip/Pass/Fail 原因可追溯。
- 任何节点异常或拓扑视图不一致不得被报告为局部成功；失败时保留原始异常和已产出数量语义。
- 生产源码若未修改，复用已有 Provider 构建证据；如发生修改，只运行受影响 L1/L2/L3 测试并更新映射。
