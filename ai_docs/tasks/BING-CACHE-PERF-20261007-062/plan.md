# Redis 槽位计算热路径优化计划

## 目标

消除缓存 Cluster/客户端分片批量分组中每个物理键的 `Substring` 和 `Encoding.UTF8.GetBytes` 临时分配，同时保持 Redis CRC16/hash-tag 槽位结果完全一致。该优化针对既有批量/扫描分配预算失败提出新的结构性假设，不重复之前的 JSON、物理键解析或扫描批次实验。

## 现状与根因

- `CacheClusterTopology.GetHashSlot` 先创建 hash-tag 子字符串，再对其执行 `Encoding.UTF8.GetBytes`。
- FreeRedis、CSRedis 各自还有相同的私有 `HashSlot` 实现，Cluster 分组、客户端分片路由和租约删除路径都会触发。
- 物理键由当前键格式生成，通常是 ASCII percent-encoded 文本，但公共槽位方法也必须保持一般 Unicode 和非法 UTF-16 输入的 `Encoding.UTF8` 替换语义。

## 实现

- 在 `CacheClusterTopology.GetHashSlot` 中按索引直接遍历 hash-tag 字符，不创建子字符串或 UTF-8 byte 数组。
- 对 ASCII、两/三/四字节 UTF-8、孤立代理项分别处理；孤立代理项使用与 `Encoding.UTF8.GetBytes` 相同的 U+FFFD 替换字节。
- FreeRedis/CSRedis 删除重复的私有 CRC16 实现，统一调用共享槽位函数，避免 Provider 之间规则漂移。
- 增加参考实现等价性测试，覆盖 ASCII、无 hash tag、空 tag、Unicode、合法代理对、孤立代理项和特殊字符。

## 非目标

- 不改变 Redis Cluster 16384 槽位规则、物理键格式、哈希标签或路由拓扑。
- 不修改公共 API 签名、配置、租约语义、批大小或性能预算。
- 不运行完整 100K/Cluster 性能矩阵；只运行受影响的正确性和构建验证。

## Change Impact Analysis

```text
ChangedFiles: CacheClusterTopology.cs；FreeRedis/CSRedis 槽位调用；CacheClusterTopologyTest.cs；本任务文档
ChangedProjects: Bing.Caching、Bing.Caching.FreeRedis、Bing.Caching.CSRedis 及其测试
ChangedPublicContracts: 无（GetHashSlot 签名不变）
ChangedRuntimePaths: Cluster 分组、客户端分片路由、租约删除分组
ChangedProviders: FreeRedis、CSRedis、EasyCaching 间接复用共享函数
ChangedTFMs: 无
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 否
AffectedDependents: Cluster/分片批量读写、扫描删除、租约协调
RiskLevel: MEDIUM
```

## 验收与验证

1. 参考 `Encoding.UTF8.GetBytes` 的 CRC16/hash-tag 实现与新实现对所有边界样本完全一致。
2. Bing.Caching 精准测试 net6/net8 通过。
3. FreeRedis/CSRedis 受影响项目构建并运行已有 Cluster/分片分组精准测试；没有 Redis 时只记录门控状态。
4. `git diff --check` 和 UTF-8 文档检查通过。
5. 性能报告只记录新假设已实现，正式预算需另行确认，不宣称阈值通过。
