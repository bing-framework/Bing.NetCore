<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_REVIEWED_AT: 2026-10-05T09:42:20.917341Z

## 当前审查结论（本节取代下方历史状态）

对照会话批准的完整 P0–P4 计划、plan.md、execution.md、工作区及暂存区改动、当前生产/测试/部署源码和实际 TRX，本次结论仍为 **NEEDS_FIX，7 组工作包未完整验收**：FIX-006、007、008、009、011、012、013。不是七项完全没有实现，不以测试数量计算完成百分比。P0 核心已有历史通过证据，P1–P4 为 PARTIAL。

本轮只读审查，不修业务代码、不补测试，不修改 plan.md/execution.md。接收此前已经启动的测试命令的终态：最新 EFCore executor 定向命令在依赖还原阶段退出1，尚未执行测试。只执行 SDK 查询、源码/TRX 核对和有界声明组合诊断。旧历史记录保留，不能当作最新工作区已通过。

### 本轮新增实现的验收状态

| 子项 | 当前源码 | 当前证据/判定 |
| --- | --- | --- |
| FIX-026 恢复结果与清理失败分开 | AdminInventoryOrderExecutor:120–160 已保留原/清理异常，保存 CommittedResult/RecoveredResult；先等待异步释放 | 代码已补，待验收；10 个案例已有，当前仅找到旧的1项/套TRX，不关闭 FIX-026 |
| 库存双进程同单/不同单竞争 | AdminInventoryProcessCases:25、48 及两套 InventoryProbe 工程已有 | 待构建/运行；扫描271份相关TRX，无新增库存进程用例结果 |
| 库存提交前/后杀进程 | AdminInventoryProcessCases:70、92 已有 | 待运行；提交前暂停在关键审计之后、CAP出箱之前；提交后暂停在 ExecuteAsync 已返回之后，均不能代替提交确认丢失 |
| 原请求恢复与有界重试 | 独立作用域、4次上限、20/40/80ms延迟、原身份恢复回调已有 | 真实1213重试/新作用域/释放时机/次数未验证；发现 FIX-028 判定边界 |
| 交付收口 | 最新实现存在于当前工作区 | inventory-orders.md:82 仍称业务入口不自动重试，需区分直接Service和新Executor；新进程方法未进入CI必需集合，新符号/案例映射及执行记录待补 |

### 原计划剩余 TODO（按建议优先级）

- [ ] **FIX-007：库存整单竞争、重试与恢复。** 先使两个新探针可构建并执行四个跨进程用例；补真实 MySQL1213、最大尝试次数、新作用域及等待清理验证；统一清理错误判定（FIX-028）。同单/不同单均核对库存、整单结果、关键审计、CAP出箱。提交不确定时只用原请求、权威写库和旧操作结束确认恢复。
- [ ] **FIX-013：真实提交不确定性与资源故障矩阵。** 验收FIX-026的10个案例；增加实际COMMIT确认丢失、出箱已写入后提交前强停、提交后Flush/Dispose异常、回滚与释放同时失败、CAP取消的确认/重投行为。超时仅结束等待，不强行释放活动资源；业务成功不等于清理成功。
- [ ] **FIX-006：有序事件生产闭环。** 两Admin尚未接入生产CAP订阅→等待持久化入箱→Worker；注册方法本身不创建CAP订阅。补租户授权的查询/重试/补偿与关键审计，派生出箱同事务，失败丢弃ORM跟踪及CAP/回调内存状态。不能把SQL保存点当作全状态回滚。
- [ ] **FIX-008：集群限流运行验收。** 两个Admin的完整认证/可信代理HTTP链路、跨进程Redis共享额度、真实断连/恢复与故障策略仍待验证。已有多身份限流修复、HTTP组件和同进程双容器额度证据，不重列为缺失实现。
- [ ] **FIX-009：自动审计与普通请求持久化。** EF ChangeTracker/FreeSQL属性级自动变更快照未补；普通HTTP审计生产落库未接入，默认仍为SimpleLogAuditingStore。补敏感字段白名单/实际日志输出、查询权限、保存取消及慢保存预算；统一审计可信身份（FIX-027）。关键库存审计同事务已有，不重复实现。
- [ ] **FIX-011：蓝绿实际角色和排空。** 两Startup仍无条件注册CapModule，HOST_ROLE/CONSUMER_ENABLED未被实际启动链消费。补Web/消费/任务角色、依赖预热、稳定消费者组、消费及任务排空；Compose/Nginx与Kubernetes分别执行长请求、绿版不就绪、切换、回滚、活动消费和强停恢复。现脚本Live仅检查就绪/选择器。
- [ ] **FIX-012：观测、留存和交付证据。** PendingStreams只是扫描页计数；补全局积压、最老年龄、有限标签指标导出、授权诊断、告警节流；幂等/审计/消息归档及未处理保护。新增集群组件持续分配、GC、存活对象、延迟和恢复追赶测量。完成新执行器/探针映射、迁移文案与CI必需方法集合。

### 新确认的问题

#### FIX-027：HTTP审计跨认证身份组合用户与租户

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；归属FIX-009；状态：OPEN。
- 问题：每个声明独立遍历所有已认证身份，形成不存在于任何单一身份中的用户/租户组合。
- 证据：BingHttpAuditingMiddleware.cs:180–183调用独立FindAuthenticatedClaim；:218–220通过SelectMany查询各身份。真实ClaimsPrincipal有界诊断：scheme-A只有sub=user-A，scheme-B有sub=user-B/tenant=tenant-B，当前选择算法得到user-A/tenant-B；没有任何身份提供该对组合。不是生产HTTP复现，不证明远程可利用。
- 影响：多认证方案下操作审计可能错误归属；与已修正的限流身份策略不一致。
- 修复目标/要求：一次确定可信身份，全部审计声明从该身份读取；有意采用多方案合并时必须显式策略，不能无意拼接。
- 验证方式：双认证身份、第一身份缺租户、第一身份缺用户、未认证身份、真实TestServer；断言审计与限流归属一致。
- [ ] 统一审计身份选取并补直接测试。

#### FIX-028：死锁重试没有统一排除事务清理失败

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；归属FIX-007/013；状态：OPEN。
- 问题：IsRetryableDeadlock只排除Bing.InventoryScopeCleanupException，而HasCleanupFailure还识别事务清理、回滚诊断键与聚合异常。原生1213带RolledBack及事务清理诊断时，仍可进入重试。
- 证据：AdminInventoryOrderExecutor.cs:176–187、:195–198。当前默认Admin事务释放多用AggregateException而被类型判断拒绝；诊断键分支是扩展路径边界，未有真实1213运行复现，不声称默认路径已经发生资源误释放。
- 影响：不满足“确认回滚且清理成功才重试”的公开约束，可能重试清理尚有错误的请求。
- 修复目标/要求：重试和恢复复用一致的清理失败判断，不改变每轮独立作用域/异步等待原则。
- 验证方式：原生MySqlException1213加RolledBack及每个清理诊断键，断言不重试；无清理错误正向真实死锁，断言有界新作用域重试。
- [ ] 统一判定并增加正反向直接测试。

### 欠缺考虑的 TODO（区别于已复现缺陷）

- [ ] **运行身份统一**：限流、审计、库存授权及事件发布均定义可信租户来源；正文租户/请求ID不可替代权限校验；多认证身份不自动拼接。
- [ ] **恢复查询完整性**：恢复回调返回的身份/内容需匹配原请求；只读副本的null、网络超时和取消不证明未提交，确认旧操作已结束后再决定重试。
- [ ] **事务外状态**：SQL保存点不恢复ORM跟踪、FreeSQL变更报告、CAP缓冲和内存回调；失败代/作用域丢弃策略与外部幂等副作用必须明确。
- [ ] **序号起点和流代次**：新增消费者、完整可见流、实体删除重建、契约升级、过滤、重放和毒消息补偿；缺序号禁止超时跳过。
- [ ] **公平调度与重试预算**：串行Worker遇慢实体仍影响其他实体；有界按实体并发/背压与CAP、DB总重试预算需设计，不能叠加无界放大。
- [ ] **排空统计完整性**：异步释放、OnCompleted、SSE/WebSocket、脱离请求任务、CAP和定时任务分别核对；HTTP计数为零不代表进程已安全排空。
- [ ] **Redis运行边界**：主从切换及租期过期不提供绝对互斥；限流策略升级/主体键变化会重置或分裂额度；单Hash的HGETALL容量扫描和热键延迟需测量。
- [ ] **迁移与留存**：CREATE TABLE IF NOT EXISTS不迁移旧表；维护窗口、备份、索引/身份回填与新旧实例兼容需验收；幂等保留覆盖重放，未处理消息不可直接按年龄删除。
- [ ] **安全交付**：消息代理ACL、恢复/审计/Dashboard权限、测试日志秘密检查及旧凭据轮换；授权诊断默认不显示正文。
- [ ] **测试故障点与竞争证据**：READY/GO和不同PID证明进程独立，但不必然证明事务重叠；增加可观测临界点/争锁证据。审计后暂停不代表CAP出箱已写，业务返回后的强杀不代表COMMIT ACK丢失。
- [ ] **库存后续产品能力**：预占、撤销、返还、部分发货及不足后的新业务尝试需独立业务身份/补偿规范，不把换ID当Unknown恢复。

这些边界并非都必须扩大为本期新功能；其中身份、事务状态、故障测试与排空直接影响本期验收。

### 实际验证状态

- 最新executor-fix-ef.log及inventory-ef-probe-restore.log在NU1101阶段退出，涉及EFCore/Pomelo依赖还原；本轮没有成功构建新探针、执行新10个案例或4个进程用例。未执行不写成断言失败，亦不算通过。
- 扫描271份相关TRX：库存执行器仅有旧版本原1项/ORM通过证据；新增库存Process方法结果为0。已有FreeSQL有序进程测试不能替代库存进程用例。
- 更早真实库存MySQL/RabbitMQ各7/7、注册各12/12，限流专项双框架各35/35、审计身份各20/20，是历史验证。最新新增实现之后全量、Sample和双平台部署未重跑。
- 实际SDK8.0.425；已安装列表没有锁定8.0.424；global.json未改。锁定SDK验证仍为环境TODO。NU1900漏洞元数据不可用和既有Microsoft.Extensions10/net6警告不据此推断存在可利用漏洞。
- 旧FreeSQL通用异步查询同步回退仍是独立任务，不混入七组集群能力统计。

建议：先收口FIX-026/028及新增库存验收，再补FIX-007/013真实故障；随后FIX-006、FIX-008/009（含FIX-027）、FIX-011、FIX-012。本次审查完成，整体任务仍未验收。

---

## 以下为上一轮审查原文（保留历史证据，不代表当前状态）

## 当前审查结论（本节取代下方历史状态）

完整 P0–P4 仍为 NEEDS_FIX，共 **7 组未完整验收工作包**：FIX-006、007、008、009、011、012、013。不是七项完全不存在，也不按测试数计算百分比。P0 核心修复已有历史证据；P1–P4 部分实现。

本次核查当前源码、执行报告、实际 TRX 和部署配置；延续的库存执行器改动已完成定向构建/测试，随后转为审查。没有重新运行全仓回归、Sample 或双平台部署。下面历史记录中 FIX-024/025 和 CI 必需方法仍待完成的描述已失效。

### 已关闭的子项

- FIX-024：关键审计租户保留严格 UTF-8 精确身份，MySQL 改为 VARBINARY；003 显式迁移及两 ORM 尾空格真实业务测试通过。生产数据库仍须人工维护迁移。
- FIX-025：用户及租户声明只取第一个已认证身份，6 个新增中间件/真实 HTTP 管道用例已验证，双框架定向各35/35。
- FIX-012 的 CI 子项：已核验十个必需方法的存在与 Passed 状态，数量不足、缺方法、无关方法补数均不能通过；本次 PowerShell 测试通过。
- FIX-007 子项：新增 AdminInventoryOrderExecutor，每次尝试新建作用域并等待异步释放；只识别 RolledBack 的原生 MySQL 1213 做有界重试；提供原身份恢复回调，不盲目重新扣减。但真实死锁、Unknown 和清理错误矩阵未验收，不能关闭整个工作包。

### 计划剩余 TODO（按优先级）

- [ ] **FIX-007 / HIGH / MUST_FIX：库存跨进程、重试与恢复。** 两 ORM 的同进程独立作用域竞争已有；还缺双进程同单/不同单竞争、真实1213重试次数/新作用域/清理完成、提交确认丢失后原请求查询。当前恢复直接用回调返回值，尚无真实断连恢复验收。断言库存、整单、关键审计、出箱均不重复。
- [ ] **FIX-026 / MEDIUM / SHOULD_FIX：恢复成功不能掩盖资源清理失败（归 FIX-007/013）。** AdminInventoryOrderExecutor.cs:110–133 释放抛错后仍进入恢复，回调返回非空即正常返回；清理异常 Data 因此对调用方不可见。仅释放失败也可能因默认 Unknown 被当成提交不确定。要求分开业务结果、事务结果与清理失败；恢复可确认业务，但仍须报告清理未成功。补业务失败+释放失败+恢复成功、仅释放失败、恢复失败、查询空值、取消测试。当前仅有一项人工 Unknown 结果测试，不能证明故障矩阵。
- [ ] **FIX-013 / MEDIUM / SHOULD_FIX：真实提交与取消故障矩阵。** 增加实际提交确认丢失、提交后 Flush/Dispose 失败、回滚及释放同时失败、CAP 取消确认/重投；原操作结束与调用方停止等待分开，null 不作为回滚证明。
- [ ] **FIX-006 / HIGH / MUST_FIX：有序事件生产接入与安全恢复。** modules/admin/src 未注册 AddBingOrderedConsumer，也无业务 handler/入箱订阅。提供显式主题/稳定组/消费者接线、两 ORM Store/Factory、持久化后 CAP 返回、查询/重试授权及可靠审计。FreeSQL 工厂须取得 FreeSqlWrapper.Orm。派生消息必须绑定当前事务；保存点失败后验证 ORM 跟踪/CAP 缓冲/回调不再次提交回滚业务。不能以测试宿主接线代表生产完成。
- [ ] **FIX-008 / HIGH / MUST_FIX：限流真实宿主与故障验收。** 多身份问题已修；继续双进程共享额度、完整 Admin HTTP、可信代理/租户隔离、Redis 断连/恢复、明确503及显式降级策略验证。
- [ ] **FIX-009 / HIGH / MUST_FIX：自动审计及普通审计生产落库。** EF ChangeTracker/FreeSQL 属性变更快照、字段白名单/脱敏、普通 IAuditingStore 数据库存储配置、查询授权、保存取消/预算待补。关键库存审计已有，不等于所有 HTTP 审计可靠落库。HTTP 多重清理错误已修。
- [ ] **FIX-011 / HIGH / MUST_FIX：蓝绿角色与动态验收。** BING_HOST_ROLE/BING_CAP_CONSUMER_ENABLED 只在模板/文档；两个 Startup 仍注册 CapModule。接入 Web/消费者/任务角色、依赖预热、消费/任务排空；Compose/Nginx 和 Kubernetes 分别验证长请求、绿版不就绪、切换、回滚、活动消费及强停恢复。模板检查不代表部署通过。
- [ ] **FIX-012 / MEDIUM / SHOULD_FIX：可观测性、容量与交付。** 当前 PendingStreams 是扫描页数量；补全局积压、最老年龄、指标导出/有限标签、授权诊断/告警节流、收据/审计/消息归档、未处理消息保护。补本次集群组件持续分配、GC、存活对象、延迟、恢复追赶测量；没有业务预算时只报告数据。

七组工作包包含上述八条可执行条目；FIX-026 是其中新增代码问题，不增加能力组计数。

### 此前欠缺考虑的 TODO

- [ ] **资源结果独立表达**：业务已提交/已恢复并不表示作用域释放成功；恢复入口不能吞清理失败，不以超时强行 Dispose 活动资源。
- [ ] **保存点外状态**：ORM 跟踪、FreeSQL 变更报告、CAP 缓冲和内存回调不受 SQL 保存点回滚；明确失败作用域丢弃与派生消息策略。
- [ ] **流起点与代次**：新消费者起始游标、完整订阅流、实体删除重建、过滤/契约升级/重放和毒消息补偿；缺序号不能超时跳过。
- [ ] **有界公平处理**：串行 Worker 的慢实体阻塞其他实体；定义按实体有序的有界并发、背压和 CAP/DB 重试总预算。
- [ ] **排空覆盖范围**：异步作用域释放、OnCompleted、SSE/WebSocket、脱离请求任务、CAP 与定时任务分别统计；请求计数为零不代表全部资源已停。
- [ ] **运行权限**：消息发布者租户归属、代理 ACL、恢复/审计查询/Dashboard 最小权限、日志秘密扫描和历史凭据轮换。
- [ ] **限流部署兼容**：主体键升级导致额度重置/分裂；蓝绿策略参数冲突、Redis Hash 容量扫描/热键需测量并明确发布策略。
- [ ] **迁移与留存**：CREATE TABLE IF NOT EXISTS 不升级旧表；身份/索引回填、维护窗口、备份和旧新实例兼容；幂等保留期覆盖重放，未处理消息不得直接按年龄清理。
- [ ] **库存扩展规则（后续产品能力）**：预占、撤销、返还、部分发货及不足后的新业务尝试；与本期原请求幂等区别，不能换 ID 修复 Unknown。
- [ ] **外部副作用与强停**：HTTP/邮件需幂等键或事务出箱；强杀依赖事务/收据恢复，不承诺托管清理或恰好一次投递。

这些边界不都属于已复现漏洞，也不全部扩大为本期必做功能。

### 最新验证与限制

| 验证 | 实际结果 | 证据 |
| --- | --- | --- |
| 两 ORM 库存真实 MySQL/RabbitMQ net6 | 各7/7 | modules/admin/tests/*/TestResults/admin-*-inventory-executor-net6.trx |
| 两 ORM 库存注册 net6 | 各12/12 | admin-*-registration-executor-net6.trx |
| 人工 Unknown 回调恢复契约 net6 | 各1/1 | admin-executor-ef-net6.trx、admin-executor-freesql-net6.trx |
| 两 Admin 数据工程构建 net6 | 均退出0 | artifacts/cluster-current-audit-20261005/admin-data-*-build-executor.log |
| CI 必需方法检查脚本 | 通过 | eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1 |
| 关键审计身份专项 net6/net8（最近实施证据） | 各20/20 | artifacts/identity-contract-fix/audit-identity-final-net6/net8.trx |
| 多身份限流专项 net6/net8（最近实施证据） | 各35/35 | artifacts/identity-contract-fix/rate-identity-after-net6/net8.trx |

SDK 实际8.0.425，锁定8.0.424未验证；global.json未变。NU1900在线漏洞元数据未成功获取，Microsoft.Extensions10/net6及既有成员隐藏/过时API警告仍存在。本次未重跑全量/Sample/部署，历史通过不能宣称当前工作区全量无影响。git diff --check 无空白错误（输出含既有换行警告）。

建议顺序：FIX-026及FIX-007/013 → FIX-006 → FIX-008/009 → FIX-011 → FIX-012。保持 NEEDS_FIX。

---

## 以下保留为上一轮历史审查（不代表当前待办状态）

# 集群能力与计划差距审查

## 范围和结论

依据用户批准的完整 P0–P4 计划、plan.md、execution.md、当前暂存/未暂存改动、未跟踪源码、测试、部署配置和真实 TRX 检查。本轮不修改生产代码或单元测试，不重新运行 dotnet 测试；只更新审查与有界契约诊断证据。前一份审查保存于 review-before-identity-contract-audit-20261005.md；更早审查也保留。

仍有 **7 组未完整交付的原计划工作包**：FIX-006、007、008、009、011、012、013。多数已有组件或业务链路，缺生产接入、边界实现或完整验收；不代表七项功能完全不存在，不用测试总数推算完成百分比。

FIX-021/022 已修；本轮确认 FIX-023 极短限流键、库存精确身份/事务引擎及人工迁移、HTTP 审计多重清理错误也已有实现和直接证据，不能继续列为未修复。新增 FIX-024 库存与关键审计身份约束冲突、FIX-025 限流多身份声明来源，分别归入原工作包，不增加七组能力的计数。两个新问题由源码及有界契约探针确认，尚未完整宿主复现，不宣称可远程利用。

AdminInventoryService 已在两套 ORM 的真实工作单元中组合整单结果、库存、关键审计与 CAP 出箱。最新真实 MySQL/RabbitMQ 库存定向各 6/6，包含同进程独立作用域竞争；完整 Admin 门控 EFCore 22/22、FreeSQL 21/21。FindAsync、迁移指南和新增生产符号映射已有，仍不等于库存双进程或 Unknown 故障恢复验收完成。

## 阶段验收矩阵

| 阶段 | 结论 | 已实现 | 主要差距 |
| --- | --- | --- | --- |
| P0 并发基础 | PASS（核心历史证据） | SemaphoreSlim 本地锁、所有者校验、引用计数回收、原子 Redis 获取/释放、完整租期、本地事件快照 | 故障切换/长期负载属于后续运行验收；本轮未重跑真实 Redis |
| P1 事件与事务 | PARTIAL | CAP 适配、可选元数据、两 ORM 领域事件轮次、同事务消费收据、提交结果诊断、CAP 唯一关闭任务 | 生产恢复策略、真实取消确认/重投、提交确认丢失与外部副作用恢复 |
| P2 顺序与库存 | PARTIAL | 有序入箱/游标/序号/保存点失败标记、后台处理、ORM 工厂；整单幂等/首次结果/固定 SKU 顺序/不足回滚；Admin 库存+审计+CAP 与作用域竞争 | 库存双进程、整单死锁重试/Unknown 恢复；有序生产接线/授权审计 |
| P3 限流、审计、原子工具 | PARTIAL | 本地/Redis 令牌桶、可信身份/稳定路由/结构化主体、429/503、HTTP 审计、数据库安全投影、事务审计、实体字段填充、CAS HTTP 闸门 | 完整 Admin HTTP 与 Redis 故障；多身份可信声明；自动属性变更快照、普通审计生产落库、持续运行证据 |
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
- [x] FIX-023 已要求 MaxKeyLength >= 32，并覆盖非法边界与合理配置，最新限流定向29/29；既有Claims/摘要回归保留。
- [ ] 修 FIX-025 多身份声明来源并补真实 HTTP 直接测试。
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
- 问题/证据：Worker.RecordPendingStreams(streams.Count) 是当前页流数，尚非全局积压；指标只有进程内快照，缺年龄/导出/容量留存。新库存指南、生产符号映射和 CI 22/21 门槛已更新；execution.md 已补录 FIX-021/022、最新身份/限流/审计批次和完整门控结果；生产符号映射已追加。本轮独立核验实际TRX，交付证据的此子项可关闭。
- 影响：无法评估长期内存/磁盘和真实积压；测试数量与当前能力文案不一致。
- 修复目标/要求：总积压、最老年龄、有限标签指标导出/授权诊断/告警节流；持续 GC/分配/延迟与存活对象证据；归档/清理及新增业务迁移文档。
- 验证方式：持续负载、恢复追赶、静态/后台引用回收、收据保留和未处理消息保护；无容量预算时报告测量，不自定 MB/QPS 门槛。
- [ ] 指标导出、总积压/年龄、审计/幂等/消息归档。
- [ ] 长期分配、GC、存活对象和负载测量；旧模块资源探针不能代替本次集群组件。
- [x] 新库存 README/指南、生产符号映射、CI 最低数量更新已有；本轮核对实际新用例证据。
- [x] execution.md 已补录最新批次、双框架回归、22/21门控及失败基线，历史/当前证据分开。
- [ ] CI核验必需方法集合；当前 Test-TrxCounters 只核验数量和 Counters，替换成无关通过用例仍可能达标。

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

- 原严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：RESOLVED；归属 FIX-008。
- 当前证据：BingRateLimitMiddleware.cs:20、58 要求 MaxKeyLength >=32；保留合理配置的摘要格式。源码中的短键分支已经删除。
- 验证：Constructor_WhenMaxKeyLengthIsBelowMinimum_ShouldThrowArgumentOutOfRangeException、Constructor_WhenMaxKeyLengthMeetsMinimum_ShouldSucceed 覆盖-1/0/1/7/8/31拒绝和32/71/256接受；net6定向29/29，net8方法包含最新MVC全量。文档已说明配置迁移。
- [x] 修复、直接回归及迁移说明已有；不等于跨进程Redis/真实AdminHTTP验收。

### FIX-024：库存和关键审计的租户身份约束冲突

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE；归属 FIX-007/009。
- 问题：InventoryDeductionRequest.ValidateKey 接受非全空白的原始标识并明确保留首尾空格；InventoryOrderRequest沿用该约束。AdminInventoryService.cs:107/122直接把TenantId传给关键审计；DbAuditingStore.cs:239-245的Identity却拒绝 value != value.Trim()。
- 证据：artifacts/cluster-current-audit-20261005/Test-IdentityContracts.ps1使用上一批真实程序集，构造tenant尾空格请求成功；反射调用实际审计身份校验抛ArgumentException。结果存 identity-contract-probe.json。探针不是xUnit、不是数据库/完整Admin调用。
- 影响：组件允许的请求在Admin关键审计阶段失败并回滚，精确身份专项通过不能证明完整业务支持相同身份范围。未发现库存超卖或越权证据。
- 修复目标：明确统一的宿主身份契约；不能静默Trim以合并身份。选择保留精确值的审计方案，或在业务入口提前拒绝并准确文档化限制。
- 修复要求：补两ORM的真实整单测试，包括租户首尾空白、大小写/Unicode、审计与出箱一致性；核对审计表身份排序规则及发布迁移。
- 验证方式：请求验证、数据库存储、关键审计全链路使用同一规则；失败时库存/整单/审计/出箱均未提交。
- [ ] 统一库存与关键审计身份范围并补端到端回归。

### FIX-025：限流声明读取未限定已认证身份

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE；归属 FIX-008。
- 问题：BingRateLimitMiddleware.cs:74以User.Identity.IsAuthenticated判定后，76/79使用User.FindFirst跨全部身份读user/tenant声明；不保证声明所属ClaimsIdentity已认证。
- 证据：有界探针建立一个已认证但无sub的身份和一个未认证且有sub的身份，Principal.Identity.IsAuthenticated=true，FindFirst(sub)仍返回未认证身份中的值。现有测试只覆盖单身份；HTTP审计的FindAuthenticatedClaim已经限定已认证身份，可作为一致性参考。
- 影响：多身份宿主可能用非可信声明或来自不同身份的user/tenant组合分桶。声明如何进入Principal由宿主决定，本轮不宣称能从外部伪造认证或绕过授权。
- 修复目标：明确可信认证身份、声明来源及多scheme合并规则。
- 修复要求：只使用可信已认证身份的声明，避免错误跨身份拼接；保留单身份/匿名IP语义。
- 验证方式：真实HTTP覆盖认证身份在前/在后、未认证声明、两个已认证scheme、租户/用户归属及同主体额度。
- [ ] 修正读取规则并补多身份真实HTTP回归。

## 已闭环内容与不能重复列为缺失的项

- FIX-010：用途驱动 CAS 闸门/Interlocked 计数已有，长期负载归 FIX-012。
- FIX-014～018：请求级审计注入、可信元数据、排空失败报告、Worker 清理计数与提交后重试计数、MySQL 唯一冲突当前读已有修复和对应批次证据。
- FIX-019：默认日志/凭据/Dashboard 注册已修；实际输出和访问权限仍 PARTIAL，归 FIX-009/012。
- FIX-020：已改结构化二元组+Ordinal 去重。此前双框架定向及最新库存回归包含分隔符不同身份、真正重复和不足回滚，具体碰撞问题 RESOLVED；不等于完成 FIX-007 整单业务验收。

## 欠缺考虑或尚未充分落地的 TODO

以下区分潜在边界和已复现问题，不全部宣称为漏洞，不重复计入七组工作包。

- [x] **库存数据库精确身份/事务引擎**：stock/request已改VARBINARY及InnoDB，严格UTF-8与SQLite BINARY已有；002人工迁移及真实MySQL隔离库专项3/3通过。共享验收库未迁移，生产仍需维护窗口和存量核对。
- [ ] **全链路身份规则**：关键审计与库存仍存在FIX-024冲突；统一上下游允许范围，不能通过Trim修复精确身份。
- [ ] **生命周期替换契约**：FIX-021 已纳入直接测试；继续覆盖其他可替换注册的 Singleton/Scoped/Transient 组合、调用方对象所有权及跨作用域状态。
- [ ] **整单长期语义**：指南已有首次不足、新业务尝试及内部 order- 保留身份说明；撤销/返还/预占/部分发货需要独立业务规则，不是本期必需缺陷。不能通过换 ID 处理 Unknown。
- [ ] **序号和流代次**：订阅可见完整流、起始序号、实体删除重建、消息过滤、版本迁移/重放、毒消息补偿；未见前序不能靠超时跳过。
- [ ] **事务外内存状态**：保存点不恢复 ORM 跟踪、FreeSQL 变更报告、CAP 内存缓冲或登记回调；仅失败标记提交不得发送已回滚业务消息。
- [ ] **公平与总预算**：当前 Worker 按流串行，慢实体会阻塞后续实体；增加保留实体顺序的有界并发、CAP/入箱/整单重试总预算与背压，不使用无界队列。
- [ ] **真实排空覆盖**：请求作用域异步清理、OnCompleted、脱离请求任务、SSE/WebSocket、CAP 和定时任务分别纳入；Host.StopAsync 报错不能阻止外部调用方继续 Dispose 根容器。
- [ ] **安全运维入口**：消息发布者/租户归属、代理 ACL、恢复/审计查询与 Dashboard 权限；用测试秘密验证全部日志，实际使用过的历史凭据由运维轮换。
- [x] **HTTP审计异常完整性**：保存/释放错误已按引用去重并附完整数组到原始异常，安全日志及直接双框架12/12通过。
- [ ] **审计故障时限**：IAuditingStore普通接口缺调用方取消令牌；慢保存仍可能延长请求/停止。定义预算和状态，不能超时后强行释放仍在使用的资源。
- [ ] **数据库驱动清理**：新库存路径多处 using DbCommand/Reader；若业务异常与 Dispose 同时抛错，需直接验证原异常保留与后续清理，不用普通成功释放推断。
- [ ] **限流容量与配置**：极小摘要FIX-023已修，可信多身份待FIX-025；Redis 单策略 Hash 达上限时 HGETALL 扫描开销、热键、公平性及蓝绿策略参数冲突需测量与配置约束。新旧主体键混跑的额度重置/分裂已有说明，仍需发布策略验收。
- [ ] **容量与合规**：业务收据保留覆盖重放期限；未处理事件不可按年龄删除；审计访问、归档、磁盘预算及敏感值删除规则。
- [ ] **部署迁移**：CREATE TABLE IF NOT EXISTS 不升级旧表；库存/整单/审计/有序表需版本化迁移、索引/排序规则回填与扩展—迁移—删除；滚动期间契约兼容。
- [ ] **强停与外部副作用**：强杀/断电依靠事务、出箱及收据恢复；HTTP/邮件等须幂等或后续出箱，不能由租约或托管 Dispose 保证。

## 本轮测试和证据边界

本轮只读核对最新保存的 TRX、生产源码、直接测试和真实门控诊断，没有重新运行 dotnet 测试、Sample 或部署。这些是上一实施批次的实际结果，不写成本轮新执行。窄范围 TRX/CI/构建证据由独立 luna_worker 复核；有界算法诊断不计为 xUnit 通过项。

| 最近实施批次真实结果 | 通过 | 失败/未执行 | 证据 |
| --- | --- | --- | --- |
| 库存修复后定向net6 | 38 | 0/0 | inventory-identity-after-net6.trx |
| 库存隔离MySQL身份/旧表迁移net6 | 3 | 0/0 | inventory-identities-mysql-net6-authorized.trx |
| 限流定向net6 | 29 | 0/0 | rate-length-net6.trx |
| HTTP审计清理net6/net8 | 各12 | 各0/0 | http-audit-cleanup-after-net6.0/net8.0.trx |
| EventBus全量net6/net8 | 各194 | 各0/0 | eventbus-regression-net6.0/net8.0.trx |
| FreeSQL全量net6/net8，真实MySQL启用 | 各81 | 各0/0 | freesql-regression-net6.0/net8.0.trx |
| MVC全量net6/net8 | 各115 | 各0/1 | mvc-regression-net6.0/net8.0.trx；既有UploadRawAsync未执行 |
| Admin EFCore/FreeSQL完整MySQL/CAP门控net6 | 22 / 21 | 0/0 | inventory-identity-length/net6.0-20261005T055618182Z的两个TRX |
| Sample Lazy业务HTTP/StopAsync/端口释放net6 | 1 | 0/0 | sample-lazy-current-net6.trx |
| WebApi net6构建 | 成功 | 0错误/7警告 | webapi-build-current-net6.log |

除Admin目录外，上表文件均位于artifacts/inventory-identity-length-fix。独立luna_worker逐项核验17条results.json和对应TRX一致；Admin两套TRX各实际包含六个InventoryOrder及三个OrderedEventCap方法。诊断Passed、退出码0、SourceState为dirty-or-unknown。

失败基线保留：库存6通过/6失败；审计1通过/3失败；MySQL初次因随机库建库权限缺失0通过/3失败，临时限定命名空间授权后3/3且已撤销、删除隔离库。不能删除这些证据或将其计入修复后失败。定向/全量/双框架重叠，不相加推算独立覆盖。

- [x] 最新execution和生产符号映射已补录；不是本轮新跑测试。
- [ ] 新增FIX-024/025尚无正式直接回归；有界诊断不能计作xUnit通过。
- [ ] 锁定SDK8.0.424验证；本机清单仍无该版本，上一批实际8.0.425，global.json未变。
- [ ] NU1900在线漏洞元数据未成功获取；网络恢复后完成依赖安全核验，不凭测试通过宣称没有漏洞。
- [ ] CI只校验类过滤和22/21计数；增加必需方法集合，避免删除关键用例后由无关用例补足数量。

本地事件不跨节点、CAP至少一次、默认上下文不可卸载、强杀不保证托管清理是边界，不增加虚假的“保证恰好一次/强杀后Dispose”TODO。

## 建议推进顺序

1. FIX-024/025：统一关键审计与库存身份契约；限流声明限定可信身份；补正式直接回归和必需方法CI门槛。FIX-023、库存身份迁移及execution补录已完成。
2. FIX-007/013：库存双进程、整单回滚后有界死锁重试、提交确认丢失/Unknown 恢复。
3. FIX-006/008/009：生产有序授权恢复、跨进程限流及 Redis 故障、自动变更审计及普通审计落库。
4. FIX-011：角色开关真正接入、依赖预热、消费/任务排空与双平台动态发布。
5. FIX-012：指标导出、全局积压/年龄、容量/归档、持续资源测量与交付收口。

保持 NEEDS_FIX，完整 P0–P4 尚未完成。本轮只修改 review 和审查证据；没有修改生产/测试逻辑、plan、execution、目标框架、依赖版本或 global.json，未提交、推送或发送外部通知。