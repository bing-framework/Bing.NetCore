<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T02:43:34Z

# 集群能力与运行治理差距复核

## 范围与结论

对照会话批准的完整 P0–P4 计划、plan.md、execution.md、当前 Git 改动、生产调用链、测试源码及 TRX 复核。本轮只审查并写报告，没有修改业务代码、测试、计划或执行报告，没有重新运行测试、构建、容器或切换流量。上一版保存在 review-before-capability-audit-20261005.md。

仍有 **7 组原计划待交付工作包**：FIX-006、007、008、009、011、012、013。大多已有实现，完整接线、故障处理或验收尚未完成，不代表七项功能完全没有，不能据测试数量计算完成百分比。另发现 **4 项接入和故障路径问题**：FIX-014～017。

FIX-001～005 保留已解决核心结论。FIX-010 已落到 HTTP 流量闸门，并接入两 Admin，不再要求重复创建通用无锁 API；并发、终态、资源验证纳入 FIX-011/012，排空结果另列 FIX-016。

| 阶段 | 状态 | 实现与剩余 |
| --- | --- | --- |
| P0 | PASS（核心历史证据） | 本地/Redis 锁、所有者释放、完整租期、键回收及订阅快照已有；本轮未重跑。故障切换/长期负载另验。 |
| P1 | PARTIAL | 元数据、两 ORM 领域事件/出箱/收据、提交诊断及 CAP 安全关闭已有；取消确认、提交断连和 Unknown 恢复待验。 |
| P2 | PARTIAL | 有序存储/游标/保存点/后台处理、两 ORM 真实 CAP 测试；库存条件更新/幂等/固定排序/ORM 适配已有。生产恢复、完整库存事务及 MySQL 双进程仍缺。 |
| P3 | PARTIAL | 限流可信身份/路由、Admin 接线、真实 Redis 配额；HTTP/数据库审计、ORM 适配、实体字段和 CAS 计数已有。审计接入需修，自动快照/业务组合待补。 |
| P4 | PARTIAL | 双平台模板及 Admin HTTP 闸门/就绪路径已有；角色分离、实际预热、消费/任务排空、动态切流回滚仍缺。 |

不再列为完全缺失：HTTP 审计、DbAuditingStore、SaveTransactionalAuditAsync、FreeSQL 审计字段、库存存储/适配、Admin 限流、恢复授权扩展、有序指标、用途驱动 CAS。EFCore CAP 包装事务、七项入箱重试、历史死锁后的回归、Sample Lazy 实际解析/正常停止也有后续通过证据。

## 原计划剩余 TODO

### FIX-006：有序生产接入与恢复

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：Coordinator/Publisher/Consumer、两 ORM 工厂和真实 CAP 测试宿主已有；两个 Admin 没有 AddBingOrderedConsumer/生产恢复入口。RetryAsync 授权器可选，GetStateAsync 没有宿主授权策略。
- 影响：组件不能证明生产订阅等待入箱、正确事务或跨租户恢复安全。
- 修复目标/要求：可选生产接入、订阅等待 ReceiveAsync、状态/重试/补偿授权及关键审计，派生 CAP 消息同事务，失败丢弃上下文。
- 验证：AspectCore 宿主端到端、前序失败竞争、发布中断/断连/取消确认、慢处理器和安全停止。

### FIX-007：完整库存一致性业务

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：基础扣减、固定排序、两 ORM SQLite 适配已有；未见库存+业务请求结果+关键审计+CAP 出箱完整服务和 MySQL 双进程库存测试。
- 影响：局部条件更新不证明整单幂等、防超卖与批次原子性。
- 修复目标/要求：上述记录同事务；整单请求和首次结果；仅确认回滚的整笔业务有界死锁重试，Unknown 不重做。
- 验证：两 ORM MySQL 并发/重复/余额不足/取消重试、多 SKU 回滚、提交中断；库存非负和扣减/审计/出箱次数断言。

### FIX-008：完整限流宿主与 Redis 故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：键、可信身份/代理/路由、429/503/降级、Admin 配置已有；证据为 TestServer、Admin 注册、同进程双 DI 容器 Redis。缺完整 Admin HTTP、跨进程和故障切换。
- 影响：接线不等于真实 JWT/代理/Redis 故障已验。
- 修复目标/要求：两 Admin 组合链路、跨进程额度、断连恢复与蓝绿参数协调。
- 验证：伪造代理头、租户/路由、Retry-After、503/降级、取消及恢复额度/键回收。

### FIX-009：自动审计与关键事务

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：HTTP、数据库安全投影/白名单/上限/所有权、ORM 显式适配已有。EntityChanges 仍手工填写；Admin 默认日志存储；关键测试靠调用方回滚。HTTP 有 FIX-014/015。
- 影响：不能宣称自动关键审计保证；字段填充不等于操作日志。
- 修复目标/要求：修 HTTP，可选 EF ChangeTracker / FreeSQL 快照，完整关键业务及数据库配置；审计失败不可继续提交。
- 验证：MySQL 实际落库、库存/收据/审计/CAP 原子提交回滚、敏感值排除、执行中取消及多重清理错误。
- 迁移：ChangeType 从错误的 EntityChangeInfo 改为 EntityChangeType 属源码/二进制变化，指南已要求重编译，缺旧消费者/发布检查证据。

### FIX-010：原子工具

- 状态：RESOLVED（核心实现），不计独立缺失能力。
- 证据：BingTrafficGate CAS 状态、Interlocked 在途计数及一次释放句柄在两 Admin 使用，七项组件通过。
- 后续：高竞争/真实长请求纳入 FIX-011/012，必须修 FIX-016；不另造通用无锁库。

### FIX-011：蓝绿角色和动态验收

- 严重程度：HIGH；处理要求：MUST_FIX；状态：PARTIAL。
- 问题/证据：Admin 管道末尾 MarkReady 不验证依赖/预热/CAP 订阅。两个 Startup 无条件注册 CapModule，不识别 BING_HOST_ROLE / BING_CAP_CONSUMER_ENABLED。Live 脚本只检查配置/就绪/选择器。
- 影响：绿版 Web 可能消费，模板不能直接安全发布。
- 修复目标/要求：Web/消费者/定时任务分离，Web 可出箱不订阅，稳定组名；真实预热后就绪，消息/HTTP 分别摘流排空，明确超时，修 FIX-016。
- 验证：Compose/Nginx 和 Kubernetes 分别长请求、活动消费、绿版不就绪、切流回滚及强停恢复；镜像/config/请求消息 ID 证据。

### FIX-012：指标、容量、归档和交付

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：有序指标存在，PendingStreams 是分页数；缺完整年龄/积压/限流/审计/排空导出、持续 GC/负载和归档。新审计映射及执行报告尚未补。
- 影响：长期内存/磁盘及真实积压难评估，交付不完整。
- 修复目标/要求：修 FIX-017，有界指标/节流/授权诊断、总积压与年龄、留存归档、最新映射/报告/文档。
- 验证：持续分配/存活对象/GC/延迟/积压及后台回收；无容量预算时只报告测量，不自定 MB/QPS 门槛。

### FIX-013：Unknown 与真实故障

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：PARTIAL。
- 问题/证据：诊断、ORM 清理、CAP 唯一关闭及后续门控/Sample 已有；缺真实提交确认丢失、提交后 Flush/Dispose 故障、Unknown 查询补偿。SIGNAL 错误不等于断连。
- 影响：异常/等待失败不能判断未提交或可重做。
- 修复目标/要求：故障注入、幂等结果查询恢复、取消确认/重投及仍清理状态。
- 验证：业务/出箱状态及次数，清理双失败/回滚失败原异常，不提前释放；最新审计后受影响回归。

## 新增实现问题

### FIX-014：HTTP 审计捕获根作用域管理器

- 严重程度：HIGH；处理要求：MUST_FIX；状态：OPEN_ACTIONABLE。
- 证据：BingHttpAuditingMiddleware.cs:20、31 构造函数保存 IAuditingManager，入口是常规 UseMiddleware；AuditingRegistrationExtensions.cs:19 为 TryAddScoped。已有测试手动 new，无真实审计管道/ValidateScopes。
- 问题/影响：常规中间件由根 provider 构造，Microsoft DI 作用域校验会拒绝 Scoped；关闭校验/容器允许时捕获根实例，与业务请求管理器不同，业务无法通过自己的管理器加入 HTTP Current。
- 修复目标/要求：InvokeAsync 请求级参数或作用域 IMiddleware，禁止根解析捕获。
- 验证：ValidateScopes=true TestServer，业务同实例/Current 可见、并发不串用；AspectCore 实际请求。源码链路确定，本轮未新增运行复现。

### FIX-015：敏感查询输出和身份缺失

- 严重程度：HIGH；处理要求：MUST_FIX；状态：OPEN_ACTIONABLE。
- 证据：HTTP 审计 :109 拼 QueryString，:97 保存失败日志输出 URL；:113 固定 NameIdentifier、不填 TenantId，Admin 使用 sub。
- 问题/影响：token/password 查询可能泄入存储/错误日志，截断非脱敏；数据库投影排除 URL 无法保护此日志。用户/租户可缺失。
- 修复目标/要求：默认路径、查询白名单/脱敏、安全错误日志；可信身份选择配置，不能把保存后抛错称为事务回滚。
- 验证：敏感查询/异常时全输出无秘密，sub/租户/未认证声明、响应已发后失败。当前测试仅断言 ?id=1。

### FIX-016：停止忽略未排空

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 证据：BingTrafficGateExtensions.cs:61–64 忽略 WaitForDrainAsync 的 bool，false 后正常完成，无未完成诊断。
- 问题/影响：仍有请求却隐藏停止未完成，后续可能释放容器，违背超时报告边界。
- 修复目标/要求：可观察的未完成状态/计数/原因和部署判断，不强停或提前释放。
- 验证：真实宿主长请求正常/超时/取消、资源有效/释放一次、准入停止竞争和终态。

### FIX-017：Worker 解析失败遗留计数

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 证据：OrderedEventConsumer.cs:115 RecordStarted，:117–121 创建 scope/解析 handler，保护 finally 在其后 :123–155。
- 问题/影响：scope/handler 构造失败不执行 RecordCompleted，每轮积累虚假 InFlight。指标测试仅手动调用；RetryCount 提交前增加也需定义尝试/成功语义。
- 修复目标/要求：计数保护覆盖创建/解析/执行错误，明确指标语义。
- 验证：构造/解析/取消/释放故障后计数归零及继续处理，重试提交失败/Unknown 分别断言。

## 欠缺考虑或落实的 TODO

以下是设计/验证/已有风险，不全部等同于已复现漏洞，也不重复计入功能包。

- [ ] 库存 MySQL schema 未固定 ENGINE/排序规则，请求允许首尾空白；验证事务引擎和大小写/尾空格/Unicode/租户身份一致。
- [ ] 库存唯一冲突后仍普通 SELECT；RepeatableRead 旧快照下真实并发重复是否返回首个结果。
- [ ] 整单幂等、补货后失败请求、取消/返还规则；批次异常必须回滚，不得提交已扣前项。
- [ ] 流起点/过滤/实体代次/重放完整可见；Consumer+EventId 跨租户唯一性；JSON 稳定序列化/契约版本与毒消息策略。
- [ ] 保存点不恢复 ORM 跟踪/变更报告/回调/CAP 缓冲；避免失败标记提交 Flush 已回滚业务；死锁失效、DDL、非事务表分开处理。
- [ ] Worker 串行慢处理公平性、有界并发；入箱重试叠加 CAP 重投的总预算/过载；MySql.Data 分支直接证据。
- [ ] 状态/恢复默认权限及关键审计，消息可信发布者/租户授权/代理 ACL；不靠元数据格式证明可信。
- [ ] 真就绪、SSE/WebSocket/流式响应、脱离请求任务/CAP/定时任务的在途范围与不响应取消的停止诊断。
- [ ] WaitForDrainAsync 提前完成未取消竞争 Task.Delay，长超时/频繁等待可能保留计时器/取消注册；有界探针验证，生产不强制 GC。
- [ ] 普通审计等待/取消、已发响应与已提交业务的失败语义；可信用户租户、Unspecified→UTC、白名单重名及旧插件重编译。
- [ ] 收据保留覆盖重放；总流/总积压、完成消息和审计归档/索引/权限；不按年龄删除未处理事件。
- [ ] CREATE TABLE IF NOT EXISTS 不升级现有表；库存/审计/有序表迁移版本、兼容回填及蓝绿回滚。
- [ ] Redis 故障切换不保证租约独占；单租户键空间公平；蓝绿参数冲突的配置协调/恢复。
- [ ] 两 Admin CapModule 硬编码 RabbitMQ 账户/密码/地址需移受控配置/密钥，若真实使用应轮换，报告不复制值；核验 CAP Dashboard/诊断访问控制。
- [ ] Admin HttpLoggingFields.All / WithRequest=true 的其他日志也需统一脱敏；审计投影不能保证整个请求链不记秘密。
- [ ] 分页数与总积压、轮询 Gap/Failure 与唯一失败实体、重试尝试与成功的指标语义；有界标签/告警节流。
- [ ] 强杀/断电无托管清理保证；Unknown 查幂等结果；HTTP/邮件外部副作用需幂等或后续出箱。

## 已有验证证据：本轮未重跑

主代理核对源码/接线，luna_worker_review_evidence 独立只读核对 TRX/时点/映射。路径均位于 artifacts/，通过不代表覆盖新增问题。

| 文件/组 | 通过 / 失败 / 跳过 | 范围 |
| --- | --- | --- |
| audit-persistence/audit-persistence-net6.trx、net8.trx | 各 19 / 0 / 0 | SQLite 存储/白名单/所有权 |
| audit-persistence/audit-full-net6.trx、net8.trx | 各 150 / 0 / 0 | 审计全量，非 HTTP scoped/MySQL 组合 |
| audit-persistence/ef-audit-transaction-net6.trx | 4 / 0 / 0 | SQLite EF 事务 |
| audit-persistence/free-audit-transaction-net6.trx、net8.trx | 各 4 / 0 / 0 | FreeSQL 定向，非全量 |
| audit-persistence/ef-audit-full-net6.trx | 87 / 0 / 0 | EF 全量 |
| rate-pipeline/rate-pipeline-net6.trx、net8.trx | 各 17 / 0 / 0 | TestServer 身份/代理/路由 |
| rate-pipeline/mvc-rate-full-net6.trx、net8.trx | 各 87 / 0 / 1 | 既有 UploadRawAsync 跳过，审计手动构造 |
| rate-pipeline/redis-quota-net6-final.trx、net8-final.trx | 各 9 / 0 / 0 | 同进程双 DI 容器 |
| admin-rate-wiring/*-net6-final.trx | 两宿主各 6 / 0 / 0 | 注册，非 Admin HTTP |
| admin-rate-wiring/admin-ef-rate-regression-net6.trx | 14 / 0 / 16 | MySQL/CAP 门控跳过 |
| admin-rate-wiring/admin-free-rate-regression-net6.trx | 34 / 0 / 15 | 同上 |
| admin-mysqlcap-results/cluster-order-after-wrapper-fix/net6.0-20261004T224913429Z/ | EF 16 / 0 / 0；FreeSQL 15 / 0 / 0 | 06:53–06:54 北京时间，早于最新限流/审计 |
| ordered-post-retry-verification/freesql-full-net6-serialized.trx | 68 / 0 / 0 | 06:59 北京时间，早于新增审计 |
| ordered-post-retry-verification/sample-lazy-net6.trx | 1 / 0 / 0 | 历史实际 Lazy/停止，非蓝绿 |

历史失败均有后续实际通过，包括 EF 包装事务/FreeSQL 死锁；旧日志保留，后来的 NotExecuted 不算外部重跑。Admin 外部 SourceState=dirty-or-unknown，缺精确提交绑定。

当前 dotnet --version 为 8.0.425，global.json 为 8.0.424；TRX 无 SDK 字段，不能逐份证明锁定版。锁定版本留为环境待验证，不改 global.json 或记成代码失败。

### 证据/文档 TODO

- [ ] 最新审计后 FreeSQL net6/net8 全量及受影响 Admin/Sample 构建；定向不能代替。
- [ ] 持久化新增符号和两 ORM SaveTransactionalAuditAsync 的直接映射，当前映射只有旧字段填充。
- [ ] execution.md 追加本批 19/150/4/87 和迁移，更新开头总表；131/131 留为历史。
- [ ] cluster-consistency.md 的七项入箱测试“未运行”和已有功能“待实现”等旧状态同步更新。
- [ ] 锁定 SDK、NuGet 在线元数据、MySQL 库存/审计、跨进程 Redis、双平台动态证据分别记录。

## 建议顺序

1. 先修 FIX-014/015，补真实审计请求管道与脱敏。
2. 修 FIX-016/017，补长请求/构造失败/并发停止。
3. 完整库存+关键审计+CAP 及 MySQL 双进程，完善生产有序恢复。
4. Redis/Unknown/取消矩阵、角色开关和消费/任务排空、双平台动态演练。
5. 指标、留存迁移、GC/容量、映射及最终回归；不因环境缺失扩大无关重构。
