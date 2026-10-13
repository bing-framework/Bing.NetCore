# 本轮 TODO

## Verified Boundary

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；已有正式数据确认预算边界，本轮不重复同类微优化。

## Completed

- [x] FreeRedis Cluster 同步 `GetAll` 和同步前缀读取按 hash slot 分组、按 `BatchSize` 批量 MGET，并按原始顺序回填。
- [x] CSRedis Cluster 同步 `GetAll` 和同步前缀读取按 hash slot 分组、按 `BatchSize` 批量 MGET，并按原始顺序回填。
- [x] FreeRedis/CSRedis netstandard2.0 生产构建各 `0 warning / 0 error`。
- [x] 双 Provider net6.0/net8.0 Cluster 精准测试均成功编译，因未设置 Cluster 门控各 `0 passed / 1 skipped / 0 failed`。
- [x] 增加同步跨槽 `GetAll` 与前缀读取集成断言，`git diff --check` 无错误。
- [x] FIX-009 已由后续任务 `BING-CACHE-TODO-20261006-045` 闭环：EasyCaching 使用 Provider 调用计数，FreeRedis/CSRedis 使用 Redis `MONITOR`。

## Blocked External

- [ ] FreeRedis/CSRedis 三主三从 Cluster 的同步跨槽 MGET 和前缀扫描实机验证需要专用环境。
- [ ] 历史同构 baseline、100,000 项资源矩阵和真实网络/连接池指标需要专用环境。

## Deferred

- [ ] 独立 `review.md`、全方案性能报告和完整发布门禁尚未在本轮执行。
