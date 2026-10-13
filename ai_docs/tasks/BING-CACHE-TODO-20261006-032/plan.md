# 缓存前缀扫描物理键快路径计划

## 目标

降低 FreeRedis、CSRedis 异步 `GetByPrefixAsync` 的扫描分配：当前 List API 只需要物理键和缓存值，却通过 `TryGetLogicalKey` 为每个扫描结果构造逻辑键，再用 `StartsWith` 过滤。对规范 ASCII 逻辑键增加只返回布尔结果的物理键匹配路径，保留特殊字符和 Unicode 的完整解析回退。

## 根因与验收标准

- 根因：列表前缀扫描为每个候选键创建逻辑键字符串，并为 ASCII 后缀执行一次 `Substring` 与 UTF-8 计数/编码；这些结果随后不会暴露给调用方。
- 常见的转义前缀（例如 `item:`）必须在不构造逻辑键的情况下完成物理前缀比较、规范转义校验和命名空间哈希标签校验；纯规范 ASCII 前缀作为同一快路径的特例。
- 包含百分号编码、Unicode、反斜线或其他非规范 ASCII 的物理键必须回退到既有 `TryGetLogicalKey` 语义。
- 篡改哈希标签、相似命名空间、租约键和非法物理布局必须返回不匹配。
- `GetByPrefixAsync` 保持扫描顺序、批大小、取消、拓扑完整性校验、Cluster/分片路由和缺失值语义。
- Stream API 继续返回逻辑键和 `Hit` 状态，不改变其解析行为。

## 变更范围

### 共享键组件

- 在 `CacheKeyFormat.HashContext` 增加 ASCII 物理后缀哈希计算，复用现有池化输入缓冲。
- 增加内部 `TryMatchLogicalPrefix`：调用方一次性生成逻辑前缀的规范 URI 编码；逐键校验物理转义并将编码后缀还原为 UTF-8 字节，在不构造逻辑键的情况下比较前缀和验证 hash tag；非法或无法验证的输入回退到既有逻辑键解析。

### Redis Provider

- FreeRedis、CSRedis 各增加只产生物理键的异步扫描枚举。
- 两个 Provider 的异步 `GetByPrefixAsync` 改用该枚举；同步 API 和 Stream API 保持原路径。
- 不改变扫描前后拓扑校验、主节点筛选、批量读取或取消派发规则。

### 测试与证据

- 在 `Bing.Caching.Tests` 增加 ASCII 匹配/不匹配、哈希篡改、特殊字符回退、相似命名空间和租约键测试。
- 复用现有 FreeRedis/CSRedis 集成特殊键和 List/Stream 一致性测试；补充异步前缀 List 特殊键回归（如当前缺失）。
- 运行共享键精准测试、两个 Provider 构建及受影响单节点集成测试；必要时只做一次 1k 方向性诊断，不重建历史 baseline。

## 影响分析

ChangedFiles: `CacheKeyFormat.cs`、FreeRedis/CSRedis 异步扫描 partial 和 List 方法、共享键单元测试及本任务文档。
ChangedProjects: `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.Caching.Tests`。
ChangedPublicContracts: 无。
ChangedRuntimePaths: FreeRedis/CSRedis `GetByPrefixAsync` 单节点、Cluster、客户端分片的扫描过滤前置路径。
ChangedProviders: FreeRedis、CSRedis。
ChangedTFMs: netstandard2.0 生产库，net6.0/net8.0 测试。
ChangedBuildPackaging: 无。
ChangedBenchmarkHarness: 无。
ChangedDocsOnly: 本任务 `ai_docs/tasks/BING-CACHE-TODO-20261006-032`。
AffectedDependents: 两个 Redis Provider 的批量前缀 List 调用方；Stream、同步 API 和 EasyCaching 不直接改变。
RiskLevel: MEDIUM。

## 非目标

- 不调整物理键格式或命名空间版本。
- 不改变公共接口或 `CacheEntry<T>`。
- 不重新尝试已验证无效的 key-format/hash 或 CSRedis string MGET 候选。
- 不重建固定 baseline，不声称正式性能预算通过。
