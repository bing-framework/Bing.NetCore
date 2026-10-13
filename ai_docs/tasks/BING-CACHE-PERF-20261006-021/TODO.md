# 本轮 TODO

## Completed This Round

- [x] FreeRedis、CSRedis 默认标量批量解码改用泛型 `T[]`，去除 `Nullable<T>[]`、`Array.GetValue` 和逐项装箱。
- [x] 非 nullable 标量的缺失/JSON null 使用 `default(T)` JSON token，Hit 仍按原始响应判断。
- [x] 增加 `int`、`long`、`decimal` 的缺失、显式 null、Stream/List 等价性回归，并完成生产构建。

## Open Actionable

- none；本轮实现和本地可执行验证已完成。

## Blocked External

- [ ] EasyCaching 52800 专用端点 MONITOR、完整 Cluster/分片/独立总线矩阵。
- [ ] 可重建的旧版/中间版/优化版同构性能基线。

## Inherited Open TODO

- [ ] PERF-001：正式批量读/扫描 P95 与分配预算尚未全部达标；本轮只验证新的结构性分配变化。
- [ ] 100,000 项 FreeRedis/CSRedis 完整 List/Stream 一致性测试受大数据门控和资源边界约束。
- [ ] T02/T03 后跨进程回源协调回归证据尚未重新生成。

## Deferred

- [ ] 真实网络往返、客户端内部队列和连接池指标。
- [ ] 流式故障、消费中取消后的服务端命令停止以及 Hybrid 跨实例批量实机矩阵。
- [ ] EasyCaching List 专用直连解码；当前没有足够的分配差异证据支持本轮新增路径。
