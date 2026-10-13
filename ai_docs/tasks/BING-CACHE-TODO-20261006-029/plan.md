# BING-CACHE-TODO-20261006-029：单节点集成证据与契约哨兵修正

## 目标

补齐当前可用单节点 Redis 环境下 FreeRedis、CSRedis 和 EasyCaching 的真实集成证据，并修正共享契约测试中把“相似前缀”错误写成“目标前缀子项”的哨兵键，避免把正确的字面量前缀命中误报为实现缺陷。

## 实施范围

1. 调整 `CacheContractAssertions` 的特殊前缀哨兵键，使其不以目标前缀开头，同时继续覆盖 `*`、`?`、花括号和 Unicode 字面量。
2. 在当前隔离的单节点 Redis（127.0.0.1:6379）上运行 FreeRedis、CSRedis 和 EasyCaching 的 net8.0/net6.0 集成测试，记录通过、跳过、失败数量和门控原因。
3. 更新执行记录、TODO 和生产符号到测试映射；保持集群、双分片、独立 Hybrid 总线和正式性能预算的外部阻塞状态，不将跳过项计为通过。

## 约束

- 不改变缓存生产实现、物理键格式、命名空间隔离、流式顺序或租约语义。
- 测试哨兵必须与公共契约一致：目标前缀按字面量匹配，真正以目标前缀开头的键应当命中。
- 集成 Redis 仅使用当前非生产本地端点；不执行 `FLUSHDB`，测试使用唯一命名空间并清理自身键。
- 不伪造 Cluster/分片/总线或性能预算证据。

## 验证

- FreeRedis 集成 net8.0、net6.0。
- CSRedis 集成 net8.0、net6.0。
- EasyCaching 集成 net8.0、net6.0。
- `Bing.EasyCaching` netstandard2.0 生产构建。
- `git diff --check`。
