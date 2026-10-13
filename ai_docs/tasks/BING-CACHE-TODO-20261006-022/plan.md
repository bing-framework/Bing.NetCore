# BING-CACHE-TODO-20261006-022：CSRedis 异步批次任务分配

## 目标

消除 CSRedis 异步批量写入和删除中的 `Skip/Take/Select` 热路径分配，改用索引区间和预分配任务数组。保持输入只枚举一次、批次大小、最大并发、取消、异常传播、命名空间键格式和部分成功语义不变。

## 实施范围

1. 修改 `CSRedisCacheManager.SetAllAsync`：按 offset/count 填充 `Task[]`，再等待当前批次。
2. 修改 `CSRedisCacheManager.RemoveAllAsync`：按 offset/count 填充 `Task[]`，再等待当前批次。
3. 在 CSRedis 批量契约中补充或复用覆盖空集合、顺序、取消和异常的测试；不依赖实现细节断言任务数组。
4. 更新执行记录、TODO 和生产符号到测试方法映射。

## 约束

- 不改变同步 API 或 Redis 命令语义。
- 不把任务数组跨批次复用，避免持有已完成 Task 和改变异常观察时机。
- 派发每项前检查取消令牌；已经创建的异步命令不承诺撤销。
- 不重复运行 10k/100k 超时矩阵或历史 baseline；性能收益需要后续同构 Redis 基准确认。

## 验证

- CSRedis 集成契约测试 net6.0/net8.0（Redis 门控关闭时记录 Skip）。
- CSRedis `netstandard2.0` 生产构建。
- `Bing.Caching.Tests` 相关公共契约回归（如受影响）。
- `git diff --check`。
