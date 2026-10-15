<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T12:54:26+00:00

# 集群能力当前审查与剩余 TODO

## 审查范围与结论

对照用户批准的完整 P0–P4 计划、plan.md、execution.md、暂存及未暂存 Git Diff、未跟踪源码、实际宿主、测试和最新 TRX。结论仍为 NEEDS_FIX：**7 组业务工作包未完整验收**（FIX-006、007、008、009、011、012、013），多数已有组件，差距主要是生产接线、异常边界和集群验收，不能表述为七项功能完全未实现。FIX-029/030 已有源码修复及部分验证；本轮实测确认新的测试编译阻塞 FIX-031。这三项均归属 FIX-013，不额外计作业务能力。七组中六组有实现/验收缺口，一组为历史故障观察，不表述为七项全新功能。

本轮不修改业务源码、测试、计划或执行记录。执行 EventBus 新取消适配 net6 定向验证，测试工程因两项编译错误失败，未进入测试运行；另运行 CI 校验脚本自测并核对实际 TRX。不操作数据库、不部署、不重新运行 Sample。窄范围证据盘点委派给既有 luna_worker，只读结果经主审查整合。前次审查保存在 review-before-cancellation-adapter-audit-20261005.md。

本地事件不跨节点、CAP 至少一次、强杀不保证托管清理、数据库内部仍有锁，是已批准的边界；不把它们列为缺失的跨节点本地广播、恰好一次或全系统无锁。

## 阶段验收矩阵

| 阶段 | 状态 | 已实现 | 剩余主要差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（历史直接证据） | 异步按键锁、所有者校验和回收；Redis 原子获取/释放及完整租期；本地事件快照 | 持续负载及 Redis 故障切换不在核心通过结论内 |
| P1 事件事务 | PARTIAL | CAP 适配和元数据、两 ORM 分轮领域事件、同事务消费收据、提交结果诊断 | 消费取消真实重试、活动资源安全清理及其余组合故障 |
| P2 顺序与库存 | PARTIAL | 序号/入箱/游标/Worker、两 ORM 条件库存及幂等/审计/出箱；真实双进程、1213、COMMIT 确认丢失及后台恢复 | 有序生产接线、授权恢复、保存点外状态；历史偶发失败观察 |
| P3 限流审计原子工具 | PARTIAL | 两种令牌桶、HTTP 429/503 与可信身份、HTTP/关键事务审计、实体字段填充、Interlocked/CAS | 跨进程真实限流与故障、自动属性快照、普通审计生产存储及预算 |
| P4 蓝绿及治理 | PARTIAL | 两平台模板/切流脚本、HTTP 就绪和排空、进程内指标 | 真实角色、预热、消费/任务排空、两平台演练、全局诊断及留存/长期测量 |

## 本轮从待办移除的子项

- [x] 两 ORM 真实 InnoDB 1213：各 2/2，先清理后新作用域重试，四次上限，库存/审计/出箱断言。
- [x] 两 ORM 真实 COMMIT 确认丢失：各 2/2。服务器 OK 到达 loopback 代理后丢弃；原异常 Unknown、权威写库按原单恢复且不重复写；保留恢复和清理错误。
- [x] 两 ORM 原生 CAP 后台出箱扫描恢复：各 1/1。同一个原消息 Id 从 Scheduled 达到 Succeeded，实际 RabbitMQ 订阅回调；不手工重发目标消息或更新出箱状态。
- [x] 最新完整真实门控 EFCore 32/32、FreeSQL 31/31，0 Failed、0 NotExecuted；当时两套各 19 个必需方法通过。该证据关闭上次审查中的 FreeSQL 确认丢失及出箱后台恢复缺口。
- [x] 本轮 CI 校验脚本自测退出 0：两套各 21 方法夹具、154 个拒绝断言通过。它验证校验器，不代表新取消测试或当前完整门控通过。

当前 CI 为 EFCore 最低 34、FreeSQL 最低 33，各 21 个必需方法。共享取消文件及清理测试已链接两个工程，EF 清理直接测试 9/9；适配前 EF 真实取消 0/2，未观测到重试。最新显式适配已写入生产且两个 Admin 已启用，生产 Bing.Events 本轮编译成功，但新增 EventBus 测试有两项编译错误；适配后的双 ORM 真实取消及当前完整门控没有通过证据。旧 32/31 不能支持当前门槛验收。

## 结构化问题及功能 TODO

### FIX-031：新增取消适配测试名称解析错误，阻断当前测试工程编译

- 严重程度：HIGH；处理要求：MUST_FIX；状态：FAIL。
- 问题：两个新测试使用未限定的 Options、Extensions 名称，在 Bing 子命名空间内被解析为 Bing.Options、Bing.Extensions。
- 证据：BingSubscribeInvokerTest.cs:123、BingCapConsumerCancellationRegistrationTest.cs:96；本轮实际日志 artifacts/cluster-current-audit-20261005/cap-cancellation-current-net6.log，两个 CS0234，命令退出1。生产 Bing.Events 已编译成功；没有生成新测试 TRX。
- 影响：EventBus 当前 net6 测试工程不能编译，11项新增直接测试及该工程回归无法执行。不等同于已证实生产代码编译失败。
- 修复目标：明确引用 Microsoft.Extensions.Options.Options 与 Bing.Events.Cap.Extensions，消除名称冲突。
- 修复要求：修正测试名解析，不调整目标框架、依赖或删除用例规避。
- 验证方式：11项新定向 net6/net8，再执行受影响 EventBus 回归。
- [ ] 修复两处编译错误，运行新直接测试及受影响回归。

### FIX-013：消费取消适配与剩余真实故障矩阵

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：真实确认丢失及后台出箱恢复已验收；适配前消费取消 EF 0/2，等待 RetryStarted 超时。上一执行实际观察 CAP 记录 Succeeded/0，原生 CAP 8.0.1 执行器对 OCE 的处理不能被假定为失败重试。
- 证据：artifacts/cap-consumer-cancellation-fix/cap-cancel-ef-net6.trx 两项 Failed；BingSubscribeInvoker.cs 通过显式重实现 ISubscribeInvoker 转换逃出的 OCE；Extensions.Service.cs 显式注册；两个 Admin CapModule 已启用。新适配后的真实 TRX 不存在。
- 影响：源码适配合理但未验收；不能承诺取消后自动重试。消费主动取消、宿主令牌取消、过滤器处理取消、数据库提交结果及 RabbitMQ broker 重新入队是不同路径。
- 修复目标：显式兼容策略通过真实 CAP 持久化失败重试，同一收据阻止重复；原异常与清理异常可定位。
- 修复要求：先修 FIX-031。保留原生默认行为和自定义执行器扩展点，不手工重发、不改出箱状态；文档明确新显式入口及 Admin 的启用变化。
- 验证方式：直接调用器/注册测试；双 ORM 实际消费，原 cap.received Id 的 Failed→Succeeded、重试次数、业务和收据各一；当前完整34/33和21必需方法全通过。共享业务使用直接 MySQL 事务，不替代全部 ORM 工作单元取消验收。
- [ ] 适配后真实取消 EFCore/FreeSQL 各两项、FreeSQL 清理九项、两个 Admin 注册测试及当前完整门控。
- [ ] 补充既有过滤器、调用令牌取消、已适配派生类型/实例注册的兼容行为；更新显式入口、迁移说明和实际执行记录。
- [ ] 验证真实出箱已写后的提交前进程中断、提交后 Flush/Dispose 及回滚/释放组合失败，不重复计入已有协作取消和确认丢失覆盖。
- [ ] 生产不响应取消时保留活动资源并报告未完成；测试清理保护不等于生产 CAP/任务排空已完成。

### FIX-029：取消共享测试与当前门槛验收

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：原未链接缺陷已修，当前缺口转为适配后实测及完整门槛验收。
- 证据：两个 Admin csproj 均链接 AdminCapCancellationCases.cs 和 AdminCapCancellationCleanupTest.cs；EF 清理9/9、适配前真实取消0/2。当前CI最低34/33，各21必需方法；实际全量仍是旧32/31。
- 影响：源码、构建入口已对齐，执行结果尚未对齐门槛。
- 修复目标：双 ORM 当前必需方法全部 Passed。
- 修复要求：不得降低门槛或删除失败用例；与FIX-013一起验收，不额外计数。
- 验证方式：双 ORM 定向与当前完整真实门控TRX。
- [x] 两工程接入共享取消及清理文件。
- [ ] 完成新版本定向、完整门控及结果映射。

### FIX-030：取消夹具停止失败后的安全清理

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL（测试夹具，源码修复及EF直接证据已有）。
- 问题：已改为唯一清理任务，关闭入场、放行gate，确认Stop完成及实际回调排空后才删除与释放；无法确认时保留GUID身份、资源拥有者及原异常。
- 证据：AdminCapCancellationCases.cs 的 CapCancellationCleanup；artifacts/cap-consumer-cancellation-fix/cap-cleanup-ef-net6.trx 为9/9。覆盖停止超时/异常、活动回调、数据清理失败、同步/异步释放一次及未完成释放保留。
- 影响：原提前删活动资源风险已按源码修复，不能将EF测试夹具结果写成FreeSQL或生产宿主排空通过。
- 修复目标：双ORM同一安全清理行为并有真实宿主证据。
- 修复要求：待清理完成前不强行释放；保留错误和恢复身份，不把等待超时标成成功。
- 验证方式：FreeSQL9项及适配后两套真实取消；生产排空另由FIX-011验收。
- [x] 修复提前清理路径，EF直接测试9/9。
- [ ] FreeSQL清理定向及双ORM真实宿主验收。

### FIX-006：有序消费生产接线、权限与事务一致性

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：有序组件和测试宿主已有，两个 Admin 未注册 AddBingOrderedConsumer，也没有生产入箱订阅和安全恢复闭环。
- 证据：OrderedEventConsumer.cs:43 仅注册 Worker；Admin 源码无调用。OrderedEventCoordinator.cs:223 状态查询无宿主权限，:258 只遍历可选授权器；基础两 ORM 事务工厂未绑定 CAP 派生出箱。
- 影响：不能证明真实生产消息持久化后才确认，也不能向外暴露未授权的查询/重试；这不是已证实的远程可利用漏洞。
- 修复目标：主题/稳定组→事务入箱→两 ORM Store/Factory/业务 Worker；受控查询、重试和补偿。
- 修复要求：宿主租户授权和关键操作审计；派生出箱同事务；保存点失败必须丢弃 ORM/CAP/内存事务动作，禁止自动跳号。
- 验证方式：实际 Admin 3→1→2、重复、前序失败、重启、多实例、授权拒绝及派生消息回滚。
- [ ] 接入真实业务订阅、持久化入箱和后台消费者，而非空处理器。
- [ ] 完成租户授权查询/恢复、关键操作审计及派生 CAP 出箱适配。
- [ ] 验证保存点外的 ORM 跟踪、CAP 缓冲和事务回调不会提交已回滚业务。

### FIX-007：历史库存偶发竞争失败的诊断观察

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：真实竞争、死锁和确认丢失已通过；首次 FreeSQL 不同单进程竞争失败原日志没有 Number，无法从后续通过解释它。
- 证据：inventory-process-cleanup 保留原失败 TRX；后续安全诊断、有界重复及完整门控通过。最新真实1213不等于首次事件也为1213。
- 影响：不能宣称历史不稳定根因已修复；也不能把不可恢复的日志当明确生产缺陷反复改代码。
- 修复目标：限定观察/复现条件和证据保留；再现后依错误编号、阶段和事务结果修复。
- 修复要求：保留现有安全诊断和四次整单重试边界；未再现前不猜测调整隔离级别或无限重跑。
- 验证方式：有界竞争验证与完整方法记录；无法追溯的原事件明确记录为未定位。
- [ ] 收口历史失败的观察条件、诊断证据和再现处理流程，不将重复通过写成根因结论。

### FIX-008：跨进程限流与真实 HTTP/Redis 故障

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：组件及 Admin 注册已有，同进程两容器共享额度不能替代独立进程、完整 Admin 认证和可信代理链路。
- 证据：RedisTokenBucketRateLimiter、BingRateLimitPipelineTest、AdminRateLimitingRegistrationTest；源码未发现限流独立进程探针。
- 影响：集群额度、故障响应和主体隔离尚未完整验收。
- 修复目标：真实 HTTP、双进程共享额度和 Redis 故障恢复证据。
- 修复要求：默认基础设施失败拒绝，显式降级；不因实例增加扩大额度；代理头受信任配置保护。
- 验证方式：两进程 Redis、429/Retry-After、断连/恢复与503、租户/路由隔离、伪造头、取消和键回收。
- [ ] 完整 Admin HTTP/代理验证及独立进程共享额度。
- [ ] Redis 断连/恢复、显式降级和限流取消结果的真实验证。

### FIX-009：自动审计快照、普通落库与保存预算

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：实体审计字段和显式关键审计已有，自动属性变化快照及普通 HTTP 可靠生产存储尚未接入。
- 证据：两 ORM AuditingExtensions 只提供 SaveTransactionalAuditAsync；AuditingRegistrationExtensions.cs:20 默认 SimpleLogAuditingStore；AdminInventory 只注册 ITransactionalAuditingStore；BingHttpAuditingMiddleware.cs:111 SaveAsync 无预算/取消参数。
- 影响：手填 EntityChanges 和日志输出不能替代自动变更审计及可靠落库。
- 修复目标：可选自动快照、普通 IAuditingStore 生产配置/查询授权、异步保存预算。
- 修复要求：白名单、脱敏、长度；普通失败告警，关键失败回滚；停止等待不等于实际保存结束。
- 验证方式：两 ORM 新增/修改/删除、真实持久化、安全查询、敏感字段、慢保存/取消、原始及清理异常。
- [ ] 实现 EF ChangeTracker 和 FreeSQL 属性级自动快照。
- [ ] 普通 IAuditingStore 数据库配置和安全查询接线。
- [ ] 异步保存预算/取消、慢存储和多重故障验收。

### FIX-011：蓝绿实际角色、预热、排空与部署

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：角色环境变量只存在模板，Admin 启动未识别；当前两个宿主仍注册 CapModule，EF 宿主还注册 HangfireModule。
- 证据：两 Startup.cs:41、EF :45；两 AppModule.cs:148/127 装配管道后 MarkBingTrafficReady，没有依赖预热门槛。TrafficGateMiddleware.cs 的 lease 只覆盖 _next。
- 影响：绿版预热可能参与生产消费/任务；HTTP 计数清零不足以证明后台资源排空。
- 修复目标：真实 Web/Consumer/Task 角色、依赖预热和完整在途统计，双平台切换回滚验收。
- 修复要求：稳定逻辑组；超时明确未完成；强杀恢复靠事务/出箱/幂等。
- 验证方式：Compose/Nginx 和 Kubernetes 分别验证长请求、活动消费、任务、绿版不就绪、切流、回滚及强停恢复。
- [ ] 启动实际识别角色及消费/任务开关，绿版预热禁用生产消费。
- [ ] 接入真实依赖预热及请求、消费、任务资源排空。
- [ ] 完成两平台动态演练，不以模板静态检查代替部署通过。

### FIX-012：全局指标、留存、长期资源与文档

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：指标为进程内最近扫描页，留存和长期探针缺失；cluster-consistency.md 末尾部分已修正，但前文仍有旧16/15验收数量及库存真实1213/Unknown仍待验收的描述；新增取消适配未同步指南。
- 证据：OrderedEventConsumer.cs:153 写 streams.Count；docs/guides/cluster-consistency.md 有序链路验证段及锁与库存末段；现有 ModuleResourceProbe 不覆盖持续集群运行。
- 影响：不能以页数计算全局积压/年龄，也不能以模块回收证明集群组件无泄漏；过时文档会误导后续执行。
- 修复目标：全局积压/最老年龄、指标导出/授权诊断、留存归档、长期资源测量及当前文档对齐。
- 修复要求：未完成消息保护，幂等保留覆盖重放；有限标签及告警节流；无业务容量预算时只报告测量。
- 验证方式：多页积压、年龄/导出、安全诊断、归档重放、持续循环的分配/堆/GC/存活对象/延迟和追赶速度。
- [ ] 全局指标、导出、授权诊断与告警节流。
- [ ] 审计、收据、消息留存归档和未完成数据保护。
- [ ] 集群长期资源探针及 README/指南/符号映射/执行报告对齐。

## 欠缺考虑或尚未充分验收的边界 TODO

与上述工作包重叠，不另计作业务能力；以下并非全部已复现漏洞。

- [ ] 原请求恢复可信度：recoverAsync 返回结果受调用方保证；权威写库按原身份和内容验证。只读副本 null、超时/取消不证明未提交；不换请求 ID 重做 Unknown。
- [ ] 租户权限来源：同一 ClaimsIdentity 只保证数据来源一致，不替代访问授权；消息头、业务正文、恢复与审计查询分别校验，宿主明确多 scheme 合并策略。
- [ ] 流起点及代次：新消费者起点、过滤造成缺口、实体重建、契约版本及重放策略；序号必须针对订阅方完整可见流分配。
- [ ] 保存点外状态：ORM 跟踪、FreeSQL 变更报告、CAP 缓冲、内存回调及外部 HTTP/邮件不自动回滚，分别定义出箱/幂等边界。
- [ ] 公平与背压：Worker 逐实体串行，慢 handler 仍会阻塞其他实体；设计有界公平并发、入箱总容量和数据库/CAP 总重试预算。
- [ ] 排空完整性：请求作用域异步清理、OnCompleted、SSE/WebSocket、脱离请求任务、CAP/定时任务分别等待；超时不能提前释放仍在使用的资源。
- [ ] Redis 运行边界：租期及主从切换不保证绝对独占；单 Hash HGETALL 的热键延迟、最大分桶和策略/主体键升级需量化。
- [ ] 限流取消后的额度：已发脚本可能消耗令牌，本地取消不撤回 Redis 写入；定义是否继续处理或退款，禁止盲目退款扩大额度。
- [ ] 迁移与协调恢复：CREATE TABLE IF NOT EXISTS 不升级旧表；身份/索引回填、维护窗口、备份和新旧实例兼容，以及数据库/RabbitMQ 恢复协调。
- [ ] 留存与重放：CAP 清理不代替业务幂等保留；正文冲突检测、归档和重放期限一致，未处理数据不能按年龄删除。
- [ ] 安全交付：代理 ACL、诊断/Dashboard 权限、日志秘密扫描及旧凭据轮换，默认不暴露消息正文。
- [ ] 故障证据精度：READY/GO 不等于事务重叠；已写出箱协作取消不等于该点强杀；loopback 无 TLS 代理不证明 TLS、生产网络或主从故障切换可靠性。
- [ ] 宿主取消与 CAP 重试分层：消费者主动抛 OCE、宿主停止令牌、数据库提交结果、CAP 持久化重试及 RabbitMQ 重新入队分别验收。
- [ ] 取消分类策略：新适配将所有逃出的OCE转换为失败；区分业务主动取消、调用/宿主令牌、超时和过滤器吞/改写，确认哪些应重试、是否需要告警或人工处置。
- [ ] 消费身份精确比较：EventConsumptionStore接受含尾空格标识；指南/测试表使用utf8mb4_bin文本唯一键而复核查询用BINARY。补尾空格、Unicode与字节长度的直接MySQL测试，明确与库存/审计VARBINARY规则是否统一；当前只列待验证，不宣称已复现越权或错误去重。
- [ ] 保留资源后的恢复：清理等待超时保留GUID表/消息及资源拥有者，需给出实际停止完成后的恢复、再清理及证据保留步骤，避免长时间积留验收数据。
- [ ] 库存后续产品能力（本期范围外）：预占、撤销、返还、部分发货使用独立业务身份及补偿契约，不把换 ID 当 Unknown 恢复。

## 验证证据与限制

本轮 EventBus 新定向 net6 编译退出1（2项CS0234），未进入测试运行；没有对同一源码重复跑net8。CI校验器自测最终脚本成功退出0，21方法夹具及154拒绝断言通过。盘点历史TRX，未重跑实际数据库或Sample。首次CI包装误取内部负例进程的LASTEXITCODE=2，日志完整成功；随后按脚本异常判定成功，保留两个日志，不能把负例退出码算业务失败。定向与全量有重叠，不累计为覆盖总数。

| 验证 | 最近真实结果 | 证据/边界 |
| --- | --- | --- |
| 两 ORM 原生1213 | 各2/2 | artifacts/inventory-real-deadlock-fix/admin-ef-real-deadlock-net6.trx、admin-freesql-real-deadlock-net6.trx |
| 两 ORM COMMIT确认丢失 | 各2/2 | 同目录 admin-ef-commit-loss-fixed-net6.trx、admin-freesql-commit-loss-fixed-net6.trx |
| 两 ORM CAP后台恢复 | 各1/1 | 同目录 admin-ef-outbox-background-net6.trx、admin-freesql-outbox-background-net6.trx |
| 最新 Admin 完整门控 | EFCore32/32、FreeSQL31/31，0失败/跳过 | artifacts/admin-mysqlcap-results/inventory-commit-ack-recovery/net6.0-20261005T113751407Z/；19必需方法批次，不满足当前34/33和21方法门槛 |
| EF取消清理直接测试 | 9/9（上一执行） | artifacts/cap-consumer-cancellation-fix/cap-cleanup-ef-net6.trx；FreeSQL未执行 |
| 原生/适配前EF真实消费取消 | 0/2，两项Failed | cap-cancel-ef-net6.trx；RetryStarted等待超时，未验收重试 |
| 最新适配直接测试 | 本轮net6编译失败，0项执行 | 两项CS0234；cap-cancellation-current-net6.log；适配后的真实取消无TRX |
| CI校验器 | 本轮通过 | artifacts/cluster-current-audit-20261005/ci-current-selftest-final.log；21方法夹具、154拒绝断言，不等于业务测试通过 |
| MVC net6/net8 | 各128 Passed、1 NotExecuted、0 Failed | artifacts/audit-selected-identity-fix/mvc-regression-net6.trx、mvc-regression-net8.trx；UploadRawAsync跳过，以 UnitTestResult 为准 |
| Sample Lazy业务/StopAsync/端口释放 | 1/1（历史） | artifacts/inventory-process-fix/sample-http-stop-net6.trx，非生产Admin或蓝绿验收 |
| WebApi构建 | 0错误/2警告（历史） | artifacts/cluster-current-audit-20261005/webapi-build-inventory-audit.log |
| Git空白检查 | 本任务选定改动退出0 | 全仓库暂存检查发现历史模块 execution.md 两处 Markdown 尾空格；不修改无关任务 |

实际安装 SDK8.0.425，未安装锁定8.0.424；global.json 未修改。MySQL/RabbitMQ 已有专用验收，不再泛称 CAP 缺环境。

- [ ] 环境验收：SDK8.0.424；不能把8.0.425通过写成锁定版本通过。
- [ ] 部署验收：Compose/Nginx、Kubernetes 两套真实动态演练，先完成角色及预热接线；不是仅由缺环境解释的工作。

本轮未运行全套 Core/AOP/DDD/ORM 回归。NU1900 元数据获取及 Microsoft.Extensions10/net6 警告不直接证明可利用漏洞。旧 FreeSQL 通用异步查询同步回退保持独立任务，不混入七组计数。

建议推进顺序：FIX-031（测试编译）→013/029/030（显式取消适配的直接、真实及当前全量验收）→006（有序生产接线）→008/009→011→012。FIX-007 保留有界诊断观察，不因缺原错误编号反复猜测改代码。没有提交、推送或发送外部通知。
