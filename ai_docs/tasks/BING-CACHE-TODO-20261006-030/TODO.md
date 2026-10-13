# 本轮 TODO

## Open Actionable

- [ ] FIX-007 性能预算仍未闭环：需要在保持隔离、物理键和租约语义的前提下继续处理扫描回读/客户端原始响应分配，并以同参数 10,000 项复测确认收益。

## Completed

- [x] 已定位本轮新根因：CSRedis 每个原始值逐项 `Encoding.UTF8.GetString`，FreeRedis/CSRedis 均通过 `StringBuilder` 保留数组中间缓冲。
- [x] 新增 `CacheJsonBatch` 池化字符缓冲，接入 FreeRedis/CSRedis 标量批量读取，保留旧 null/default/Unicode 语义。
- [x] 物理键生成改用池化 UTF-8/字符缓冲，v2 格式与大写百分号编码逐字节兼容。
- [x] 公共缓存测试 net6/net8：相关 22 项键格式/JSON 组装测试通过；完整 `Bing.Caching.Tests` net6/net8 各 158 项通过。
- [x] FreeRedis/CSRedis 批量与共享契约精准集成 net6/net8 各 15 通过、1 个 100,000 项门控跳过。
- [x] EasyCaching Redis 共享契约与批量流式一致性 net6/net8 各 2 通过。
- [x] `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0 构建均 0 警告、0 错误。
- [x] 固定 baseline worktree 已建立，baseline/current 在 127.0.0.1:6379、1k、1 轮预热/3 轮采样完成；原始 JSON 已保存。

## Performance Evidence

- [ ] FreeRedis 1k：批量读 P95 +31.4%、平均分配 +29.9%；扫描 P95 +30.4%、平均分配 +51.0%。
- [ ] CSRedis 1k：批量读 P95 +29.5%、平均分配 +83.5%；扫描 P95 +39.9%、平均分配 +68.7%。
- [ ] 当前结果相比同端点 baseline 仍未满足批量/扫描 P95 20% 和分配 30% 预算；不能宣称 FIX-007 关闭。

## Blocked External

- [ ] 正式验收仍需要计划规定的 2 轮预热/10 轮采样及 10,000 项完整矩阵；本轮 1 轮/3 轮仅作为新候选方向性证据。
- [ ] 三主三从 Cluster、双节点分片和独立 Hybrid 总线故障矩阵仍需专用端点。

## Inherited Open TODO

- [ ] 批量读/扫描 P95 与分配预算尚未闭环；本轮只处理一个新的分配结构热点。
- [ ] 100,000 项跨拓扑一致性和 T02/T03 后双进程回源证据仍未全部完成。

## Deferred

- [ ] 网络往返、连接池排队、服务端 SCAN 停止和重试退避指标仍需专用观测环境。
