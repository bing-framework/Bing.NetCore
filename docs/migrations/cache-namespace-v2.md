# 缓存命名空间 v2 迁移

两个 Redis 适配器现在要求注入 `CacheConfiguration`，并使用版本化、按实例隔离的物理键。旧键不会回读或自动清除，因此部署后是冷缓存。发布时应评估后端回源容量，必要时分批滚动发布或预热。

1. 移除 Redis 客户端连接串中的 `prefix`，在受信配置中设置应用或租户的命名空间及 Provider 名称。
2. 为 CSRedis 注册 `CSRedisClient`、`CacheConfiguration` 和 `CSRedisCacheManager`；为 FreeRedis 注册已设置序列化器的 `RedisClient`、`CacheConfiguration` 和 `FreeRedisCacheManager`。
3. 在共享 Redis 数据库中，`Clear` 现在只清理本实例数据。旧键需要通过单独审核的运维流程处理，不能把新 `Clear` 当成旧 `FLUSHDB` 使用。
4. 多节点部署须显式配置 `CacheTopology` 和完整、稳定顺序的主节点扫描客户端列表。客户端分片还须设置可信的 `expectedShardCount`；变更分片顺序等同于重新分布缓存数据。Cluster 扫描会核对 Redis 当前主节点清单，切主后须更新客户端配置。
5. `CacheOptions.Expiration` 必须大于零；显式值按毫秒向上取整。`SetAllAsync` 现在覆盖已有值。`SingleTypeLocalMemoryBase.Reader` 与 `ListLocalMemoryBase.ReaderAll` 现在返回浅快照。

## 输入长度限制

缓存实例默认保持旧行为，不限制逻辑输入长度。对于请求参数可能直接影响缓存键、前缀或模式的接入方，建议在可信配置中调用 `CacheConfiguration.WithInputLimits` 设置 `CacheInputLimits`。限制按 .NET `string.Length` 计算，超限在本地参数校验阶段抛出 `ArgumentOutOfRangeException`，不会派发 Redis 命令；值为 0 表示不限制。该能力不限制总批量项数、底层客户端接收缓冲、反序列化对象或兼容 `List` API 的最终结果大小，相关边界仍需由调用方和资源配置共同控制。

回源租约用于减轻击穿。原生 Redis 和 EasyCaching Redis/Hybrid 模式均使用随机令牌租约，并通过同槽 Lua 脚本校验所有权后原子写回；Hybrid 写回成功后还会发布本地层失效通知。回源委托需要幂等，Hybrid 仍是最终一致缓存；Redis 故障转移期间不保证严格一次执行。租约、写回或失效发布失败会向调用方报告，已完成的 Redis 写入无法撤销。

## 回源、资源与流式边界

`CacheResourceOptions` 默认允许 8 个不同键协调持有者、128 个等待持有者；先完成进程内同键合并，再申请共享准入预算。复用同一资源配置对象的实例/分片共用预算，分别创建的配置对象独立计数；同键 gate 等待不计入不同键准入队列。命中读取不消耗该预算。嵌套不同键回源仍受并发上限约束，应避免在工厂内同步等待已经耗尽的同一预算；排队满或等待超时明确失败。

原始读取响应的默认字节预算为 8MiB，按单个 MGET/同槽脚本响应累计 UTF-8/原始字节，在解码前检查，超过预算会按键递归拆分并重读；单个值即使单键读取仍超过预算时明确抛异常。自适应拆分不能限制底层客户端已经接收的缓冲、反序列化后的对象大小或 List 的最终结果体积；子批次重读也不是跨键快照，可能观察到不同时间点的值，并会增加 Redis 命令数。调用方仍应结合 `BatchSize` 与实际值大小设置资源预算。

`RetrievalTimeout` 在获得租约后、调用工厂前起算；正的不足一毫秒协调时间向上取整。异步工厂收到同时关联调用方取消、总时限及失租的生命周期令牌。忽略令牌的异步任务到期后不阻塞调用方，晚结果不写回，晚异常被观察。同步用户代码以及异步委托返回 Task 前的阻塞代码无法安全强制终止；到期停止续租并拒绝结果写回，但占用的本地 gate/额度要等该代码返回后释放。清理等待有界；原异常优先，释放异常附加到 `Exception.Data["CacheCleanupException"]`。

显式 Set、成功的 TrySet、删除与批量对应操作原子撤销当前同键租约，旧工厂不能覆盖新值或复活删除项。TrySet 未写入时保留原租约。Clear/前缀扫描删除仍为非原子操作，不承诺并发写入后最终为空。

Hybrid 回填用同一次原子 GET/PTTL 的剩余 TTL，扣除读取与回填耗时，并关闭本地随机 TTL 延长；永久或已到期值不推断默认本地 TTL。总线发布者主动处理自己的本地层，自身通知回环被过滤；其他实例失效仍为最终一致。订阅断线与重连清空本地层，恢复断线窗口丢失的更新/删除；总线暂停但连接尚未断开时不能承诺立即发现丢失消息。发布失败会报告，Redis 已完成的写入不能撤销。

## Hybrid 失效投递与 outbox

`AddHybridCaching` 和 `AddShardedHybridCaching` 默认不启用持久化 outbox，现有失败语义保持不变。需要有限重试和故障恢复时，在注册入口配置 `HybridInvalidationDeliveryOptions`：`MaxPublishAttempts` 控制单条消息的总尝试次数，`RetryDelay` 控制重试间隔，`MaxPendingMessages` 大于零才创建 Redis pending/processing 列表。若要隔离持续失败的毒消息，必须同时设置正的 `MaxDeliveryAttempts` 和 `DeadLetterMaxMessages`；配置不完整会在注册/构造阶段失败。

队列容量同时计算 pending 与 processing；达到上限时当前缓存操作仍报告原始总线异常，不静默成功。消息从 pending 移入 processing 后，只有发布成功才确认删除；进程在发布和确认之间退出时，重建后的投递器会再次处理该消息。达到持久化投递次数阈值时，投递器使用同槽 Lua 脚本把消息原子移入有界 dead-letter sorted set，并清理尝试计数；容量不足、取消或脚本失败都会保留 processing。`DeadLetterMaxBytes` 可选限制 dead-letter JSON envelope 的 UTF-8 payload 字节总和，零表示关闭；启用后旧 member 的 payload 大小会在辅助计数缺失时自动重算。`DeadLetterRetention` 默认关闭，显式启用后按 dead-letter 转移时间和批大小自动清理，清理失败只进入诊断并等待下一轮，不改变 processing。`DeadLetterMaxMessages` 是消息数容量预算，`DeadLetterMaxBytes` 只计算 payload 字节，不代表 Redis allocator 的实际内存。该能力提供有界、至少一次的失效投递，不提供严格一次、跨键事务或全局顺序保证，失效处理必须保持幂等。启用 outbox 要求分布式 EasyCaching Redis Provider 暴露 `StackExchange.Redis.IDatabase`。

可通过 DI 解析 `IHybridInvalidationDeliveryDiagnostics` 获取队列和 dead-letter 深度、payload 字节总数、最早消息年龄、当前进程发布成功/失败尝试/入队/隔离/过期清理计数和最近失败文本。客户端分片每个主题对应一个诊断实例。计数器不跨进程合并，状态字段不是同一事务快照；诊断读取不会消费或移动队列。旧 envelope 没有入队时间时仍正常投递但不报告年龄。若需要人工处置，必须同时注册 `IHybridInvalidationDeadLetterAuthorizer` 与 `IHybridInvalidationDeadLetterAuditSink`，再通过 `IHybridInvalidationDeadLetterAdmin` 进行有界查看、容量受控重放和 confirmation token 确认删除；返回的 handle/token 只包含摘要，不含原始缓存键。管理接口不提供 Redis allocator 实际内存预算、跨进程全局指标或跨 Redis 写入与失效入队的事务保证。

前缀流基于 SCAN，属于非快照遍历：并发变化可能导致重复或遗漏，已产出的条目不会因后续错误撤回；扫描命中与随后的读取也可能因过期变为 miss。List API 仍收集所有最终结果，内存复杂度为 O(N)；Stream 只保留响应批次及拓扑/当前节点状态，调用方持有的结果另计。提前停止消费不会完成全拓扑结束校验，不能据此声称全命名空间遍历成功。取消停止后续命令派发，已发送的命令无法撤销。
