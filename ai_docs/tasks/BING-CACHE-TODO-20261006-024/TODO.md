# 本轮 TODO

## Open Actionable

- 无。

## Completed

- [x] 基于当前运行时 `Uri.EscapeDataString` 生成一次 ASCII 原样字符表。
- [x] 无百分号、全 ASCII 且无需转义的后缀走直接候选路径；其他输入保留完整 URI 解码和 canonical escaping 校验。
- [x] 增加规范 ASCII round-trip 回归；物理键配置测试 net6.0/net8.0 均 14 项通过。
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
- [ ] 完全无分配的 Unicode/百分号解析，需要自有 UTF-8 解码器或多目标 Span 路径，另行评估。
