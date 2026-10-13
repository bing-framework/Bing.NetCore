# BING-CACHE-TODO-20261007-075 TODO

## 本轮实施

- [x] 为 EasyCaching 客户端分片异步前缀删除增加 `MaxConcurrency` 有界调度。
- [x] 为 EasyCaching 客户端分片异步模式删除增加 `MaxConcurrency` 有界调度。
- [x] 为 EasyCaching 客户端分片异步 Clear 增加 `MaxConcurrency` 有界调度。
- [x] 保持共享扫描预算、取消、异常观察和拓扑完整性语义。
- [x] 补充分片并发、取消、失败和三条清理 API 的直接测试。
- [x] 完成 L0-L3 验证并更新权威缓存 TODO 索引。

## 明确延后

- [ ] 同步清理 API 的并发化及其同步异常契约设计；当前仍按顺序调用同步 Provider。
- [ ] 真实双节点、Cluster、双进程回源和独立总线环境验证。
- [ ] Hybrid 代理背压、重试、outbox 和跨分片顺序保证。
