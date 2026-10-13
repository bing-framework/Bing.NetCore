# 本轮 TODO

## Open Actionable

- 无。

## Completed

- [x] `HashContext` 复用固定的 32 字符标签缓冲；每次通过 `new string` 生成独立不可变结果。
- [x] 增加批量结果稳定性回归，并在 net6.0/net8.0 运行 13 项物理键配置测试。
- [x] `Bing.Caching` `netstandard2.0` 生产构建和差异检查通过。

## Blocked External

- [ ] Redis MONITOR、Cluster/分片和完整性能基准仍需对应非生产环境。
- [ ] 旧版/当前版/优化版严格三方基线仍缺可重建历史源码。

## Inherited Open TODO

- [ ] 正式批量读/扫描 P95 与分配预算尚未全部达标。
- [ ] 100,000 项完整 List/Stream 一致性与 T02/T03 后跨进程回源回归仍未重新生成。

## Deferred

- [ ] 真实网络往返、连接池排队和客户端内部重试指标。
- [ ] EasyCaching List 专用直连解码与 Hybrid 跨实例批量实机矩阵。
- [ ] `TryGetLogicalKey` 的 substring/escape 解析分配优化，等待独立证据和风险评估。
