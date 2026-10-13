# BING-CACHE-TODO-20261006-014：EasyCaching 流式特殊键隔离证据

## 目标

补齐 EasyCaching `GetByPrefixStreamAsync` 在特殊逻辑键和相似命名空间下的独立契约证据。测试必须证明流式路径按逻辑前缀过滤、正确还原 Unicode/花括号/通配字符/反斜线，并且不会读到相邻命名空间的哨兵键。

## 范围

- 只修改 `framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs` 及本任务记录。
- 使用现有 mock `IEasyCachingProvider`，不依赖 Redis、Cluster、总线或性能环境。
- 不改变生产 API、键格式、Provider 注册和流式实现。

## 实现步骤

1. 构造两个相似命名空间的配置和特殊逻辑键物理键。
2. 让 mock Provider 返回同一命名空间内的物理键及相邻命名空间哨兵键。
3. 让批量读取 mock 返回对应值，消费 `GetByPrefixStreamAsync` 并断言逻辑键、值、Hit 和哨兵隔离。
4. 在 net6.0/net8.0 运行精准测试，执行 `git diff --check`，更新测试映射和执行报告。

## 验收标准

- 流式结果只包含目标命名空间和逻辑前缀匹配的键。
- Unicode、花括号、`*`、`?`、反斜线等键值原样恢复。
- 相似命名空间哨兵不出现在结果中。
- 两个目标框架精准测试通过，任务文档状态一致。

## 不在本轮范围

- Redis MONITOR、Cluster/分片、Hybrid 实机一致性和性能预算。
- 已存在的 FreeRedis/CSRedis 特殊键及大规模一致性测试不重复实现。
