# BING-CACHE-TODO-20261006-020：批量结果容量预分配

## 目标

减少 `GetAllAsync`/`GetByPrefixAsync` 在调用方已经提供集合规模时的 `List<T>` 扩容和复制，保持现有输入只枚举一次、顺序、重复键、缺失值和流式行为不变。该轮只处理仓库内可验证的本地分配热点，不把单节点结果冒充完整性能预算通过。

## 实施范围

1. 在 `Bing.Caching` 增加内部集合计数辅助逻辑，仅读取 `ICollection<T>`/`IReadOnlyCollection<T>` 的已有 Count，不枚举输入。
2. FreeRedis、CSRedis、EasyCaching 的结果列表 API 使用已知输入容量初始化；未知长度仍使用默认列表行为。
3. 保持 `CacheKey` 投影、流式 API、Cluster/分片路由、命名空间、取消和租约路径不变。
4. 增加必要的契约回归，验证自定义单次枚举输入仍只枚举一次，空集合不访问 Redis。

## 验证

- `Bing.Caching.Tests` net6.0/net8.0 精准测试。
- FreeRedis、CSRedis、EasyCaching 受影响测试或可用的精准单元测试。
- 四个生产项目 netstandard2.0 构建。
- `git diff --check`。
- 更新 `TODO.md`、`execution.md`、`test-mapping.md`，记录性能预算仍需同构 baseline 判定。

## 边界

- 不改变公开 API 签名，不引入新的缓存语义。
- 不重复 100,000 项超时、Cluster/分片或独立总线实验；这些继续按 `BLOCKED_EXTERNAL`/`VERIFIED_BOUNDARY` 记录。
- 不宣称容量预分配单独足以通过历史 P95/分配预算。
