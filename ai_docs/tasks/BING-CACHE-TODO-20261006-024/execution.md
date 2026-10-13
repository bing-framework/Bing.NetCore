<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261006-024
AI_EXECUTION_FINISHED_AT: 2026-10-05T21:43:00Z

# 执行记录

## 变更

- `CacheKeyFormat` 启动时根据当前运行时的 `Uri.EscapeDataString` 结果生成 128 项 ASCII 原样字符表。
- `TryGetLogicalKey` 对无 `%`、全 ASCII、每个字符均保持原样的后缀直接取得候选逻辑键；百分号、Unicode、空白和需转义字符继续走原完整路径。
- 新增 `TryGetLogicalKey_RoundTripsCanonicalAsciiWithoutChangingPhysicalFormat`，并保留大小写百分号、`%7E`、特殊字符、Unicode、租约键及命名空间隔离回归。

## 影响分析

- 影响面：仅扫描键解析的规范 ASCII 分支；物理键格式、哈希校验、URI 运行时字符集合和拒绝语义不变。
- 兼容性：不使用 netstandard2.0 不具备的 Span URI API；所有不确定输入回退现有实现。
- 性能边界：快路径跳过一次 `UnescapeDataString`/重新 `EscapeDataString`，但仍保留候选子串和哈希分配；正式 keyParse 分配收益需基准确认。

## 验证

- `dotnet build framework/src/Bing.Caching/Bing.Caching.csproj -f netstandard2.0 --no-restore`：0 警告，0 错误。
- `CacheConfigurationAndKeyFormatTest` net8.0：14 通过，0 失败，0 跳过。
- `CacheConfigurationAndKeyFormatTest` net6.0：14 通过，0 失败，0 跳过。
- `git diff --check`：通过。

## TODO 状态

- `OPEN_ACTIONABLE = 0`（本轮范围内）。
- `BLOCKED_EXTERNAL`：Redis MONITOR、Cluster/分片、完整性能基准和历史三方基线仍需要外部环境或历史源码。
- `INHERITED`：正式 P95/分配预算、100,000 项矩阵、T02/T03 后跨进程回源证据仍未重新生成。
- `DEFERRED`：真实网络/连接池/重试指标、EasyCaching List/Hybrid 实机矩阵，以及完全无分配的 Unicode/百分号解析。
