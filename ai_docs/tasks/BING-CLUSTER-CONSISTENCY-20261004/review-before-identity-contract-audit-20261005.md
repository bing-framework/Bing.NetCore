<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T05:20:48Z

# 集群能力与计划差距审查

## 范围和结论

依据用户批准的完整 P0–P4 计划、plan.md、execution.md、当前暂存/未暂存改动、未跟踪源码、测试、部署配置和真实 TRX 检查。本轮不修改生产代码或单元测试，不重新运行测试，只更新审查及有界算法诊断证据。前一份审查保存于 review-before-scope-identity-audit-20261005.md；更早审查也保留。

仍有 **7 组未完整交付的原计划工作包**：FIX-006、007、008、009、011、012、013。多数已有组件或业务链路，缺生产接入、边界实现或完整验收；不代表七项功能完全不存在，不用测试总数推算完成百分比。

FIX-021 订单存储捕获 Scoped 依赖和 FIX-022 默认租户/用户键边界碰撞已修复，有修复前失败及修复后直接测试。新增 FIX-023：MaxKeyLength 允许极短值，摘要截断后不同身份仍可能共享额度；归入 FIX-008，不额外计作一组计划能力。

AdminInventoryService 已在两套 ORM 的真实工作单元中组合整单结果、库存、关键审计与 CAP 出箱。最新真实 MySQL/RabbitMQ 库存定向各 6/6，包含同进程独立作用域竞争；完整 Admin 门控 EFCore 22/22、FreeSQL 21/21。FindAsync、迁移指南和新增生产符号映射已有，仍不等于库存双进程或 Unknown 故障恢复验收完成。

## 阶段验收矩阵

| 阶段 | 结论 | 已实现 | 主要差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（核心历史证据） | SemaphoreSlim 本地锁、所有者校验、引用计数回收、原子 Redis 获取/释放、完整租期、本地事件快照 | 故障切换/长期负载属于后续运行验收；本轮未重跑真实 Redis |
| P1 事件与事务 | PARTIAL | CAP 适配、可选元数据、两 ORM 领域事件轮次、同事务消费收据、提交结果诊断、CAP 唯一关闭任务 | 生产恢复策略、真实取消确认/重投、提交确认丢失与外部副作用恢复 |
| P2 顺序与库存 | PARTIAL | 有序入箱/游标/序号/保存点失败标记、后台处理、ORM 工厂；整单幂等/首次结果/固定 SKU 顺序/不足回滚；Admin 库存+审计+CAP 与作用域竞争 | 库存双进程、整单死锁重试/Unknown 恢复；有序生产接线/授权审计 |
| P3 限流、审计、原子工具 | PARTIAL | 本地/Redis 令牌桶、可信身份/稳定路由/结构化主体、429/503、HTTP 审计、数据库安全投影、事务审计、实体字段填充、CAS HTTP 闸门 | FIX-023；完整 Admin HTTP 与 Redis 故障；自动属性变更快照、普通审计生产落库、持续运行证据 |
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
- 已有证据：InventoryOrderRequest/Schema/Store、FindAsync 和 AdminInventoryService 已实现；Inventory:Enabled 显式启用两 Admin 业务。最新真实 MySQL/RabbitMQ 两套 ORM 各 6/6，覆盖整单重复、两种作用域竞争、后项不足、关键审计失败、实际出箱后取消。FIX-021 已解决。
- 剩余影响：同进程作用域竞争不能证明跨进程竞争或提交中断恢复；整单业务没有自动死锁重试。
- 修复目标/要求：保持已修正的作用域规则；仅确认回滚且清理完成后做有界整笔死锁重试；Unknown 用原身份查询并确认原操作结束，不换订单 ID 盲目重做。
- 验证方式：两 ORM MySQL 同进程独立作用域及双进程竞争；重复、余额不足、多 SKU 原子性、取消重试、提交前后中断；同时断言库存、整单结果、审计及出箱次数。
- [x] ScopesCompeteForSameOrder / DifferentOrdersCompete 两项已包含在最新真实测试，不能再列为未执行。
- [ ] 补库存真实双进程竞争及故障恢复；有序事件双进程测试不能代替库存业务。
- [ ] 补整单死锁重试及 Unknown 恢复策略；调用/查询指南已有，仍需版本化数据库迁移与故障恢复验收。

### FIX-008：限流隔离、宿主与 Redis 故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：令牌桶、配置接线、可信代理/身份组件、429/503、降级及 FIX-022 结构化身份修复已有；Redis IndependentProviders 测试仍为同进程两 DI 容器。MaxKeyLength 过小的问题见 FIX-023。
- 影响：不能证明完整 Admin JWT/代理链路、跨进程共享配额或断连后恢复符合策略。
- 修复目标/要求：限制不安全摘要截断；真实 Admin HTTP 和 Redis 跨进程、断连/恢复验证；保持故障默认拒绝且显式降级。
- 验证方式：分隔符身份、租户/路由、伪造代理头、Retry-After、取消、额度恢复、键回收与同名策略冲突。
- [x] FIX-022 已修并有默认 Claims、同身份、长身份摘要回归。
- [ ] 修 FIX-023；新增安全长度边界、不同身份与配置错误直接测试。
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
- 问题/证据：Worker.RecordPendingStreams(streams.Count) 是当前页流数，尚非全局积压；指标只有进程内快照，缺年龄/导出/容量留存。新库存指南、生产符号映射和 CI 22/21 门槛已更新；execution.md 开头已更新，但末尾尚未补齐最新 FIX-021/022 和完整门控结果。
- 影响：无法评估长期内存/磁盘和真实积压；测试数量与当前能力文案不一致。
- 修复目标/要求：总积压、最老年龄、有限标签指标导出/授权诊断/告警节流；持续 GC/分配/延迟与存活对象证据；归档/清理及新增业务迁移文档。
- 验证方式：持续负载、恢复追赶、静态/后台引用回收、收据保留和未处理消息保护；无容量预算时报告测量，不自定 MB/QPS 门槛。
- [ ] 指标导出、总积压/年龄、审计/幂等/消息归档。
- [ ] 长期分配、GC、存活对象和负载测量；旧模块资源探针不能代替本次集群组件。
- [x] 新库存 README/指南、生产符号映射、CI 最低数量更新已有；本轮核对实际新用例证据。
- [ ] 将最新 FIX-021/022、双框架回归及 22/21 门控结果补齐到 execution.md，并明确历史/当前、失败复现与最终结果；后续门控宜核验必需方法集合，不能只依赖数量。

### FIX-013：Unknown、真实断连和清理矩阵

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 已有证据：提交状态诊断、ORM 异常清理、CAP 生命周期修复以及整单结果查询。SIGNAL 原生错误形状测试不等于网络断连；正常 FindAsync 测试不等于真实提交确认丢失恢复。
- 影响：异常或取消不能证明未提交；查询 null 也不能证明旧操作回滚或结束。
- 修复目标/要求：实际提交确认丢失、提交后 Flush/Dispose 失败、原结果查询恢复、CAP 取消确认与重投；保留原始和清理异常，不提前释放活动资源。
- 验证方式：断连/中断注入，核对业务/审计/出箱及次数；回滚与释放同时失败；旧操作结束后恢复，等待取消与真正清理完成分开。
- [ ] 完成真实故障矩阵及恢复示例。

## 上轮具体问题的修复核查

### FIX-021：订单单例捕获可替换 Scoped 库存存储

- 原严重程度：HIGH；处理要求：MUST_FIX；状态：RESOLVED；归属 FIX-007。
- 当前证据：InventoryServiceCollectionExtensions.cs:42 改为 TryAddScoped<IInventoryOrderStore>，从当前作用域解析 IInventoryStore，并保留已有注册。
- 直接复现：artifacts/cluster-capability-audit-20261005/Test-InventoryScope.ps1 使用本轮编译的程序集和真实 Microsoft DI；在 CreateScope 内解析订单服务，ValidateScopes=true 抛 Cannot resolve scoped service 'Bing.Data.Inventory.IInventoryStore' from root provider。完整日志同目录 inventory-scope-reproduction.log。
- 原影响：单例工厂会捕获根作用域 Scoped 依赖，现默认路径已消除。
- 验证：AddBingInventoryOrders_WithScopedInventoryStore_ShouldIsolateScopesAndDisposeOnce 使用真实 Microsoft DI、ValidateScopes/ValidateOnBuild、SQLite、两个异步作用域，断言实例隔离及释放一次。修复前 inventory-scope-before-net6.trx 为 0/1；修复后库存双框架各 26/26。
- [x] 修复、直接生命周期测试及真实 Admin 调用回归已有；调用方自行注册错误单例仍属其扩展契约责任。

### FIX-022：默认限流主体的租户/用户边界碰撞

- 原严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：RESOLVED；归属 FIX-008。
- 当前证据：默认带租户身份使用 EncodeIdentityPart 对租户和 user/IP 身份分别添加长度前缀；外层主体编码及摘要仍存在。
- 验证：InvokeAsync_TenantAndUserSeparators_ShouldKeepBucketsSeparate、InvokeAsync_SameTenantAndUser_ShouldReuseBucket、InvokeAsync_LongTenantAndUserClaims_ShouldKeepDigestBucketsSeparate 覆盖默认 Claims 路径。修复前 identity-before-net6.trx 为 2通过/1失败；修复后中间件与真实 HTTP 管道双框架各 20/20。
- 迁移：旧/新带租户键不能共享额度，新桶获得初始额度；cluster-consistency.md 已说明需协调发布。没有自动迁移剩余额度，不把此项当作蓝绿完整验收。
- [x] 当前身份边界修复及直接回归已有。

## 新增可执行代码问题

### FIX-023：限流摘要长度可配置得过短

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE；归属 FIX-008。
- 问题/证据：BingRateLimitMiddleware 构造函数只要求 MaxKeyLength > 0；CreateBucketKey 在配置 <= 7 时直接截断 SHA-256 十六进制文本。配置 1 时只保留 16 种可能值。默认策略/未知路由的 user:1 和 user:4 都得到键 7。
- 直接证据：artifacts/cluster-current-audit-20261005/short-rate-key-reproduction.json 按当前源码算法计算；本轮未执行运行期中间件探针。既有长度测试配置为 32，不覆盖极小值。默认 256 不触发此风险。
- 影响：显式极短配置会使不同身份共享额度；Redis 对截断后的键再哈希也不能恢复区别。不是授权绕过结论。
- 修复目标/要求：拒绝不安全长度，或将完整内部身份摘要与可展示长度分开；保持合理配置的兼容性，给出清晰启动诊断。
- 验证方式：零/负值、极小值、边界值、合理超长身份、两种限流器及不同身份直接测试；同步补迁移说明。
- [ ] 修复配置约束及直接回归。

## 已闭环内容与不能重复列为缺失的项

- FIX-010：用途驱动 CAS 闸门/Interlocked 计数已有，长期负载归 FIX-012。
- FIX-014～018：请求级审计注入、可信元数据、排空失败报告、Worker 清理计数与提交后重试计数、MySQL 唯一冲突当前读已有修复和对应批次证据。
- FIX-019：默认日志/凭据/Dashboard 注册已修；实际输出和访问权限仍 PARTIAL，归 FIX-009/012。
- FIX-020：已改结构化二元组+Ordinal 去重。此前双框架定向及最新库存回归包含分隔符不同身份、真正重复和不足回滚，具体碰撞问题 RESOLVED；不等于完成 FIX-007 整单业务验收。

## 欠缺考虑或尚未充分落地的 TODO

以下区分潜在边界和已复现问题，不全部宣称为漏洞，不重复计入七组工作包。

- [ ] **全链路身份规则**：整单表使用 UTF-8 二进制身份，但旧 stock/request 表仍 VARCHAR 且未固定 ENGINE/排序规则。需统一大小写、尾空格、Unicode/字节上限及租户唯一范围，并用真实 MySQL 验证，不能把整单表二进制测试扩大为库存键一致。
- [ ] **生命周期替换契约**：FIX-021 已纳入直接测试；继续覆盖其他可替换注册的 Singleton/Scoped/Transient 组合、调用方对象所有权及跨作用域状态。
- [ ] **整单长期语义**：指南已有首次不足、新业务尝试及内部 order- 保留身份说明；撤销/返还/预占/部分发货需要独立业务规则，不是本期必需缺陷。不能通过换 ID 处理 Unknown。
- [ ] **序号和流代次**：订阅可见完整流、起始序号、实体删除重建、消息过滤、版本迁移/重放、毒消息补偿；未见前序不能靠超时跳过。
- [ ] **事务外内存状态**：保存点不恢复 ORM 跟踪、FreeSQL 变更报告、CAP 内存缓冲或登记回调；仅失败标记提交不得发送已回滚业务消息。
- [ ] **公平与总预算**：当前 Worker 按流串行，慢实体会阻塞后续实体；增加保留实体顺序的有界并发、CAP/入箱/整单重试总预算与背压，不使用无界队列。
- [ ] **真实排空覆盖**：请求作用域异步清理、OnCompleted、脱离请求任务、SSE/WebSocket、CAP 和定时任务分别纳入；Host.StopAsync 报错不能阻止外部调用方继续 Dispose 根容器。
- [ ] **安全运维入口**：消息发布者/租户归属、代理 ACL、恢复/审计查询与 Dashboard 权限；用测试秘密验证全部日志，实际使用过的历史凭据由运维轮换。
- [ ] **审计故障时限与异常完整性**：IAuditingStore 普通路径缺调用方取消令牌；慢保存可能延长请求和停止。中间件以 storeException ??= exception 接收释放异常，同时保存/释放失败时后者丢失；需直接复现和完整汇总。不能超时后强行释放仍在使用的资源。
- [ ] **数据库驱动清理**：新库存路径多处 using DbCommand/Reader；若业务异常与 Dispose 同时抛错，需直接验证原异常保留与后续清理，不用普通成功释放推断。
- [ ] **限流容量与配置**：极小摘要已独立列 FIX-023；Redis 单策略 Hash 达上限时 HGETALL 扫描开销、热键、公平性及蓝绿策略参数冲突需测量与配置约束。新旧主体键混跑的额度重置/分裂已有说明，仍需发布策略验收。
- [ ] **容量与合规**：业务收据保留覆盖重放期限；未处理事件不可按年龄删除；审计访问、归档、磁盘预算及敏感值删除规则。
- [ ] **部署迁移**：CREATE TABLE IF NOT EXISTS 不升级旧表；库存/整单/审计/有序表需版本化迁移、索引/排序规则回填与扩展—迁移—删除；滚动期间契约兼容。
- [ ] **强停与外部副作用**：强杀/断电依靠事务、出箱及收据恢复；HTTP/邮件等须幂等或后续出箱，不能由租约或托管 Dispose 保证。

## 本轮测试和证据边界

本轮只读核对最新保存的 TRX、生产源码、直接测试和真实门控诊断，没有重新运行 dotnet 测试、Sample 或部署。这些是上一实施批次的实际结果，不写成本轮新执行。窄范围 TRX/CI/构建证据由独立 luna_worker 复核；有界算法诊断不计为 xUnit 通过项。

| 最新实施批次实际执行 | 通过 | 失败/未执行 | 证据 |
| --- | --- | --- | --- |
| 库存定向 net6.0 / net8.0 | 各 26 | 各 0/0 | artifacts/cluster-scope-identity-fix/inventory-after-net6.trx、inventory-after-net8.trx |
| 限流中间件/HTTP 管道 net6.0 / net8.0 | 各 20 | 各 0/0 | 同目录 identity-after-net6.trx、identity-after-net8.trx |
| EventBus 全量 net6.0 / net8.0 | 各 182 | 各 0/0 | 同目录 eventbus-net6.trx、eventbus-net8.trx |
| MVC 全量 net6.0 / net8.0 | 各 102 | 各 0/1 | 同目录 mvc-net6.trx、mvc-net8.trx；现有 UploadRawAsync 为 NotExecuted |
| Admin EFCore / FreeSQL 库存真实 MySQL/RabbitMQ 定向 net6.0 | 各 6 | 各 0/0 | 同目录 admin-ef-inventory-current-net6.trx、admin-free-inventory-current-net6.trx |
| Admin EFCore 完整真实门控 net6.0 | 22 | 0/0 | artifacts/admin-mysqlcap-results/inventory-scope-identity/net6.0-20261005T050557761Z/admin-efcore-mysqlcap.trx |
| Admin FreeSQL 完整真实门控 net6.0 | 21 | 0/0 | 同目录 admin-freesql-mysqlcap.trx |
| Sample Lazy 业务 HTTP/StopAsync net6.0 | 1 | 0/0 | artifacts/cluster-scope-identity-fix/sample-lazy-net6.trx |
| WebApi net6.0 构建 | 成功 | 0 错误、7 警告 | 同目录 webapi-build-net6.log |

各组按实际 UnitTestResult/outcome 计数；定向与全量、不同框架之间重叠，不能相加当作独立覆盖数量。真实门控诊断为 Passed、MySQL/Rabbit 可达、两个退出码 0，两套门控均实际包含六个 InventoryOrder 方法。工作区是 dirty，保存的 TRX 不是后续任意变更的自动认证。

保留失败复现：inventory-scope-before-net6.trx 为 0通过/1失败；identity-before-net6.trx 为 2通过/1失败。它们用于证明修复前缺陷，不计作修复后失败。更早 inventory-orders 的 24/24、Admin 各 4/4，以及 cluster-capability-audit 的 181/181 等均保留为历史证据，不替代上表最新结果。

- [x] 新库存竞争、最新完整真实业务与 22/21 门控已有；不再列为未执行。
- [x] 最新 EventBus、MVC、Sample 和 WebApi 构建证据已有。
- [ ] 后续修改按影响范围补相应回归；本批未新跑 FreeSQL/EFCore/审计全部测试，不把 Admin 链路通过当作所有工程全量通过。
- [ ] 锁定 SDK 8.0.424 验证；本轮读取本机 SDK 清单确认没有 8.0.424，保存结果使用 8.0.425，global.json 未变。
- [ ] NU1900 表示在线漏洞元数据获取失败；依赖漏洞扫描仍待网络恢复，不能据此声称安全检查通过。

本地事件不跨节点、CAP 至少一次投递、默认加载上下文不可卸载、强杀不保证托管清理，不因这些明确边界重复创建“保证恰好一次/强杀后 Dispose”的 TODO。

## 建议推进顺序

1. FIX-023 及库存键比较/事务引擎迁移；补最新 execution 证据，避免继续使用过时结论。
2. FIX-007/013：库存双进程、整单回滚后有界死锁重试、提交确认丢失/Unknown 恢复。
3. FIX-006/008/009：生产有序授权恢复、跨进程限流及 Redis 故障、自动变更审计及普通审计落库。
4. FIX-011：角色开关真正接入、依赖预热、消费/任务排空与双平台动态发布。
5. FIX-012：指标导出、全局积压/年龄、容量/归档、持续资源测量与交付收口。

保持 NEEDS_FIX，完整 P0–P4 尚未完成。本轮只修改 review 和审查证据；没有修改生产/测试逻辑、plan、execution、目标框架、依赖版本或 global.json，未提交、推送或发送外部通知。
