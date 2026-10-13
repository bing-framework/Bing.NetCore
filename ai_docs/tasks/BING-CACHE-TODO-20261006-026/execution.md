<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261006-026
AI_EXECUTION_FINISHED_AT: 2026-10-05T22:01:53.3387956Z

# 执行记录

## 变更

- `CacheContractAssertions.VerifyAsync` 增加特殊逻辑键流式前缀读取：覆盖花括号、通配字符、反斜线、Unicode、命中 null 和相似前缀哨兵隔离。
- EasyCaching `CacheManagerTests` 增加 `GetAllStream_PropagatesSecondBatchFailureAfterFirstBatch`，验证首批元素仍可消费，第二批异常按原对象传播，调用次数停止在两批。

## 影响分析

- 仅修改测试和公共契约辅助代码，不改变生产 API、键格式、批量大小或异常实现。
- 公共契约会在 Redis 集成环境中同时覆盖 FreeRedis、CSRedis 和 EasyCaching；无端点时只证明编译，不能宣称运行通过。
- 特殊键测试使用唯一命名空间并显式删除测试键，未使用整库清理。

## 验证

- EasyCaching `CacheManagerTests` net8.0：28 通过，0 失败，0 跳过。
- EasyCaching `CacheManagerTests` net6.0：28 通过，0 失败，0 跳过。
- FreeRedis 公共契约 net8.0：1 Skip，原因是未启用 Redis 集成门控；项目编译成功。
- CSRedis 公共契约 net8.0：1 Skip，原因是未启用 Redis 集成门控；项目编译成功。
- `git diff --check`：通过。

## TODO 状态

- `OPEN_ACTIONABLE = 0`（本轮范围内）。
- `BLOCKED_EXTERNAL`：Redis 公共契约实际运行、MONITOR、Cluster/分片/独立总线矩阵和严格历史三方基线仍需要外部环境或历史源码。
- `INHERITED`：正式批量读/扫描 P95 与分配预算、100,000 项一致性、T02/T03 后跨进程回源证据仍未闭环。
- `DEFERRED`：流式第二批后的 Cluster/分片故障实机传播、服务端命令停止、Hybrid 跨实例批量实机和真实网络指标。
