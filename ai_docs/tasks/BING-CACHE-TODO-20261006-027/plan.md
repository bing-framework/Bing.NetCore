# BING-CACHE-TODO-20261006-027：分片扫描取消后的清理边界

## 目标

修正 EasyCaching 客户端分片异步扫描在取消或提前结束消费后仍使用
`CancellationToken.None` 做分片身份探测的问题。取消应停止后续扫描和清理探测；完整成功时仍执行末尾身份校验；已有读取或取消异常不得被清理探测覆盖。

## 实施范围

1. `ShardedRedisCacheManager.GetByPrefixStreamAsync` 仅在完整消费且未取消时执行末尾分片身份校验。
2. `ShardedRedisCacheManager.RunScanAsync` 仅在扫描动作完整成功且未取消时执行末尾分片身份校验。
3. 清理探测失败时沿用共享清理异常优先级：无主异常时抛出，已有主异常时写入 `CacheCleanupException`。
4. 增加客户端分片流取消回归，证明取消后不再发 `INFO SERVER` 探测，并保留完整成功校验。

## 约束

- 不改变物理键、分片路由、拓扑快照或数据删除语义。
- 不以取消后的额外探测替代拓扑完整性验证；完整成功仍必须验证身份。
- 不吞掉无主异常，也不覆盖已经产生的主异常。

## 验证

- `Bing.EasyCaching.Tests` net8.0、net6.0。
- `Bing.EasyCaching` netstandard2.0 生产构建。
- `git diff --check`。
