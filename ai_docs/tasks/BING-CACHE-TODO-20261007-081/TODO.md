# BING-CACHE-TODO-20261007-081 TODO

## 本轮

- [x] 为 EasyCaching IDatabase 路径增加同步/异步原生 SCAN 迭代器。
- [x] 将 `MaxScanPages` 接入 List、Stream、前缀/模式删除和 Clear 的共享预算。
- [x] 将分片管理器和普通管理器的扫描预算改为传递完整资源配置。
- [x] 增加原生 SCAN 解析、页预算、取消和命名空间隔离测试。
- [x] 使用隔离 Redis 7.4 完成 Redis/Hybrid 真实页预算回归。
- [x] 更新执行报告、测试映射和权威 TODO 索引。

## 保留边界

- [~] 自定义 EasyCaching Provider 的隐藏分页仍无法从适配器层观察，继续由 `MaxScanKeys` 保护。
- [~] 真实网络往返和 StackExchange.Redis 客户端内部排队指标仍需独立观测工具和口径。
- [~] 正式性能预算、100,000 项矩阵、双进程回源和独立总线故障不属于本轮。
