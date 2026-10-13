# 缓存删除取消测试闸门计划

## 目标

消除 FreeRedis、CSRedis 删除扫描取消集成测试对 Redis `MONITOR` 调度时序的依赖，使用测试专用 RESP 代理固定“首个匹配 `DEL` 请求已发送但响应尚未返回”的窗口。

## 范围

- 在 `Bing.Test.Shared` 增加可复用的 Redis 删除响应闸门。
- FreeRedis、CSRedis 的 `ScanDeleteBatchTest` 改用闸门连接。
- 闸门只用于测试，不修改生产 Redis 客户端或取消语义。
- 不执行 `FLUSHDB`，测试仍使用随机命名空间并在 finally 清理。

## 设计要求

1. 代理解析 RESP 请求帧，转发全部非目标命令。
2. 第一个包含当前测试命名空间的 `DEL` 请求转发到上游，但暂存其完整响应，不写回客户端。
3. 暴露 `DeleteResponseCaptured`、`ReleaseHeldDeleteResponse` 和已观察目标命令计数，测试在捕获后取消，再释放响应。
4. 代理支持多个连接、TCP 分片、异常清理和取消；释放或释放代理时不得遗留监听/上游连接。
5. 两个 Provider 的 net6.0/net8.0 测试稳定断言：取消异常传播，目标 `DEL` 计数为 1。

## 验证

- L0：共享测试项目和两个集成测试项目 Release 构建、`git diff --check`。
- L1：FreeRedis/CSRedis 删除取消单测各 TFM。
- L2：两个 Redis 集成项目完整门控测试，Cluster/分片按条件跳过。
