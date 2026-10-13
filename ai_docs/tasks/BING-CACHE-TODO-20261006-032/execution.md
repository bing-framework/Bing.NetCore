<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261006-032
AI_EXECUTION_FINISHED_AT: 2026-10-05T23:59:39.895Z
AI_EXECUTION_STARTED_AT: 2026-10-05T23:39:17.670Z

# 执行记录

状态：本轮实现和受影响回归已完成；正式性能预算继续开放。

## 当前验证协议

- 先运行共享键精准测试和受影响项目构建。
- 再运行 FreeRedis/CSRedis 单节点集成测试（`RUN_INTEGRATION_TESTS=true` 时使用非生产 Redis）。
- `git diff --check` 和 UTF-8 检查必须通过。
- 性能只记录方向性结果；不重建固定 baseline，不把未完成正式矩阵标记为通过。

## 变更

- `CacheKeyFormat.HashContext` 增加池化 UTF-8 解码校验和编码后缀哈希；物理键扫描不再为规范转义键逐项构造逻辑键。
- `TryMatchLogicalPrefix` 由调用方一次性生成规范 URI 前缀，直接比较物理前缀，验证 canonical percent encoding、严格 UTF-8、非空白键和 namespace hash tag；无法验证时回退 `TryGetLogicalKey`。
- FreeRedis、CSRedis 的异步 `GetByPrefixAsync` 改用物理键扫描枚举；同步 `GetByPrefix`、`GetByPrefixStreamAsync` 和 `GetAllStreamAsync` 行为保持不变。
- 未修改公共接口、物理键格式、租约键、Cluster/分片路由或 EasyCaching。

## 影响分析

ChangedPublicContracts: 无。
ChangedRuntimePaths: FreeRedis/CSRedis 异步前缀 List 扫描过滤。
ChangedProviders: FreeRedis、CSRedis。
ChangedTFMs: 生产 netstandard2.0；验证 net6.0/net8.0。
RiskLevel: MEDIUM；重点为 URI 转义、UTF-8 合法性、哈希校验和特殊键回退。

## 验证

- `Bing.Caching` netstandard2.0：0 警告、0 错误。
- `Bing.Caching.FreeRedis` netstandard2.0：0 警告、0 错误。
- `Bing.Caching.CSRedis` netstandard2.0：0 警告、0 错误。
- `CacheConfigurationAndKeyFormatTest`：net8.0 17/17、net6.0 17/17。
- FreeRedis 单节点特殊键/批量精准集成：net8.0 17 通过、1 跳过；net6.0 17 通过、1 跳过。跳过项为既有 100k 门控。
- CSRedis 单节点特殊键/批量精准集成：net8.0 17 通过、1 跳过；net6.0 17 通过、1 跳过。跳过项为既有 100k 门控。
- `git diff --check`：通过；仅有仓库既有 CRLF 转换提示。

## 性能证据

固定端点 `127.0.0.1:6379`、.NET 8.0.30、1,000 项、1 轮预热、3 轮样本、并发 8，比较 task 032 编码前缀修正前后方向：

| Provider | 阶段 | 修正前 P95 | 修正后 P95 | 修正前平均分配 | 修正后平均分配 |
| --- | --- | ---: | ---: | ---: | ---: |
| FreeRedis | scan | 12.9559 ms | 11.6446 ms | 4,241,680 B | 4,107,386.7 B |
| CSRedis | scan | 12.7918 ms | 12.5809 ms | 2,035,882.7 B | 1,901,600 B |
| FreeRedis | batchRead | 5.7034 ms | 6.3239 ms | 2,391,608 B | 2,391,680 B |
| CSRedis | batchRead | 5.3241 ms | 5.3658 ms | 1,642,080 B | 1,642,152 B |

修正前产物为本任务早期诊断，修正后产物为 `diagnostic-prefixfast-v2-freeredis-1k.json` 与 `diagnostic-prefixfast-v2-csredis-1k.json`。两次均为方向性 1k 运行，不是固定 baseline 的正式对照；FIX-007 的正式 2 轮预热/10 轮采样、10k/100k 矩阵和预算仍未关闭。
