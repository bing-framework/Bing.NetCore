# 缓存删除批次 TODO

## 已完成

- [x] `CacheResourceOptions.MaxBatchCommandBytes` 配置和参数校验。
- [x] 按 UTF-8 字节预算与 `BatchSize` 切分删除批次。
- [x] 数据键与 `:lease` 键保持同一子批次；单键超限时发送前失败。
- [x] FreeRedis、CSRedis、EasyCaching Redis/Cluster/客户端分片接入。
- [x] 公共边界、EasyCaching mock、FreeRedis/CSRedis MONITOR 门控测试和文档。

## TODO：外部阻塞

- [ ] `BLOCKED_EXTERNAL`：启用非生产单节点 Redis，验证真实 `DEL` 命令数量、长 Unicode 键切分和跨命名空间哨兵保护。
- [ ] `BLOCKED_EXTERNAL`：在三主三从 Cluster、双节点客户端分片和 Hybrid 总线环境验证跨槽/跨节点删除、拓扑变化和失效传播。

## TODO：欠缺考虑或延后

- [ ] `DEFERRED`：若需要严格网络请求上限，补充 RESP 协议帧、TLS 和客户端队列的字节开销模型；当前预算只覆盖键参数。
- [ ] `DEFERRED`：补做旧版/当前版/优化版的删除命令数、P50/P95/P99、分配量和工作集对照；不能用本轮单元测试推导性能收益。
- [ ] `ACCEPTED_LIMITATION`：多批次删除没有跨批次事务性；部分成功后抛异常，调用方需要按部分完成处理。

## 当前可执行项

- `OPEN_ACTIONABLE = 0`。本轮同范围实现已闭环，剩余项目依赖外部 Redis 拓扑或另一个性能基准任务。
