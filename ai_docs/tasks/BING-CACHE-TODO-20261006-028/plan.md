# BING-CACHE-TODO-20261006-028：分片同步扫描异常边界

## 目标

让 EasyCaching 客户端分片同步前缀、模式和 Clear 扫描在动作失败后保留主异常，不再由 `finally` 中的分片身份探测覆盖或追加无关命令；完整成功路径仍执行身份校验。

## 实施范围

1. 修改 `ShardedRedisCacheManager.RunScan`，只有动作完整成功时执行末尾分片身份校验。
2. 增加同步扫描失败回归，验证动作异常原样传播且失败后没有额外身份探测。
3. 更新执行记录、TODO 和生产符号到测试映射。

## 约束

- 不改变同步扫描成功时的拓扑完整性校验。
- 不吞掉删除、模式匹配或 Provider 抛出的原始异常。
- 不改变命名空间隔离、批处理和分片路由。

## 验证

- `Bing.EasyCaching.Tests` net8.0、net6.0。
- `Bing.EasyCaching` netstandard2.0 生产构建。
- `git diff --check`。
