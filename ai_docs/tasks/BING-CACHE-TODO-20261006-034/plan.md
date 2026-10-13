# 当前源码双进程回源协调验证计划

## 目标

补齐缓存计划中 T02/T03 后的跨进程协调证据。历史 `Bing.Caching.ProcessProbe` 证明过 FreeRedis、CSRedis 和 EasyCaching Redis 的租约行为，但部分协调生命周期代码在后续任务中调整过；本轮使用当前工作树重新执行同一矩阵，确认实现没有回归。

## 验证范围

- 正常同键回源合并：两个进程只执行一次工厂。
- 持有者异常退出：等待者在租约过期后接管并写入新值。
- 回源超时：旧工厂晚返回时不得写入缓存，后续进程可以接管。
- 主动失租：旧持有者提交被拒绝，缓存保持未写入。
- 等待者超时：等待已有租约超过上限时显式失败。
- Provider：FreeRedis、CSRedis、EasyCaching Redis。

## Change Impact Analysis

ChangedFiles: 仅本任务执行记录、TODO、测试映射和验证产物；不修改生产代码。
ChangedProjects: `Bing.Caching.ProcessProbe` 运行时验证。
ChangedPublicContracts: 无。
ChangedRuntimePaths: 当前三个 Provider 的跨进程回源协调、租约续期、令牌校验和清理路径。
ChangedTFMs: ProcessProbe `net8.0`；引用生产 `netstandard2.0` Provider。
RiskLevel: LOW；使用随机命名空间，不执行 `FLUSHDB`，端点为本机隔离 Redis。

## 验收

- `Run-ProcessProbe.ps1 -Connection 127.0.0.1:6379 -Provider both -Configuration Release -NoBuild` 全部场景成功。
- 每个 Provider 的正常场景工厂计数为 1；故障场景满足 README 中的退出码和输出断言。
- 记录完整输出摘要、执行时间、端点和代码状态。
- 若验证失败，暂停收口并创建最小修复；若通过，将旧的 T02/T03 证据缺口标记为当前源码已验证，但保留 Cluster/分片/性能等其他 TODO。
