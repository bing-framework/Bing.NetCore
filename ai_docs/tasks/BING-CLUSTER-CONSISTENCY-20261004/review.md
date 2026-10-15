<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T13:49:30+00:00

# 集群能力当前审查与剩余 TODO

## 审查结论与范围

对照用户批准的完整 P0–P4 计划、plan.md、execution.md、工作区/暂存差异、未跟踪生产源码、实际宿主与 TRX。结论为 NEEDS_FIX：**仍有 7 组未完整交付的工作包：6 组实现或验收缺口，加 1 组历史及新近库存故障观察。** 对应 FIX-006、007、008、009、011、012、013；不代表七项能力从零缺失，也不以测试总数计算完成百分比。

本轮只审查，不修改生产代码、测试、plan.md 或 execution.md。不操作数据库、不部署、不重新运行 Sample。既有 luna_worker 停止实现，仅汇总 CAP 正式包 IL 的窄范围证据。前版审查保存在 review-before-ordered-state-audit-20261005.md。

本轮实际执行当前有序状态定向 net6 测试及 EventBus net6/net8 全量；核对上轮最新 Admin 真实 MySQL/RabbitMQ 门控和取消测试 TRX。刚新增的状态协议已通过直接测试，但没有生产实现者，CAP 有序事务工厂仍未实现。最新 Admin 门控先于这些状态协议改动，不能替代它们在真实两 ORM 中的验收。

本地事件不跨节点、CAP 至少一次、强杀无法保证托管清理、数据库内部仍有锁，均为批准边界；不新增跨节点本地广播、恰好一次或全系统无锁承诺。

## 阶段验收矩阵

| 阶段 | 状态 | 已有能力 | 主要差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（历史直接证据） | 异步按键锁、所有者校验与键回收、Redis 原子锁与完整租期、本地事件快照 | 持续负载和 Redis 故障切换不包含在核心通过结论内 |
| P1 事件事务 | PARTIAL | CAP 适配、元数据、两 ORM 分轮领域事件、同事务收据、提交诊断；显式取消失败重试已真实验证 | 剩余组合故障、生产活动资源排空及重试分类 |
| P2 顺序与库存 | PARTIAL | 入箱/序号/游标/Worker、两 ORM 库存/幂等/关键审计/出箱、真实双进程/1213/确认丢失/后台恢复 | 有序生产接线、授权恢复、CAP/ORM 保存点外状态、偶发库存错误观察 |
| P3 限流审计原子工具 | PARTIAL | 本地/Redis 令牌桶、HTTP 429/503/可信身份、审计管理器/关键事务审计/实体字段、Interlocked/CAS | 独立进程限流和 Redis 故障、自动属性快照、普通审计落库及保存预算 |
| P4 蓝绿与治理 | PARTIAL | 双平台模板、HTTP 就绪/排空、进程内指标 | 实际角色/预热/消费和任务排空、动态部署、全局指标/归档/长期资源测量 |

## 上轮子项最新结论

| 问题 | 当前状态 | 依据 |
| --- | --- | --- |
| FIX-031 取消测试编译 | COMPLETED | Options/Extensions 限定名已修正；新适配直接测试双框架各18/18，本轮整个工程各217/217 |
| FIX-029 共享取消文件及门控 | COMPLETED（测试入口与门控子项） | 两工程均链接共享文件；最新真实完整 EFCore34/34、FreeSQL33/33，各21必需方法 Passed |
| FIX-030 取消夹具安全清理 | COMPLETED（测试夹具） | 两套定向各12/12，含真实取消2项、清理9项及注册1项；不等于生产消费排空完成 |
| FIX-013 显式取消策略 | 已验收子项，整体PARTIAL | 同一 cap.received Id Failed→Succeeded、Retries>=1、单次原发布、业务/收据各一；保留原生0/2失败基线 |
| 有序事务状态协议 | 仅协议和直接行为验收 | 新增 IOrderedEventTransactionState 与协调器接入，本轮 SQLite直接5/5；无生产实现者，无CAP事务适配工厂 |

取消直接测试还覆盖已处理/未处理/改写异常的过滤器、调用令牌、派生类型/实例及注册幂等。无过滤器处理的 OCE 转普通失败，基础 AddCapEventBus 默认行为不变，两个 Admin 显式启用。消费取消实际业务使用直接 MySQL 事务，不能替代全部 ORM 工作单元取消路径。

## 结构化问题及功能 TODO

### FIX-006：有序生产接线、权限及 CAP/ORM 事务状态

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：组件和独立测试宿主已具备，两套实际 Admin 尚未注册 AddBingOrderedConsumer、Store/Factory/实际入箱业务订阅；没有授权查询/恢复和关键审计闭环。新增事务状态仅有测试实现，不能清理生产 CAP/ORM 状态。
- 证据：modules/admin/src 搜索 AddBingOrderedConsumer、IOrderedEventTransactionFactory、IOrderedEventRecoveryAuthorizer 无生产接线。EfCoreOrderedEventTransactionFactory.TransactionScope、FreeSqlOrderedEventTransactionFactory 仍使用原 IOrderedEventTransaction。OrderedEventCoordinator.cs 的保存点失败分支仅在可选状态实现存在时调用 ResetBusinessStateAsync；新工厂 CapOrderedEventTransactionFactory.cs 尚不存在。
- 影响：保存点只回滚数据库，不能据此宣称已清除 ORM 跟踪、CAP 待发送缓冲或内存事务动作。公开恢复若未经宿主租户授权不能安全暴露；未发现已暴露可利用恢复端点。
- 修复目标：稳定主题/组→持久化入箱→两 ORM 业务 Worker→事务提交；查询/重试/补偿授权与关键审计；派生 CAP 出箱同事务。
- 修复要求：明确物理事务唯一资源所有者，CAP 仅适配，不重复提交/释放；重置失败整笔回滚；数据库已提交后的发送错误保持 Committed；不自动跳号；真正异步 Begin/Reset 必须有可验证的 ambient 接线。
- 验证方式：两套真实 Admin 3→1→2、重复、前序失败/重启/多实例、跨租户拒绝、保存点回滚后派生出箱/ORM状态未泄漏、真实异步 Begin/Reset/Dispose。
- [ ] 实际宿主接入入箱订阅、Store、事务工厂及有业务修改的消费者。
- [ ] 接入租户授权查询、人工恢复/补偿及关键操作审计。
- [ ] 完成 CAP 有序事务适配和两 ORM 保存点外状态清理；验证提交/发送/释放错误的准确结果。

### FIX-007：库存偶发竞争失败的诊断收口

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL（观察与诊断，不是整组库存未实现）。
- 问题：真实库存竞争、1213有界重试、COMMIT确认丢失和后台恢复已有通过证据。原 FreeSQL 不同单竞争失败日志无 Number；之后一次 EFCore 同项失败明确 Number=1062、Phase=Execute、Outcome=RolledBack，具体冲突键及原因未定位。
- 证据：历史 inventory-process-cleanup 的失败 TRX；artifacts/admin-mysqlcap-results/cap-consumer-cancellation/net6.0-20261005T130619042Z/admin-efcore-mysqlcap.trx 为33通过/1失败。增强诊断后定向两套各17/17（16诊断直接案例+1实际竞争），随后完整34/33通过。
- 影响：后续通过不能说明原失败已修复；不能把1062推断为死锁，也不能将 CAP 原生 ID 生成器相关性猜测当成根因。
- 修复目标：有界复现/观察，保留错误类型、编号、阶段、事务结果、白名单键类别和方法调用链；再现后按真实冲突处理。
- 修复要求：不记录SQL/重复值/凭据；不盲改隔离级别、CAP WorkerId或扩大重试；不无限重复运行。
- 验证方式：保留每次原始TRX、安全诊断和再现条件；确认具体键后增加失败基线及最小修复。
- [ ] 收口历史无编号事件和新1062事件的诊断、复现条件及恢复流程；未定位保持明确记录。

### FIX-008：独立进程限流及完整 HTTP/Redis 故障链路

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：组件、HTTP适配和Admin注册已有；同进程两容器共享额度不能替代独立进程及真实Admin认证/代理链路。
- 证据：RedisTokenBucketRateLimiter、BingRateLimitPipelineTest、AdminRateLimitingRegistrationTest；未发现限流独立进程探针。
- 影响：集群额度、跨租户/路由隔离和基础设施故障策略未完整验收。
- 修复目标：独立双进程共同消耗额度，完整HTTP安全身份和故障恢复。
- 修复要求：默认故障拒绝，显式降级；可信代理保护匿名IP；请求取消不盲目退款。
- 验证方式：真实Redis及HTTP429/Retry-After、503、断连/恢复、显式降级、取消、键回收、伪造头和身份隔离。
- [ ] 两进程共享额度与完整 Admin HTTP/认证/可信代理集成测试。
- [ ] Redis断连、恢复、显式降级及取消后的额度行为验收。

### FIX-009：自动审计快照、普通落库及保存预算

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：已有实体审计字段和显式关键事务审计，尚无两ORM属性级自动快照，普通HTTP审计默认仅日志存储，保存缺少预算/取消接线。
- 证据：EfCoreAuditingExtensions/FreeSqlAuditingExtensions 仅提供 SaveTransactionalAuditAsync；AuditingRegistrationExtensions 默认 SimpleLogAuditingStore；Admin库存只注册 ITransactionalAuditingStore；BingHttpAuditingMiddleware.cs:111 调用 SaveAsync()。
- 影响：手填 EntityChanges 不等于自动采集；日志不等于普通审计可靠落库；慢保存可能拖延请求和关闭。
- 修复目标：可选自动变更快照、普通 IAuditingStore 数据库配置/授权查询、异步保存预算。
- 修复要求：白名单/脱敏/长度上限；普通失败告警，关键失败回滚；等待超时不能提前释放在途存储。
- 验证方式：两ORM新增/修改/删除、实际落库与安全查询、敏感字段、慢存储/取消、保存/释放组合异常。
- [ ] EF ChangeTracker 与 FreeSQL 自动属性变化快照。
- [ ] 普通审计数据库存储、生产配置和授权查询。
- [ ] 保存预算/取消及慢存储资源安全清理。

### FIX-011：蓝绿实际角色、预热、完整排空与部署

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：BING_HOST_ROLE/消费开关只在模板，两个Startup仍注册CapModule，EF仍注册HangfireModule；AppModule装配完成即就绪，没有真实依赖预热门槛。现有计数仅HTTP_next范围。
- 证据：两Startup.cs:41、EF:45；两AppModule.cs:148/127；deploy/bluegreen/README.md明确模板暂不能安全动态切流。
- 影响：绿版预热可能提前参与生产消费/任务，HTTP计数清零不能证明所有资源已排空。
- 修复目标：Web/Consumer/Task实际角色、依赖预热、消费/任务/请求资源排空、双平台切换回滚验收。
- 修复要求：稳定逻辑组与幂等键；绿版消费关闭；停止等待与清理完成分开，超时保留活动资源。
- 验证方式：Compose/Nginx和Kubernetes分别覆盖长请求、实际消费/任务、未就绪、切换/回滚和强停恢复。
- [ ] 实际识别角色及消费/任务开关。
- [ ] 真实依赖预热和完整请求/消费/任务排空。
- [ ] 两平台动态部署验收，不能仅用模板静态检查。

### FIX-012：全局诊断、留存、长期资源与交付证据

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：Worker当前记录扫描页 streams.Count，不是全局积压；缺最老年龄、指标导出、归档和长期集群资源测量。指南已补取消迁移和真实1213/确认丢失，execution.md仍止于上个32/31批次，未记录最新34/33和中间1062失败。
- 证据：OrderedEventConsumer.cs:153；ModuleResourceProbe仅模块生命周期；本轮核对execution.md、TRX和新协议直接测试，没有新状态协议的符号映射与执行记录。
- 影响：不能用页大小监控总积压，用模块弱引用宣称集群无泄漏，或用旧报告替代当前验证。
- 修复目标：全局指标/最老年龄、导出与授权诊断、留存/归档、长期测量及交付对齐。
- 修复要求：有限标签、告警节流；未处理消息不可按年龄删除，幂等留存覆盖重放；未定预算时只报告数据。
- 验证方式：多页积压/年龄、导出/安全诊断、归档重放、预热与持续循环的分配/堆/GC/存活对象/延迟。
- [ ] 全局积压/最老年龄、指标导出、授权诊断与告警节流。
- [ ] 审计/收据/消息归档、重放与未完成数据保护。
- [ ] 长期集群资源探针及README/指南/符号映射/执行报告同步。

### FIX-013：剩余组合故障及生产活动资源清理

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：显式取消适配、真实确认丢失和原生后台出箱恢复已验收，仍未覆盖全部提交/Flush/释放组合故障及生产活动消费排空。
- 证据：双ORM取消各2真实用例通过；当前完整34/33。现有提交前强杀暂停点在审计后/出箱前，不证明出箱已写后的强杀；测试CapCancellationCleanup不等于生产消费资源跟踪。
- 影响：数据库提交与CAP内存调度、调用方取消与实际清理完成不能混用一个成功状态。
- 修复目标：准确保留原异常、阶段、物理提交结果及清理错误；不释放仍活动的资源。
- 修复要求：所有逃出OCE转换失败为当前明确策略，业务取消/宿主令牌/超时/过滤器不同分层；不手工重发或修改出箱状态制造验收。
- 验证方式：出箱实际已写后的提交前强杀、提交后发送/Dispose、回滚+释放多失败；不响应取消时仍持有资源，退出后才完成清理。
- [ ] 补出箱已写后的强杀、提交后发送/释放、回滚/释放组合故障矩阵。
- [ ] 生产在途消费/任务资源清理与超时后继续等待/恢复流程，与FIX-011共同验收。

## 欠缺考虑或尚未充分验收的边界 TODO

以下与工作包重叠，不另计作19组新功能；除明确代码行为外，不能表述为已复现漏洞。

- [ ] 原请求恢复可信度：权威写库按原请求身份和内容查询；只读副本null/超时不证明未提交，Unknown不换ID重做。
- [ ] 租户权限来源：同一ClaimsIdentity不替代授权，消息头/正文/查询/恢复独立校验；明确多认证scheme策略。
- [ ] 流起点与代次：新消费者起点、过滤形成缺口、实体重建、契约升级和重放；序号面向订阅方完整可见流。
- [ ] 保存点外状态：ORM跟踪、FreeSQL变更报告、CAP缓冲、内存回调及外部HTTP/邮件分别定义清理/出箱/幂等。
- [ ] 异步ambient传播：CAP8.0.1直接AsyncLocal值，异步Begin/Reset里赋值不回流调用方；需显式可验证的同步激活/解绑协议，不能依赖恰好同步完成或失败残留占位事务。
- [ ] CAP发送完成语义：本地正式包IL显示Flush调用dispatcher后丢弃返回ValueTask；物理提交、入队请求、异步发送完成和后台扫描恢复分别取证，不以Flush返回证明全部发送完成。
- [ ] 公平与背压：逐实体串行Worker的慢handler会延迟其他实体；有界公平并发、全局入箱容量和重试总预算。
- [ ] 排空完整性：作用域异步清理、OnCompleted、SSE/WebSocket、脱离请求任务、CAP/定时任务都需覆盖，超时不提前释放。
- [ ] Redis运行边界：租期/主从切换不保证绝对独占；热Hash/HGETALL延迟、分桶容量和策略/键升级需量化。
- [ ] 限流取消后的额度：脚本可能已扣额度，取消不撤销Redis写入；不能盲目退款扩大可用额度。
- [ ] 迁移与协调恢复：CREATE TABLE IF NOT EXISTS不会升级旧表；身份/索引回填、新旧版本混跑及数据库/RabbitMQ恢复需演练。
- [ ] 留存与重放：CAP清理不替代业务幂等，正文冲突检测、归档和重放期限对齐，未处理数据不能按年龄删除。
- [ ] 安全交付：代理ACL、Dashboard/诊断授权、秘密日志扫描和旧凭据轮换，默认不公开消息正文。
- [ ] 故障证据精度：READY/GO不等于事务重叠；协作取消不等于指定点强杀；无TLS loopback断连不证明生产TLS/主从切换。
- [ ] CAP取消分层：主动OCE、调用/宿主令牌、DB提交结果、CAP持久化重试和RabbitMQ重新入队分别验收；转换策略不能代替重试上限及人工处置。
- [ ] 消费身份精确比较：收据接受尾空格，MySQL文本唯一键与BINARY复核存在规则不一致风险；补尾空格/Unicode/字节长度真实测试，当前未直接复现。
- [ ] 保留资源后的恢复：停止等待超时所保留的GUID数据/资源拥有者，需实际停止后的再清理与证据保留流程。
- [ ] 库存事件根因证据：1062需定位具体唯一键，不猜测等同业务重复或CAP ID冲突；无编号历史事件不能恢复成确定根因。
- [ ] 库存后续产品能力（本期范围外）：预占/撤销/返还/部分发货独立补偿契约，不把换ID当Unknown恢复。

## 验证证据与限制

| 验证 | 实际结果 | 来源与边界 |
| --- | --- | --- |
| 本轮新事务状态定向net6 | 5/5，0失败/跳过 | artifacts/cluster-current-state-review-20261005/ordered-state-current-net6.trx；真实SQLite+测试状态适配器，不是CAP/ORM接入 |
| 本轮EventBus全量net6/net8 | 各217/217，0失败/跳过 | 同目录eventbus-current-net6.trx、eventbus-current-net8.trx；新增状态5项包含在全量中 |
| 上轮取消适配直接net6/net8 | 各18/18 | artifacts/cap-consumer-cancellation-fix/cap-adapter-direct-final-net6.trx、net8.trx |
| 上轮双ORM取消/清理/注册定向 | 各12/12 | cap-cancel-ef-after-net6.trx、cap-cancel-freesql-after-net6.trx；各2实际取消+9清理+1注册 |
| 上轮Admin最新完整门控 | EFCore34/34、FreeSQL33/33，各21必需方法，0失败/跳过 | artifacts/admin-mysqlcap-results/cap-consumer-cancellation-diagnostics/net6.0-20261005T132336032Z/；先于新状态协议，未在本轮重跑 |
| 上轮中间库存失败 | EFCore33通过/1失败 | cap-consumer-cancellation/net6.0-20261005T130619042Z/；1062/Execute/RolledBack，保留证据 |
| 上轮库存安全诊断和实际竞争 | 双ORM各17/17 | cap-gate-inventory-diagnostic-ef-net6.trx、freesql-net6.trx；16直接诊断+1实际竞争，不能说明历史根因已修复 |
| 原生1213 / 确认丢失 / 后台出箱恢复 | 双ORM分别各2/2、2/2、1/1（已有记录） | artifacts/inventory-real-deadlock-fix/；不代替剩余组合故障 |
| CI校验器自测 | 已有通过记录：21方法夹具、154拒绝断言 | artifacts/cluster-current-audit-20261005/ci-current-selftest-final.log；非业务通过数 |
| MVC net6/net8 | 各128 Passed、1 NotExecuted、0 Failed（已有记录） | audit-selected-identity-fix；UploadRawAsync既有跳过，本轮未重跑 |
| Sample Lazy业务/StopAsync/端口释放 | 1/1（已有记录） | inventory-process-fix/sample-http-stop-net6.trx；本轮未重跑，不是蓝绿验证 |

本轮实际 dotnet --version 为8.0.425，global.json仍锁定8.0.424，未修改。未重新执行全套Core/AOP/DDD/ORM/Admin/Sample，不将这些历史记录标成当前源码全仓库通过。NU1900及现存依赖/编译警告不直接证明可利用漏洞。

- [ ] 环境验收：使用锁定SDK8.0.424；实际425测试与锁定版本分开记录。
- [ ] 部署验收：Compose/Nginx及Kubernetes动态演练，先补实际角色/预热，不能全部归因于环境缺失。
- [ ] 旧FreeSQL通用异步查询同步回退为独立历史任务，不混入本次七组计数。

建议执行顺序：FIX-006（有序CAP/ORM状态、实际业务接线及授权）→FIX-008/009→FIX-011→FIX-012；FIX-013按相关事务/关闭改动补故障验收，FIX-007仅做有界诊断观察。FIX-031/029/030不再作为未修能力重复列出。

本轮只生成审查报告和验证产物，未修改业务/测试/计划/执行记录，未提交、推送或发送外部通知。
