# BING-CACHE-TODO-20261006-025：EasyCaching 批量投影分配

## 目标

减少 EasyCaching Redis/Hybrid 批量读取热路径中 `Select(...).ToArray()` 和 `Enumerable.Range(...).Select(...)` 的迭代器、闭包和重复投影分配，保持 Redis/Cluster/Hybrid 的批次边界、顺序、命中状态、TTL 回填和异常语义不变。

## 实施范围

1. 在 Hybrid 原子读取脚本、Redis 批量读取和异步读取中使用预分配数组填充 `RedisKey[]`。
2. 在原始 `RedisResult[]`/`RedisValue[]` 转换路径中使用显式索引循环。
3. 为批量预算检查增加基于索引选择器的内部循环，避免只为校验创建 LINQ 枚举器。
4. 复用现有 EasyCaching 批量、Hybrid、Cluster 路由测试，必要时补充投影结果一致性测试。

## 约束

- 不改变 `CacheBatchAdaptive` 的拆分和最大负载预算。
- 不改变 Cluster 同槽分组、Hybrid 原子 GET/PTTL、失效通知或本地回填时序。
- 不改变 `RedisResult.IsNull`、`RedisValue` null、序列化和异常传播语义。
- 仅移除迭代器/闭包分配；正式 P95/分配预算仍需同构基准确认。

## 验证

- EasyCaching 批量/Hybrid/Cluster 相关单元测试 net6.0/net8.0。
- `Bing.EasyCaching` `netstandard2.0` 生产构建。
- `git diff --check`。
