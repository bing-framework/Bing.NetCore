# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；本轮方向性候选不能替代正式性能门禁。

## Completed

- [x] FreeRedis/CSRedis List 前缀扫描改用可复用完整批次数组，尾批才复制。
- [x] FreeRedis/CSRedis 输入流和前缀流改用可复用完整批次数组，保持顺序、重复、缺失和取消语义。
- [x] 两个 Provider netstandard2.0 Release 构建通过。
- [x] 两个 Provider 100,000 项 List/Stream 一致性精准测试通过。
- [x] 同参数 FreeRedis/CSRedis 方向性 benchmark 完成；完整批次数组复用没有形成可量化分配收益。

## Blocked External

- [ ] 三主三从 Cluster 正式性能矩阵、双节点/多拓扑 EasyCaching 正式矩阵和外部生产资源指标。

## Deferred

- [ ] 100,000 项性能矩阵、网络往返、连接池和 Provider 内部并发指标。

## Accepted Limitations

- [ ] 兼容 List API 仍然物化最终结果；流式 API 才能让调用方控制消费速度和驻留内存。

## No-progress 结论

- [x] 本候选已完成一次方向性确认，后续不重复数组容器复用实验。
- [ ] FIX-007 仍开放，需新的根因或实质不同方案，不能由本轮关闭。
