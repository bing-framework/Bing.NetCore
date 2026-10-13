# 本轮 TODO

- [x] 增加 `MaxInvalidationMessageBytes` 配置和单键超限异常。
- [x] 实现按实际 EasyCaching serializer 载荷拆分失效消息。
- [x] 接入普通 Hybrid 与客户端分片 Hybrid 的同步/异步回调。
- [x] 补充顺序、重复键、取消和总线失败传播测试。
- [x] 更新当前 TODO 索引，区分已完成的消息预算与仍延后的背压/重试/outbox。

## 验收状态

- 默认 `MaxInvalidationMessageBytes=0` 保持单条消息行为；正值按实际 serializer 载荷拆分，单键超限 fail fast。
- 普通 Hybrid 与客户端分片 Hybrid 的同步/异步回调均统一使用拆分器。
- 公共资源测试 net6.0/net8.0 各 202/202；EasyCaching 测试 net6.0/net8.0 各 92 通过、54 个 Redis/Cluster 门控跳过。
- Bing.Caching、Bing.EasyCaching 的 netstandard2.0 构建通过且无编译警告。
- 真实 Redis/总线拓扑未提供；本轮不宣称跨进程消息代理上限、背压、重试或 outbox 能力。
