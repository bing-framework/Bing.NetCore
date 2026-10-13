<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261006-030
AI_EXECUTION_FINISHED_AT: 2026-10-05T23:05:00Z

# 执行记录

## 变更

- 新增 `CacheJsonBatch`：用 `ArrayPool<char>` 直接拼接 Redis JSON 片段，避免 CSRedis 每个值先生成 UTF-8 临时字符串；FreeRedis 同样避免 `StringBuilder` 内部缓冲。保留空白、JSON `null`、缺失值和默认 token 的原有分支语义。
- FreeRedis/CSRedis 标量批量读取接入共享组装路径；对象类型、自定义序列化器和全局 Newtonsoft 设置仍逐项解码。
- `CacheKeyFormat` 物理键生成改为池化 UTF-8/字符缓冲：一次生成最终物理键字符串，减少 URI 转义字符串和前缀拼接中间分配。百分号转义使用大写十六进制，哈希标签仍使用小写，保持 v2 物理格式逐字节兼容。
- 新增 `CacheJsonBatchTest` 8 项直接测试；复用并通过现有键格式兼容、特殊字符、哈希校验和命名空间隔离测试。

## 影响分析

- 只改变临时缓冲的构造和归还时机，不改变物理键格式、命中状态、批次顺序、取消语义、租约提交或资源预算。
- `ArrayPool` 缓冲在返回最终字符串前不会暴露给 Redis 客户端或 JSON 解析器；异常路径也归还缓冲。
- 当前版 1k 对照显示分配下降方向，但正式性能预算仍未闭环，尤其 CSRedis 批量和两 Provider 扫描路径仍超预算。

## 验证

- `CacheJsonBatchTest` net8.0：8 通过；net6.0：8 通过。
- `CacheConfigurationAndKeyFormatTest` net8.0：14 通过；net6.0：14 通过。
- `Bing.Caching.Tests` 完整 net8.0：158 通过；net6.0：158 通过。
- FreeRedis/CSRedis 共享契约与批量精准集成：net8/net6 各 15 通过、1 跳过（100,000 项门控）。
- EasyCaching Redis 共享契约与批量流式测试：net8/net6 各 2 通过。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0：均 0 警告、0 错误。
- `git diff --check`：通过；仅报告仓库既有 CRLF 转换提示。

## 性能证据

固定 baseline worktree：`24c93cb1a5355797223637e2d42ac3dce968d7ef`；同端点 `127.0.0.1:6379`、.NET 8.0.30、1,000 项、1 轮预热、3 轮样本、并发 8。

| Provider | 阶段 | baseline P95 | current P95 | P95 回退 | baseline 平均分配 | current 平均分配 | 分配回退 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| FreeRedis | batchRead | 4.73 ms | 6.22 ms | +31.4% | 1,843,720 B | 2,395,736 B | +29.9% |
| FreeRedis | scan | 10.05 ms | 13.10 ms | +30.4% | 2,825,965 B | 4,266,235 B | +51.0% |
| CSRedis | batchRead | 4.50 ms | 5.83 ms | +29.5% | 899,352 B | 1,650,272 B | +83.5% |
| CSRedis | scan | 10.20 ms | 14.27 ms | +39.9% | 1,223,717 B | 2,064,251 B | +68.7% |

原始文件：

- `baseline-freeredis-1k.json`、`current-keypool-freeredis-1k.json`
- `baseline-csredis-1k.json`、`current-keypool-csredis-1k.json`
- `current-freeredis-1k.json`、`current-csredis-1k.json`（仅 JSON 池化阶段的前一候选）

本轮结果是同端点、同运行时的方向性 1k 对照，但采样数低于正式验收要求；FIX-007 仍为 `OPEN_ACTIONABLE`。
