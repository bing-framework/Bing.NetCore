# BING-CACHE-TODO-20261007-094

## 本轮目标

补齐 Hybrid Redis outbox 的只读运维可观测性，帮助识别积压、老消息和发布故障，同时保持默认行为与投递语义不变。

## TODO

- [x] 增加 `IHybridInvalidationDeliveryDiagnostics` 和不可变快照类型。
- [x] 快照报告 pending/processing 深度、最早消息年龄、成功发布、失败尝试、入队计数和最近失败信息。
- [x] Redis envelope 保存入队 Unix 毫秒时间；旧 envelope 缺少时间字段时仍可投递。
- [x] outbox 状态读取只读取列表长度与队尾样本，不完整收集或移动消息。
- [x] Hybrid 单实例和客户端分片 DI 注册诊断服务。
- [x] 补充无 outbox、失败计数、入队年龄、processing 状态和真实 Redis 状态读取测试。
- [x] 更新 README、迁移说明、权威 TODO 索引和生产符号映射。
- [x] 明确死信、毒消息、保留期和跨进程全局计数不在本轮范围。

## 本轮未解决的运维 TODO

- [ ] 增加 poison message 的最大生命周期、死信转移和人工重放机制。
- [ ] 增加 pending/processing/dead-letter 的保留期、空间预算和清理策略。
- [ ] 跨进程聚合成功/失败计数并接入统一指标后端。
