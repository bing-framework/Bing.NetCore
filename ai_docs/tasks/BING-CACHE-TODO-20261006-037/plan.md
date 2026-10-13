# BING-CACHE-TODO-20261006-037：标量批量解码方向性性能验证

## 目标

验证任务 021 已实现的 FreeRedis/CSRedis 默认标量批量解码优化：从 `Nullable<T>[]`、反射 `Array.GetValue` 和逐项装箱改为直接反序列化 `T[]`。本轮只验证该新候选，不修改生产代码，不重复数组复用、键格式、字符串 MGET 或旧 JSON 拼接实验。

## 实验条件

- Redis：本机非生产单节点 `127.0.0.1:6379`。
- CacheBench：当前工作树、net8.0；1 次预热、3 次正式样本、请求并发 8、1,000 项、`allocation-components=true`。
- Provider：FreeRedis、CSRedis。
- 对照：任务 035 同端点、同参数、标量解码改动前的方向性 JSON；不替代固定历史 baseline，也不宣称正式预算通过。

## Change Impact Analysis

- ChangedFiles：本任务计划、两个方向性 benchmark JSON、TODO、执行记录和测试映射。
- ChangedProjects：CacheBench 工程运行；生产代码不在本任务修改范围内。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：FreeRedis/CSRedis 单节点 List 批量读取、前缀扫描和标量解码。
- ChangedProviders：FreeRedis、CSRedis。
- ChangedTFMs：benchmark net8.0；生产引用 netstandard2.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务记录和原始证据。
- AffectedDependents：两个 Redis Provider 的批量契约和生产构建。
- RiskLevel：LOW。

## 验收

1. 当前 CacheBench Release 构建成功，两个 Provider 输出 `status=COMPLETE`。
2. 保存 batchRead/scan P95、平均分配、Redis 命令数和 allocation components。
3. 与任务 035 方向性数据比较，明确收益、噪声或退化；不把 1/3 样本结果当正式性能门禁。
4. 运行受影响 Provider 构建、标量批量精准测试和 `git diff --check`。
