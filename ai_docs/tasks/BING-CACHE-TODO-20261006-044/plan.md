# BING-CACHE-TODO-20261006-044：同步 Cluster 批量读取按槽分组

## 目标

修复 FreeRedis、CSRedis 同步 `GetAll` 和同步前缀读取在 Redis Cluster 下逐键读取的问题，使同步路径复用已有按槽分组的 MGET 能力，同时保持输入/扫描顺序和缺失值语义。

## 实现范围

- FreeRedis Cluster 同步 `GetAll` 按 hash slot 分组、按 `BatchSize` MGET，并按原始索引回填。
- FreeRedis Cluster 同步前缀读取按槽批量读取后按扫描顺序追加。
- CSRedis Cluster 同步 `GetAll` 按 hash slot 分组、按 `BatchSize` MGET，并按原始索引回填。
- CSRedis Cluster 同步前缀读取按槽批量读取后按扫描顺序追加。
- 保留单节点、客户端分片路径；不修改异步路径、键格式、拓扑校验、资源预算和序列化策略。

## 验收

- FreeRedis、CSRedis 生产 `netstandard2.0` 构建通过。
- 受影响同步路径的精准测试或集成门控结果明确记录；跨槽批量行为由真实 Cluster 环境验证，缺少环境时标记 `BLOCKED_EXTERNAL`。
- `git diff --check` 无错误，更新执行记录、TODO 和生产符号映射。

## 非目标

- 不把不同源码时点的短基准拼成严格 before/after。
- 不调整正式 P95/分配预算，FIX-007 继续保留。
