# 生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 状态 |
| --- | --- | --- |
| `RedisLeaseCoordinator.WriteAsync` 提交后失效使用非取消令牌 | `RedisLeaseCoordinatorBatchTest.WriteAsync_CancellationAfterRedisCommit_StillInvalidates` | 已通过 |
| `RedisLeaseCoordinator.RemoveAsync` 提交后失效使用非取消令牌 | `RedisLeaseCoordinatorBatchTest.RemoveAsync_CancellationAfterRedisCommit_StillInvalidates` | 已通过 |
| `RedisLeaseCoordinator.WriteBatchAsync` 首批提交后取消仍失效，后续批次不派发 | `RedisLeaseCoordinatorBatchTest.WriteBatchAsync_CancellationAfterFirstCommit_InvalidatesCommittedBatchOnly` | 已通过 |
| `RedisLeaseCoordinator.RemoveBatchAsync` 首批提交后取消仍失效，后续批次不派发 | `RedisLeaseCoordinatorBatchTest.RemoveBatchAsync_CancellationAfterFirstCommit_InvalidatesCommittedBatchOnly` | 已通过 |
