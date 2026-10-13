<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261006-023
AI_EXECUTION_FINISHED_AT: 2026-10-05T21:33:03.8444945Z

# 执行记录

## 变更

- `CacheKeyFormat.HashContext` 新增固定长度标签字符缓冲，`FormatPhysicalKey` 复用该缓冲生成十六进制哈希标签。
- 每个物理键仍通过 `new string` 拷贝标签内容，返回值不引用后续生成使用的可变缓冲。
- 新增 `PhysicalKeys_BatchResultsRemainStableAfterLaterGeneration`，覆盖批量结果在后续键生成后保持稳定，并与单键格式一致。

## 影响分析

- 影响面：仅物理键格式化内部热路径；SHA 输入、哈希槽标签、URI 转义、版本化前缀和租约键格式不变。
- 并发边界：`HashContext` 仍为单次操作私有实例，复用字符缓冲不会扩大跨线程共享范围。
- 性能边界：去除每键 `char[32]` 临时数组；正式 P95 和分配预算仍需后续同构基准确认。

## 验证

- `dotnet build framework/src/Bing.Caching/Bing.Caching.csproj -f netstandard2.0 --no-restore`：0 警告，0 错误。
- `CacheConfigurationAndKeyFormatTest` net8.0：13 通过，0 失败，0 跳过。
- `CacheConfigurationAndKeyFormatTest` net6.0：13 通过，0 失败，0 跳过。
- `git diff --check`：通过。

## TODO 状态

- `OPEN_ACTIONABLE = 0`（本轮范围内）。
- `BLOCKED_EXTERNAL`：Redis MONITOR、Cluster/分片、完整性能基准和历史三方基线仍需要外部环境或历史源码。
- `INHERITED`：正式 P95/分配预算、100,000 项矩阵、T02/T03 后跨进程回源证据仍未重新生成。
- `DEFERRED`：真实网络/连接池/重试指标、EasyCaching List/Hybrid 实机矩阵，以及 `TryGetLogicalKey` 解析分配优化。
