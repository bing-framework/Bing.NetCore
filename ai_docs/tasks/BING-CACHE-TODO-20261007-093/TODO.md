# BING-CACHE-TODO-20261007-093

## 本轮目标

为 EasyCaching Hybrid 失效通知补齐默认兼容的有限重试、背压和 Redis outbox，同时保留当前操作失败语义与至少一次投递边界。

## TODO

- [x] 增加实例级 `HybridInvalidationDeliveryOptions`，校验尝试次数、重试间隔、队列上限和轮询间隔。
- [x] 在同步和异步 Hybrid 失效路径接入有限重试；最终失败继续传播原始总线异常。
- [x] 增加 Redis pending/processing outbox，使用原子脚本限制两类消息总量，并修正 FIFO 入队顺序。
- [x] 让后台投递器在发布成功后确认删除，失败或取消时保留 processing 消息，重建投递器可恢复。
- [x] 在 `AddHybridCaching` 与 `AddShardedHybridCaching` 增加可选配置并注册单例投递器；默认配置不创建 outbox。
- [x] 保持 Hybrid Manager、客户端分片和已有直接批量发布测试的兼容调用链。
- [x] 补充重试、队列满、恢复、取消、Redis 列表容量和进程重建测试。
- [x] 更新 EasyCaching README、缓存命名空间迁移说明和权威 TODO 索引。
- [x] 记录跨产品消息代理最大帧协商、真实网络往返和客户端排队观测仍属于独立延后事项。

## 本轮不改变的边界

- outbox 默认关闭；启用时要求分布式 EasyCaching Redis Provider 暴露 `StackExchange.Redis.IDatabase`。
- 当前缓存操作不会因为成功入队而静默成功；队列满、入队失败或最终发布失败仍报告原始异常。
- processing 恢复允许重复失效，不保证严格一次、跨键事务或全局顺序。
- Redis 写入完成到创建失效消息之前的进程崩溃窗口仍没有事务性 outbox 保证。
