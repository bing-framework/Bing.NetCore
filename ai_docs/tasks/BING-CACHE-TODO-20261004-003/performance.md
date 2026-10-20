# 续做性能证据

固定历史基线为 `24c93cb1a5355797223637e2d42ac3dce968d7ef`。本轮 1k 诊断使用相同 Redis 端点 `127.0.0.1:52782`、.NET 8、2 轮预热和 10 轮正式样本。原始数据位于上一任务的 `bench/diagnostic-current-*-todo-1k.json`、`bench/diagnostic-current-*-batchhash-1k.json` 和 `bench/diagnostic-current-cluster-*-1k.json`。

| Provider | 版本 | 批量读 P95 ms | 批量读平均分配 MB | 扫描 P95 ms | 扫描平均分配 MB | 命中读 P95 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| FreeRedis | 固定旧版 | 6.386 | 1.85 | 11.798 | 2.83 | 37.471 |
| FreeRedis | 本轮最终 1k 候选 | 7.582 | 5.62 | 15.820 | 8.06 | 36.808 |
| CSRedis | 固定旧版 | 4.958 | 0.92 | 10.606 | 1.24 | 36.327 |
| CSRedis | 本轮最终 1k 候选 | 7.018 | 4.88 | 14.430 | 5.86 | 36.779 |

FreeRedis 批量读 P95 约回退 18.7%，但扫描约 34.1%、批量读分配约 203.7%；CSRedis 批量读/扫描 P95 约回退 41.5%/36.1%，批量读分配约 431.4%。因此本轮不宣称预算通过。物理键微探针在 10k 键调用下测到约 11.9 MB 分配，说明键隔离格式是成本来源之一；该数字不是完整 Provider 归因，也不代表可以移除隔离。

三主三从 Cluster 的当前版 1k 样本：FreeRedis 批量读/扫描 P95 为 352.08/354.60 ms，CSRedis 为 350.23/346.82 ms，各监控 3 个主节点。历史版无等价 Cluster 能力，不计算回退百分比。该容器在性能采样后为故障测试关闭了 7000 主节点。

`networkRequests` 与 Provider 内部并发仍按探针定义记为不可观测，不用 Redis 命令数替代。10k 最终候选和 100k 矩阵未运行，本轮 1k 结果已证实分配与扫描预算失败，遵循 No Progress Guard 停止同类实验。
