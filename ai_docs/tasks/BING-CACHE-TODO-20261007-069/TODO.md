# TODO

- [x] 增加 `MaxScanPages` 配置和带限制类型的扫描预算异常。
- [x] 接入 FreeRedis/CSRedis 原生同步、异步、流式和删除扫描路径。
- [x] 补充公共预算测试；Provider 页预算回归保留为真实 Redis 集成门禁，当前环境未提供可丢弃 Redis。
- [x] 记录 EasyCaching 隐藏分页不纳入本轮的边界。

## 验收状态

- 公共预算测试：net6.0、net8.0 均通过。
- 受影响生产项目：Bing.Caching、Bing.Caching.FreeRedis、Bing.Caching.CSRedis 的 netstandard2.0 构建通过。
- FreeRedis/CSRedis 的原生同步、异步、前缀流和删除/清理扫描路径均在发出下一条 `SCAN` 前消耗页预算。
- EasyCaching 的 `Keys/KeysAsync` 隐藏分页不计入 `MaxScanPages`；继续使用 `MaxScanKeys`，该观测缺口列入权威 TODO 索引的 `DEFERRED`。
- 真实 Redis Provider 页级回归需设置 `RUN_INTEGRATION_TESTS=true` 和非生产 `ConnectionStrings__RedisConnection`，当前列为 `BLOCKED_EXTERNAL`，不伪造为本地通过。
