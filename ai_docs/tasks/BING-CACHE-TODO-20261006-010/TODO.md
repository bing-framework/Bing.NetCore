# 本轮 TODO

- [x] FreeRedis/CSRedis 流式首批取消后实际只派发一个 MGET。
- [x] FreeRedis/CSRedis 单节点完整集成回归。
- [x] EasyCaching Redis/Hybrid 单节点批量、流式和跨实例失效回归。
- [x] 三 Provider 独立进程回源协调五类场景。

## 延后项

- [ ] 独立总线节点的暂停、断线、重连和消息恢复。
- [ ] 历史 baseline checkout 与严格三方性能对照。
- [ ] 三主三从 Cluster、客户端双分片和完整故障转移矩阵。
- [ ] 既定批量/扫描性能预算；需新的结构性根因后再进入下一性能任务。
