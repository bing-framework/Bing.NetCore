# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| `HybridInvalidationMessageBatcher.CreateMessages` | `HybridInvalidationMessageBatcherTest.Publish_UsesMeasuredMessageInstance` | net6.0/net8.0 各 6/6 精准测试通过；追踪 serializer 确认发布对象来自已测量实例 |
| 正值消息预算拆分 | `HybridInvalidationMessageBatcherTest.Publish_SplitsBySerializedBytesAndPreservesDuplicates` | net6.0/net8.0 各 6/6 精准测试通过；顺序、重复键和单键超限由当前任务与 070 回归覆盖 |
| 默认 0 行为 | `HybridInvalidationMessageBatcherTest.Publish_DefaultBudgetKeepsOneMessageAndInputOrder` | net6.0/net8.0 各 6/6 精准测试通过；默认不测量且保持单消息行为 |

上一轮任务 070 的 EasyCaching.Tests 完整门控证据（net6.0/net8.0 各 92 通过、54 跳过）在本轮未受影响，按相同作用域复用；Redis/Cluster 集成跳过仍由外部环境门禁决定。
