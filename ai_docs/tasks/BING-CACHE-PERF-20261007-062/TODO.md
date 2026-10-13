# 本轮 TODO

## 已完成

- [x] 共享 `CacheClusterTopology.GetHashSlot` 直接按 hash-tag 字符区间计算 CRC16，移除 `Substring` 和 `Encoding.UTF8.GetBytes` 的每键临时分配。
- [x] FreeRedis、CSRedis 的 Cluster 分组、客户端分片路由和租约删除分组统一调用共享槽位实现，删除重复 CRC16 逻辑。
- [x] 保持 Redis hash-tag、16384 槽位和非法 UTF-16 的 `Encoding.UTF8` 替换语义不变。
- [x] 增加 ASCII、空 tag、Unicode、合法代理对、孤立代理项和特殊字符的参考实现等价性测试。
- [x] 完成 Bing.Caching net6.0/net8.0 精准测试、三个受影响生产项目 netstandard2.0 构建和 `git diff --check`。

## 当前仍未完成

- [ ] 正式批量读、扫描 P95 和分配预算仍需同构历史 baseline 与当前候选；本轮只关闭一个新的局部热路径分配根因，不宣称预算通过。
- [ ] 三主三从 Cluster、双节点客户端分片、双进程回源和独立 Hybrid 总线仍需要外部环境，沿用权威索引中的 `BLOCKED_EXTERNAL`。
- [ ] 真实网络往返、客户端内部排队和 Easy `KeysAsync` 隐藏分页仍缺少观测工具，沿用 `DEFERRED`。

## 证据边界

- 本轮未运行 100,000 项完整性能矩阵，也未用单节点结果替代 Cluster/分片实机证据。
- `GetHashSlot` 的正确性已在 net6.0/net8.0 各 12/12 通过；正式性能预算仍保持 `VERIFIED_BOUNDARY`。
