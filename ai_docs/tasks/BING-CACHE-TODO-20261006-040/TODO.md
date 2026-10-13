# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算尚未闭环；本轮只处理 EasyCaching 解码中间数组。

## Completed

- [x] `DecodeRawRedisBatch<T>` 直接对 `RedisValue[]` 做预算检查和逐项反序列化，移除 `byte[][]` 中间数组。
- [x] 新增数据库批量响应单元测试，覆盖顺序、缺失值、命中状态和反序列化调用次数。
- [x] EasyCaching net6.0/net8.0 完整测试通过，生产 `netstandard2.0` Release 构建通过。
- [x] `git diff --check` 无错误。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵和外部资源指标仍需专用环境。

## Deferred

- [ ] 100,000 项 EasyCaching 正式性能矩阵、网络往返、连接池和 Provider 内部排队指标。
