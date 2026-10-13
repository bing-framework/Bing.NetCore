# 本轮 TODO

- [x] 将 `CacheScanBudget` 改为并发安全并保留真实分片诊断。
- [x] 将 EasyCaching 分片前缀 List 读取改为 `MaxConcurrency` 有界并发。
- [x] 将 EasyCaching 分片前缀 Stream 读取改为交错流式消费并支持取消清理。
- [x] 让同步前缀读取复用异步有界路径。
- [x] 补充预算、取消、失败传播测试并更新权威索引。

## 分类边界

- 本轮只处理 EasyCaching 客户端分片前缀读取。
- 前缀删除、模式删除、Clear、真实双节点/总线和正式性能预算不在本轮实现范围。

## 本轮结论

- `CacheScanBudget` 的候选键和扫描页计数在并发任务下保持全局上限，异常保存真实已完成和失败分片索引。
- EasyCaching 分片前缀 List 按 `MaxConcurrency` 窗口扫描并按分片索引合并；Stream 保持每分片一个在途读取并按完成顺序交错产出，不积累全局结果。
- 同步 `GetByPrefix` 复用异步实现；真实两个 Redis 节点的跨分片运行仍受集成环境门控。
