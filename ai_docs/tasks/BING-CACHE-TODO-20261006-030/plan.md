# BING-CACHE-TODO-20261006-030：批量 JSON 组装分配优化

## 目标

处理性能审查 `BING-CACHE-PERF-STREAM-20261004-002` 的 FIX-007 中尚可在工作区直接修复的分配热点。当前 FreeRedis/CSRedis 标量批量读取在进入一次 JSON 数组解析前，仍为每个 CSRedis 原始字节值创建 UTF-8 临时字符串，并为数组文本保留 `StringBuilder` 内部缓冲和最终字符串；FreeRedis 也保留了可复用的中间字符缓冲机会。

本轮使用池化字符缓冲直接拼接 JSON 数组，并在确认该热点后将物理键转义/拼接改为池化 UTF-8/字符缓冲，只产生一份最终键字符串；不改变 Redis 返回类型、序列化格式、命中状态、顺序、命名空间隔离或资源预算。

## 实施范围

1. 在 `Bing.Caching` 增加内部 UTF-8/字符串 JSON 批量数组组装工具，使用 `ArrayPool<char>`，处理 null、空白 JSON null、默认值 token 和 UTF-8 解码。
2. FreeRedis、CSRedis 标量快路径改用共享组装工具；对象、自定义序列化器和非默认设置继续逐项解码。
3. 将物理键生成中的 URI 转义和前缀/哈希标签拼接改为池化缓冲；保持现有 v2 字节格式与大写百分号编码。
4. 为组装工具和键格式补充直接单元测试，覆盖字符串/字节输入、null/default、Unicode、空批次、特殊键和格式兼容边界。
5. 使用同一可用 Redis 端点建立固定 baseline worktree，并执行 baseline/current 受控分配对照；历史预算是否闭环必须依据相同端点、运行时和采样参数。
6. 更新 `execution.md`、`TODO.md`、`test-mapping.md`；若结构性收益不足以关闭 FIX-007，保留为新的性能证据并明确剩余预算差距。

## 约束

- 仅池化临时构造缓冲，归还前不得让 Newtonsoft.Json 或调用方持有其引用。
- 不放宽 `CacheBatchBudget`，不删除物理键校验，不改变 `CacheEntry<T>.Hit`。
- 不改变公共 API 和 netstandard2.0 目标框架；不引入需要更高 TFM 的 API。
- 取消、部分成功、异常主次关系和批次顺序必须保持现有契约。

## 验证

- `Bing.Caching.Tests` 新增工具测试 net6.0/net8.0。
- FreeRedis、CSRedis 相关精准测试及现有单节点集成回归。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0 构建。
- 可用 Redis 上的当前版定向批量读/扫描分配探针；不重复已否定的键格式、MGET 返回类型或同一解析策略实验。
- `git diff --check`。
