<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T12:03:22+00:00

# 集群能力当前审查与剩余 TODO

## 审查范围与结论

对照用户批准的完整 P0–P4 计划、plan.md、execution.md、暂存及未暂存 Git Diff、未跟踪源码、实际宿主、测试和最新 TRX。结论仍为 NEEDS_FIX：**7 组业务工作包未完整验收**（FIX-006、007、008、009、011、012、013），多数已有组件，差距主要是生产接线、异常边界和集群验收，不能表述为七项功能完全未实现。本轮另外确认两项测试交付问题 FIX-029/030，均归属 FIX-013，不额外计作业务能力。

本轮只审查，不修改业务源码、测试、计划或执行记录，不运行 dotnet 测试、数据库操作或部署。窄范围证据盘点委派给既有 luna_worker，只读结果经主审查整合。当前审查覆盖截至上述时间的工作区，前次审查保存在 review-before-cap-cancellation-audit-20261005.md。

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

当前 CI 已提高到 EFCore 最低 34、FreeSQL 最低 33，21 个必需方法，但新消费取消文件未接入两个工程、未编译、没有 TRX。因此最新 32/31 不能支持当前门槛验收，不能把数字上调写成实际通过。

## 结构化问题及功能 TODO

### FIX-013：消费取消与剩余真实故障矩阵

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：确认丢失及后台恢复已经完成双 ORM 验收，但新消费取消代码未构建、失败清理不安全，其余真实组合故障未完整验证。
- 证据：AdminCapCancellationCases.cs:121/152；真实故障结果见 commit-ack-recovery-results.json；现有 InventoryOrder_WhenCancelledAfterOutbox 已覆盖协作取消，不能替代 CAP 消费回调取消或该故障点进程中断。
- 影响：请求取消、调用方停止等待、业务已提交及资源已清理不能混为同一状态；不能据测试源码承诺 CAP 的实际取消重试行为。
- 修复目标：消费提交前/后取消的原生 CAP 重试、同一收据幂等和安全排空；原始错误与清理错误均可定位。
- 修复要求：先完成 FIX-029/030 再执行新用例；不响应取消时保持活动资源，停止超时只报告未完成。
- 验证方式：两 ORM 真实宿主专项，原 cap.received Id 的 Failed→Succeeded/重试次数、业务和收据各一；随后当前完整 34/33 门控。该共享测试用直接 MySQL 事务，不能替代所有 ORM 工作单元取消行为；CAP 本地持久化重试不等于 RabbitMQ broker 重新入队，也不等于宿主停止取消。
- [ ] 完成消费提交前/后取消、原生重试、同事务幂等及当前完整门控。
- [ ] 验证真实出箱已写后的提交前进程中断、提交后 Flush/Dispose 及回滚/释放组合失败；不重复计入已有协作取消和确认丢失覆盖。
- [ ] 验证不响应取消时的停止等待、活动回调与资源安全保留，明确清理未完成状态。

### FIX-029：新增取消测试未进入编译但 CI 已要求它们

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：FAIL。
- 问题：两个测试工程只显式链接既有 Shared 文件，未链接 AdminCapCancellationCases.cs；默认编译范围不包含上级兄弟 Shared 目录。
- 证据：Bing.Admin.EFCore.Tests.csproj:16、Bing.Admin.FreeSQL.Tests.csproj:15 的完整 Compile 清单；Invoke-AdminMySqlCapIntegrationTests.ps1:24/43/53/72 已要求最低 34/33 及两个 Consumption_WhenCancelled 方法。
- 影响：当前构建无法提供两个必需方法，现有 32/31 不满足当前 CI；源码存在不能证明测试已发现、编译或执行。
- 修复目标：源码、构建、方法级门控和实际 TRX 一致。
- 修复要求：修复 FIX-030 后链接两个工程，编译并先运行定向，再完整门控；不得删必需方法或降低门槛掩盖未实现。
- 验证方式：发现且通过两项新方法，当前 34/33 真实结果和 21 项必需方法全部 Passed。
- [ ] 接入新共享文件，完成编译、定向、完整门控并更新方法映射和实际执行记录。

### FIX-030：新取消夹具可能在停止失败后提前删活动资源

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：FAIL（测试夹具风险，不宣称生产漏洞）。
- 问题：内部 StopAsync 失败可附于 validationFailure，外层 finally 仍清理收据和 DROP TABLE；Started==Ended 断言只在正常返回路径执行。
- 证据：AdminCapCancellationCases.cs:115–128、138–152、273。新用例尚未运行，本结论来自明确的异常控制流。
- 影响：停止超时或失败时可能还有活动回调，过早删除会破坏失败证据并制造次生错误，不符合批准的“不强行释放活动资源”原则。
- 修复目标：失败路径也必须确认停止与实际业务回调排空，再清理测试资源。
- 修复要求：finally 放行重试 gate；等待实际完成。无法确认完成时保留 GUID 资源与恢复身份，附加诊断，不继续破坏性清理；保留原始异常。
- 验证方式：停止超时/异常、活动回调延迟完成、最终清理一次的直接测试；实际 CAP 两套定向。
- [ ] 修复停止失败后的清理条件，并补失败路径直接断言。

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
- 问题：指标为进程内最近扫描页，留存和长期探针缺失；cluster-consistency.md 的待实施条目仍把已验收整单业务/双进程竞争列为未完成。
- 证据：OrderedEventConsumer.cs:153 写 streams.Count；docs/guides/cluster-consistency.md 待实施闭环第3/4项；现有 ModuleResourceProbe 不覆盖持续集群运行。
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
- [ ] 库存后续产品能力（本期范围外）：预占、撤销、返还、部分发货使用独立业务身份及补偿契约，不把换 ID 当 Unknown 恢复。

## 验证证据与限制

本轮重新运行 CI 校验器自测，退出0；仅盘点既有 TRX，没有重跑 dotnet 或 Sample。定向与全量有重叠，不累计为覆盖总数。

| 验证 | 最近真实结果 | 证据/边界 |
| --- | --- | --- |
| 两 ORM 原生1213 | 各2/2 | artifacts/inventory-real-deadlock-fix/admin-ef-real-deadlock-net6.trx、admin-freesql-real-deadlock-net6.trx |
| 两 ORM COMMIT确认丢失 | 各2/2 | 同目录 admin-ef-commit-loss-fixed-net6.trx、admin-freesql-commit-loss-fixed-net6.trx |
| 两 ORM CAP后台恢复 | 各1/1 | 同目录 admin-ef-outbox-background-net6.trx、admin-freesql-outbox-background-net6.trx |
| 最新 Admin 完整门控 | EFCore32/32、FreeSQL31/31，0失败/跳过 | artifacts/admin-mysqlcap-results/inventory-commit-ack-recovery/net6.0-20261005T113751407Z/；19必需方法批次，不满足当前34/33和21方法门槛 |
| 新消费取消两项 | 未编译/未运行 | 无对应 TRX，两个 csproj 未链接 |
| CI校验器 | 本轮通过 | 21方法夹具、154拒绝断言；不等于业务测试通过 |
| MVC net6/net8 | 各128 Passed、1 NotExecuted、0 Failed | artifacts/audit-selected-identity-fix/mvc-regression-net6.trx、mvc-regression-net8.trx；UploadRawAsync跳过，以 UnitTestResult 为准 |
| Sample Lazy业务/StopAsync/端口释放 | 1/1（历史） | artifacts/inventory-process-fix/sample-http-stop-net6.trx，非生产Admin或蓝绿验收 |
| WebApi构建 | 0错误/2警告（历史） | artifacts/cluster-current-audit-20261005/webapi-build-inventory-audit.log |
| Git空白检查 | 本任务选定改动退出0 | 全仓库暂存检查发现历史模块 execution.md 两处 Markdown 尾空格；不修改无关任务 |

实际安装 SDK8.0.425，未安装锁定8.0.424；global.json 未修改。MySQL/RabbitMQ 已有专用验收，不再泛称 CAP 缺环境。

- [ ] 环境验收：SDK8.0.424；不能把8.0.425通过写成锁定版本通过。
- [ ] 部署验收：Compose/Nginx、Kubernetes 两套真实动态演练，先完成角色及预热接线；不是仅由缺环境解释的工作。

本轮未运行全套 Core/AOP/DDD/ORM 回归。NU1900 元数据获取及 Microsoft.Extensions10/net6 警告不直接证明可利用漏洞。旧 FreeSQL 通用异步查询同步回退保持独立任务，不混入七组计数。

建议推进顺序：FIX-030→029→013（安全接入取消验收及当前门控）→006（有序生产接线）→008/009→011→012。FIX-007 保留有界诊断观察，不因缺原错误编号反复猜测改代码。没有提交、推送或发送外部通知。
