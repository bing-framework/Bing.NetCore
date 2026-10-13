# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试或证据 |
| --- | --- | --- |
| `CSRedisCacheManager.SetAllAsync` | 空输入不派发；取消在派发前抛出；批量写入保持公共契约 | `BatchContractTest.BatchWritesAndRemoves_HonorEmptyInputAndCancellationBeforeDispatch`；既有 `SetAllAsync` 批量契约；`netstandard2.0` 构建 |
| `CSRedisCacheManager.RemoveAllAsync` | 空输入不派发；取消在派发前抛出；批量删除保持公共契约 | `BatchContractTest.BatchWritesAndRemoves_HonorEmptyInputAndCancellationBeforeDispatch`；既有 `RemoveAllAsync` 批量契约；`netstandard2.0` 构建 |
| CSRedis 批量任务边界 | 输入只枚举一次、顺序/重复/缺失语义和取消消费回归 | `GetAllAsync_PreservesOrderDuplicatesAndMissingDefault_AndEnumeratesOnce`、`GetAllStream_CancellationDuringConsumption_StopsNextBatch` 及同文件既有批量测试 |
