# BING-CACHE-TODO-20261007-080：原生 Redis SCAN 页预算真实回归

## 目标

关闭权威缓存 TODO 中“FreeRedis/CSRedis 原生扫描页预算真实 Redis 回归”的外部证据缺口。使用隔离的单节点 Redis 验证 `CacheResourceOptions.MaxScanPages` 不是只在公共单元测试中存在，而是在真实 Redis `SCAN` 路径上阻止下一页命令并传播正确异常。

## 现状与证据

- 069 已实现 `MaxScanPages`、`CacheScanBudget.ConsumePage()`，并接入 FreeRedis/CSRedis 同步、异步、流式、删除和 Clear 扫描路径。
- 069 的公共预算测试和生产 netstandard2.0 构建已通过，但当时没有可丢弃 Redis，因此真实 Provider 回归仍为 `BLOCKED_EXTERNAL`。
- 当前工作区已有 Docker Redis 镜像和可用运行时；不需要修改生产代码或公共 API。

## 任务

### P1：测试契约

- 在 FreeRedis、CSRedis 集成测试项目新增页预算回归类。
- 每个 Provider 覆盖同步 `GetByPrefix`、异步 `GetByPrefixAsync`、`GetByPrefixStreamAsync`、同步/异步 `RemoveByPrefix` 和同步/异步 `Clear`。
- 通过 `RedisCommandMonitor` 统计带当前物理命名空间的 `SCAN` 命令，避免把其他测试或其他命名空间的扫描计入结果。
- 每个操作使用独立命名空间和足量键，配置 `batchSize=1`、`MaxScanPages=1`；断言抛出 `CacheScanLimitExceededException`、`Kind=ScanPages`、`Consumed=1`，且在操作完成后观察到的匹配 `SCAN` 数量为 1。

### P2：隔离环境与验证

- 使用任务专用单节点 Redis 容器和非生产端口；测试只设置进程级 `RUN_INTEGRATION_TESTS=true` 与 `ConnectionStrings__RedisConnection`。
- 先运行新增测试的 FreeRedis/CSRedis `net8.0`，再运行 `net6.0`；失败时先判断 Provider 行为缺陷还是测试观测问题。
- 清理临时容器/网络，确认仓库原有 Redis 6379 未受影响。

### P3：证据收口

- 更新 080 `execution.md`、`TODO.md`、生产符号到测试方法映射。
- 将权威索引中的真实页预算项从 `BLOCKED_EXTERNAL` 更新为 `CLOSED`；EasyCaching 隐藏分页仍保持 `DEFERRED`。

## 依赖与风险

- 依赖本地 Docker Redis 镜像；不使用生产 Redis，不执行 `FLUSHDB`。
- Redis `SCAN COUNT 1` 的页大小是提示值，不能假设每页恰好返回一项；测试使用足量键并以实际 `MONITOR` 命令数作为断言。
- 删除和 Clear 允许在抛错前产生部分结果；测试只验证页预算和异常，不宣称操作原子性。
- 测试项目变更不改变生产公共契约；风险为低，主要是测试时间和 MONITOR 观测时序。

## Definition of Done

- FreeRedis/CSRedis net6/net8 新增页预算测试全部通过。
- 每个测试明确证明只有一条匹配命名空间的 `SCAN` 命令被发送。
- 任务专用 Redis 清理完成，原有服务未受影响。
- 079 及此前生产代码不被重写；索引、执行报告和映射一致。
