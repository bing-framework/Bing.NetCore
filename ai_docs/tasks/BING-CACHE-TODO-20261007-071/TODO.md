# 本轮 TODO

- [x] 保存并发布已通过预算测量的同一消息实例。
- [x] 增加动态 serializer 元数据测试，防止重新生成消息绕过预算。
- [x] 更新任务证据和权威索引状态。

## 完成证据

- `HybridInvalidationMessageBatcher` 的正值预算路径现在保存测量过的 `EasyCachingMessage`，拆分和收尾直接发布该实例，不重新生成 `Id` 或消息元数据。
- `HybridInvalidationMessageBatcherTest.Publish_UsesMeasuredMessageInstance` 使用追踪 serializer 验证每个发布消息的 `Id` 都来自已测量实例；net6.0/net8.0 均为 6/6 通过。
- `Bing.EasyCaching` netstandard2.0 生产构建通过，0 警告、0 错误；权威索引和执行报告已更新。
