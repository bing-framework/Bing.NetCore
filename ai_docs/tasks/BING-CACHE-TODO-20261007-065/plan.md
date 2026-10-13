# EasyCaching 协调删除命令预算修复计划

## 背景

上一轮将 EasyCaching Redis/Hybrid 的协调删除从逐键命令改为批量 `KeyDelete`，但 `_coordinator` 分支提前返回，绕过了既有 `CacheResourceOptions.MaxBatchCommandBytes` 删除预算。这样大键批次可能超出配置的 Redis 命令参数上限，且 README 仍描述旧的逐键行为。

## 实施范围

- `RedisLeaseCoordinator.RemoveBatch` 与 `RemoveBatchAsync` 先按 Redis Cluster hash slot 分组，再按 `BatchSize` 和 `MaxBatchCommandBytes` 使用共享 `CacheDeleteBatchBudget.Partition` 切分。
- 异步删除对切分后的批次使用 `MaxConcurrency` 有界派发；取消停止后续派发并等待已发送命令，保留已提交删除无法回滚和部分成功语义。
- 每个实际 Redis 删除批次同时删除数据键和 `:lease` 键，并为该批次执行一次 Hybrid 批量失效回调。
- 更新 EasyCaching README，准确说明协调删除的字节预算和 Hybrid 批量失效行为。
- 新增同步/异步预算测试，覆盖多批、Cluster 分组和单键超预算 fail-fast。

## 非目标

- 不改变公开接口、物理键格式、租约语义、命名空间隔离或批量写入路径。
- 不修改 FreeRedis、CSRedis 或正式性能预算，不运行外部 Cluster/分片环境。

## 验收

1. 协调删除永不发送超过 `MaxBatchCommandBytes` 的数据键/租约键参数批次。
2. 单键对超过预算时在发送 Redis 命令前抛出预算异常。
3. 同步/异步和 Cluster 分槽路径都保持批量失效、取消与异常传播语义。
4. EasyCaching `netstandard2.0` 构建、精准测试、受影响项目回归和 `git diff --check` 通过。
