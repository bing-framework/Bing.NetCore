<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T03:57:18Z

# 集群能力与运行治理差距复核

## 范围、方法与结论

对照会话批准的完整 P0–P4 计划、plan.md、execution.md、当前工作区源码、Git 差异、生产接线、测试代码及最新有效 TRX 复核。本轮未修改业务代码、测试、计划或执行记录，未重新运行 .NET 测试、构建、容器和流量切换；只更新审查报告并保存上一版至 review-before-current-evidence-check-20261005.md。独立 luna_worker 只读核对 TRX 中实际 UnitTestResult，避免把跳过计为通过。

仍有 **7 组原计划待交付工作包**：FIX-006、007、008、009、011、012、013。它们大多已有组件实现，缺完整接线、边界处理或验收；不能理解为七项功能完全没有，也不以测试数量计算完成百分比。

FIX-014～018 的具体问题现已有修复与直接测试证据；FIX-019 的默认日志、凭据和 Dashboard 注册已调整，完整日志输出及访问授权仍待验。新增确认 FIX-020：库存批次使用可碰撞的字符串组合键。不重复计入原计划七组，FIX-020 纳入 FIX-007 的修复。

| 阶段 | 状态 | 已有能力与剩余 |
| --- | --- | --- |
| P0 | PASS（核心历史证据） | 本地/Redis 原子锁、所有者释放、完整租期、键回收、订阅快照已实现。故障切换和长期负载另验。 |
| P1 | PARTIAL | 元数据、两 ORM 领域事件/出箱/收据、提交结果诊断、CAP 关闭已有；真实断连、取消确认和 Unknown 恢复待验。 |
| P2 | PARTIAL | 有序存储/游标/后台处理、历史真实 CAP 链路、库存条件更新/幂等/固定排序/ORM 适配已有；MySQL 唯一冲突旧快照已复现修复。生产恢复、库存完整事务和库存双进程验收仍缺。 |
| P3 | PARTIAL | 本地/Redis 限流、HTTP 可信身份/代理、Admin 接线、审计管理/数据库存储/ORM 适配、用途驱动 CAS 已有；自动变更快照、关键业务组合、故障/容量验收仍缺。 |
| P4 | PARTIAL | 双平台模板及 HTTP 就绪/排空组件已有；真实角色分离、依赖预热、消费/任务排空、动态切流和回滚仍缺。 |

不再作为完全缺失列项：HTTP 审计、数据库审计存储、两 ORM 事务审计入口、FreeSQL 实体审计字段、库存存储/适配、Admin 限流、恢复授权扩展、有序指标、HTTP 原子闸门。先前 EFCore CAP 包装事务、入箱重试、Sample Lazy 实际解析/正常停止的通过仍只代表对应历史批次。

## 原计划剩余 TODO

### FIX-006：有序生产接入与恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题与证据：已有 Coordinator/Publisher/Worker、两 ORM 工厂及 CAP 测试；两个 Admin 未接入 AddBingOrderedConsumer 或生产状态/重试入口。恢复授权器可选，GetStateAsync 不提供宿主权限策略。
- 影响：组件通过不能证明生产订阅等待入箱、正确绑定事务或安全跨租户恢复。
- 修复目标与要求：显式生产接线；订阅等待 ReceiveAsync；状态、重试和补偿授权及关键审计；派生 CAP 消息绑定当前事务；失败丢弃作用域及工作单元。
- 验证方式：真实宿主端到端、前序失败竞争、派生消息回滚、慢处理器停止及取消确认。

### FIX-007：完整库存一致性业务

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题与证据：DbInventoryStore、InventoryBatchOperations、两 ORM SQLite 适配已有；未见库存、整单结果、关键审计、CAP 出箱的完整业务服务及真实 MySQL 库存双进程测试。
- 影响：局部条件更新不能证明整单幂等、批次原子性和完整防超卖闭环。
- 修复目标与要求：业务记录同事务；整单请求和首次结果；仅在确认回滚后做有界整笔死锁重试；Unknown 先查询结果，不能盲目重做；补 FIX-020。FIX-018 已有两个交错事务的当前读修复，不再列为待复现。
- 验证方式：两 ORM MySQL 并发、重复、余额不足、取消重试、多 SKU 回滚、提交中断，断言库存非负及扣减/审计/出箱次数。

### FIX-008：完整限流宿主与 Redis 故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题与证据：429/503、可信身份/路由/代理、Admin 配置和真实 Redis 配额已有；现有 Redis 双容器测试是同进程独立 DI 容器。完整 Admin HTTP、跨进程及故障切换证据仍缺。
- 影响：组件管道和注册测试不能代替实际 JWT/代理/Redis 故障链路。
- 修复目标与要求：两 Admin 完整请求、跨进程共享额度、断连/恢复、蓝绿参数协调。
- 验证方式：伪造代理头、租户/路由、Retry-After、503/显式降级、取消、额度恢复及键回收。

### FIX-009：自动审计与关键事务

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题与证据：HTTP 请求作用域、安全投影/白名单/上限、数据库存储、ORM 显式事务适配已实现；EntityChanges 仍手工填写，Admin 默认日志存储，关键测试由调用方显式回滚。
- 影响：不能宣称自动操作审计或完整关键业务原子性；实体字段填充不等于日志快照。
- 修复目标与要求：可选 EF ChangeTracker/FreeSQL 快照；生产落库配置；关键业务审计失败必须回滚；响应已发/业务已提交后的 HTTP 审计错误不伪装为回滚；同步处理 FIX-019。
- 验证方式：真实 MySQL、库存/收据/审计/CAP 组合提交与回滚、敏感值排除、执行中取消、多重清理错误。
- 迁移：EntityChangeInfo.ChangeType 改为 EntityChangeType 具有源码/二进制影响，尚缺对应旧消费者与发布检查证据。

### FIX-010：用途驱动原子工具

- 状态：RESOLVED（核心实现），不计独立缺失能力。
- 证据：BingTrafficGate 的 CAS 状态、Interlocked 在途计数和一次释放句柄接入两 Admin，最新定向包括准入与排空竞争。
- 后续：持续负载和内存纳入 FIX-012，不重复创建通用无锁库。

### FIX-011：蓝绿角色和动态验收

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题与证据：两个 Startup 无条件注册 CapModule，源码未消费 BING_HOST_ROLE/BING_CAP_CONSUMER_ENABLED。AppModule 管道末尾 MarkBingTrafficReady 未校验数据库/消息依赖或业务预热。Test-BlueGreen.ps1 的 Live 路径只检查配置/就绪/选择器。
- 影响：模板环境变量不能真正禁止绿版消费；模板存在不代表安全蓝绿部署已验收。
- 修复目标与要求：Web/消费者/定时任务角色分离，Web 可出箱不订阅，稳定逻辑组；真实预热后就绪；HTTP 与消费分别摘流/排空，超时报告并保留恢复依据。
- 验证方式：Compose/Nginx、Kubernetes 分别覆盖长请求、活动消费、绿版不就绪、切流/回滚和强停恢复，记录镜像/config/请求及消息证据。

### FIX-012：指标、容量、归档和交付

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题与证据：现有指标是进程内快照，PendingStreams 来自本页 streams.Count；缺总积压/年龄、指标导出、持续 GC/负载及归档。最新映射已补，但 AuditLogSchema 未知数据库测试方法名不正确；执行记录尚未追加最新批次结果。
- 影响：无法据此评估长期内存/磁盘或真正积压；文档与实现存在差异。
- 修复目标与要求：FIX-017 已确认提交计数和故障 finally 通过；继续有界标签/告警节流/授权诊断、总积压与年龄、留存归档、最新映射/报告/迁移。
- 验证方式：持续分配、存活对象、GC、延迟、积压及后台对象回收；未给容量预算时只报告测量，不自定 MB/QPS 合格阈值。

### FIX-013：Unknown 与真实故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题与证据：提交诊断、ORM 清理及 CAP 单一关闭已有；缺真实提交确认丢失、提交后 Flush/Dispose 失败和 Unknown 查询恢复。SIGNAL 原生错误测试不是网络断连。
- 影响：抛异常或等待结束不能判断业务未提交或可以重做。
- 修复目标与要求：故障注入、幂等结果查询恢复、CAP 取消确认/重投、清理中状态；不提前释放活动资源。
- 验证方式：业务/出箱状态与次数、回滚/清理双失败原异常、最终恢复和受影响最新回归。

## 上轮具体问题的最新状态

| 项目 | 状态 | 当前证据和限制 |
| --- | --- | --- |
| FIX-014 请求审计捕获根 Scoped | RESOLVED（组件双框架） | InvokeAsync(HttpContext, IAuditingManager) 请求级注入；真实 Microsoft DI/AspectCore TestServer 验证共享 Current 和并发隔离。完整 Admin HTTP 仍需验收。 |
| FIX-015 查询秘密/用户租户缺失 | RESOLVED（组件双框架） | 默认只记录路径，查询白名单快照；错误日志不输出异常正文或 URL；只读已认证身份，提供四类 Claim 配置。两 Admin 声明配置的注册测试已通过。其他日志另见 FIX-019。 |
| FIX-016 排空 false 被忽略 | RESOLVED（组件双框架） | StopAsync 报告超时/取消和在途计数；net6 Host 聚合与 net8 直接传播均验证，活动资源保留直至请求结束。提前完成取消剩余 Delay/令牌注册。 |
| FIX-017 Worker 与重试计数 | RESOLVED（组件双框架） | 外层 finally 覆盖 scope 创建、解析、执行和异步释放；故障后归零及后续处理通过。RetryCount 仅确认提交后增加，Unknown 不增加，提交后清理失败仍增加一次并保留异常。 |

FIX-017 的具体缺陷关闭，不代表指标平台、生产故障恢复或容量验收完成；这些继续归 FIX-006/012/013。未新增持有插件类型的静态反射缓存。

## 需要优先确认和处理的风险

### FIX-018：MySQL 重复库存请求旧快照

- 严重程度：HIGH；原处理要求：MUST_FIX；状态：RESOLVED（具体缺陷）。
- 证据：修复前真实 MySQL RepeatableRead 测试返回 1062，基线 TRX 为 1 通过/1 失败。修复仅在唯一请求键冲突后使用 FOR UPDATE 当前读，初次查询保持普通读。
- 验证：修复后 net6/net8 定向各 2/2；最新 FreeSQL 全量各 78/78，包含两个隔离级别及不同扣减数量拒绝三个用例。
- 范围：同进程两个真实连接/交错事务。此证据证明重复请求结果恢复，不证明库存完整业务、双进程防超卖或整单重试；那些仍属 FIX-007。

### FIX-019：审计安全不覆盖其他日志与凭据

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 已修复：EFCore Admin HTTP 日志只采集方法/状态；旧完整请求采集默认关闭。两宿主 RabbitMQ 地址/账户/密码改由配置提供并预检；Dashboard 默认不注册。注册和安全配置直接测试已通过。
- 仍缺：真实 Admin 请求携带测试秘密后检查全部日志路径；显式启用 Dashboard/恢复诊断时的授权与租户限制；真实使用过的历史凭据需运维轮换。源码删除不撤销 Git 历史或已经泄露的值。
- 影响：安全审计组件不能约束其他业务/第三方日志；Dashboard 注册测试不验证访问授权。当前不能断言秘密已经泄漏，也不能声称全应用已脱敏。
- 修复目标与要求：完成实际输出和权限验收；部署提供受控密钥，按需轮换历史凭据。
- 验证方式：只使用测试秘密，不输出生产凭据。此风险的后续工作归 FIX-009/012。

### FIX-020：库存批次组合键碰撞

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 证据：InventoryBatchOperations.cs:38 用 TenantId + U+001F + RequestId 组成 HashSet 键；InventoryDeductionRequest.ValidateKey 只限制空白与长度，允许标识内部包含 U+001F。
- 复现：租户/请求二元组 ("a\\u001fb", "c") 与 ("a", "b\\u001fc") 不同，但组合字符串相同。本轮只读执行相同拼接算法确认碰撞，尚未添加或运行 C# 业务复现测试。
- 影响：合法的不同租户请求被误报为重复，批次已执行的前项还依赖调用方回滚。没有证据表明此路径会直接超卖。
- 修复目标与要求：使用结构化二元组及 Ordinal 比较去重，或无歧义编码；保持公开标识兼容，不临时禁止此前允许的字符。
- 验证方式：增加两个不同二元组但拼接相同的成功测试；真正同租户同请求仍拒绝；不同租户重用请求 ID、单项失败整批回滚继续直接验证。

## 欠缺考虑或尚未落实的 TODO

以下包含已有设计边界的落地缺口，不全部是新发现，也不重复计为功能包。

- [ ] **库存键语义**：固定事务引擎、大小写/尾空格/Unicode 规则；InventorySchema 没有明确 ENGINE/排序规则，应用 Ordinal 比较可能与 MySQL 唯一键语义不同。
- [ ] **组合标识无歧义**：先修 FIX-020 的分隔符碰撞；复核批次、缓存、幂等键在各层的结构化身份范围，不能通过拼接默认保证唯一。
- [ ] **整单生命周期**：整单幂等、失败后补货重试、取消/返还规则；批次抛异常必须回滚，不能提交已经扣减的前项。
- [ ] **消息流完整性**：过滤、起始序号、实体代次、重放、契约升级和毒消息策略；唯一事件 ID 的跨租户范围要明确，否则永久缺口或误去重。
- [ ] **保存点的非数据库状态**：数据库回滚不恢复 ORM 跟踪、变更报告、回调或 CAP 缓冲；失败标记提交不得 Flush 已回滚业务的消息。
- [ ] **重试与公平性**：CAP 重投和入箱重试总预算、慢实体对串行 Worker 的影响、有界并发、MySql.Data 分支直接证据。
- [ ] **可信边界**：消息发布者、租户归属、代理 ACL、状态/恢复授权及关键审计；格式正确不等于身份可信。
- [ ] **真正排空范围**：HTTP 闸门只等待下游管道，不能证明 OnCompleted、请求作用域异步清理、脱离请求的任务、SSE/WebSocket、CAP 和定时任务均结束；StopAsync 失败不能保证调用方不继续 Dispose 根容器。
- [ ] **审计故障边界**：普通保存链无取消令牌入口，慢存储可延长请求/停止；ThrowOnStoreFailure 发生在业务后，不能撤回已发响应或已提交业务；验证多重清理异常和 Unspecified 时间语义。
- [ ] **容量与留存**：收据覆盖允许重放期限；全局流数/总积压/索引/审计归档和权限；未处理消息不能按年龄直接删除。
- [ ] **部署迁移**：CREATE TABLE IF NOT EXISTS 不升级现有表；库存/审计/有序表需迁移版本、兼容回填、扩展—迁移—删除和蓝绿回滚策略。
- [ ] **Redis 运维**：故障切换/租期到期不保证独占；分桶公平、最大键数、蓝绿相同策略参数冲突及配置协调。
- [ ] **GC 与运行指标**：分页面数不是总积压；轮询 Gap/Failure 不是唯一失败实体数，重试尝试与成功不同；有界标签/告警节流和长期存活对象证据。
- [ ] **强停和外部副作用**：强杀/断电无托管清理保证；Unknown 查询幂等结果；HTTP/邮件等副作用使用幂等键或后续出箱。
- [ ] **配置迁移与旧消费者**：两 Admin 现在缺少必需 RabbitMQ 配置会注册失败；验证实际部署的密钥注入及滚动升级。EntityChangeInfo.ChangeType 类型调整需最小旧消费者与发布说明，类型转发不等于所有旧二进制兼容。

## 证据与文档验收 TODO

本轮只核对已有记录，不重新执行测试。独立 XML 核查按 UnitTestResult 统计，以下是对应最新修改后的验证范围：

| 范围 | 实际结果 | 证据与限制 |
| --- | --- | --- |
| EventBus 全量 | net6/net8 各 164 通过、0 失败/跳过 | artifacts/cluster-latest-review-fix/eventbus-full-net6.trx、eventbus-full-net8.trx，包含 Worker 和重试计数故障路径 |
| MVC 全量 | net6/net8 各 99 通过、1 NotExecuted、0 失败 | 同目录 mvc-full-net6.trx、mvc-full-net8.trx；UploadRawAsync 跳过，Counters.notExecuted 为 0 不改变实际跳过事实 |
| FreeSQL 全量（MySQL 门控开启） | net6/net8 各 78 通过、0 失败/跳过 | 同目录 freesql-full-mysql-net6.0.trx、freesql-full-mysql-net8.0.trx；含全部三个库存旧快照用例和有序双进程用例。审计事务相关用例仍是 SQLite 范围 |
| Admin 注册定向 | EFCore 16/16；FreeSQL 15/15 | 同目录 admin-ef-registration-net6-final.trx、admin-free-registration-net6.trx；含 Claim、安全日志、Rabbit 配置和 Dashboard 默认状态，不建立真实生产宿主请求 |
| Admin 本地全量 | EFCore 24 通过、16 跳过；FreeSQL 43 通过、15 跳过；0 失败 | 同目录 admin-ef-full-local-net6.trx、admin-free-full-local-net6.trx；跳过的 MySQL/CAP 场景不能计为本批验证通过 |
| Sample Lazy 冒烟 | net6 1/1 | 同目录 sample-lazy-net6.trx，实际 Lazy 业务解析和正常 Host 停止；不代表集群部署验收 |
| 审计/排空定向 | net6/net8 各 24/24 | artifacts/http-audit-review-fix/http-audit-drain-net6-verified.trx、http-audit-drain-net8-final.trx |
| 库存旧快照复现与修复 | 修复前 1 通过/1 失败；修复后双框架定向各 2/2 | artifacts/inventory-snapshot-review-fix/，新增第三个身份冲突用例在 FreeSQL 全量通过 |

早期 HTTP net8 和 EFCore Dashboard 注册的失败文件仍保留；最终测试修正了运行时异常包装/注册描述符判断，未删掉超时、活动资源或 Dashboard 默认关闭的业务断言。

- 历史 artifacts/audit-persistence/：审计定向各 19、全量各 150；EF 事务 4、全量 87；FreeSQL 事务各 4。这些属于 SQLite 存储/事务适配，不证明完整 MySQL 库存/审计/CAP 业务。
- 历史真实 Redis 双 DI 容器各 9/9、真实 CAP EF 16/16 和 FreeSQL 15/15 保留为对应批次证据；最新 Admin 本地全量跳过外部场景，不等于历史测试不存在，也不等于升级后的生产 CapModule 已重新连接验证。

- [ ] execution.md 追加持久化审计及最新 FIX-014～019 的结果、失败基线和实际验收范围；开头完成表已更新，但后文仍有“下一阶段进入有序消费”“审计持久化 OPEN_ACTIONABLE”等陈旧段落。
- [ ] 修正 test-mapping.md 的 AuditLogSchema 未知数据库方法名：当前写 GetCreateStatements_WhenUnsupported_ShouldThrow，真实方法为 GetCreateStatements_WhenDatabaseIsUnknown_ShouldThrow。其余新增映射已补，不再整体列为缺失。
- [ ] 新构建依赖/配置迁移后的真实两 Admin HTTP、CAP 和安全输出验收；锁定 SDK、动态双平台及持续负载仍按未完成处理。
- [x] README/集群配置指南已说明请求级注入、查询白名单/Claim 配置、StopAsync 新异常和根容器约束；当前指南不再把已实现审计/原子工具列为完全缺失。
- [ ] 锁定 SDK 8.0.424 验证。本轮 dotnet --version 为 8.0.425，global.json 仍为 8.0.424；不修改锁定文件，不把环境差异记成代码失败。

## 建议推进顺序

1. 修 FIX-020，收口 FIX-019 的真实日志/授权验收，补准确执行记录和映射；不重复把 FIX-014～018 列为未实现。
2. FIX-007/009 的库存、幂等、关键审计、CAP 完整事务及 MySQL 双进程验收。
3. FIX-006 生产有序恢复授权/审计；FIX-008/013 的跨进程、断连、Unknown 与取消矩阵。
4. FIX-011 角色开关、依赖预热、消费/任务排空与双平台动态演练。
5. FIX-012 长期指标、留存、迁移、GC/容量和文档回归收口；不因环境缺失扩展无关重构。
