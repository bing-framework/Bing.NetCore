# 本轮 TODO

- [x] 实现可复用哈希上下文和池化 UTF-8 输入缓冲，保持物理键字节完全一致；固定格式测试按旧算法逐字节比对。
- [x] FreeRedis、CSRedis、EasyCaching 扫描/清理循环接入上下文；生产项目均通过 `netstandard2.0` 构建。
- [x] 完成公共键格式、批量/扫描相关精准测试；`Bing.Caching.Tests` 双 TFM 各 134/134 通过，FreeRedis 特殊键/命名空间各 10/10，CSRedis 各 9/9。
- [x] 运行 FreeRedis/CSRedis 1,000 项当前版短基准并记录绝对指标：见 `current-freeredis-1k.json`、`current-csredis-1k.json`。
- [ ] 整体性能预算仍未闭环；本轮只验证新的哈希分配根因，短基准没有同构历史 baseline，不能计算 before/after。
- [ ] 独立总线、历史 baseline、完整 Cluster/分片矩阵继续按 `BLOCKED_EXTERNAL` 记录。

## 当前版短基准摘要

| Provider | batchRead P95 | batchRead 分配均值 | scan P95 | scan 分配均值 |
| --- | ---: | ---: | ---: | ---: |
| FreeRedis | 8.0317 ms | 2,772,880 B | 12.682 ms | 4,346,200 B |
| CSRedis | 6.013 ms | 2,059,336 B | 14.8075 ms | 2,176,400 B |

参数为单节点 `127.0.0.1:6379`、1,000 项、1 次预热、3 次样本、请求并发 4；未执行 `FLUSHDB`。指标只代表当前版绝对值。
