<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T10:36:50.407702+00:00

# 集群能力当前审查与剩余 TODO

## 范围与结论

对照用户批准的完整 P0–P4 计划、plan.md、execution.md、暂存/未暂存变更、未跟踪的实际源码、测试、部署文件及最新 TRX 进行审查。仍有 **7 组未完整验收的工作包**：FIX-006、007、008、009、011、012、013。它们大部分已有实现；差距包括实际宿主接入、功能边界与真实环境验收，不能解释为七项功能完全不存在，也不按测试总数计算完成率。

本轮不修改业务代码、测试、plan.md 或 execution.md，不重新运行 dotnet 或部署环境。执行了相关 Git 空白检查及 Admin CI 校验脚本自测，均退出0；独立证据盘点核查最新结果和源码修改时间。旧审查原文保存在 review-before-latest-progress-audit-20261005.md，历史待办不代表当前状态。

## 阶段矩阵

| 阶段 | 状态 | 已实现 | 剩余关键差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（已有实施及历史证据） | 本地异步锁/所有者/引用回收、Redis 原子获取和释放/完整租期、本地事件快照 | Redis 故障切换与持续负载不因核心通过而自动验收 |
| P1 事件事务 | PARTIAL | CAP 适配、可选元数据、两 ORM 事件派发与有限轮次、同事务幂等收据、事务结果诊断 | 真实 CAP 取消确认/重投、提交不确定性与外部副作用恢复 |
| P2 顺序与库存 | PARTIAL | 入箱/游标/序号/保存点失败标记、Worker、两 ORM 工厂；整单条件扣减/幂等/审计/出箱、独立作用域重试、库存跨进程用例 | 有序生产接线/授权恢复、真实死锁与提交确认丢失、偶发失败根因 |
| P3 限流审计原子工具 | PARTIAL | 本地/Redis 令牌桶、HTTP 429/503、可信身份隔离、HTTP 审计/安全投影/事务审计、实体审计字段、Interlocked/CAS 闸门 | 完整 Admin HTTP/Redis 故障、属性级自动快照、普通审计生产存储、预算 |
| P4 蓝绿诊断 | PARTIAL | 两平台模板与切流脚本、HTTP 就绪/排空、有序进程内指标 | 实际角色/预热/消费任务排空、两平台动态演练、全局指标/归档/长期测量 |

本地事件不跨节点、CAP 至少一次、强杀不保证托管清理、数据库内部仍有锁，是明确设计边界，不作为缺失的跨节点本地广播、恰好一次投递或全系统无锁能力。

## 本轮确认已完成的子项

- [x] FIX-026：正常业务结果、恢复结果和资源清理结果分别表达；恢复不吞掉清理失败；释放完成后才查询或重试。
- [x] FIX-028：自动库存重试与恢复共用清理错误判断；事务释放/回滚、作用域释放及聚合错误均阻止自动重试。源码：AdminInventoryOrderExecutor.cs:176、195。
- [x] FIX-027：HTTP 审计只选一次首个已认证身份，四种声明不跨身份补齐；缺失保留 null。源码：BingHttpAuditingMiddleware.cs:185、224。
- [x] 两 ORM 四个真实库存跨进程用例有通过证据：同单、不同单、提交前中断、返回结果后中断；核对库存、整单、关键审计与出箱数量。
- [x] 探针停止/释放异常不遮蔽业务原异常，清理继续且只选择一个释放路径；新增四项直接测试，两套执行器加清理定向各22/22。
- [x] CI 两套各14个必需方法，最低数量27/26；缺方法、非Passed、无关通过补数均拒绝。自测本轮再次通过。

这些子项的完成不关闭其所属完整工作包。人工驱动异常编号不证明真实死锁，返回结果后强杀不证明 COMMIT 确认丢失。

## 结构化剩余问题

### FIX-007：库存真实竞争、死锁与恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：独立进程正常竞争已有通过证据，真实1213和网络Unknown未验收；首次FreeSQL进程竞争失败根因未定位。
- 证据：AdminInventoryOrderExecutor.cs:176只对原生1213、RolledBack且无清理错误重试；AdminInventoryOrderExecutorTest通过反射构造真实驱动异常，并非制造InnoDB死锁。AdminInventoryProcessCases.cs:25、48以READY/GO同步独立PID。早期完整TRX的不同单竞争出现MySqlException，后续全门控和有限重复通过。
- 影响：不能证明真实驱动包装、事务回滚与清理顺序均满足重试条件，也不能用重跑通过关闭稳定性问题。
- 修复目标：可重复真实锁竞争/死锁证据，明确原请求恢复规则，定位偶发失败。
- 修复要求：保持最多四次、每轮新作用域、先等待清理；查询使用权威写库、原身份和原内容，null不证明回滚。
- 验证方式：两ORM真实MySQL死锁，断言尝试次数、旧作用域释放、库存/审计/出箱不重复；记录安全错误编号、阶段与事务结果。
- [ ] 定位首次FreeSQL库存进程竞争失败，不以重复通过代替修复结论。
- [ ] 增加可观测争锁/事务重叠证据与真实1213整单重试验收。
- [ ] 在最终探针清理改动后顺序重跑完整Admin MySQL/CAP门控。

### FIX-013：提交不确定性、取消与清理故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：恢复和清理直接契约已补，但缺真实COMMIT确认丢失、CAP取消确认/重投及真实组合故障。
- 证据：AdminInventoryProcessProbe.cs:76的before-commit暂停在审计后、CAP出箱前；:110的after-commit是在Executor已返回后暂停。各22/22直接测试不执行真实网络确认丢失。
- 影响：不能将已提交状态和调用方收到结果、请求取消和实际结束、业务成功和资源清理成功视为同一状态。
- 修复目标：真实故障点覆盖，恢复不重复扣减且不隐藏错误。
- 修复要求：不提前释放活动资源，不用新请求ID盲目重做；原始异常与清理异常均保留。
- 验证方式：真实数据库确认丢失/断连、出箱已写后的提交前中断、提交后Flush/Dispose失败、回滚与释放同时失败、实际CAP取消后确认/重投。
- [ ] 补上述真实故障矩阵并核对数据库、出箱、收据和资源次数。
- [ ] 明确不响应取消时的等待、告警和恢复状态，不能报已清理或已卸载。

### FIX-006：有序消费实际宿主接入与安全恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：组件及测试宿主已有，但两个Admin源码未注册AddBingOrderedConsumer，没有生产有序订阅和恢复权限闭环。
- 证据：OrderedEventConsumer.cs:43提供Worker注册；注册不创建CAP订阅。OrderedEventCoordinator.cs:258仅遍历已注册授权器，无授权器时保留兼容调用；GetStateAsync没有宿主租户权限。当前基础事务工厂不自动绑定CAP派生发布。
- 影响：未证明生产消息等待入箱后确认，或人工重试/查询受租户授权及关键审计保护。
- 修复目标：显式主题、稳定消费者组、入箱订阅、两ORM Store/Factory与Worker接线；受控查询/重试/补偿。
- 修复要求：FreeSQL从FreeSqlWrapper.Orm取实际实例；派生出箱同事务；保存点失败后丢弃ORM/CAP/回调内存状态，禁止自动跳号。
- 验证方式：真实Admin端到端、有序3→1→2/重复/前序失败/重启、多实例、授权拒绝、派生消息回滚与清理。
- [ ] 接入生产订阅→持久化入箱→后台业务处理。
- [ ] 实现宿主租户授权、查询/恢复关键审计及派生出箱事务适配。
- [ ] 验证SQL保存点外的ORM跟踪、CAP缓冲和回调不会提交已回滚业务。

### FIX-008：集群限流真实链路与故障验收

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：令牌桶及HTTP组件已接线，当前额度测试不能代替跨进程和完整Admin认证/代理验证。
- 证据：两AppModule按Enabled注册/执行限流；此前IndependentProviders为同进程两容器。当前源码未发现限流独立进程探针。身份与最小摘要长度问题已修，不重复列待办。
- 影响：不能确认集群额度、主体隔离和Redis故障下实际响应策略。
- 修复目标：真实HTTP和跨进程额度/故障验收。
- 修复要求：可信代理与同一认证身份取声明，默认故障拒绝且显式降级；不以实例数量扩大额度。
- 验证方式：两进程共享额度、Redis断连/恢复、429/Retry-After和基础设施503、租户与路由隔离、伪造代理头、取消及键回收。
- [ ] 补完整Admin HTTP验证及独立进程共享Redis额度。
- [ ] 补Redis断连/恢复和显式降级策略的真实测试。

### FIX-009：自动变更审计与普通请求生产存储

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：实体审计字段、手填EntityChanges和关键库存审计已有，自动属性快照与普通HTTP可靠生产落库尚未接入。
- 证据：EfCoreAuditingExtensions/FreeSqlAuditingExtensions只提供SaveTransactionalAuditAsync；默认AuditingRegistrationExtensions.cs:20为SimpleLogAuditingStore，AdminInventory注册仅ITransactionalAuditingStore。HTTP中间件:111保存无取消/预算参数。
- 影响：不能宣称完整自动变更审计；普通日志输出不等于可靠数据库审计。
- 修复目标：显式自动快照、普通数据库审计配置、安全查询及保存预算。
- 修复要求：白名单/脱敏/长度限制；普通失败告警、关键失败回滚；不在保存仍执行时释放相关资源。
- 验证方式：两ORM新增/修改/删除快照，真实持久化和权限，敏感字段，慢保存/取消、多重异常和资源次数。
- [ ] 实现可选EF ChangeTracker及FreeSQL属性级快照。
- [ ] 接入普通IAuditingStore生产数据库配置及查询权限。
- [ ] 补异步保存预算/取消策略与真实故障验收。

### FIX-011：蓝绿角色、预热与完整排空

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题：环境变量仅出现在模板/文档，宿主未使用；双平台没有动态验收证据。
- 证据：两个Startup.cs:41仍无条件AddModule<CapModule>，EF宿主:45还注册HangfireModule；AppModule:148/127管道装配后MarkBingTrafficReady，没有依赖预热门槛。TrafficGateMiddleware统计_next调用，不覆盖所有后台资源。
- 影响：HTTP绿版预热可能仍参与消费/任务；仅HTTP切流不能保证后台排空或安全回滚。
- 修复目标：真实Web/Consumer/Task角色、依赖预热和各类在途任务统计，双平台验收。
- 修复要求：稳定逻辑组，不按版本改组；排空超时明确失败，恢复依据事务/出箱/幂等，不承诺强杀清理。
- 验证方式：Compose/Nginx和Kubernetes分别验证长请求、活动消费、任务、绿版不就绪、切换/回滚/强停恢复。
- [ ] 宿主实际识别角色/消费开关，预热期间禁止生产消费与任务。
- [ ] 接入依赖预热、消费/任务/请求资源排空。
- [ ] 完成两套平台动态演练及证据留存。

### FIX-012：全局诊断、容量、留存与交付

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题：当前指标是进程内及最近扫描页数据；归档和集群组件长期资源证据不完整，部分指南保留过时门控数量。
- 证据：OrderedEventConsumer.cs:153将streams.Count写入PendingStreams；Metrics文档已明确最近扫描，不应当作全局积压。未发现集群组件总分配/GC探针；现有ModuleResourceProbe只覆盖模块。ordered-events.md仍写原16/15等批次与旧门控组合，当前门控为27/26、14个必需方法/套。
- 影响：不能据此计算真实积压/年龄或容量，也不能把模块资源测试当持续集群运行无泄漏证明。
- 修复目标：全局积压/最老年龄、有限标签指标导出、授权诊断、归档/保留、长期资源测量及证据收口。
- 修复要求：未处理消息不得按年龄删除，幂等保留覆盖重放；没有容量预算时仅报告测量结果。
- 验证方式：多页积压/最老年龄，授权/安全日志/告警节流，归档不破坏去重与重放，持续循环测量分配、堆、GC、存活对象、延迟及追赶速度。
- [ ] 补全局指标与导出、授权诊断和节流告警。
- [ ] 设计并验收审计/收据/消息留存归档及未完成数据保护。
- [ ] 补集群长期资源探针，更新指南、执行记录、符号映射和最新结果的覆盖边界。

## 此前欠缺考虑或尚未充分验收的边界 TODO

下列并非全部新发现，也不全部属于已复现漏洞；与上述工作包重叠的内容不重复计数。

- [ ] 恢复可信度：Executor信任recoverAsync返回值，InventoryOrderResult不包含请求身份；优先使用FindAsync核对原请求内容，明确调用方验证责任。只读副本null、超时/取消都不证明原操作未提交。
- [ ] 身份与权限分层：同一ClaimsIdentity只是数据来源一致，不替代租户访问权限；消息头/业务正文租户、恢复和审计查询均需授权。多认证scheme聚合规则由宿主明确。
- [ ] 流起点/代次：新消费者从0开始、过滤掉的序号、实体删除重建、契约升级、重放及毒消息补偿；完整可见流是连续序号成立的前提。
- [ ] 保存点外状态：ORM跟踪、FreeSQL变更报告、CAP缓冲、内存事务动作和HTTP/邮件不随SQL回滚；派生消息及外部副作用需明确原子性/幂等边界。
- [ ] 公平与背压：Worker逐实体串行，慢handler仍阻塞其他实体；设计有界公平并发和DB/CAP总重试预算，避免多层重试放大。
- [ ] 排空覆盖：请求作用域异步释放、OnCompleted、SSE/WebSocket、脱离请求的任务、CAP及定时任务分别统计；HTTP计数归零不证明进程全部完成。
- [ ] Redis运行边界：租期过期/主从切换不保证绝对互斥；政策/主体键升级会重置或分裂额度；单Hash HGETALL回收会影响Redis线程，测量热键和最大分桶延迟。
- [ ] 迁移与恢复：CREATE TABLE IF NOT EXISTS不升级旧表，验证身份/索引回填、维护窗口、备份、新旧实例兼容、数据库/RabbitMQ恢复协调；破坏性DDL不承诺一键回滚。
- [ ] 留存与重放：CAP消息表清理不代替业务幂等记录保留；完成正文冲突检测与归档策略协调，未处理数据保护和容量上限需明确。
- [ ] 安全交付：代理ACL、诊断/Dashboard权限、日志秘密扫描与旧凭据轮换；授权接口默认不显示消息正文。
- [ ] 测试故障点：独立PID和READY/GO不必然证明事务重叠；审计后暂停不等于CAP已写，ExecuteAsync返回后强杀不等于COMMIT确认丢失。
- [ ] 库存后续产品能力（非本期新增要求）：预占、撤销、返还、部分发货及不足后的新尝试使用独立业务身份/补偿规范，不把换ID当Unknown恢复。

## 验证证据与实际限制

最近实施证据，不是本轮重新运行的dotnet结果；定向与全量重叠，不相加。

| 验证 | 结果 | 实际证据 |
| --- | --- | --- |
| 最终执行器及探针清理net6 | EFCore/FreeSQL各22/22 | artifacts/inventory-executor-cleanup-fix/executor-probe-ef-final-net6.trx、executor-probe-freesql-final-net6.trx |
| 审计身份net6/net8 | 各24/24 | artifacts/audit-selected-identity-fix/audit-identity-after-net6.trx、audit-identity-after-net8.trx |
| MVC全量net6/net8 | 各128 Passed、1 UploadRawAsync NotExecuted、0 Failed | artifacts/audit-selected-identity-fix/mvc-regression-net6.trx、mvc-regression-net8.trx |
| 完整真实Admin MySQL/CAP/RabbitMQ net6 | EFCore27/27、FreeSQL26/26 | artifacts/admin-mysqlcap-results/inventory-process-diagnostics/net6.0-20261005T101117620Z/ |
| Sample业务Lazy、StopAsync与端口释放net6 | 1/1 | artifacts/inventory-process-fix/sample-http-stop-net6.trx |
| WebApi构建net6 | 0错误、2警告 | artifacts/cluster-current-audit-20261005/webapi-build-inventory-audit.log |
| CI校验脚本 | 本轮退出0 | eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1；两套14方法及56拒绝断言 |

完整门控2026-10-05北京时间18:12结束，早于最终探针清理改动18:18及清理测试18:22；之后各22/22只覆盖直接契约。不能宣称最新探针完整集成已验收。早期FreeSQL25通过/1失败保留于inventory-process-cleanup目录，后续全通过不解释根因。

- [ ] 环境：锁定SDK8.0.424验收。当前安装及实际日志为8.0.425，global.json未修改；不将环境缺失写为代码失败。
- [ ] 环境：真实双平台蓝绿运行验收尚缺证据；MySQL/RabbitMQ已有专用测试环境，不再泛称CAP无环境。

本轮未重跑全部Core、AOP、DDD、ORM或Admin完整HTTP回归；NU1900在线漏洞元数据及既有Microsoft.Extensions10/net6警告不能据此推断存在可利用漏洞。旧FreeSQL通用异步查询同步回退是独立任务，不混入七组统计。

优先顺序：FIX-007/013（真实死锁、偶发失败及提交故障）→ FIX-006（生产有序闭环）→ FIX-008/009 → FIX-011 → FIX-012。整体继续NEEDS_FIX；本轮没有修改业务源码、提交、推送或发送外部通知。
