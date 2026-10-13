# 本轮 TODO

## Open Actionable

- 无。

## Completed

- [x] `SetAllAsync` 使用单次输入数组和按 offset/count 填充的 `Task[]`，移除批次内 `Skip/Take/Select` 分配。
- [x] `RemoveAllAsync` 使用相同的有界索引派发路径，保留每项取消检查和批次等待语义。
- [x] 增加空集合与取消前派发行为测试；net6.0/net8.0 均完成编译，因 Redis 门控关闭而 Skip。
- [x] `netstandard2.0` 生产构建、差异检查和生产符号映射已完成。

## Blocked External

- [ ] Redis MONITOR、Cluster/分片和完整性能基准仍需对应非生产环境。
- [ ] 旧版/当前版/优化版严格三方基线仍缺可重建历史源码。

## Inherited Open TODO

- [ ] 正式批量读/扫描 P95 与分配预算尚未全部达标。
- [ ] 100,000 项完整 List/Stream 一致性与 T02/T03 后跨进程回源回归仍未重新生成。

## Deferred

- [ ] 真实网络往返、连接池排队和客户端内部重试指标。
- [ ] EasyCaching List 专用直连解码与 Hybrid 跨实例批量实机矩阵。

## Next Local Candidate

- [ ] `CacheKeyFormat.FormatPhysicalKey` 每个键仍创建 `char[32]` 标签缓冲；已有诊断显示 1,000 项调用平均约 0.98 MB 分配，下一轮可改为 `HashContext` 复用字符缓冲并增加返回字符串独立性回归。
