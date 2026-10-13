# 本轮 TODO

- [x] 将 FreeRedis 同步 ClientSharded `GetAll` 和前缀批次读取复用有界异步分片路由。
- [x] 将 CSRedis 同步 ClientSharded `GetAll` 和前缀批次读取复用有界异步分片路由。
- [x] 为两个 Provider 补充同步顺序、重复键、缺失值和前缀一致性回归证据。
- [x] 更新执行记录、生产符号映射和权威 TODO 索引。

## 分类边界

- 本轮只处理 FreeRedis/CSRedis 同步客户端分片读取调度。
- EasyCaching 分片扫描并发、真实双节点/双进程/总线和正式性能预算继续按权威索引中的 `DEFERRED`、`BLOCKED_EXTERNAL` 或 `VERIFIED_BOUNDARY` 处理。

## 本轮结论

- FreeRedis 和 CSRedis 的同步 ClientSharded List/前缀批次读取现在复用已有 `MaxConcurrency` 有界异步路由，保留输入顺序、重复键和缺失/default 语义。
- 同步桥接不使用 `Task.Run`；已发送命令的取消语义仍只适用于异步 API。
- 真实双节点并发和路由结果仍需要第二个非生产 Redis 节点，当前测试因环境门控跳过。
