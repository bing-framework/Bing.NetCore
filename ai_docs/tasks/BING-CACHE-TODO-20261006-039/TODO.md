# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算尚未闭环；本轮只处理 EasyCaching 分组容器候选。

## Completed

- [x] 采集 EasyCaching Redis 改动前方向性基线。
- [x] 单节点 Redis/Hybrid 读取移除外层分组列表和重复键数组。
- [x] Cluster 异步批量读取使用预分配任务/分组键数组，保留有界并发。
- [x] EasyCaching.Tests net6.0/net8.0 项目级回归通过。
- [x] netstandard2.0 生产构建、CacheBench 构建和 `git diff --check` 完成。
- [x] 改动后方向性基准完成：批读/扫描 P95 下降约 7%–13%，分配量基本不变。

## Blocked External

- [ ] 三主三从 Cluster 正式矩阵、双节点客户端分片/Hybrid 矩阵和外部资源指标。

## Deferred

- [ ] 100,000 项 EasyCaching 性能矩阵、网络往返、连接池和 Provider 内部并发指标。

## Accepted Limitations

- [ ] 兼容 List API 仍然物化最终结果；流式 API 才能让调用方控制消费速度和驻留内存。

## No-progress 结论

- [x] 本轮 EasyCaching 分组容器候选已完成一次改动前/后确认，保留实现，不重复同一实验。
- [ ] FIX-007 仍开放，需新的根因或正式性能门禁证据。
