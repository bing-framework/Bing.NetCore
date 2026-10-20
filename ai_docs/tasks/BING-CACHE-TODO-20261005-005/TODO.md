# 本轮剩余 TODO

- [x] checkpoint 已记录每个 round 的实际命名空间、物理扫描模式和清理状态；稳定 workload identity 与随机 run identity 已分离，endpoint 已脱敏。

- [x] 在隔离 Redis 环境启用 FreeRedis/CSRedis MONITOR 测试，确认首批取消后实际命令数为 1；见 `BING-CACHE-TODO-20261006-010`，net6.0/net8.0 各 1/1。
- [ ] 恢复历史 baseline checkout 后编译 baseline CacheBench，确认共享基准入口的 checkpoint 改动不破坏历史构建；当前状态 `BLOCKED_EXTERNAL`。
- [x] 按字节自适应拆分已实现：超预算批次递归二分重读，单值超预算明确抛错；单元测试覆盖顺序、单值失败、取消和非预算异常不重试。重读不是跨键快照，额外命令通过现有命令探针计入。
- [x] 为 T02/T03 修改后的当前候选补双进程回源回归，覆盖同键合并、失租取消、显式写删撤销和持有者退出；见 `BING-CACHE-TODO-20261006-010`，FreeRedis、CSRedis、EasyCaching Redis 全场景通过。
- [x] Hybrid 回填代次闸门已实现：本地写删和已接收的远端失效推进代次，过期同步/异步回填不会重新写入 L1；同步/异步闸门单元测试已通过。两个真实实例的总线竞态仍需隔离 Redis 门禁验证。
