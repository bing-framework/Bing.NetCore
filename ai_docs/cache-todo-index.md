# 缓存能力 TODO 权威索引

> 能力状态日期：2026-10-07；文档入口整理：2026-10-14
>
> 本文件是缓存审计、流式批量优化和性能任务的当前状态入口。已完成的 009–098 局部任务已合并到 [缓存任务总览](tasks/completed/caching.md)，001–008 总体/性能任务及 099 发布任务保留原文；历史记录中旧的未勾选复选框不再单独构成当前 `OPEN_ACTIONABLE`。后续审计、Review 和 Fix 任务应以本索引及其引用的最新证据为准。

## 状态定义

| 状态 | 含义 | 是否允许进入下一轮 Fix |
| --- | --- | --- |
| `CLOSED` | 代码和所需本地证据已完成 | 否 |
| `OPEN_ACTIONABLE` | 当前工作区可直接实现或补证的事项 | 是 |
| `BLOCKED_EXTERNAL` | 需要未提供的 Redis 拓扑、进程、总线或源码环境 | 否 |
| `BLOCKED_APPROVAL` | 需要用户或发布方明确决策 | 否 |
| `VERIFIED_BOUNDARY` | 已实测为边界、预算失败或超时，不能当作未实现 | 否 |
| `DEFERRED` | 价值或口径尚未确定，暂不重复工作 | 否 |
| `NOT_APPLICABLE` | 计划没有要求或旧版本没有可比能力 | 否 |
| `ACCEPTED_LIMITATION` | 当前契约明确接受的限制 | 否 |

## 当前状态

| 事项 | 状态 | 当前结论和证据 | 下一动作 |
| --- | --- | --- | --- |
| 公共缓存契约、批量顺序/重复键/缺失键/null-hit、TTL、键格式、命名空间和清理边界 | `CLOSED` | 共享契约及前序 Provider 回归已覆盖；UTF-16 代理项、相似命名空间和规范键解码测试已补齐。见 [BING-CACHE-TODO-20261007-052](tasks/completed/caching.md#BING-CACHE-TODO-20261007-052)、[BING-CACHE-TODO-20261007-053](tasks/completed/caching.md#BING-CACHE-TODO-20261007-053)、[BING-CACHE-TODO-20261007-054](tasks/completed/caching.md#BING-CACHE-TODO-20261007-054)。 | 停止重复实现 |
| NullCache 流式读取是否也返回逐键 `Hit=false` 条目 | `BLOCKED_APPROVAL` | 当前 NullCache 的设计是禁用缓存时返回空 List/空 Stream；统一到公共流式契约会改变现有调用方看到的条目数量，属于可观察的行为选择，已有任务 009 明确保留旧默认行为。 | 明确 NullCache 是“空结果适配器”还是必须完全遵循流式缺失项契约后再实现 |
| Hybrid 失效消息独立字节预算与有界拆分 | `CLOSED` | 已增加可选 `MaxInvalidationMessageBytes`，使用实际 EasyCaching serializer 载荷按物理键顺序拆分普通/分片 Hybrid 的同步和异步通知；单键超限在发布前抛错。公共资源和 EasyCaching 双 TFM 测试通过。见 [BING-CACHE-TODO-20261007-070](tasks/completed/caching.md#BING-CACHE-TODO-20261007-070)。 | 停止重复消息拆分；可选投递重试和 Redis outbox 见 093 |
| Hybrid 失效消息预算测量实例一致性 | `CLOSED` | 正值预算路径保存并发布同一已测量 `EasyCachingMessage`；追踪 serializer 回归确认发布 `Id` 来自已测量实例。见 [BING-CACHE-TODO-20261007-071](tasks/completed/caching.md#BING-CACHE-TODO-20261007-071)。 | 停止重复消息实例一致性实验 |
| FreeRedis/CSRedis 客户端分片批量读取固定串行 | `CLOSED` | 两个 Provider 的 ClientSharded List/批量/前缀流路径均已按 `MaxConcurrency` 窗口并发派发，按原索引回填并观察已派发任务；生产 netstandard2.0 构建 0 警告/0 错误。见 [BING-CACHE-TODO-20261007-072](tasks/completed/caching.md#BING-CACHE-TODO-20261007-072)。 | 停止重复本地实现；双节点真实并发/取消/第二批失败证据沿用外部门禁 |
| FreeRedis/CSRedis 同步客户端分片批量/前缀读取固定串行 | `CLOSED` | 同步 `GetAll` 和前缀批次读取已桥接到现有 `MaxConcurrency` 有界异步路由，保持顺序、重复键、缺失/default 和异常传播；两个生产项目 netstandard2.0 构建通过。见 [BING-CACHE-TODO-20261007-073](tasks/completed/caching.md#BING-CACHE-TODO-20261007-073)。 | 停止重复本地实现；真实双节点并发仍按外部门禁 |
| Hybrid 失效消息代理背压、重试与 outbox | `CLOSED` | 093 增加了默认关闭、按实例配置的有限重试和 Redis pending/processing outbox；队列上限原子计算 pending+processing，发布失败仍传播原始异常，processing 在重建投递器后可恢复。单元测试 net6/net8 各 5/5，真实 Redis outbox 门禁 net6/net8 各 1/1。见 [BING-CACHE-TODO-20261007-093](tasks/completed/caching.md#BING-CACHE-TODO-20261007-093)。 | 停止重复实现；跨产品代理最大帧协商和网络/客户端排队观测仍单独延后 |
| Hybrid outbox 运维可观测性 | `CLOSED` | 094 增加 `IHybridInvalidationDeliveryDiagnostics` 快照，报告 pending/processing 深度、最早消息年龄、进程内成功/失败/入队计数和最近失败；旧 envelope 时间字段缺失时仍兼容投递。单元测试与真实 Redis outbox 门禁在 net6/net8 通过。见 [BING-CACHE-TODO-20261007-094](tasks/completed/caching.md#BING-CACHE-TODO-20261007-094)。 | 停止重复状态读取；死信保留期、人工重放和跨进程全局计数另立任务 |
| Hybrid outbox 毒消息隔离 | `CLOSED` | 095 增加默认关闭的持久化投递尝试次数、有界 dead-letter Sorted Set 和原子 processing 转移；重建 outbox 不会清零尝试次数，dead-letter 满/取消/异常时保留 processing。单元测试和真实 Redis 门禁在 net6/net8 通过。见 [BING-CACHE-TODO-20261007-095](tasks/completed/caching.md#BING-CACHE-TODO-20261007-095)。 | 保留期/空间预算、人工重放和跨进程全局指标另立运维任务 |
| Hybrid outbox dead-letter 保留期与有界清理 | `CLOSED` | 096 增加默认关闭的 dead-letter 保留期、按 transfer score 的有界 Lua 清理、最早死信年龄和过期清理计数；清理失败只进入诊断，不影响 processing 保留。双 TFM 单元、完整 EasyCaching 回归和真实 Redis outbox 门禁通过。见 [BING-CACHE-TODO-20261007-096](tasks/completed/caching.md#BING-CACHE-TODO-20261007-096)。 | 精确字节预算、人工重放和跨进程指标另立运维任务 |
| Hybrid outbox dead-letter 精确 payload 字节预算 | `CLOSED` | 097 增加 `DeadLetterMaxBytes`、同命名空间 sizes hash/bytes counter、Lua 原子预算检查、历史 payload 重算和 `DeadLetterBytes` 诊断；双 TFM 单元/完整回归、真实 Redis 门禁和生产构建通过。见 [BING-CACHE-TODO-20261007-097](tasks/completed/caching.md#BING-CACHE-TODO-20261007-097)。 | Redis allocator 实际内存、人工重放和跨进程指标另立运维任务 |
| Hybrid outbox dead-letter 人工管理能力 | `CLOSED` | 098 增加有界查看、SHA-256 opaque handle、confirmation token、容量受控原子重放、确认删除、授权器、审计接收器和单节点/分片 DI 注册；malformed、失败审计、取消、并发单赢家和历史字节预算均有双 TFM/真实 Redis 证据。见 [BING-CACHE-TODO-20261007-098](tasks/completed/caching.md#BING-CACHE-TODO-20261007-098)。 | Redis allocator 实际内存、跨进程全局指标和事务 outbox 仍按边界保留 |
| Hybrid dead-letter handle 无状态操作和旧存储惰性迁移 | `BLOCKED_EXTERNAL` | 099 增加仅凭 handle/token 的跨请求 API、payload hash 布局、旧 JSON member 有界迁移、重复 member/字节预算修正；临时 Redis 7.4 outbox 门禁 net6/net8 各 11/11 通过，临时三主三从 Cluster 门禁 net6/net8 各 1/1 通过，EasyCaching 全量测试双 TFM 各 133 通过/70 跳过，生产构建通过。仅旧/新二进制蓝绿切换仍未实测。见 [BING-CACHE-TODO-20261007-099](tasks/BING-CACHE-TODO-20261007-099/execution.md)。 | 在隔离发布环境验证旧二进制停止访问后，新版本读取并迁移旧数据 |
| FreeRedis、CSRedis 扫描删除分组、批量/租约键字节预算和取消响应闸门 | `CLOSED` | 删除批处理与测试专用 RESP 闸门已完成，单节点 net6/net8 精准和完整门控回归通过。见 [BING-CACHE-TODO-20261007-051](tasks/completed/caching.md#BING-CACHE-TODO-20261007-051) 和 [BING-CACHE-TODO-20261007-056](tasks/completed/caching.md#BING-CACHE-TODO-20261007-056)。 | 停止重复 MONITOR/取消实验 |
| FreeRedis、CSRedis、EasyCaching 多值响应超过预算时的自适应拆分 | `CLOSED` | 真实 Provider 已验证递归拆分，List/Stream 保持顺序、重复、缺失和 Hit；单值超限仍显式失败。见 [BING-CACHE-TODO-20261007-058](tasks/completed/caching.md#BING-CACHE-TODO-20261007-058)。 | 停止重复同一拆分实验 |
| EasyCaching Hybrid 命中 null 的本地回填和合并批量预算 | `CLOSED` | 已修复 null 写入内存 Provider 的异常，并移除自适应子批合并后的错误二次预算检查；Redis/Hybrid net6/net8 精准回归通过。见 [BING-CACHE-TODO-20261007-058](tasks/completed/caching.md#BING-CACHE-TODO-20261007-058)。 | 停止重复 |
| 单节点流式读取及普通批量 API 的基本一致性 | `CLOSED` | FreeRedis、CSRedis、EasyCaching 的单节点 List/Stream、扫描和特殊键基础证据已落盘。见 [BING-CACHE-TODO-20261007-055](tasks/completed/caching.md#BING-CACHE-TODO-20261007-055) 和 [BING-CACHE-TODO-20261007-057](tasks/completed/caching.md#BING-CACHE-TODO-20261007-057)。 | 仅处理下方明确的测试缺口 |
| 消费第一批后取消时停止派发下一批 | `CLOSED` | FreeRedis/CSRedis net6/net8 各通过 7/7；EasyCaching mock net6/net8 各通过 40/40。覆盖 `GetAllStreamAsync`、`GetByPrefixStreamAsync` 和批次数断言。见 [BING-CACHE-TODO-20261007-060](tasks/completed/caching.md#BING-CACHE-TODO-20261007-060)。 | 停止重复 |
| 流式中途 Redis/Provider 失败的异常传播和部分结果语义 | `CLOSED` | FreeRedis/CSRedis 的第二批失败测试各在 net6/net8 通过；EasyCaching Redis/Hybrid 第二批解码失败在 net6/net8 通过 5/5 集成子集。 | 停止重复；Cluster/分片故障仍按外部门禁处理 |
| 1,000/10,000 项 List 与 Stream 结果一致性 | `CLOSED` | FreeRedis/CSRedis 的 1,000/10,000 项 `GetAll`/前缀对照在 net6/net8 各通过 7/7；EasyCaching mock 和 Redis/Hybrid 集成对照通过。 | 停止重复；100,000 项仍属于性能矩阵边界 |
| 特殊逻辑键和相似命名空间的前缀 Stream 隔离 | `CLOSED` | FreeRedis/CSRedis 特殊键 Stream 在 net6/net8 各通过；EasyCaching Hybrid 特殊键扫描和任务 057 实机流式隔离已通过。 | 停止重复；多节点隔离仍需外部拓扑 |
| EasyCaching Hybrid 批量写入、读取、删除和失效传播的实机一致性 | `CLOSED` | 单节点批量 refill/失败传播测试在 net6/net8 各通过；任务 077 首次双节点门禁复现写入实例本地层缺失，修复后 `ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge` 在 net6/net8 各 1/1 通过。见 [BING-CACHE-TODO-20261007-077](tasks/completed/caching.md#BING-CACHE-TODO-20261007-077)。 | 停止重复 Hybrid 批量回填；独立总线/Cluster 故障仍需外部环境 |
| Core 集合缓存（P7） | `CLOSED` | `SingleTypeLocalMemoryBase` 在同一稳定锁范围内完成检查、修改和快照；并发添加、更新不存在键、批量删除返回值和列表顺序由 `LocalMemoryCacheBaseTest` net6/net8 各 5/5 覆盖。 | 停止重复 |
| EasyCaching 显式注册与拓扑入口（P6） | `CLOSED` | Memory/Redis/Hybrid 注册入口已恢复；通用 Redis/Hybrid 入口对 `ClientSharded` 明确快速失败，专用 `AddShardedRedisCaching`/`AddShardedHybridCaching` 负责分片，注册边界测试已覆盖。 | 停止重复；真实双节点运行仍按外部门禁处理 |
| Redis Cluster/客户端分片槽位计算热路径 | `CLOSED` | `CacheClusterTopology.GetHashSlot` 已移除每键 `Substring`/`Encoding.UTF8.GetBytes` 临时分配；FreeRedis/CSRedis Cluster 分组、分片路由和租约删除统一调用共享实现。ASCII、Unicode、合法/孤立代理项参考等价性测试在 net6/net8 各 12/12 通过，三个生产项目 netstandard2.0 构建 0 警告/0 错误。见 [BING-CACHE-PERF-20261007-062](tasks/completed/caching.md#BING-CACHE-PERF-20261007-062)。 | 停止重复局部槽位实验；正式性能预算仍按下方边界处理 |
| EasyCaching 前缀流 null 命中和扫描竞态 | `CLOSED` | `ReadPhysicalBatchAsync` 已按物理键存在性产出条目，`CacheValue.HasValue` 只表达 Hit；null 命中/缺失值回归测试在 net6/net8 通过，完整 EasyCaching.Tests 双 TFM 各 72/72 通过。见 [BING-CACHE-TODO-20261007-063](tasks/completed/caching.md#BING-CACHE-TODO-20261007-063)。 | 停止重复本地 null 流实验；实机门禁沿用下方状态 |
| EasyCaching Redis/Hybrid 协调批量写入、删除和批量失效性能路径 | `CLOSED` | `RedisLeaseCoordinator` 已按单节点/Cluster 分组执行批量 Lua/删除，Hybrid 使用批量本地失效和单条多键失效消息；同步、异步、取消和 CacheManager 接入测试在 net6/net8 通过。见 [BING-CACHE-PERF-20261007-064](tasks/completed/caching.md#BING-CACHE-PERF-20261007-064)。 | 停止重复本地逐键协调实验；实机拓扑和正式性能预算沿用下方边界 |
| EasyCaching 协调删除的命令字节预算 | `CLOSED` | `_coordinator` 删除路径已按 Cluster 槽位、`BatchSize` 和 `MaxBatchCommandBytes` 切分；单键超限在 Redis 命令前失败，README 已同步。双 TFM 精准测试各 9/9、完整 EasyCaching.Tests 各 82/136 通过。见 [BING-CACHE-TODO-20261007-065](tasks/completed/caching.md#BING-CACHE-TODO-20261007-065)。 | 停止重复预算分批实验；外部拓扑沿用下方状态 |
| EasyCaching 已提交异步变更的取消后失效传播 | `CLOSED` | `RedisLeaseCoordinator` 的单键和批量异步写入/删除在 Redis 命令提交后统一使用不可取消令牌完成 Hybrid 本地失效和总线发布；任务 066 单元精准测试双 TFM 各 13/13，完整 EasyCaching.Tests 各 86 通过、54 跳过。见 [BING-CACHE-TODO-20261007-066](tasks/completed/caching.md#BING-CACHE-TODO-20261007-066)。 | 停止重复提交后取消实验 |
| EasyCaching 前缀流逐键回退的缺失项契约 | `CLOSED` | `ReadPhysicalBatchAsync` 的逐键回退现在为每个扫描候选键产出 `CacheEntry`，按 `HasValue` 区分 null 命中和缺失；任务 067 双 TFM 精准各 10/10、完整测试各 87 通过/54 跳过，生产 netstandard2.0 构建通过。见 [BING-CACHE-TODO-20261007-067](tasks/completed/caching.md#BING-CACHE-TODO-20261007-067)。 | 停止重复回退分支实验 |
| 当前缓存注册文档与 EasyCaching 实际入口对齐 | `CLOSED` | 已将当前指南、FAQ、包索引、配置参考和架构说明改为 `AddMemoryCaching`、`AddRedisCaching`、`AddHybridCaching` 及分片专用入口；迁移、CHANGELOG 和 ADR 历史快照保持不变。见 [BING-CACHE-TODO-20261007-068](tasks/completed/caching.md#BING-CACHE-TODO-20261007-068)。 | 停止重复文档扫描 |
| 原生 Redis SCAN 页级工作量预算 | `CLOSED` | 已增加可选 `MaxScanPages` 和带 `CacheScanLimitKind.ScanPages` 的异常；FreeRedis/CSRedis 同步、异步、流式、模式和清理扫描均在下一条 `SCAN` 前执行页闸门。公共预算测试双 TFM 通过，三个受影响生产项目 netstandard2.0 构建通过。见 [BING-CACHE-TODO-20261007-069](tasks/completed/caching.md#BING-CACHE-TODO-20261007-069)。 | 停止重复本地实现；真实 Redis 回归见下方外部门禁 |
| FreeRedis/CSRedis 原生扫描页预算真实 Redis 回归 | `CLOSED` | 使用任务专用 Redis 7.4 单节点验证 FreeRedis/CSRedis net6/net8 的同步、异步、流式前缀扫描、前缀删除和 Clear；每个操作在 `MaxScanPages=1` 时只发送 1 条匹配命名空间的 `SCAN` 并抛出 `ScanPages` 限制异常。见 [BING-CACHE-TODO-20261007-080](tasks/completed/caching.md#BING-CACHE-TODO-20261007-080)。自定义 EasyCaching Provider 页级契约另见 087。 | 停止重复 Provider 页预算门禁 |
| 三主三从 Cluster 的跨槽批量、全主节点扫描、槽迁移、故障转移和拓扑变化 | `CLOSED` | 使用任务专用 Redis 7.4 三主三从拓扑完成 FreeRedis/CSRedis net6/net8 的正常跨槽、槽迁移、扫描在途失败、7000 主节点故障转移和副本接管后的拓扑转换门禁；082 又补齐 EasyCaching Redis/Hybrid net6/net8 的跨槽批量、流式、全主节点扫描、删除、Clear 和命名空间哨兵隔离。所有临时容器和网络已清理，原有 `redis:6379` 未受影响。见 [BING-CACHE-TODO-20261007-079](tasks/completed/caching.md#BING-CACHE-TODO-20261007-079)、[BING-CACHE-TODO-20261007-082](tasks/completed/caching.md#BING-CACHE-TODO-20261007-082)。 | 停止重复 Cluster 功能门禁；大规模性能、槽迁移和故障证据仍按边界处理 |
| FreeRedis/CSRedis 双节点客户端分片标准门禁 | `CLOSED` | 两个独立 Redis 5.0.12 节点上的 FreeRedis 与 CSRedis 分片标准测试，net6/net8 各 7/7 通过；FreeRedis 同客户端批量写入调度和单键 MGET 响应错配已修复。见 [BING-CACHE-TODO-20261007-078](tasks/completed/caching.md#BING-CACHE-TODO-20261007-078)。 | 停止重复双节点标准门禁；Cluster、双进程和独立总线仍按下列事项处理 |
| FreeRedis/CSRedis/EasyCaching Redis 双进程回源协调 | `CLOSED` | 任务 034 已验证基础矩阵；089 使用当前候选和隔离 Redis 重新运行正常同键合并、持有者退出接管、超时拒写、失租拒写和等待超时，三个 Provider 均通过。见 [BING-CACHE-TODO-20261006-034](tasks/completed/caching.md#BING-CACHE-TODO-20261006-034) 和 [BING-CACHE-TODO-20261007-089](tasks/completed/caching.md#BING-CACHE-TODO-20261007-089)。 | 停止重复同一矩阵；仅在协调实现再次变更时重跑 |
| EasyCaching 独立 Redis 总线失效、重连和跨实例失效 | `CLOSED` | 084 使用独立数据端 `6383` 和总线端 `6384`，`EasyHybridBusReconnectIntegrationTest` 在 net6/net8 各通过 4/4，覆盖订阅断开、总线暂停、发布失败传播和恢复后的跨实例失效。见 [BING-CACHE-TODO-20261007-084](tasks/completed/caching.md#BING-CACHE-TODO-20261007-084)。 | 停止重复基本独立总线重连门禁 |
| EasyCaching 双数据分片重启与总线组合故障 | `CLOSED` | 085 使用两个独立数据 Redis `6385/6386` 和独立总线 Redis `6387`，在 net6/net8 各通过组合故障门禁；覆盖总线暂停、两个分片同时重启、身份变化、失败传播和重建后跨实例失效。见 [BING-CACHE-TODO-20261007-085](tasks/completed/caching.md#BING-CACHE-TODO-20261007-085)。 | 停止重复该组合故障门禁；Cluster 公告代理故障仍按下方边界处理 |
| 正式性能预算（批读、扫描、分配量） | `VERIFIED_BOUNDARY` | 命中读 P95 通过；批读 P95 1/4、扫描 P95 2/4、分配量 0/8 通过，预算结论为 `COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED`。任务 062 仅关闭槽位计算局部分配根因，未提供同构 before/after，不能改变预算结论。见 [正式性能报告](tasks/BING-CACHE-TODO-20261005-004/performance.md) 和 [任务 062](tasks/completed/caching.md#BING-CACHE-PERF-20261007-062)。 | 只有新的同构 baseline/候选性能证据获准后才开性能任务，禁止重复同一矩阵 |
| 100,000 项完整矩阵和 600 秒超时 cell | `VERIFIED_BOUNDARY` | 14 个 cell 中 7 个完整结束、7 个达到超时边界；超时没有完整统计，不能据此宣称能力失败，也不为消除边界重复运行。 | 保留原始结果和边界说明 |
| EasyCaching Redis/Hybrid 原生 SCAN 页预算真实回归 | `CLOSED` | `CacheManager` 的 `IDatabase` 路径已改为显式原生 `SCAN`，Redis/Hybrid net6/net8 真实 Redis 7.4 门禁覆盖同步、异步、流式前缀扫描、前缀删除和 Clear；每个操作在 `MaxScanPages=1` 时只发送一条匹配命名空间的 `SCAN` 并抛出 `ScanPages` 限制异常。见 [BING-CACHE-TODO-20261007-081](tasks/completed/caching.md#BING-CACHE-TODO-20261007-081)。自定义 Provider 隐藏分页仍保留边界。 | 停止重复 EasyCaching 原生页预算实现 |
| EasyCaching 单节点 SCAN 响应丢失和 Cluster 槽位归属变化 | `CLOSED` | 任务专用 Redis 7.4 单节点的响应中断门禁、三主 Cluster 的槽位归属变化门禁均在 net6/net8 通过；Cluster 复核覆盖 16,384 槽位并已清理临时资源。见 [BING-CACHE-TODO-20261007-083](tasks/completed/caching.md#BING-CACHE-TODO-20261007-083)。 | 停止重复单节点响应和槽位变化门禁；Cluster 在途故障仍按下方外部边界处理 |
| EasyCaching Cluster 扫描在途响应丢失 | `CLOSED` | 086 修正三主节点公告和共享 RESP 代理监听地址后，在真实 Redis 7.4 三主 Cluster 上完成 net6/net8 门禁；代理暂存 SCAN 响应后中断连接，流式读取传播非超时异常，finally 后槽位仍完整。见 [BING-CACHE-TODO-20261007-086](tasks/completed/caching.md#BING-CACHE-TODO-20261007-086)。 | 停止重复 Cluster SCAN 响应丢失门禁 |
| EasyCaching Redis Cluster 副本提升后重建 Manager | `CLOSED` | 090 使用任务专用 Redis 7.4 三主三从 Cluster，`CLUSTER FAILOVER TAKEOVER` 后重建 EasyCaching Manager；跨槽 `GetAllAsync`、前缀 List/Stream、Clear 和完整槽位断言在 net6/net8 各 1/1 通过。见 [BING-CACHE-TODO-20261007-090](tasks/completed/caching.md#BING-CACHE-TODO-20261007-090)。 | 停止重复副本提升后重建 Manager 门禁 |
| EasyCaching Redis Cluster 主节点重启后重建 Manager | `CLOSED` | 091 在任务专用 Redis 7.4 三主三从 Cluster 上重启 7000 主节点，断言 `run_id` 变化、完整槽位恢复，并验证重建 Manager 的跨槽批读、前缀 List/Stream、Clear 和命名空间哨兵隔离；net6/net8 各 1/1 通过。见 [BING-CACHE-TODO-20261007-091](tasks/completed/caching.md#BING-CACHE-TODO-20261007-091)。 | 停止重复主节点重启恢复门禁 |
| EasyCaching Cluster 节点重启、故障转移和组合故障 | `CLOSED` | 090/091 分别关闭副本提升和主节点重启后的重建 Manager 证据；092 在完整 SCAN 响应挂起期间重启主节点，断言扫描传播异常、部分结果不伪造成功，并在公告恢复后验证重建 Manager 读写和清理。三项均在 net6/net8 通过。见 [BING-CACHE-TODO-20261007-092](tasks/completed/caching.md#BING-CACHE-TODO-20261007-092)。 | 停止重复同一故障矩阵 |
| 真实网络往返、客户端内部排队观测 | `DEFERRED` | 当前探针只覆盖 Redis 命令、已接入 Provider 调用和实际并发；不能把命令数或请求并发参数当作网络/客户端指标。 | 先确定观测工具和口径，再单独立项 |
| 自定义 EasyCaching Provider 隐藏分页 | `CLOSED` | 新增可选 `ICacheKeyPageStreamProvider`，EasyCaching 异步前缀读取、前缀/模式删除和 Clear 共享路径会记录页级探针并执行 `MaxScanPages`；旧逐键流和完整集合 Provider 保持兼容回退。双 TFM 页流优先级、顺序、空页、页预算和取消测试通过。见 [BING-CACHE-TODO-20261007-087](tasks/completed/caching.md#BING-CACHE-TODO-20261007-087)。 | 同步 Provider 和旧隐藏分页仍按边界处理，不伪造页数 |
| EasyCaching 客户端分片前缀读取并发 | `CLOSED` | `CacheScanBudget` 已支持并发计数和真实分片诊断；`ShardedRedisCacheManager` 的 List 使用 `MaxConcurrency` 窗口，Stream 使用每分片一个在途读取并按完成顺序产出；Bing.Caching/EasyCaching 双 TFM 单元测试通过。见 [BING-CACHE-TODO-20261007-074](tasks/completed/caching.md#BING-CACHE-TODO-20261007-074)。 | 停止重复前缀读取实现；真实双节点沿用外部门禁 |
| EasyCaching 客户端分片异步清理/模式扫描并发 | `CLOSED` | `RemoveByPrefixAsync`、`RemoveByPatternAsync` 和 `ClearAsync` 已使用 `MaxConcurrency` 分窗、共享扫描预算、链接取消和已派发任务观察；前缀/模式/Clear 直接并发测试及完整 EasyCaching 双 TFM 回归通过。见 [BING-CACHE-TODO-20261007-075](tasks/completed/caching.md#BING-CACHE-TODO-20261007-075)。 | 停止重复异步清理实现；真实双节点仍沿用外部门禁 |
| EasyCaching 客户端分片同步清理并发契约 | `CLOSED` | 已冻结 `Sequential` 默认行为，并新增显式 `WithSyncCleanupMode(BoundedParallel)`；按 `MaxConcurrency` 分窗派发，窗口失败停止后续派发并稳定传播首个原始异常。前缀、模式和 Clear 共用调度器，088 双 TFM 精准/完整回归通过。见 [BING-CACHE-TODO-20261007-088](tasks/completed/caching.md#BING-CACHE-TODO-20261007-088)。 | 停止重复本地实现；真实双节点 Provider 并发仍沿外部门禁 |
| EasyCaching 分片异步清理首错快速取消 | `CLOSED` | `RunShardScansAsync` 已按完成顺序观察窗口；首个失败/取消立即取消链接令牌，已派发任务随后被观察并保留原始异常。首错取消闸门和 EasyCaching 双 TFM 完整回归通过。见 [BING-CACHE-TODO-20261007-076](tasks/completed/caching.md#BING-CACHE-TODO-20261007-076)。 | 停止重复清理首错取消实现 |
| 严格旧版/中间版/优化版三方性能对照 | `BLOCKED_EXTERNAL` | 缺少可重建的中间源码快照，不能拼接不同候选、探针和采样结果。见 [正式性能报告](tasks/BING-CACHE-TODO-20261005-004/performance.md)。 | 提供中间源码和同探针产物 |
| 旧版没有等价流式 API，旧版没有合法 Cluster/分片构造 | `NOT_APPLICABLE` | 没有同构历史能力，流式和拓扑能力只能报告当前绝对值，不能伪造回退百分比。 | 不创建伪 baseline |
| SCAN/Clear 非原子、批量部分成功、跨键自适应重读非快照、Hybrid null 不回填本地层 | `ACCEPTED_LIMITATION` | 这些行为已写入当前契约和任务 058 风险说明，不属于隐藏未实现。 | 在迁移/接口文档中保持说明 |
| Redis 提交后进程在 Hybrid 总线发布前退出 | `ACCEPTED_LIMITATION` | 093 的 outbox 能恢复已经进入 delivery、但尚未确认的 pending/processing 消息；若进程在 Redis 提交后、创建失效消息前退出，当前仍没有跨 Redis 写入与 outbox 入队的事务保证，其他实例可能暂时保留旧本地值。Hybrid 继续保持最终一致，不宣称强一致。 | 若业务需要覆盖“数据提交到失效入队之前”的崩溃窗口，另立事务 outbox/补偿设计任务 |
| 历史任务 TODO 的状态治理 | `CLOSED` | 本索引统一了 001-008、021 和 046-058 的最新状态；历史文档保留为快照，不删除或改写旧勾选。 | 后续审计只读本索引 |

## 历史任务覆盖（046-095）

| 任务 | 当前索引结论 | 主要证据 |
| --- | --- | --- |
| 046 | `CLOSED` 本地分片契约已补；真实双节点仍 `BLOCKED_EXTERNAL` | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-046) |
| 047 | `CLOSED` 覆盖写入、空输入和预取消契约已补 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-047) |
| 048 | `CLOSED` 扫描删除分组和取消回归已补；性能为 `VERIFIED_BOUNDARY` | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-048) |
| 049 | `CLOSED` 批量删除分组验证已补；真实命令计数依赖外部 Redis | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-049) |
| 050 | `CLOSED` 字节预算删除拆分已补；网络/队列观测 `DEFERRED` | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-050) |
| 051 | `CLOSED` 删除字节预算和 Provider 接入已补；跨拓扑实机 `BLOCKED_EXTERNAL` | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-051) |
| 052 | `CLOSED` 孤立代理项键校验已补 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-052) |
| 053 | `CLOSED` 前缀/模式代理项校验已补 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-053) |
| 054 | `CLOSED` 配置代理项和物理键隔离已补 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-054) |
| 055 | `CLOSED` 单节点真实批量、扫描、TTL 和流式证据已补；原 RESP 缺口由 056 关闭 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-055) |
| 056 | `CLOSED` 删除取消 RESP 闸门已补 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-056) |
| 057 | `CLOSED` Hybrid 特殊键和相似命名空间流式隔离已补 | [TODO](tasks/completed/caching.md#BING-CACHE-TODO-20261007-057) |
| 058 | `CLOSED` 自适应拆分和 Hybrid null 回填修复已补 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-058) |
| 062 | `CLOSED` 共享 Redis 槽位计算移除每键临时分配；正式性能预算仍为边界 | [execution](tasks/completed/caching.md#BING-CACHE-PERF-20261007-062) |
| 063 | `CLOSED` EasyCaching 前缀流保留 null 命中并报告扫描竞态 miss | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-063) |
| 064 | `CLOSED` EasyCaching Redis/Hybrid 协调批量写入、删除、分槽和批量失效 | [execution](tasks/completed/caching.md#BING-CACHE-PERF-20261007-064) |
| 065 | `CLOSED` EasyCaching 协调删除接入命令字节预算和 Cluster 分槽切分 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-065) |
| 066 | `CLOSED` EasyCaching 已提交异步变更的取消后失效传播 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-066) |
| 067 | `CLOSED` EasyCaching 前缀流逐键回退保留缺失项和 null 命中 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-067) |
| 068 | `CLOSED` 当前缓存注册文档与 EasyCaching 实际入口对齐 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-068) |
| 069 | `CLOSED` 原生 Redis SCAN 页级工作量预算实现；真实 Provider 回归由 080 关闭 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-069) |
| 070 | `CLOSED` Hybrid 失效消息独立字节预算与有界拆分 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-070) |
| 071 | `CLOSED` Hybrid 失效消息预算测量实例一致性 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-071) |
| 072 | `CLOSED` FreeRedis/CSRedis 客户端分片批量读取已按 `MaxConcurrency` 窗口调度；真实双节点门禁仍外部阻塞 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-072) |
| 073 | `CLOSED` FreeRedis/CSRedis 同步客户端分片 List/前缀读取复用有界路由；真实双节点门禁仍外部阻塞 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-073) |
| 074 | `CLOSED` EasyCaching 分片前缀 List/Stream 使用共享预算和 `MaxConcurrency` 有界调度；真实双节点门禁仍外部阻塞 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-074) |
| 075 | `CLOSED` EasyCaching 分片异步前缀/模式删除和 Clear 使用有界并发；同步清理契约已由 088 收口，真实双节点门禁仍外部阻塞 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-075) |
| 076 | `CLOSED` EasyCaching 分片清理首错立即取消在途任务并保持原始异常传播；真实双节点门禁仍外部阻塞 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-076) |
| 077 | `CLOSED` EasyCaching 双节点 Redis/Hybrid 标准门禁和 Hybrid 批量写入实例本地回填修复；大规模、Cluster、独立总线仍列外部/延后边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-077) |
| 078 | `CLOSED` FreeRedis/CSRedis 双节点客户端分片标准门禁；修复 FreeRedis 同客户端批量写入并发和单键 MGET 响应错配 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-078) |
| 079 | `CLOSED` Redis 7.4 三主三从 Cluster 正常跨槽、槽迁移、扫描在途失败、故障转移和拓扑转换门禁；修正缺失扫描主节点测试中的错误直接键流断言 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-079) |
| 080 | `CLOSED` FreeRedis/CSRedis 原生 SCAN 页预算在真实 Redis 单节点上覆盖同步、异步、流式、删除和 Clear 路径；每个操作只发送一页扫描命令 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-080) |
| 081 | `CLOSED` EasyCaching Redis/Hybrid 原生 SCAN 页预算在真实 Redis 单节点上覆盖同步、异步、流式、删除和 Clear 路径；每个操作只发送一页扫描命令 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-081) |
| 082 | `CLOSED` EasyCaching Redis/Hybrid 在真实三主三从 Redis Cluster 上覆盖小规模跨槽批量、流式、全主节点扫描、删除、Clear 和命名空间哨兵隔离 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-082) |
| 083 | `CLOSED` EasyCaching 单节点 SCAN 响应丢失和三主 Cluster 槽位归属变化门禁；Cluster 在途故障保留外部边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-083) |
| 084 | `CLOSED` EasyCaching 独立数据 Redis/总线 Redis 的订阅重连、暂停失败和恢复门禁；双数据分片组合故障保留外部边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-084) |
| 085 | `CLOSED` EasyCaching 两个数据分片同时重启与独立总线暂停组合故障；Cluster 公告代理类故障保留外部边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-085) |
| 086 | `CLOSED` EasyCaching Cluster SCAN 响应丢失门禁；修正三节点公告配置和共享代理监听地址，节点故障转移仍保留外部边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-086) |
| 087 | `CLOSED` 自定义 EasyCaching Provider 可选页流契约、页级预算、探针和取消回归；同步 Provider 与旧回退仍保留边界 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-087) |
| 088 | `CLOSED` EasyCaching 客户端分片同步清理显式有界并发；默认顺序、窗口失败观察和前缀/模式/Clear 回归已补 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-088) |
| 089 | `CLOSED` 当前候选 FreeRedis/CSRedis/EasyCaching Redis 跨进程回源协调复验；隔离 Redis 五类场景全部通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-089) |
| 090 | `CLOSED` EasyCaching Redis Cluster 副本提升后重建 Manager；跨槽批读、前缀 List/Stream、Clear 和完整槽位在 net6/net8 各通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-090) |
| 091 | `CLOSED` EasyCaching Redis Cluster 主节点重启后重建 Manager；run_id、完整槽位、跨槽批读、前缀 List/Stream 和 Clear 在 net6/net8 各通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-091) |
| 092 | `CLOSED` EasyCaching Cluster 扫描响应在途期间主节点重启；扫描异常传播、部分结果不伪造成功和恢复后重建 Manager 在 net6/net8 各通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-092) |
| 093 | `CLOSED` Hybrid 失效投递增加默认关闭的有限重试、Redis pending/processing outbox、队列上限和重建恢复；单元与真实 Redis 门禁在 net6/net8 通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-093) |
| 094 | `CLOSED` Hybrid outbox 增加只读队列深度、最早消息年龄、进程内计数和最近失败诊断；旧 envelope 兼容，双 TFM 单元与 Redis 门禁通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-094) |
| 095 | `CLOSED` Hybrid outbox 增加持久化投递尝试次数、有界 dead-letter 原子转移和诊断计数；默认关闭，双 TFM 单元与真实 Redis 门禁通过 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-095) |
| 096 | `CLOSED` Hybrid dead-letter 增加保留期和有界清理 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-096) |
| 097 | `CLOSED` Hybrid dead-letter 增加精确 payload 字节预算 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-097) |
| 098 | `CLOSED` Hybrid dead-letter 增加人工查看、重放、确认删除、授权与审计 | [execution](tasks/completed/caching.md#BING-CACHE-TODO-20261007-098) |
| 099 | `BLOCKED_EXTERNAL` Hybrid dead-letter 功能、单节点 Redis 和三主三从 Cluster 门禁已通过；跨版本发布验证未运行 | [execution](tasks/BING-CACHE-TODO-20261007-099/execution.md) |

## 后续读取规则

- 只有本文件标为 `OPEN_ACTIONABLE` 的事项可以触发下一轮实现任务。
- `BLOCKED_EXTERNAL`、`VERIFIED_BOUNDARY`、`DEFERRED`、`NOT_APPLICABLE` 和 `ACCEPTED_LIMITATION` 不得因为旧文档的 `[ ]` 自动重新打开。
- 新任务必须引用本文件中的行项目和最新测试证据；完成后先更新本索引，再更新对应任务文档。
- 不得用单节点结果替代 Cluster、分片、双进程或独立总线证据，也不得把未开启的性能探针字段填成推测值。

当前 `OPEN_ACTIONABLE`：无。099 已完成 handle API、payload 分离、惰性迁移逻辑；单节点 Redis 与三主三从 Cluster 的 dead-letter 门禁已经通过，唯一剩余外部项是旧/新二进制蓝绿切换验证，状态详见 099。098 关闭了 Hybrid outbox dead-letter 人工查看、重放、确认删除、授权和审计；097 关闭了精确 payload 字节预算、历史 payload 重算和字节诊断；096 关闭了 Hybrid outbox dead-letter 保留期与有界清理；095 关闭了 Hybrid outbox 毒消息隔离；094 关闭了 Hybrid outbox 运维可观测性；093 关闭了 Hybrid 失效投递的可选重试、背压和 Redis outbox 能力；092 关闭了 EasyCaching Cluster 扫描在途与主节点重启组合故障门禁；091 关闭了主节点重启后重建 Manager 的真实门禁；090 关闭了副本提升后重建 Manager 的真实门禁；089 用当前候选重新关闭了三 Provider 跨进程回源协调证据缺口；088 关闭了 EasyCaching 客户端分片同步清理的显式有界并发契约；087 关闭了自定义 EasyCaching Provider 的可选页流、页级预算、探针和取消能力；086 关闭了 EasyCaching Cluster SCAN 响应丢失门禁；085 关闭了两个数据分片同时重启与独立总线暂停组合故障，084 关闭了独立 EasyCaching 数据端/总线端的基本失效与恢复门禁；此前 083 补齐了 EasyCaching 单节点 SCAN 响应丢失和三主 Cluster 槽位归属变化门禁，082 补齐了 EasyCaching Redis/Hybrid 的三主 Cluster 小规模功能门禁，081 关闭了 EasyCaching 原生 SCAN 页预算，080 关闭了 FreeRedis/CSRedis 页预算，079 关闭了 FreeRedis/CSRedis Cluster 故障和拓扑门禁。其余正式性能边界、真实网络/客户端排队观测、严格三方性能对照、NullCache 语义审批、Redis allocator 实际内存预算、跨进程全局指标以及数据提交到失效入队前的崩溃窗口仍属于边界、延后、外部或决策状态；同步自定义 Provider 仍无法报告异步页数，不伪造该指标。
