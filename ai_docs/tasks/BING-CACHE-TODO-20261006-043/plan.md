# BING-CACHE-TODO-20261006-043：客户端分片 List 批量快路径

## 目标

消除 `ShardedRedisCacheManager` 的两个 List API 不必要的 `CacheEntry<T>` 中间对象：异步批量读取直接按分片调用值列表批处理，异步前缀读取复用单分片带共享扫描预算的值列表入口。

## 实现范围

- 在 `CacheManager` 内增加带 `CacheScanBudget` 的异步前缀 List 内部实现，公共 `GetByPrefixAsync` 复用该实现。
- 在 `ShardedRedisCacheManager` 中让 `GetAllAsync` 使用有界分片值批处理，不再通过 `GetAllStreamAsync` 收集。
- 让 `ShardedRedisCacheManager.GetByPrefixAsync` 按分片直接调用值列表入口，保留一份扫描预算和前后身份校验。
- 保留同步 API、Stream API、分片路由、输入顺序、缺失/default、取消和异常传播语义。
- 不修改公共接口、键格式、租约、拓扑配置或性能预算。

## 验收

- 分片 List 批量读取和前缀读取精准测试通过，输入顺序、跨分片结果、缺失值和取消行为不变。
- `Bing.EasyCaching.Tests` 双目标框架完整测试通过。
- `Bing.EasyCaching` netstandard2.0 Release 构建通过。
- `git diff --check` 无错误；更新执行记录、TODO 和生产符号映射。

## 非目标

- 不声称正式 P95/分配预算通过；FIX-007 继续保留。
- 不替代真实 Cluster、双节点分片/Hybrid 或 100,000 项外部集成验证。
