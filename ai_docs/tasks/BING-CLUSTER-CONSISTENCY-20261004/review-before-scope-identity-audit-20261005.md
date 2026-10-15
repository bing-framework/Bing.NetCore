<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T04:43:34Z

# 集群能力与计划差距审查

## 范围和结论

依据用户批准的完整 P0–P4 计划、plan.md、execution.md、当前暂存/未暂存改动、未跟踪源码、测试、部署配置和真实 TRX 检查。本轮不修改生产代码或单元测试，只生成审查与诊断证据。旧审查保存于 review-before-inventory-current-audit-20261005.md。

仍有 **7 组未完整交付的原计划工作包**：FIX-006、007、008、009、011、012、013。多数已有组件或业务链路，缺生产接入、边界实现或完整验收；不代表七项功能完全不存在，不用测试总数推算完成百分比。

新增两个具体代码问题：FIX-021 单例订单存储捕获可替换 Scoped 依赖；FIX-022 默认限流主体键内部边界碰撞。它们分别归入 FIX-007、008，不重复计为新的原计划能力包。

上一份审查中 FIX-007/009 的“未见完整库存业务”已经过时：新增 AdminInventoryService 在两套 ORM 的真实工作单元中组合整单结果、库存、关键审计与 CAP 出箱；已有对应真实 MySQL/RabbitMQ 定向证据。新增 FindAsync 也不再是完全缺失，但不等于 Unknown 故障恢复验收完成。

## 阶段验收矩阵

| 阶段 | 结论 | 已实现 | 主要差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（核心历史证据） | SemaphoreSlim 本地锁、所有者校验、引用计数回收、原子 Redis 获取/释放、完整租期、本地事件快照 | 故障切换/长期负载属于后续运行验收；本轮未重跑真实 Redis |
| P1 事件与事务 | PARTIAL | CAP 适配、可选元数据、两 ORM 领域事件轮次、同事务消费收据、提交结果诊断、CAP 唯一关闭任务 | 生产恢复策略、真实取消确认/重投、提交确认丢失与外部副作用恢复 |
| P2 顺序与库存 | PARTIAL | 有序入箱/游标/序号/保存点失败标记、后台处理、ORM 工厂；整单幂等/首次结果/固定 SKU 顺序/不足回滚；Admin 库存+审计+CAP 业务 | FIX-021；库存双进程、最新同进程竞争用例、整单死锁重试/Unknown 恢复；有序生产接线/授权审计 |
| P3 限流、审计、原子工具 | PARTIAL | 本地/Redis 令牌桶、可信身份/稳定路由、429/503、HTTP 审计、数据库安全投影、事务审计、实体字段填充、CAS HTTP 闸门 | FIX-022；完整 Admin HTTP 与 Redis 故障；自动属性变更快照、普通审计生产落库、持续运行证据 |
| P4 蓝绿与诊断 | PARTIAL | Compose/Nginx、Kubernetes 模板、切流脚本、HTTP 就绪/排空、有序进程内指标 | 宿主角色实际分离、依赖预热、消费/任务排空、双平台动态切换/回滚、指标导出及归档 |

本地事件不跨节点、CAP 至少一次投递、Redis 锁不能替代数据库正确性、强杀不保证托管释放，是现有边界，不列为“尚未实现跨节点本地事件/恰好一次投递”。

## 原计划剩余 TODO

### FIX-006：有序事件生产接入和恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：模块组件、两 ORM CAP 定向及 MySQL 有序双进程证据已有；当前两个 Admin 源码仍未注册 AddBingOrderedConsumer。RetryAsync 的授权器是可选扩展；GetStateAsync 本身没有宿主租户权限策略。
- 影响：组件通过不能证明生产订阅等待入箱，或人工恢复受授权和关键审计约束。
- 修复目标/要求：显式生产订阅和 Worker 接线；等待持久化后返回；查询、重试、补偿授权及审计；派生 CAP 出箱绑定当前业务事务；失败丢弃 ORM/作用域状态。
- 验证方式：真实宿主端到端、前序失败竞争、派生消息回滚、慢处理器停止、取消确认/重投；不自动跳过缺失序号。
- [ ] 完成生产接线和受控恢复入口。
- [ ] 验证保存点回滚后 ORM 跟踪、CAP 缓冲及事务回调不会再次提交已回滚业务。

### FIX-007：整单库存并发与恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 已有证据：InventoryOrderRequest/Schema/Store、FindAsync 和 AdminInventoryService 已实现；Inventory:Enabled 显式启用两 Admin 业务。历史真实 MySQL/RabbitMQ 两套 ORM 各 4/4，覆盖整单重复、后项不足、关键审计失败、实际出箱后取消。
- 剩余影响：这些通过不能证明跨进程竞争、提交中断恢复或最新竞争用例通过；默认 DI 可替换 Scoped 路径存在 FIX-021。
- 修复目标/要求：修复作用域；仅确认回滚且清理完成后做有界整笔死锁重试；Unknown 用原身份查询并确认原操作结束，不换订单 ID 盲目重做。
- 验证方式：两 ORM MySQL 同进程独立作用域及双进程竞争；重复、余额不足、多 SKU 原子性、取消重试、提交前后中断；同时断言库存、整单结果、审计及出箱次数。
- [ ] 执行已新增的 ScopesCompeteForSameOrder / DifferentOrdersCompete 两项真实测试；本轮只编译，旧 4/4 TRX 不包含它们。
- [ ] 补库存真实双进程竞争及故障恢复；有序事件双进程测试不能代替库存业务。
- [ ] 补整单重试/Unknown 恢复策略和部署迁移、调用示例。

### FIX-008：限流隔离、宿主与 Redis 故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：令牌桶、配置接线、可信代理/身份组件、429/503、降级已有；Redis IndependentProviders 测试仍为同进程两 DI 容器。默认 tenant/user 拼接存在 FIX-022。
- 影响：不能证明完整 Admin JWT/代理链路、跨进程共享配额或断连后恢复符合策略。
- 修复目标/要求：结构化主体键；真实 Admin HTTP 和 Redis 跨进程、断连/恢复验证；保持故障默认拒绝且显式降级。
- 验证方式：分隔符身份、租户/路由、伪造代理头、Retry-After、取消、额度恢复、键回收与同名策略冲突。
- [ ] 修 FIX-022 并增加默认 Claims 路径直接回归。
- [ ] 完成双进程配额、Redis 故障恢复及完整 Admin HTTP 验收。

### FIX-009：自动审计与生产存储

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 已有证据：HTTP 采集、请求级作用域、白名单和安全投影、DbAuditingStore、两 ORM 事务适配、Admin 整单关键审计已实现，关键审计失败导致整单回滚已有真实环境测试。
- 剩余问题：EntityChanges/PropertyChanges 仍由业务填写；EF ChangeTracker/FreeSQL 属性级自动快照未实现。普通 HTTP 审计与关键事务审计是不同服务契约，AddAdminInventory 不会将普通 IAuditingStore 改为可靠数据库存储。
- 影响：不能宣称完整自动变更审计或所有请求已可靠落库；HTTP 保存失败不能撤销已提交业务/已发送响应。
- 修复目标/要求：可选自动快照、普通审计生产落库配置、实际输出脱敏和权限验收；保留业务原异常并汇总保存与释放错误。
- 验证方式：属性新增/修改/删除、安全字段白名单、敏感值、普通与关键故障策略、多重清理异常、真实宿主权限；关键库存组合不重复从零实现。
- [ ] 自动实体变更快照及普通审计生产存储。
- [ ] 完成 FIX-019 的全日志测试秘密检查、Dashboard/恢复授权与租户隔离。
- [ ] 明确 EntityChangeInfo.ChangeType 类型调整的旧编译消费者与发布迁移边界。

### FIX-011：蓝绿角色、预热和动态验收

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：两个 Startup 仍无条件 AddModule<CapModule>；BING_HOST_ROLE/BING_CAP_CONSUMER_ENABLED 仅在模板和文档出现，未被启动流程消费。AppModule 末尾 MarkBingTrafficReady 不等待业务依赖预热。Test-BlueGreen.ps1 的 Live 只检查就绪/选择器，末尾明确动态演练仍待执行。
- 影响：目前模板不能保证绿版预热不消费；仅 HTTP 切流不能完成消费者、定时任务或事务排空。
- 修复目标/要求：Web/消费者/定时任务角色分离，Web 可出箱但不订阅，稳定逻辑组；依赖就绪和预热后放流；排空超时报告未完成，不提前释放资源。
- 验证方式：Compose/Nginx 与 Kubernetes 分别验证长请求、活动消费、绿版不就绪、切流、排空、回滚和强停恢复。
- [ ] 让角色配置真正影响生产注册和启动。
- [ ] 增加依赖预热、消费者/任务排空及双平台动态验收。

### FIX-012：观测、容量、归档和交付

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：Worker.RecordPendingStreams(streams.Count) 是当前页流数，尚非全局积压；指标只有进程内快照，缺年龄/导出/容量留存。execution.md 和指南仍称完整库存业务缺失；test-mapping.md 未映射新订单符号，AuditLogSchema 的未知数据库方法名也错误。
- 影响：无法评估长期内存/磁盘和真实积压；测试数量与当前能力文案不一致。
- 修复目标/要求：总积压、最老年龄、有限标签指标导出/授权诊断/告警节流；持续 GC/分配/延迟与存活对象证据；归档/清理及新增业务迁移文档。
- 验证方式：持续负载、恢复追赶、静态/后台引用回收、收据保留和未处理消息保护；无容量预算时报告测量，不自定 MB/QPS 门槛。
- [ ] 指标导出、总积压/年龄、审计/幂等/消息归档。
- [ ] 长期分配、GC、存活对象和负载测量；旧模块资源探针不能代替本次集群组件。
- [ ] 更新 execution、README/指南、生产符号到测试方法映射和门控最低数量/方法清单，覆盖新库存用例。

### FIX-013：Unknown、真实断连和清理矩阵

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 已有证据：提交状态诊断、ORM 异常清理、CAP 生命周期修复以及整单结果查询。SIGNAL 原生错误形状测试不等于网络断连；正常 FindAsync 测试不等于真实提交确认丢失恢复。
- 影响：异常或取消不能证明未提交；查询 null 也不能证明旧操作回滚或结束。
- 修复目标/要求：实际提交确认丢失、提交后 Flush/Dispose 失败、原结果查询恢复、CAP 取消确认与重投；保留原始和清理异常，不提前释放活动资源。
- 验证方式：断连/中断注入，核对业务/审计/出箱及次数；回滚与释放同时失败；旧操作结束后恢复，等待取消与真正清理完成分开。
- [ ] 完成真实故障矩阵及恢复示例。

## 新发现的具体问题

### FIX-021：订单单例捕获可替换 Scoped 库存存储

- 严重程度：HIGH；处理要求：MUST_FIX；状态：OPEN_ACTIONABLE；归属 FIX-007。
- 证据：InventoryServiceCollectionExtensions.cs:42–43 的 TryAddSingleton<IInventoryOrderStore> 工厂解析 IInventoryStore；TryAdd 保留调用方预先注册的 Scoped 实现。
- 直接复现：artifacts/cluster-capability-audit-20261005/Test-InventoryScope.ps1 使用本轮编译的程序集和真实 Microsoft DI；在 CreateScope 内解析订单服务，ValidateScopes=true 抛 Cannot resolve scoped service 'Bing.Data.Inventory.IInventoryStore' from root provider。完整日志同目录 inventory-scope-reproduction.log。
- 影响：即使在子作用域中解析，单例工厂仍使用根容器。关闭校验会把 Scoped 依赖提升到根生命周期并跨请求共享；默认无状态存储路径不受影响，不能因此忽略自定义契约。
- 修复目标/要求：订单存储按 Scoped 注册，保留已有自定义注册；或明确限制并拒绝不兼容生命周期，不能静默捕获根依赖。
- 验证方式：ValidateScopes 下成功解析、不同作用域依赖隔离、释放次数、重复注册与 Admin 调用回归；当前自定义测试只覆盖 Singleton，尚未覆盖此场景。
- [ ] 修复并补直接生命周期测试。

### FIX-022：默认限流主体的租户/用户边界碰撞

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE；归属 FIX-008。
- 证据：BingRateLimitMiddleware.cs:59 使用 tenant:{tenantId}|user:{userId}；tenant=a|user:b,user=c 与 tenant=a,user=b|user:c 都得到 tenant:a|user:b|user:c。本轮只读算法复现确认相等，独立 worker 复核源码与现有测试。
- 影响：相同策略/路由下两个不同身份共享额度，破坏租户分桶隔离。外层 CreateBucketKey 的 subject 长度编码或 SHA-256 不恢复已丢失的内部分界。没有证明此路径可直接绕过配额或授权。
- 修复目标/要求：对身份种类、租户、用户/IP 分别结构化/长度编码；保持已认证身份规则。不要临时禁止之前允许的字符。
- 验证方式：真实中间件 Claims 路径两个碰撞身份的桶不同，普通同身份桶相同，自定义键及路由回归；现有 SeparatorInCustomKey 用例不覆盖默认主体。
- [ ] 修复并补默认主体边界测试。

## 已闭环内容与不能重复列为缺失的项

- FIX-010：用途驱动 CAS 闸门/Interlocked 计数已有，长期负载归 FIX-012。
- FIX-014～018：请求级审计注入、可信元数据、排空失败报告、Worker 清理计数与提交后重试计数、MySQL 唯一冲突当前读已有修复和对应批次证据。
- FIX-019：默认日志/凭据/Dashboard 注册已修；实际输出和访问权限仍 PARTIAL，归 FIX-009/012。
- FIX-020：已改结构化二元组+Ordinal 去重。本轮双框架定向直接通过分隔符不同身份、真正重复和不足回滚三个回归，具体碰撞问题 RESOLVED；不等于完成 FIX-007 整单业务验收。

## 欠缺考虑或尚未充分落地的 TODO

以下区分潜在边界和已复现问题，不全部宣称为漏洞，不重复计入七组工作包。

- [ ] **全链路身份规则**：整单表使用 UTF-8 二进制身份，但旧 stock/request 表仍 VARCHAR 且未固定 ENGINE/排序规则。需统一大小写、尾空格、Unicode/字节上限及租户唯一范围，并用真实 MySQL 验证，不能把整单表二进制测试扩大为库存键一致。
- [ ] **生命周期替换契约**：注册工厂的 Singleton/Scoped/Transient 组合、调用方对象所有权及跨作用域状态；将 FIX-021 纳入常规扩展契约测试。
- [ ] **整单长期语义**：不足结果已持久化，补货后重复订单仍返回首次不足；定义新尝试、撤销/返还/部分发货、请求内容冲突与内部 order- 请求 ID 命名空间。不能通过换 ID 处理 Unknown。
- [ ] **序号和流代次**：订阅可见完整流、起始序号、实体删除重建、消息过滤、版本迁移/重放、毒消息补偿；未见前序不能靠超时跳过。
- [ ] **事务外内存状态**：保存点不恢复 ORM 跟踪、FreeSQL 变更报告、CAP 内存缓冲或登记回调；仅失败标记提交不得发送已回滚业务消息。
- [ ] **公平与总预算**：当前 Worker 按流串行，慢实体会阻塞后续实体；增加保留实体顺序的有界并发、CAP/入箱/整单重试总预算与背压，不使用无界队列。
- [ ] **真实排空覆盖**：请求作用域异步清理、OnCompleted、脱离请求任务、SSE/WebSocket、CAP 和定时任务分别纳入；Host.StopAsync 报错不能阻止外部调用方继续 Dispose 根容器。
- [ ] **安全运维入口**：消息发布者/租户归属、代理 ACL、恢复/审计查询与 Dashboard 权限；用测试秘密验证全部日志，实际使用过的历史凭据由运维轮换。
- [ ] **审计故障时限**：IAuditingStore 普通路径缺调用方取消令牌；慢保存可能延长请求和停止。多重保存/释放失败需保留完整诊断，关键审计故障回滚与普通业务后告警不能混用。
- [ ] **数据库驱动清理**：新库存路径多处 using DbCommand/Reader；若业务异常与 Dispose 同时抛错，需直接验证原异常保留与后续清理，不用普通成功释放推断。
- [ ] **限流容量与配置**：MaxKeyLength 允许很小值，摘要截断可能破坏身份隔离；Redis 单策略 Hash 达上限时 HGETALL 扫描开销、热键、公平性及蓝绿策略参数冲突需测量与配置约束。
- [ ] **容量与合规**：业务收据保留覆盖重放期限；未处理事件不可按年龄删除；审计访问、归档、磁盘预算及敏感值删除规则。
- [ ] **部署迁移**：CREATE TABLE IF NOT EXISTS 不升级旧表；库存/整单/审计/有序表需版本化迁移、索引/排序规则回填与扩展—迁移—删除；滚动期间契约兼容。
- [ ] **强停与外部副作用**：强杀/断电依靠事务、出箱及收据恢复；HTTP/邮件等须幂等或后续出箱，不能由租约或托管 Dispose 保证。

## 本轮测试和证据边界

本轮实际重新构建库存定向及两 Admin 注册测试，再用各框架刚编译的程序集运行 EventBus 全量。未重跑 MySQL/RabbitMQ、Redis、MVC、Sample 或双平台部署。只读诊断不计为 xUnit 通过项。

| 本轮实际执行 | 通过 | 失败/跳过 | 证据 |
| --- | --- | --- | --- |
| 库存定向 net6.0 | 25 | 0/0 | artifacts/cluster-capability-audit-20261005/inventory-current-net6.trx |
| 库存定向 net8.0 | 25 | 0/0 | 同目录 inventory-current-net8.trx |
| EventBus 全量 net6.0 | 181 | 0/0 | 同目录 eventbus-current-net6.trx |
| EventBus 全量 net8.0 | 181 | 0/0 | 同目录 eventbus-current-net8.trx |
| Admin EFCore CAP 注册定向 net6.0 | 12 | 0/0 | 同目录 admin-ef-registration-current-net6.trx |
| Admin FreeSQL CAP 注册定向 net6.0 | 12 | 0/0 | 同目录 admin-free-registration-current-net6.trx |

各组按实际 UnitTestResult/outcome 计数；定向包含在全量中，不能相加当独立覆盖数量。两 Admin 注册通过证明当前工程可编译和注册规则，不证明真实消息消费/HTTP/数据库行为。

历史 artifacts/inventory-orders：库存双框架各 24/24、Admin EFCore/FreeSQL 真实 MySQL/RabbitMQ 各 4/4。新 FindAsync、审计实体记录、消息 Lines 和两个竞争方法在历史 TRX 后新增；本轮只有相应编译及库存 FindAsync 直接测试，不把历史四项当作最新全部业务验证。

历史 cluster-latest-review-fix：EventBus 各 164/164、MVC 各 99 通过+1 NotExecuted、FreeSQL MySQL 门控各 78/78、Sample Lazy 1/1；Admin 本地 EF 24通过+16跳过、Free 43通过+15跳过。更早真实 CAP 与 Redis 记录保留，但不是本轮执行。旧失败 TRX 保留，不删除失败复现。

- [ ] 库存新增竞争/最新真实链路与 CI 全组回归，分别记录外部场景和跳过。
- [ ] 最新改动后 MVC/FreeSQL/审计/Sample 受影响回归与必要构建，按范围执行，不重复扩大无关测试。
- [ ] 锁定 SDK 8.0.424 验证；本轮实际 8.0.425，global.json 未变。
- [ ] NU1900 表示在线漏洞元数据获取失败；依赖漏洞扫描仍待网络恢复，不能据此声称安全检查通过。

## 建议推进顺序

1. 修 FIX-021/022，固定身份和生命周期回归，更新当前业务/测试文档。
2. FIX-007/009：两 ORM 库存竞争和完整业务最新回归，补双进程、整单重试/Unknown；自动变更审计。
3. FIX-006/008/013：生产有序授权恢复、跨进程限流、真实断连和 CAP 取消语义。
4. FIX-011：角色开关、依赖预热、消费/任务排空与双平台动态发布。
5. FIX-012：指标导出、容量/归档、持续资源测量与交付收口。

保持 NEEDS_FIX。未修改生产/测试逻辑、目标框架、依赖版本、global.json；未提交、推送或发送外部通知。
