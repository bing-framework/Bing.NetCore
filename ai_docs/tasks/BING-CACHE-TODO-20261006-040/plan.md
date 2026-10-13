# BING-CACHE-TODO-20261006-040：EasyCaching 批量响应中间数组消除

## 目标

消除 EasyCaching Redis/Hybrid 批量读取在解码前创建的 `byte[][]` 中间数组。`RedisValue[]` 已经是 Redis 客户端返回的批量响应，当前实现又复制一层引用数组后才做预算检查和逐项反序列化；该复制不参与业务语义。

## 范围

- 修改 `Bing.EasyCaching` 内部 `DecodeRawRedisBatch`，直接对 `RedisValue[]` 做预算检查并逐项转换。
- 保留 null/default、序列化异常、输入顺序、重复键、命名空间隔离、批量预算和 Cluster/Hybrid 路由语义。
- 删除不再需要的 `ToByteArrays` 辅助方法。
- 不修改公共 API、Redis 命令、租约、Provider 注册或 Benchmark 口径。

## Change Impact Analysis

- ChangedFiles：`CacheManager.Hybrid.cs`、本任务执行记录、TODO、生产符号映射。
- ChangedProjects：`Bing.EasyCaching` 及其测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：Redis/Hybrid 原始批量响应解码私有路径。
- ChangedProviders：EasyCaching Redis、Hybrid。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮不重复正式性能矩阵。
- AffectedDependents：EasyCaching 批量 List/Stream、Cluster 分组读取和 Hybrid 回填。
- RiskLevel：MEDIUM；仅移除不必要的引用数组，必须验证 null/default、复杂序列化和异常传播。

## 验收

1. `DecodeRawRedisBatch` 不再创建 `byte[][]` 中间数组。
2. EasyCaching 批量/流式契约、序列化边界和取消测试通过。
3. `Bing.EasyCaching` netstandard2.0 Release 构建通过，`git diff --check` 无错误。
4. 性能预算仍保持开放；本轮结构优化不能单独宣称 FIX-007 通过。
