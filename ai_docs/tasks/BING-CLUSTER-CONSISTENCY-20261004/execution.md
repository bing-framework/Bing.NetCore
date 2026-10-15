<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: BING-CLUSTER-CONSISTENCY-20261004
AI_EXECUTION_STARTED_AT: 2026-10-04T15:46:11.622Z

# 实施执行报告

## 执行结论

P0 核心并发修复已交付；P1 的元数据、领域事件、CAP 出箱和收据已有直接与外部证据。P2 新增整单首次结果、关键审计和 CAP 出箱的 Admin 业务，两套 ORM 的专用 Docker MySQL 8.4/RabbitMQ 3.13 完整门控为 EFCore 34/34、FreeSQL 33/33，均无失败或跳过，覆盖库存并发、提交确认丢失、进程中断恢复、消费取消和有序消息乱序。整单作用域捕获、默认限流身份键碰撞、极短限流摘要及审计多重清理错误已修复；库存首次部署及人工迁移补齐 UTF-8 精确身份/InnoDB。P3 的限流/HTTP、数据库审计、自动实体快照、普通审计分批保留和原子闸门、P4 的模板与 HTTP 排空已有实现。生产多实例故障、角色分离与动态蓝绿、长期容量/归档仍未完成。各批次验证范围分别记录，不把专用单实例通过解释为生产集群全部验收。

## 当前候选状态（2026-10-08）

- `CLOSED`：本轮有序消费者失败流退避、处理器持续构造失败退避、失败探测缓存并发上限、缓存满载时基于持久化状态的探测、缺口告警节流，以及 CAP 消息头读写总字节边界；隔离定向测试和有序消费者/头部契约测试均已通过。
- `CLOSED`：消费收据消费者/事件标识的首尾空白、非 BMP、多字节字符和字符上限边界已有 SQLite 直接测试；MySQL 字符集和多实例竞争仍保留为外部门控。
- `CLOSED`：当前源码范围内的 P0 本地锁、事件订阅快照、数据库有序收件箱、人工恢复授权、审计保存边界和库存 Unknown 原请求恢复已有对应实现或直接测试证据。历史 `review.md` 的 FIX-006～013 状态来自 2026-10-05，不能覆盖后续执行记录。
- `CLOSED`：`CapOrderedEventTransactionFactory` 已将 `_finished` 与 `Outcome` 的更新纳入同一同步边界，物理提交完成后拒绝再次激活 CAP ambient；隔离测试工程对该适配器的 23 项定向测试全部通过。
- `OPEN_ACTIONABLE`：本轮有序消费者诊断范围内无新的本地可执行缺口。
- `BLOCKED_EXTERNAL`：锁定 SDK 8.0.424、真实多实例 Redis 故障切换、生产环境网络分区与 broker 确认丢失、Compose/Kubernetes 动态蓝绿、跨节点审计清理和长期容量/GC 观察需要外部环境或预先批准的资源门槛；专用 Docker 单实例 CAP 门控已在本轮通过。
- `DEFERRED`：跨节点共享失败退避和跨进程即时唤醒不属于当前单进程消费者缓存修复；依赖统一持久化/协调协议，另立集群调度任务。历史归档调度已提供显式、默认关闭的配置入口，但长期分区、容量预算、备份恢复和跨节点治理仍需独立验收。
- `ACCEPTED_LIMITATION`：本地事件仅限当前进程，CAP 至少一次投递，强制终止不保证托管清理，数据库条件更新仍依赖数据库内部锁。

## 任务信息与计划执行情况

| 阶段 | 状态 | 当前证据与未完成部分 |
| --- | --- | --- |
| P0 锁与订阅并发 | 完成核心实现 | 本地锁跨线程释放/键回收、Redis 原子获取与所有者释放、事件订阅快照；真实 Redis 与 net6/net8 测试通过。旧本地锁无锁释放返回 true 的兼容语义保留。 |
| P1 事件与工作单元 | 部分验收 | CAP 消息头、旧接口适配、两 ORM 事务内领域事件、出箱/消费和收据已有验证；整单只读结果查询已补，提交确认丢失、CAP 取消确认和 Unknown 真实恢复仍缺。 |
| P2 库存与顺序 | 部分完成 | 有序存储/游标/Worker、两 ORM 事务工厂、有序双进程及真实 CAP 已有；整单幂等/首次结果、多 SKU 不足回滚、关键审计/CAP、独立作用域竞争已接入并真实验证。整单重试、Unknown 故障、生产有序恢复和库存双进程验收仍缺。 |
| P3 限流、审计、无锁 | 部分完成 | 本地/Redis 限流、HTTP/数据库审计、安全投影、ORM 显式事务适配、库存关键审计组合、实体字段、自动变更快照、普通审计分批保留和 CAS 闸门已有；真实 Admin HTTP/Redis 故障、删除授权、冷归档、长期指标和容量测量仍缺。 |
| P4 双平台蓝绿 | 部分完成 | Compose/Nginx、Kubernetes 模板和 Admin HTTP 就绪/排空已有；真实依赖预热、宿主角色、消费/任务排空及动态切流回滚仍缺。 |

## 已完成事项与 API 变化

- `LocalLock` 使用按键 `SemaphoreSlim` 与引用计数；新增 `ILockLease`、`TryAcquireLeaseAsync`。
- `CSRedisDistributedLock` 使用 Redis 原子脚本获取和所有者校验释放，新增独立等待/租期的异步租约入口。
- Redis 锁的业务回调与释放同时失败时保留原始异常，并在异常数据中附加释放错误。
- `LocalEventBus` 在锁内复制订阅快照后调用处理器。
- 新增 `ITokenBucketRateLimiter`、本地/Redis 令牌桶及 `UseBingRateLimiting`；HTTP 限流键做无歧义编码。
- 本地与 Redis 令牌桶仅在额度补满后回收闲置键；Redis 同策略配置不一致时拒绝复用，不缩短仍有活动分桶的过期时间。
- 新增 `AuditingManager`、`AddBingAuditing`；默认日志存储避免输出审计参数正文。
- 增加 `BeginAsyncScope()`，释放时等待已启动的保存并传播保存失败；同步释放遇到在途保存时要求异步释放。
- 增加 `IClusterEventMetadata`、CAP 消息头读写及 `Bing.Events.EventBusAdapter` 的显式旧接口适配；不自动双派发。
- `TransactionActionManager` 按最多 32 轮执行回调中新登记的操作；回调失败或超限后清除待执行回调，拒绝并发提交。
- 新增 `IHasDomainEvents` 与 `DomainEventDispatchSession`；EFCore 自动收集跟踪聚合根，FreeSQL 通过 `RegisterDomainEventSource` 显式登记。事务内最多 32 轮分发，保存成功后移除事件；拒绝外部事务和分发期间替换已读取事件。
- 两套 ORM 的保存后通知失败均保留原始异常；存在外部事务时不能据此推断最终已提交。
- 新增 `EventConsumptionStore`，基于数据库 `(Consumer, EventId)` 唯一键，在调用方 `DbTransaction` 内原子登记消费；MySQL 与 SQLite 两种语法显式选择，重复事件返回 `false`，不接管事务所有权。
- 提供双平台蓝绿模板、切流及静态验证脚本，另有使用边界文档。

## 历史验证结果（按批次，不代表最新全量）

- `Bing.Core.Tests`：新增异步定向测试前 net8.0、net6.0 各 623 通过；新增测试后本地限流定向在两个框架各 8 通过，完整回归未重复执行。
- `Bing.EventBus.Tests`：net8.0、net6.0 各 85 通过。
- `Bing.Auditing.Tests`：net8.0、net6.0 各 127 通过。
- `Bing.AspNetCore.Mvc.Tests`：修改前完整回归 net8.0、net6.0 各 62 通过、1 跳过；HTTP 键编码后的定向测试在 net8.0、net6.0 各 5 通过，完整回归未在最后改动后重跑。
- 真实 Redis 定向测试：锁和令牌桶在 net8.0、net6.0 各 9 通过；注册修正后的令牌桶定向在 net8.0、net6.0 各 5 通过。
- WebApi 示例构建成功；未执行集群业务请求、两种蓝绿动态部署及真实 MySQL 双实例验收。
- Compose 配置解析、PowerShell 脚本语法及模板静态断言已通过；模板不能直接用于当前 Admin 镜像。
- 本轮审查修复的定向验证：本地令牌桶 net8.0/net6.0 各 9 项，审计 net8.0/net6.0 各 9 项，真实 Redis 锁与令牌桶 net8.0/net6.0 各 17 项通过。
- 新增事件元数据和旧接口适配的 7 项定向测试在 net8.0/net6.0 均通过；`Bing.EventBus.Tests` 完整回归两个框架各 92 项通过。
- 本轮在新增修复后重跑 `Bing.Core.Tests` 与 `Bing.Auditing.Tests` 完整回归：net8.0/net6.0 分别各 625 项、各 130 项通过。
- `Bing.Data.Tests` 完整回归在 net8.0/net6.0 各 151 项通过；框架层 EFCore 保存路径 12 项、FreeSQL 取消路径 5 项在 net6.0 通过。
- 本轮提交后通知改造后，`Bing.EntityFrameworkCore.Tests` 完整回归 net6.0 为 64/64；`Bing.FreeSQL.Tests` 完整回归 net6.0/net8.0 各 37/37；`Bing.Data.Tests` 完整回归 net6.0/net8.0 各 151/151。EFCore 测试项目仅配置 net6.0，未伪称 net8.0 已运行。外部事务测试确认通知失败不阻止调用方随后回滚。
- Admin EFCore 与 FreeSQL 两个 net6.0 宿主均构建成功、0 错误；现有 Microsoft.Extensions 10/net6 兼容警告和 NU1900 仍在，本轮未修改依赖版本。
- 临时 MySQL 8.4 + RabbitMQ 4.3 容器中，Admin EFCore 与 FreeSQL 的 `AdminMySqlCapIntegrationTest` 各 7 项在 net6.0 通过，包括新增的元数据往返、真实订阅和派生消息同事务出箱。容器测试后已停止并移除。
- 第一次连接 MySQL 时本机 TLS 协商失败；仅对本机临时测试连接使用 `SslMode=None` 后通过。FreeSQL 一次完整运行有 1 项原有消费用例等待失败，单独复测及再次完整运行均通过；保留该偶发观察，不把首次失败抹除。
- Admin 两个集成测试项目仅目标 net6.0；尝试 net8.0 因资产文件无该目标而未运行，不改项目目标框架绕过。
- 本轮领域事件改造后，`Bing.Ddd.Domain.Tests` net6.0/net8.0 各 152/152，`Bing.EntityFrameworkCore.Tests` net6.0 为 69/69，`Bing.FreeSQL.Tests` net6.0/net8.0 各 42/42；两个 Admin net6.0 宿主构建成功。领域事件直接测试包含同步/异步分发、派生事件、32 轮上限、失败回滚、外部事务拒绝和清理异常。
- 本机临时 MySQL 8.4 + RabbitMQ 4.3 上，Admin EFCore、FreeSQL 的 CAP 真实集成测试最终各 7/7 通过。EFCore 首次运行有一项订阅等待超时，单项与全组复测通过；本机测试连接需 `SslMode=None;AllowPublicKeyRetrieval=True`。临时容器已停止移除。
- Admin MySQL/CAP 验收脚本改为至少 7 项且全部通过；脚本自测通过。现有验收脚本仍依赖专用外部环境，不由模板自动启动容器。
- 本轮消费收据改造后，`Bing.EventBus.Tests` 完整回归 net8.0/net6.0 各 96/96；新增 SQLite 测试覆盖重复、逻辑消费者隔离、事务回滚重试及取消。
- 临时 MySQL 8.4/RabbitMQ 4.3 上，新增 EFCore 幂等、回滚、并发竞争 3/3，FreeSQL 幂等和回滚 2/2 定向测试通过；EFCore CAP 全组 10/10 通过。
- FreeSQL CAP 全组在新增用例后连续两次运行分别为 6/9、6/9，订阅等待项在 30 秒内超时；定向运行可通过。CAP 表中相关消息一部分已 `Succeeded`，另一部分仍为 `Scheduled`。尝试隔离测试队列版本未改善结果，已撤回该尝试。该全组稳定性仍是可执行问题，不能记作通过。
- 随后定位到短生命周期测试宿主启动竞态：`Host.StartAsync()` 返回时 CAP 的 per-group dispatcher 和 RabbitMQ 主题绑定可能尚未就绪。两套 ORM 的集成测试在待测发布前通过同主题真实消费探针确认就绪；FreeSQL 修复后原 9 项全组连续两轮 9/9。
- 两套 Admin 工作单元改由基类在选择事务路径前登记领域事件，再调用各自 CAP 事务钩子；真实 MySQL 8.4/RabbitMQ 4.3 测试中，EFCore 全组 13/13、FreeSQL 全组 12/12 通过，包含领域事件异步提交、同步保存、失败回滚和消费幂等。一次额外 FreeSQL 验证因测试容器已停止而出现 12 项连接失败，重用仍运行的隔离容器复测 12/12 通过；这是验证环境中断，不是代码回归。
- `Bing.EntityFrameworkCore.Tests` net6.0 69/69；`Bing.FreeSQL.Tests` net6.0/net8.0 各 42/42；Admin EFCore 非环境门控测试 8 通过、13 跳过。
- Admin FreeSQL 非环境门控测试 23 通过、12 跳过；WebApi 示例及两套 Admin 宿主构建均为 0 错误。Admin 宿主各有 52 个现存警告，本轮未因警告修改依赖版本。
- MySQL/CAP 验收脚本按项目分别要求 EFCore 至少 13 项、FreeSQL 至少 12 项，且全部通过无跳过；PowerShell 自测覆盖门槛及预检边界并通过。

实际 SDK 为 8.0.425；锁定的 8.0.424 未验证。NuGet 漏洞元数据网络不可用产生 NU1900，未造成上述构建失败。

## 计划偏差、已知问题与后续可执行项

本轮在完成 P0 核心修复后并行落地了 P3/P4 的独立基础设施；这不改变 P1、P2 必须独立验收的要求。旧 `Bing.EventBus` 与当前推荐 `Bing.Events` 并存，不能隐式绑定到同一发布链。Redis 租约过期及主从切换不能保证库存独占；默认审计日志不等于可靠落库。

该早期阶段的“尚未进入有序消费、审计持久化”结论已被后续实现取代：有序存储、数据库审计、Admin HTTP 接线、实体变更快照和整单库存业务均已存在。当前仍需库存双进程、生产有序恢复、关键恢复审计、真实故障、角色分离、动态 Kubernetes/Compose、锁定 SDK 与长期容量验收；各历史单机或双进程证据只覆盖对应范围。

## Review 修复记录

- FIX-001：已修复闲置回收提前恢复额度的问题；本地可控时钟和真实 Redis 有直接回归。
- FIX-002：已修复释放失败遮蔽业务异常的问题；同步和异步 Redis 双失败路径有直接回归。
- FIX-003：已提供公开异步审计作用域并等待在途保存；保存失败及同步释放约束有直接回归。
- FIX-004：发布侧元数据、旧接口显式适配、双框架回归及真实 CAP 出箱/订阅链路已验证。
- FIX-005：完成事务回调分轮、两 ORM 派生消息同事务出箱、事务内领域事件分派、保存后通知失败诊断、可选业务消费收据，以及领域事件到真实 CAP 消费的端到端验证。测试宿主订阅就绪竞态已用同主题探针隔离。
- FIX-006～012：仍有可执行工作，详见独立 `review.md`。不得把单机 MySQL/CAP 测试替代多实例竞争或蓝绿动态验收。

## Git 状态

工作区在本任务开始前已有大量其他未提交更改；本轮保留它们。未自动 git commit、push 或创建 PR。未执行可能发送外部通知的 `task-finish.mjs`。

## 2026-10-07 FreeSQL Admin Hangfire Task 槽位补齐

FreeSQL Admin 已与 EFCore Admin 对齐 Hangfire 任务宿主接线：生产工程链接 Hangfire 模块、运行时、应用层创建/执行排空过滤器和宿主停止等待服务；`Startup` 在 Web/Task 角色加入 Hangfire 模块，在 Consumer 角色跳过该模块。生产工程 `net6.0` 构建结果为 0 错误、98 个既有警告。

新增 `Bing.Admin.FreeSQL.Tests.AdminHangfireRegistrationTest`，覆盖 Task 角色运行时和托管服务、Web/Consumer 角色不启动 Server、排空过滤器拒绝新任务并等待在途任务、Consumer 启动配置跳过 Hangfire 模块以及 Task 启动配置加入 Hangfire 模块。定向测试最终为 6/6 通过，结果文件为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-freesql-hangfire-drain-net6-v3.trx`。

因此蓝绿 Task 模板不再只适用于 EFCore；两套宿主均具备同一应用层排空契约。真实 Hangfire Server 拉取停止、任务存储迁移、多节点排空和动态 Compose/Kubernetes 切换仍未在本机验证，不能据此宣称 P4 动态验收完成。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证。

## 2026-10-05：FIX-006 / FIX-013 继续实施

### 当前实现

- 有序存储：`OrderedEventSchema`、`DbOrderedEventStore` 及中立契约；按租户/流/实体分配序号，收件箱持久化、游标同业务事务推进、当前读防旧快照、失败前序阻塞和显式重试。默认正文 65536 UTF-8 字节、单流未完成 10000 条；全局容量、时间指标和归档尚缺。
- 新入口：`OrderedEventPublisher` 要求调用方已有相同 CAP 数据库事务；`OrderedEventCoordinator` 等待接收提交，并保留 Committed/RolledBack/Unknown 及原始清理异常；`AddBingOrderedConsumer<THandler>` 使用短期作用域分页处理，失败流不阻断其他分页。慢处理器隔离和公平调度仍缺。
- 两 ORM 所有权：工厂只释放本次事务/工作单元；重复释放共享一次清理任务，释放后拒绝事务操作，清理双失败保留。FreeSQL 原生 Commit/Rollback 为同步 API，不宣称非阻塞 I/O。
- 旧收据已移除广义 INSERT IGNORE/INSERT OR IGNORE；仅目标重复返回 false，其他约束错误继续传播。先前 SQLite 两项失败用例保留于 artifacts/receipt-constraint-before-fix.log；修复后通过记录保留。
- 提交诊断：两 ORM/Admin 标记提交结果，已提交后的事件移除失败不遮蔽原错；FreeSQL 保存阶段使用 DeferredCommitUnitOfWork 抑制内部提前提交。失败后丢弃上下文；Unknown 先查业务幂等结果，不重放外部副作用。
- 新增有序事件指南、Events README 入口及生产符号测试映射。基础有序事务工厂不会自动将处理器派生 CAP 发布绑定同事务，文档明确该限制，不把示例文案当作已实现适配。

### 此次夹具修复和验证

实际 SDK 8.0.425；global.json 8.0.424 未改，锁定版本仍未验证。使用当前任务标签的本机临时 MySQL 8.4/RabbitMQ 4.3 容器，专用 bing_cluster_test 数据库及随机业务隔离键。

| 验证 | 记录 | 结果 |
| --- | --- | --- |
| SQLite 收据与有序入口 net6.0 | artifacts/receipt-and-ordering-tests.log | 26/26；CAP 发布器边界使用替身，不代表真实 CAP 验收 |
| EFCore 保存类与有序事务工厂 net6.0 | artifacts/ordered-ef-lifecycle-tests.log | 30/30（保存类 22、工厂 8） |
| 首次 MySQL/FreeSQL 定向 net6.0 | artifacts/ordered-mysql-process-tests.log | 13 通过、2 失败、0 跳过；两项快照测试第二连接丢口令，在业务断言前认证失败 |
| 修正快照连接后 MySQL/FreeSQL 定向 net6.0 | artifacts/ordered-mysql-process-fixed-tests.log | 14 通过、1 失败、0 跳过；两项快照通过，双进程消费父等待期限超时 |
| 修正父子期限与诊断后 MySQL/FreeSQL 定向 net6.0 | artifacts/ordered-mysql-process-final-tests.log | 15/15、0 跳过，44 秒；两个快照、两进程分配/消费、消费强杀恢复及收据约束全部通过 |

两项快照测试改从原始测试配置创建连接，不设置 PersistSecurityInfo、不记录连接串、不修改生产认证。消费探针自身期限 60 秒，父进程原先只等 50 秒，会在探针清理/诊断前截断；改为 75 秒保留清理窗口，原断言和子进程 60 秒上限不变。新增提交进度及退出码/错误输出；最终 15/15 通过。此前失败日志保留，不删除首次失败证据。消费进程 ACTIVE/COMMITTED 时强杀、两进程序号分配、收据 CHECK/非目标唯一键及两工厂终态已有直接测试。

### 明确剩余

FIX-006：正在补真实 CAP 新入口测试；仍需发布进程中断/真实提交断连、授权查询/恢复、失败标记并发窗口等。FIX-013：最后改动后两套 Admin CAP 全组与完整受影响回归尚待运行。FIX-007～012 均保留原计划边界和可执行 TODO，不因本批定向通过改成完成。review.md 保留独立审查证据，修复状态以本报告与日志为准。

本阶段继续实施中，不发送外部通知、不 commit/push。当前临时验收容器仍供 CAP 验证使用；仅处理本任务创建容器，不动其他服务。

### CAP 真实链路和回归进展

新增 `Bing.Admin.FreeSQL.Tests/OrderedEventCapIntegrationTest.cs`：独立 Host 使用生产发布器/协调器/后台消费者及 FreeSQL 事务工厂，稳定订阅组和随机业务流，探针确认订阅实际就绪。验证业务/出箱同事务、后台提交顺序、重复投递及发布回滚。首次空库一项探针在 CAP 后台建表前失败；只在探针阶段对原生 1146/cap.published 缺表增加有界等待，业务错误不重试。首次日志 artifacts/ordered-cap-integration-tests.log 保留；修复后两项 net6.0 全通过，日志 artifacts/ordered-cap-final-integration-tests.log。

旧 Admin CAP 全组在最后事务修改后重新运行：EFCore 13/13、FreeSQL 12/12，0 跳过，验收脚本状态 Passed。TRX 与诊断目录 artifacts/admin-mysqlcap-results/cluster-order-final/net6.0-20261004T174838497Z。

随后补第三项真实 CAP 3→1→2 到达测试，缺号时游标0且无业务，补齐后按实际插入顺序1→2→3。三项新链路定向 net6.0 为3/3、0跳过，日志 artifacts/ordered-cap-three-integration-tests.log。CI 脚本 FreeSQL 验收范围扩大为旧 Admin 类 + 新有序类，门槛由12提高到15，EFCore仍13；脚本自测同步验证范围和计数，10项计数边界、范围检查和5项预检均通过。扩大范围后的完整脚本正在运行。上述旧25项通过不冒充升级后15项门槛通过。

### 扩大门控失败及 FIX-013 / FIX-012 修复

上述扩大门控已经结束，结果为失败：cluster-order-gated/net6.0-20261004T175711741Z 的 EFCore TRX 为12通过、1失败、0跳过；FreeSQL尚未执行。失败方法 DomainEvent_WhenBusinessFails_ShouldRollbackCapMessage，在业务断言完成后的 Host.Dispose 抛 ObjectDisposedException，调用链为 ConsumerRegister.Pulse→Dispose→AspectCore ServiceResolver.Dispose。原失败 TRX 和诊断保留。

读取 CAP8.0.1 正式包提交 e787869a1d324f04f19abc73cae8516b7717cca7 的消费者及启动器源码：消费者 Start 先挂停止令牌的 Dispose，最后重置释放标识；Bootstrapper 原先经 BackgroundService 启动，在异步启动尚未结束时 Host.StartAsync 已可返回。停止期间可能取消活动启动、释放取消源，再被启动尾部恢复为可释放状态。原生已取消令牌和启动中取消均可通过直接测试复现 ObjectDisposedException。相关官方源码保存于 artifacts/cap-consumer-register-8.0.1.cs、cap-bootstrapper-8.0.1.cs，不作为生产编译文件。

修复使用内部 CapHostedLifecycleService，在 AddCapEventBus 处衔接公开 IBootstrapper.BootstrapAsync；保留其他 HostedService 顺序及幂等注册。启动完成后才执行唯一关闭任务；关闭的同步回调在独立任务中运行，停止等待取消不提前释放活动资源。Dispose/DisposeAsync 共用关闭任务；启动原异常与关闭错误分别保留。普通 AddCap 和直接调用原生接口仍保留底层行为，框架没有重写 CAP 或 DI 容器。

行为变化：Bing 的 Host.StartAsync 会等待 CAP 启动方法结束；启动令牌取消后也等待安全清理。此处不承诺 RabbitMQ 订阅就绪或全部在途业务已经排空；保留真实消息就绪探针。停止等待超时后必须等待后续清理完成，再释放根容器；强杀仍依靠事务及幂等恢复。

验证：artifacts/cap-host-lifecycle-before.log 为修复前7项中6失败、1通过；初次修复后7/7。扩展测试发现重复注册多一个 CAP 宿主，修复后 artifacts/cap-host-lifecycle-complete.log 为16/16、0跳过（net6.0），覆盖两容器、启动/停止并发、取消、阻塞同步清理及原异常保留。另补两容器 DisposeAsync 直接路径和真正 Generic Host 的停止超时断言，尚待最终回归。保留中途失败日志，不删除失败断言。

FIX-012 中的验收失败诊断已修复：保留真实预检、失败项目、测试退出码、实际存在的 TRX 列表；门槛失败与测试退出码0区分。luna_worker 仅负责两个PowerShell脚本，未修改生产代码；自测通过8项预检/失败诊断和原有计数门槛。新验收仍维持 EFCore至少13、FreeSQL至少15且无失败/跳过。

该批 cap-lifecycle-fixed 门控已结束：EFCore13/13、FreeSQL15/15，0失败/跳过；后续保存点和入箱重试另需新回归。SDK实际8.0.425，锁定8.0.424未验证。此修复不关闭整个 FIX-013：真实提交断连、CAP提交后清理故障注入、Unknown查询示例及最新完整回归仍需分别交付。FIX-006～012的其他能力继续按审查范围实施，不缩减完整P0–P4目标。

## 2026-10-05：失败标记竞争与只读状态查询

继续处理FIX-006、FIX-012、FIX-013，完整P0–P4目标仍在执行。本次保留独立review.md，不将剩余库存、自动审计、限流生产接入、蓝绿宿主和运行治理工作标为完成。

### 根因与候选实现

旧协调器在业务回滚后解锁游标，再另开事务写失败标记，允许其他节点在窗口内自动执行业务。新路径在游标锁之后建立保存点：普通业务异常回滚保存点中的业务，再在仍持有游标锁的外层事务写失败标记并提交。调用方取消直接回滚，不写失败标记；对外保持同一原始业务异常，写入、提交、回滚和释放错误附加保存。

外层物理事务只提交失败标记时，TransactionOutcome为Committed、BusinessRolledBack=true、FailureRecorded=true；这不表示业务成功。标记提交确认丢失仍为Unknown，必须查持久状态。内置MySQL/SQLite存储对旧驱动使用SQL保存点；自定义存储缺少原生保存点时在业务回调前拒绝执行。保存点不恢复ORM跟踪、事务回调或CAP内存发送缓冲，基础工厂不自动绑定派生CAP消息；失败后丢弃上下文。

DbOrderedEventStore.GetStateAsync从隐式游标创建/写锁改成一条普通SELECT，未知流返回空状态。同语句内部状态由调用方事务快照决定，不承诺总是最新；协调器的事务工厂和SQLite默认立即事务仍有锁行为，不能把存储查询只读等同于入口无锁。

### 直接失败证据及修正

- 初次候选：ordered-atomic-tests/ordered-atomic-initial.trx为7通过/1失败，失败发生在只读SQLite夹具的默认立即事务；已显式改为deferred，未降低只读断言。
- 真实FreeSQL SQLite初次候选：freesql-savepoint-initial.trx为0通过/1失败，原生DbTransaction不支持保存点；为内置存储增加同事务SQL回落，未另开失败记录事务。
- 旧方法回放：ordered-old-replay/Bing.EventBus.Tests-old.trx为1通过/2失败，直接复现未知流新增游标、LockCursorAsync在只读事务写入；Bing.FreeSQL.Tests-old.trx为0通过/2失败，在真实MySQL复现失败标记窗口业务可被另一消费者执行及只读事务拒绝写入。它们是旧方法对照，不是修复后的结果。回放finally恢复候选两文件，逐字比对备份一致。
- 修复后EventBus有序专项net6：ordered-candidate-tests/eventbus-ordering-net6.trx为28/28，0失败/跳过，包含六项保存点原子失败/取消/提交前后故障及三项状态只读/未知流/隔离测试。
- 补充真实FreeSQL SQLite自定义存储边界测试Coordinator_WhenCustomStoreLacksNativeSavepoints_ShouldRollbackBeforeBusiness，验证回调不执行、外层一次回滚/释放、仍待处理、宿主ORM可继续使用；已包含在ordered-verified-regressions/freesql-direct-net6.trx的10/10通过中，EFCore工厂专项为9/9。

### 还原与实际版本

实际SDK8.0.425；global.json8.0.424未改。初次EF测试还原因在线NuGet不可达、6.0.*浮动版本列表缺失报NU1101；原日志ordered-candidate-tests/ef-restore.log保留。改用现有全局缓存及仓库.admin-packages作为本地源，临时命令关闭NuGet漏洞元数据查询，还原成功（ef-restore-local.log）。资产中的Microsoft.EntityFrameworkCore为6.0.36，与此前已编译deps.json一致；无依赖声明或版本修改。此还原不代表完成在线漏洞数据库检查。

### 已结束基线与本次验证范围

补录上一批结果：CAP宿主定向net6/net8各18/18；Admin cap-lifecycle-fixed门控EFCore13/13、FreeSQL15/15，0失败/跳过；cap-lifecycle-regressions.json的9次回归合计1089项执行通过。它们是保存点/查询改动前基线，不代替最新结果。

保存点/查询改动后六次执行已结束：FreeSQL专项10/10、EFCore专项9/9、EventBus net8/net6各145/145、FreeSQL net8为61/61、net6为60/61，合计430通过/1失败/0跳过。失败发生于MySQL前置入箱INSERT1213死锁，不是只读查询断言。脚本因失败退出，后续EFCore全量、Admin门控、Sample与构建未执行；旧JSON/TRX保留，后续结果另目录存放。

文档已更新有序指南、集群指南、Events README和生产符号映射；后续最终结果另附本报告。当前状态IN_PROGRESS，未commit/push或发送外部通知。

## Review 修复记录：2026-10-05 入箱死锁及EFCore有序链路

- Review状态：NEEDS_FIX；Fix Scope：recommended；仍处理FIX-006/012/013，完整P0–P4未完成。
- 实际问题：两个不同消费者的入箱事务产生InnoDB PRIMARY supremum间隙锁/插入意向锁死锁。诊断见ordered-verified-regressions/mysql-deadlock.log；保留原FreeSQL60/61失败，不将其归为环境或查询缺陷。
- 修复：ReceiveAsync只对内置DbOrderedEventStore、实际驱动MySQL1213、确认RolledBack且无清理失败，最多追加三次整笔入箱事务尝试；分别等待20/40/80ms并响应取消。每次回滚释放先完成；不重试业务处理、Unknown/Committed、1205、自定义存储或清理失败。类型元数据仅在调用内使用，无新增静态缓存或驱动生产依赖。
- 定向验证：ordered-receive-retry-regressions/receive-retry-direct-net6.trx及net8.trx各7/7、0失败/跳过；覆盖成功重试、四次耗尽、清理失败、取消、提交阶段Unknown、1205和自定义存储。SIGNAL为真实驱动错误形状注入，不声称制造引擎死锁或确认丢失。
- 回归：同目录results.json顺序更新，尚在执行；其后Admin13/15门控、Sample业务值/StopAsync及构建未提前记为通过。SDK8.0.425，锁定8.0.424未变。
- 文档：有序指南、集群指南、Events README与生产符号映射补充重试范围、CAP重投预算和故障证据边界；回归完成后补最终计数。
- 下一能力：补EFCore真实MySQL/CAP/RabbitMQ有序三条链路，宿主使用AspectCore；先独立测试，确认通过再扩大CI门槛。不修改独立review.md。


## 2026-10-05：EFCore CAP 事务包装、门控与回归收口

- 修复 `OrderedEventPublisher` 的事务身份判断：CAP 的 `DbTransaction` 可以直接持有 `DbTransaction`，也可以持有 EFCore `IDbContextTransaction` 包装对象。发布器现在在不引入 EFCore 编译依赖的前提下，通过受限反射调用已加载的 `DbContextTransactionExtensions.GetDbTransaction`，仅在底层事务对象与调用参数相同才通过校验；无法取得底层事务时仍拒绝发布。
- 新增 `OrderedEventPublisherTest.PublishAsync_WhenCapTransactionWrapsDatabaseTransaction_ShouldAllocateAndPublish`，Bing.EventBus.Tests 的该定向组在 net6.0、net8.0 均为 4/4，通过直接事务与包装事务两条路径。
- `OrderedEventCapIntegrationTest` 在 AspectCore + EFCore + CAP + RabbitMQ 真实宿主中 net6.0 为 3/3；此前的失败是 EFCore 包装事务被错误拒绝，已由生产修复收口。
- Admin MySQL/CAP 门控已把 EFCore 新有序链路纳入范围：EFCore 16/16、FreeSQL 15/15，0 失败、0 跳过；门控脚本自测仍通过。
- Sample `/sample/lazy` HTTP 请求和 `StopAsync` 端口释放 net6.0 为 1/1；WebApi net6.0 构建 0 错误。
- FreeSQL net6.0 全量回归在 MySQL 有序集成测试串行化共享表后为 68/68；此前同组并行初始化导致的 InnoDB 入箱 1213 死锁证据仍保留。EFCore 全量 net6.0 为 80/80。
- 本轮使用 SDK 8.0.425，`global.json` 仍锁定 8.0.424；未宣称锁定版本已验证。NU1900、net6.0 EOL 和 Microsoft.Extensions 10/net6 警告仍为环境/依赖现状，不改变上述测试退出码。

本批修复仅完成 EFCore 真实事务兼容、受影响门控和当前回归收口，不关闭 FIX-006～013。库存业务、HTTP/ORM 审计、Admin 限流接线、用途驱动原子工具、蓝绿动态演练、提交确认丢失与 CAP 清理故障注入、指标容量/归档/GC 证据仍按 `review.md` 的 MUST_FIX/SHOULD_FIX 和边界 TODO 推进。


## 2026-10-05：HTTP 限流故障策略与键长度收口

- `BingRateLimitOptions` 新增 `MaxKeyLength` 与 `FailOpenOnInfrastructureError`。中间件对超长策略/路由/主体键使用长度受限的 SHA-256 摘要，避免高基数原文进入共享存储；基础设施异常默认记录错误并返回 503，只有显式启用降级时才继续执行请求。
- `BingRateLimitMiddlewareTest` net6.0、net8.0 各 7/7 通过，覆盖默认拒绝、显式降级、超长键上限、取消传播和既有 429/键隔离行为。
- 该批次只收口中间件的故障语义和键长度边界；FIX-008 的 Admin 生产接线、可信代理/租户键验证、真实 Redis 故障演练仍未完成。


## 2026-10-05：HTTP 审计中间件基础能力

- `Bing.AspNetCore` 新增可选 `AddBingHttpAuditing` / `UseBingHttpAuditing`。中间件只采集方法、受长度限制的 URL、状态码、客户端地址、身份、客户端标识和关联 ID，不读取请求正文、令牌或完整参数。
- 支持 `DisableAuditingAttribute` 端点标记；审计保存失败默认记录日志并保留业务异常。`ThrowOnStoreFailure` 仅传播没有原始业务异常时的保存错误；保存发生在业务后，不能撤回响应或数据库提交，关键事务审计使用后续提供的显式事务入口。作用域保存后始终执行异步释放。
- `BingHttpAuditingMiddlewareTest` net6.0、net8.0 各 5/5，通过请求元数据、禁用标记、业务异常优先、保存失败策略和注册配置测试。
- 该实现只完成 HTTP 采集入口；默认 `SimpleLogAuditingStore` 仍不是可靠落库，EFCore/FreeSQL 审计存储、敏感字段白名单和关键业务同事务审计仍属于 FIX-009 剩余工作。


## 2026-10-05：HTTP 流量闸门与原子排空计数

- `Bing.AspNetCore.Hosting` 新增 `IBingTrafficGate`、`BingTrafficGate` 和 `BingTrafficGateMiddleware`。闸门使用 CAS 切换接收/排空状态，使用 `Interlocked` 维护在途 HTTP 请求数；句柄重复释放不会减少两次。
- `AddBingTrafficGate` 注册宿主停止排空服务，`UseBingTrafficGate` 提供 `/health/ready` 和排空期间的 503/Retry-After。等待超时或取消只结束等待，不提前释放活动请求。
- `BingTrafficGateTest` net6.0、net8.0 各 7/7，通过重复释放、显式就绪、排空拒绝、取消等待、就绪探针、幂等注册和宿主停止取消测试。
- 该组件只覆盖 HTTP 请求，不能替代 CAP 消费者、定时任务或 Admin 宿主的角色切换；FIX-011 的生产接线和 Compose/Kubernetes 动态演练仍未完成。


## 2026-10-05：Admin 宿主接入流量闸门与 HTTP 审计

- EFCore 与 FreeSQL Admin 的 `AppModule` 已注册 `AddBingTrafficGate`、`AddBingHttpAuditing`，请求管道分别启用流量闸门和 HTTP 审计；审计放在 `UseRouting` 之后，以便识别端点禁用标记。
- 两个 Admin 宿主 net6.0 构建均为 0 错误；现有 net6 生命周期、Microsoft.Extensions 10 和 NU1900 警告保留，未通过升级依赖规避。
- 该接线提供真实 `/health/ready` 和 HTTP 请求排空入口，但尚未完成消费者角色开关、CAP 消费排空、切流回滚和动态 Compose/Kubernetes 演练，不能据此关闭 FIX-011。


## 2026-10-05：有序消费者聚合指标

- `Bing.Events.Ordering` 新增 `IOrderedEventMetrics`、`BingOrderedEventMetricsSnapshot` 和 `BingOrderedEventMetrics`，按逻辑消费者隔离记录待处理流数、成功、缺口、失败、重试、在途数和最近处理耗时。计数使用原子操作，重复完成不会产生负在途数，不保存事件 ID/实体键。
- `AddBingOrderedConsumer` 将指标实例与对应 Worker 绑定；Worker 记录扫描、处理、缺口和失败，协调器成功执行显式 `RetryAsync` 后记录重试。
- `OrderedEventMetricsTest` net6.0、net8.0 各 3/3 通过。该指标仍是进程内快照，尚未接入 OpenTelemetry/Prometheus，也不能替代积压年龄、归档和长期容量验收。

## 2026-10-05：HTTP 限流租户与稳定路由键

- `BingRateLimitOptions` 新增 `TenantClaimType`、`IncludeTenantInKey` 和 `RouteKeySelector`。默认主体键读取 `tenant_id` 声明并与用户或客户端地址组合；路由选择器允许宿主返回不含参数值的稳定模板。组件不直接信任转发头，代理解析仍由宿主在限流中间件之前完成。
- `BingRateLimitMiddlewareTest` net6.0、net8.0 各 10/10 通过，覆盖租户隔离、显式稳定路由键和匹配路由模板，以及既有拒绝、故障降级、取消和长度上限行为。
- 该批只收口限流键语义；FIX-008 的 Redis 双实例故障演练、可信代理真实链路和 Admin 按部署策略接入仍未完成。

## 2026-10-05：事件总线指标接入回归

- `Bing.EventBus.Tests` 在 net6.0、net8.0 完整运行各 149/149，通过 0、跳过 0；覆盖有序消费者、CAP 适配、旧本地事件总线和新增指标路径。
- 该回归不替代 EFCore/FreeSQL 的 MySQL/CAP 外部环境验收，也不关闭库存、持久化审计、蓝绿消费者排空和提交不确定性等剩余工作包。

## 2026-10-05：有序事件恢复授权扩展点

- 新增 `IOrderedEventRecoveryAuthorizer` 与 `AddBingOrderedEventRecoveryAuthorization<TAuthorizer>`。`RetryAsync` 在开启恢复事务前执行所有授权器，拒绝时不会创建或写入数据库事务；未注册授权器时保持旧的显式调用兼容行为。
- `OrderedEventCoordinatorTest` net6.0、net8.0 各 7/7 通过，覆盖拒绝前置、数据库状态未改变和作用域注册生命周期。
- 该扩展点提供授权接入位置，不自动实现租户权限、审计落库或身份上下文；生产宿主仍需提供具体授权器和关键操作审计。
- `OrderedEventConsumerTest` 在 net6.0、net8.0 各 3/3 通过，确认新增可选构造参数不会改变后台消费者的默认作用域解析和停止行为。

## 2026-10-05：库存条件扣减基础存储

- `Bing.Data.Inventory` 新增 `InventoryDeductionRequest`、`IInventoryStore`、`DbInventoryStore` 和 `InventorySchema`。存储在调用方事务内登记租户/请求唯一键，再执行余额条件更新；重复请求返回首个结果，余额不足不产生负数，业务回滚同时撤销库存和请求记录。
- `InventoryStoreTest` net6.0、net8.0 各 5/5 通过，覆盖成功重复、余额不足、请求身份冲突、调用方回滚及 DI 注册保护。
- 该批是 ORM 无关基础能力；EFCore/FreeSQL 工作单元适配、关键审计同事务、多 SKU 固定排序、死锁有界重试和真实 MySQL 双实例竞争仍未完成。
- 新增库存及授权改动后的 `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 各 158/158 通过，0 跳过。
- `InventoryBatchOperations.DeductAsync` 新增固定排序和整批失败语义；库存定向测试在 net6.0、net8.0 各 8/8 通过，覆盖无序输入、余额不足中止、同租户重复请求以及不同租户复用同一请求标识。

## 2026-10-05：库存 EFCore/FreeSQL 事务适配

- 新增 `EfCoreInventoryExtensions` 和 `FreeSqlInventoryExtensions`。两者只复用调用方当前事务，不创建、提交或释放外部事务；没有活动事务时明确失败，避免库存扣减脱离业务工作单元。
- EFCore 适配定向测试 net6.0 为 2/2 通过；FreeSQL 适配定向测试 net6.0、net8.0 各 2/2 通过，覆盖有事务复用和无活动事务拒绝。生产工程 `Bing.EntityFrameworkCore` net6.0 和 `Bing.FreeSQL` netstandard2.0 均以 `--no-restore` 构建 0 错误。
- 该批仅证明两套 ORM 的当前事务复用和提交可见性；真实 MySQL 双实例竞争、CAP 出箱及关键审计同事务仍未完成，不能把 SQLite 适配测试扩展为分布式库存验收。
- 库存键边界修正后，`Bing.EventBus.Tests` 全量回归 net6.0、net8.0 各 159/159 通过，0 失败、0 跳过；实际 SDK 为 8.0.425，`global.json` 锁定的 8.0.424 仍未验证。

## 2026-10-05：FreeSQL 实体审计字段接入

- 将 `IAuditPropertySetter` 下沉到 `Bing.Auditing.Contracts`，`Bing.Auditing` 通过类型转发保留旧程序集限定名；FreeSQL 增加对契约程序集的直接引用，避免依赖审计实现包。
- `StoreBase` 在新增、修改和删除的单项及集合入口调用当前工作单元可选的 `IAuditPropertySetter`。未注册设置器时不改变原有行为；集合入口先物化并校验空实体，避免多次枚举和部分审计。
- `QueryStoreBaseTest.StoreBase_WhenAuditPropertySetterIsRegistered_ShouldSetCreateAndModifyProperties` 在 net6.0 通过 1/1；写入存储测试类在 net6.0、net8.0 各 22/22 通过。旧程序集转发测试 `IAuditPropertySetter_LegacyAssemblyQualifiedName_ShouldResolve` 在 net6.0、net8.0 各 1/1 通过；审计项目全量回归在 net6.0、net8.0 各 131/131 通过。
- 本批只补齐 FreeSQL 的实体字段填充，不代表审计日志持久化、EntityChanges 快照或关键审计与业务事务已经完成。
- `EfCoreAuditPropertySetterIntegrationTest.SaveChangesAsync_ShouldPersistCreationAndModificationAuditFields` 使用真实 SQLite 工作单元验证创建、修改审计字段写入并重新读取，net6.0 通过 1/1；同样不覆盖持久化审计日志和关键审计同事务。

## 2026-10-05：FIX-008 路由与可信身份限流

- 未匹配端点的请求改为共享 `unmatched` 分桶，避免随机 URL 持续创建分桶。匹配路由仍使用路由模板或端点名称，自定义 `RouteKeySelector` 保持显式覆盖能力。这是未匹配路径配额语义的有意调整，部署需确认未知路径不依赖逐路径配额。
- 新增 `BingRateLimitOptions.UserIdClaimType`，默认保留 `ClaimTypes.NameIdentifier`，JWT 宿主可使用 `sub`。默认租户声明只从已经认证的身份读取，未认证声明不再改变匿名客户端分桶。
- 新增 `BingRateLimitPipelineTest`，以真实 TestServer、认证处理器、本地令牌桶及 `ForwardedHeaders` 验证受信/不受信代理、同租户路由参数合桶、跨租户隔离和未知路由。
- 限流定向 net6.0、net8.0 各 17/17；MVC 全量各 87 通过、1 项现有 `UploadRawAsync` 跳过、0 失败。TRX 位于 `artifacts/rate-pipeline/`，实际 SDK 8.0.425，锁定 8.0.424 尚未验证。
- 文档修正：摘要只限制单键长度，不能降低键数量；宿主不得以无代理校验的 `UseRealIp()` 作为匿名限流的可信地址来源。Redis 双实例及故障演练、生产宿主整体验收仍属于 FIX-008 剩余工作；不修改独立 `review.md`，不把 P0–P4 标记完成。

### FIX-008：Admin 显式启用与代理信任接线

- EFCore/FreeSQL 两个 `AppModule` 增加 `RateLimiting:Enabled` 配置，默认关闭。开启时要求容量、补充率、闲置时间和键数，直接构造限流器完成数值预检；只注册一个 Redis 限流单例，保留调用方预注册服务。
- 两宿主使用固定策略 `Bing.Admin` 和 `BingClaimTypes.UserId/TenantId`，限流位于认证及路由之后。EFCore 宿主新增对已有 `Bing.Locks.CSRedis` 工程的引用，没有调整依赖版本。
- EFCore 原无条件 `UseRealIp()` 已移除。两宿主仅在显式提供有效 `RateLimiting:KnownProxies` 时调用 `UseForwardedHeaders()`，明确代理 IP、单跳转发；没有名单时保留传输层连接地址。这是旧代理 IP 用法的迁移项，指南已说明。
- 两个 Admin 注册测试组各 6/6、0 失败/跳过，TRX 位于 `artifacts/admin-rate-wiring/*-final.trx`。主代理核对最终 TRX 计数及修改；窄范围实现由 luna_worker 完成。
- 受影响本地回归：Admin EFCore 14 通过、16 MySQL/CAP 门控跳过；FreeSQL 34 通过、15 门控跳过，均 0 失败。本轮未启用 Admin 外部基础设施门控，不能把这些结果算作外部事务验证或实际 Admin HTTP 验收。
- Docker 普通权限访问失败，但经已授权的只读提升检查已确认 Redis/MySQL/RabbitMQ 本地验收容器在运行；Redis PING 成功。后续 Redis 专项使用本机 6379 的测试数据库 15 与唯一测试策略，不清空数据库、不停止现有容器。
- 真实 Redis 令牌桶专项最终 net6.0、net8.0 各 9/9、0 失败/跳过，TRX 为 `artifacts/rate-pipeline/redis-quota-net6-final.trx` 和 `redis-quota-net8-final.trx`。新增独立 DI 容器的 64 次并发竞争，合计只允许容量 5 次，并验证另一租户仍有独立额度；该测试在 finally 只删除自己拥有的策略键。
- FIX-008 仍为 PARTIAL：现有证据是组件真实 HTTP 管道、Admin 生产注册和真实 Redis 双容器配额的组合验证；完整 Admin HTTP/代理请求、跨进程额度、Redis 断连/故障切换演练尚未完成。SDK 锁定版、审计事务落库、库存完整业务、蓝绿动态验收及其余 FIX 保持未完成。

## 2026-10-05：整单业务、作用域与身份边界批次补录

本节补录上一实施批次；不把它当成本次新增测试。上文更早“完整库存业务缺失”的结论由本节及开头现状取代，历史结果保留。

- FIX-021：COMPLETED。`AddBingInventoryOrders` 默认由 Singleton 改为 Scoped，复用当前作用域的可替换 `IInventoryStore`。真实 Microsoft DI 的 ValidateScopes/ValidateOnBuild、SQLite 两作用域、释放一次回归已补。修复前 `inventory-scope-before-net6.trx` 为 0通过/1失败，修复后库存双框架各 26/26。
- FIX-022：COMPLETED。默认带租户主体分别对 tenant 和 user/IP 添加长度前缀；默认 Claims 碰撞、同身份及长摘要路径有直接测试。修复前 `identity-before-net6.trx` 为 2通过/1失败，修复后中间件/HTTP 管道双框架各 20/20。
- Admin 整单路径已组合首次结果、全部明细不足回滚、关键审计和实际 CAP 出箱。两 ORM 定向各 6/6，包含同订单竞争及不同订单竞争；均为同进程独立作用域，不宣称库存双进程已验收。
- 原批次 EventBus 各 182/182；MVC 各 102通过/1 NotExecuted；Sample 1/1；WebApi 构建 0错误/7警告。证据位于 `artifacts/cluster-scope-identity-fix/`。
- 原批次完整真实门控 EFCore22/22、FreeSQL21/21，0失败/跳过，位于 `artifacts/admin-mysqlcap-results/inventory-scope-identity/net6.0-20261005T050557761Z/`。CI 最低数量已从16/15提升到22/21，门控脚本自测及失败诊断通过。
- 新库存指南、配置说明、README 和生产符号映射已补；带租户限流键升级会使新旧实例使用不同桶，新桶有初始额度，文档要求协调切换，不声称自动延续原额度。

## Review 修复记录：2026-10-05 精确库存身份、限流长度与审计清理

- 模式：REVIEW_FIX；Review Round：1（复用 active 记录）；Fix Scope：recommended；Review 仍为 NEEDS_FIX。
- 本批处理 FIX-023，以及 FIX-007/009/012 中已有明确证据的身份迁移、清理异常和交付差距。保持 `review.md` 不变，完整 P0–P4 仍为 IN_PROGRESS。

### FIX-023：极短限流键

- 执行状态：COMPLETED。构造时要求 `MaxKeyLength >= 32`，移除小于前缀长度的截断分支；合理长度下保留现有 `sha256:` 键格式。
- 直接测试分别覆盖 -1、0、1、7、8、31 的拒绝，以及32、71、256的接受；同身份、长Claims、自定义键和真实HTTP管道保留。原风险的源算法复现见 `artifacts/cluster-current-audit-20261005/short-rate-key-reproduction.json`，不是运行期测试。
- 定向 net6 为29/29；net8的相同方法包含在最新MVC全量。显式配置小于32属于需迁移项；当前默认256不变。

### FIX-007：精确库存身份与事务引擎

- 工作包执行状态：PARTIAL；本批身份/首次部署/人工迁移子项已完成，不关闭库存双进程、整单死锁重试及Unknown恢复。
- `InventorySchema` MySQL身份列改为 VARBINARY512/1024并明确ENGINE=InnoDB；SQLite显式TEXT COLLATE BINARY。`DbInventoryStore` 在MySQL使用严格UTF-8字节参数和无损读取，SQLite保持文本；不依赖数据库默认排序规则。
- `InventoryDeductionRequest` 保留现有UTF-16长度128/256及大小写/尾空格/Unicode原值，拒绝无法无损编码的代理字符。新增直接基线为6失败/6通过：两项完整SQL和四种无效代理身份失败；修复后库存定向38/38。
- 新增显式脚本 `deploy/migrations/inventory/002-exact-identities.mysql.sql`：旧VARCHAR先转换utf8mb4及InnoDB，再改VARBINARY，避免直接保留latin1旧字节。注册及运行期不执行DDL；需要停写、备份、存量核对、维护窗口，DDL不原子且不承诺一键回滚。未对共享验收库或生产表执行迁移。
- 真实MySQL三项专项在随机独占库核对逐表/逐列元数据、四类身份的20次扣减/重复结果，以及latin1/MyISAM旧收据保留和回滚。新库创建会话使用MyISAM默认值，验证显式引擎不被默认覆盖。
- 初次专项0通过/3失败原因是测试账号没有随机库建库权限；原TRX保留，不算代码失败或通过。临时授权只限 `bing_inv_identity_<随机值>_test` 命名空间；最终撤销并确认无残留库，证据 `mysql-isolation-cleanup.log`。权限补齐后专项3/3，FreeSQL完整双框架各81/81。
- 两套Admin最新门控仍各包含六项库存完整业务，证明参数改动未破坏实际工作单元/CAP组合。新二进制表及迁移由隔离MySQL专项验证；共享库未迁移，普通门控不代替生产部署验收。

### FIX-009：HTTP审计多重清理错误

- 工作包执行状态：PARTIAL；本批异常完整性子项已完成。自动实体快照、普通审计生产配置、调用方取消令牌/慢保存预算及权限验收未关闭。
- 保存失败后仍独立等待异步释放；保存/释放错误按引用去重，将完整Exception[]附于原请求或首个清理异常的Data["Bing.HttpAuditCleanupErrors"]。请求原异常对象优先；默认告警和ThrowOnStoreFailure原规则保持。
- 日志仅输出错误数、有限类型和关联标识，不传递异常对象/消息；同文件XML注释已补。
- 新增失败基线3失败/1通过；修复后审计中间件双框架各12/12。覆盖保存/释放同时失败、请求原异常、只有释放失败、默认放行及秘密不泄露；真实AuditingManager同一失败在Save/Dispose重复出现时只保留一条。

### 本批当前验证

所有组顺序运行；数量来自实际 UnitTestResult/outcome，定向和全量重叠，不能累计作独立覆盖。汇总见 `artifacts/inventory-identity-length-fix/results.json`。

| 范围 | 当前结果 |
| --- | --- |
| 库存新直接基线net6 | 6通过/6失败；修复前证据 |
| HTTP审计新基线net6 | 1通过/3失败；修复前证据 |
| 库存修复后定向net6 | 38/38 |
| 限流定向net6 | 29/29 |
| 审计中间件定向net6/net8 | 各12/12 |
| EventBus全量net6/net8 | 各194/194 |
| FreeSQL全量net6/net8（启用真实MySQL） | 各81/81 |
| MVC全量net6/net8 | 各115通过/1既有UploadRawAsync NotExecuted，0失败 |
| Admin完整真实MySQL/CAP/RabbitMQ net6 | EFCore22/22、FreeSQL21/21，0失败/跳过 |
| Sample真实Lazy业务请求、StopAsync及端口释放net6 | 1/1 |
| WebApi net6构建 | 0错误/7警告 |
| FreeSQL.Tests 最终夹具源码net6构建 | 0错误/15警告；夹具初始化失败时附加清理错误，保留原始异常 |

完整Admin证据：`artifacts/admin-mysqlcap-results/inventory-identity-length/net6.0-20261005T055618182Z/`。其余证据在本批目录；失败基线和环境初次失败不删除。176个中文XML文档块解析成功，迁移相对链接存在；新增和修改生产符号已映射直接方法。

本轮实际SDK8.0.425；global.json仍锁定8.0.424，未修改目标框架、依赖或包版本。NU1900在线漏洞元数据、既有Admin Microsoft.Extensions10/net6及MSB3277警告保留，未以升级依赖规避。没有以测试通过宣称“没有内存泄漏”；集群组件长期GC、容量和存活对象探针仍待完成。

后续仍按review推进：库存双进程/确认回滚后的整单重试及真实Unknown；生产有序授权恢复；跨进程限流与Redis故障；自动审计；角色/预热/消费任务排空与双平台动态部署；指标导出、归档及长期测量。不执行task-finish，不commit/push或发送外部通知。

## Review 修复记录：2026-10-05 身份契约与限流多身份

- 模式：REVIEW_FIX；Review Round：1（复用 active 记录）；Fix Scope：recommended；独立审查仍为 NEEDS_FIX，其他工作包继续进行。

### FIX-024：库存与关键审计租户身份约束冲突

- 执行状态：COMPLETED（生产代码、迁移脚本和两套真实 Admin 直接用例已完成）。
- 根因：库存请求保留首尾空格和 Unicode 原值，关键审计原先使用 Trim 拒绝且 MySQL 租户列为文本类型，导致同一业务入口的身份契约不一致；旧 MySQL 的 PAD SPACE 规则还会合并 `tenant` 与 `tenant `。
- 修复：`DbAuditingStore` 新增严格 UTF-8 租户校验并保留原值，MySQL 审计租户列改为 `VARBINARY(512)`；新增 `deploy/migrations/auditing/003-exact-tenant-identities.mysql.sql`，要求停写、备份和显式维护窗口。SQLite 仍使用 `TEXT COLLATE BINARY`。文档已说明不得 Trim 或 Unicode 规范化。
- 修复前证据：`admin-ef-exact-tenant-net6.trx` 为0通过/1失败，证明旧 VARCHAR/PAD SPACE 会把两种租户合并；失败未删除。
- 修复后：`DbAuditingStoreTest` 与 `AuditLogSchemaTest` 为20/20，`artifacts/identity-contract-fix/audit-identity-final-net6.trx`；执行迁移后的专用 MySQL/RabbitMQ 真实 Admin EFCore 与 FreeSQL 尾空格租户用例各1/1，证据为 `admin-ef-exact-tenant-after-migration-net6.trx` 和 `admin-freesql-exact-tenant-after-migration-net6.trx`。
- 外部状态：迁移只执行于专用 `bing_cluster_test`，未执行生产数据库；两条 DDL 为非原子维护操作，旧实例不能与新身份规则混跑。

### FIX-025：限流声明读取未限定已认证身份

- 执行状态：COMPLETED。
- 根因：中间件先检查 `User.Identity.IsAuthenticated`，再通过 `FindFirst` 跨所有 ClaimsIdentity 读取租户和用户声明。
- 修复：按声明主体顺序选择第一个已认证 `ClaimsIdentity`，用户和租户声明均从该身份读取；没有已认证身份时继续使用匿名 IP，显式 `KeySelector` 语义不变。指南补充多身份可信声明规则。
- 失败基线：新增6个单元/TestServer测试复现问题，`artifacts/identity-contract-fix/rate-identity-before-net6.trx` 为29通过/6失败。
- 修复后：限流定向 net6/net8 各35/35，证据为 `rate-identity-after-net6.trx` 和 `rate-identity-after-net8.trx`；包含认证身份顺序、未认证声明、双认证 scheme 和真实 TestServer。

### 本批执行边界

- 未修改 `review.md` 状态；未提交、推送或执行 task-finish。完整 P0–P4 和其余 FIX 保持未完成。
- 实际 SDK 为8.0.425，锁定8.0.424仍未验证；NU1900在线漏洞元数据获取失败仍保留。


## Review 修复记录：2026-10-05 库存清理、跨进程验收与审计身份

- 模式：REVIEW_FIX；reviewRound=1（复用active记录）；fixScope=recommended。review.md保持本轮独立审查原文，完整P0–P4继续IN_PROGRESS。
- 上一Goal回合分类为progress：只读审查更新了两个问题的代码证据及验收缺口。本批按FIX-026/028、FIX-027及库存验收推进，不缩减七组完整交付目标。

### FIX-026：恢复结果不能遮蔽清理错误

- 执行状态：COMPLETED（实现及直接定向验收）；FIX-007/013整体仍PARTIAL。
- 业务成功但作用域释放失败时保留释放异常，标明Committed，首次结果附于Bing.InventoryCommittedResult；恢复查询确认提交但仍有清理错误时保留原失败及Bing.InventoryRecoveredResult；查询失败附于Bing.InventoryRecoveryException。
- 原始业务异常优先，作用域清理错误附于Bing.InventoryScopeCleanupException；等待释放完成后才返回、查询恢复或进入新尝试。调用方取消不启动恢复，不以超时强行释放。
- 新10个行为案例覆盖正常恢复、Unknown/Committed加清理失败、仅清理失败、空恢复、恢复抛错、RolledBack、取消及两个异步释放等待路径。包含在两套Admin各18/18的执行器定向中；不能将假工作单元恢复契约测试当实际网络提交确认丢失。

### FIX-028：统一重试和恢复的清理判定

- 执行状态：COMPLETED。
- IsRetryableDeadlock复用HasCleanupFailure；作用域、事务释放、回滚诊断或聚合错误均阻止自动重试。保持原生驱动1213、明确RolledBack、最多四次独立作用域和20/40/80ms间隔。
- 新8个案例覆盖三清理诊断键、四次上限/独立工作单元/上一轮清理完成、第二轮成功、Unknown/Committed/1205不重试。
- 失败基线executor-ef-before-net6.trx为16通过/2失败，复现事务清理与回滚诊断被忽略；修复后EFCore和FreeSQL各18/18。
- 此处用真实驱动异常类型/编号验证执行器分派，不声称已发生真实InnoDB死锁。真实1213及网络Unknown仍是FIX-007/013后续验收。

### FIX-027：HTTP审计统一可信身份

- 执行状态：COMPLETED。
- PopulateRequest只选择一次首个IsAuthenticated ClaimsIdentity，全部用户/名称/租户/客户端声明从同一身份精确读取；缺失保持null，不从另一已认证身份补齐。
- 新7个案例覆盖缺用户、缺租户、缺名称/客户端、身份顺序和两种容器的真实TestServer。失败基线19通过/5失败；修复后net6/net8定向各24/24。
- 审计持久化指南已说明多认证方案顺序与身份聚合责任；不改变保存、释放和异常策略。

### FIX-007/012：真实库存进程及CI门控

- 执行状态：PARTIAL（跨进程正常/中断测试子项已有当前通过证据）。
- 两套隔离InventoryProbe使用真实Admin工作单元、AspectCore、关键审计、MySQL与RabbitMQ；补现有AspectCore.Extensions.Hosting3.0.0测试引用及FreeSql类型别名，未增加生产依赖。
- 最初NU1101来自受限网络下浮动EF/Pomelo元数据解析；改用已有NuGet缓存作为本次restore源恢复，未改global.json/版本声明。核对EF探针实际EF6.0.36/Pomelo6.0.3，AspectCore均3.0.0、CAP8.0.1、FreeSQL3.5.311。
- 同单/不同单竞争使用独立PID，核对库存非负、整单、关键审计和出箱次数；提交前中断后原单重新提交，提交后中断查询首次结果且重入不重复写。两ORM定向各4/4。
- 首次完整门控EF27/27，FreeSQL25通过/1失败；失败为不同单进程竞争，原探针只记录MySqlException，没有错误编号。原TRX保留。诊断增强后一次定向及有界8次同项均通过，随后完整门控EF27/27、FreeSQL26/26。重复通过不证明首次失败根因已解决，仍保留不稳定观察项。
- 新诊断只输出异常完整类型、Number、事务结果和阶段；停止/释放错误独立收集且保留业务原异常，避免后续故障定位被清理错误遮蔽。最新清理调整的直接及真实回归结果在后续补录。
- 查询专用MySQL的最近InnoDB死锁记录只发现更早有序收件箱死锁，不足以解释本次库存失败；未因猜测修改生产事务隔离或CAP配置。
- CI两套各14个必需方法（原10加4个库存进程方法），最低数量根据完整TRX提高为27/26。自测两套完整方法夹具及56个拒绝断言通过，缺方法/非Passed不能以无关通过补数。
- 库存指南与生产符号映射已更新，区分直接Service、Executor、提交结果及清理结果。提交前暂停位于审计后、出箱前；提交后强停位于ExecuteAsync返回后，不能代替COMMIT确认丢失。

### 当前验证与剩余边界

| 验证 | 结果/证据 |
| --- | --- |
| 库存执行器net6（两ORM） | 各18/18，artifacts/inventory-executor-cleanup-fix/ |
| 库存独立进程net6（两ORM） | 各4/4，artifacts/inventory-process-fix/ |
| 审计身份定向net6/net8 | 各24/24，artifacts/audit-selected-identity-fix/ |
| MVC全量net6/net8 | 各128通过/1既有UploadRawAsync跳过/0失败 |
| Admin完整真实MySQL/CAP/RabbitMQ | 最新27/27和26/26，artifacts/admin-mysqlcap-results/inventory-process-diagnostics/net6.0-20261005T101117620Z/；首次失败另存inventory-process-cleanup目录 |
| Sample Lazy真实业务请求与StopAsync/端口关闭 | 1/1，sample-http-stop-net6.trx |
| WebApi net6构建 | 0错误/2警告，webapi-build-inventory-audit.log |
| CI脚本自测 | 两套14必需方法与56拒绝断言通过 |

定向和全量重叠，不相加为独立覆盖。实际SDK8.0.425，锁定8.0.424未验证；既有Microsoft.Extensions10/net6、NETSDK1138/NU1900等警告未通过升依赖或改目标框架规避。没有将当前测试通过解释为无内存泄漏。

七组完整工作包继续进行：真实1213/提交确认丢失及首次库存失败诊断、有序生产授权接线、跨进程限流故障、自动审计和生产存储、实际蓝绿角色/预热/排空/双平台演练、指标/归档/长期资源测量。未task-finish、commit/push或发送外部通知。


## Review 修复记录：2026-10-05 真实 InnoDB 死锁及最新探针门控

- 模式REVIEW_FIX，reviewRound=1，fixScope=recommended；上一审查回合产出最新7组TODO及证据覆盖边界，分类为progress。review.md不修改，完整目标继续IN_PROGRESS。
- FIX-007状态PARTIAL：真实死锁子项已完成。新增共享AdminInventoryDeadlockCases及两ORM链接，使用真实库存事务持有a/b库存行、独立竞争事务修改256个独占InnoDB测试行，再按相反锁顺序形成循环；没有SIGNAL、反射异常或模拟事务。较小库存事务实际收到原生1213。
- 两项直接测试：一次死锁后第二个新工作单元提交；持续死锁在四次上限停止。断言真实事务/工作单元引用不同、下一轮服务构造前上一轮作用域存储已释放、最终释放次数、原生错误1213、RolledBack、无清理失败、库存/整单/审计/出箱数量。保留最后同一异常。异步释放等待的直接契约仍由既有22/22测试覆盖。
- 夹具只创建GUID命名独占测试表，停止并清理本测试竞争事务后删除该表，未操作生产库；原服务注册/目标框架/生产依赖/global.json不变。测试CreateHost增加可选库存配置回调，不改变默认注册路径。
- 初次编译CS0117来自测试引用生产internal判定方法，已改为观察公开入口真实行为及异常诊断，不扩大生产可见性；该编译失败日志保留，不记作测试通过。
- 定向真实MySQL/CAP：EFCore2/2、FreeSQL2/2，artifacts/inventory-real-deadlock-fix/admin-ef-real-deadlock-net6.trx、admin-freesql-real-deadlock-net6.trx。
- 更新完整门控两套各16必需方法，最低29/28；PowerShell自测完整集合及84拒绝断言通过。
- 最终探针清理修改之后，完整专用MySQL/CAP/RabbitMQ门控EFCore29/29、FreeSQL28/28，无失败或跳过，证据artifacts/admin-mysqlcap-results/inventory-real-deadlock/net6.0-20261005T104832092Z/。门控日志artifacts/inventory-real-deadlock-fix/admin-full-gate.log，退出0。已补此前“完整门控早于探针清理”的验收缺口。
- 首次历史FreeSQL不同单进程竞争MySqlException的错误编号不可恢复，新的真实死锁与完整回归不能证明该偶发事件已解释，继续保留根因观察项。
- 下一子项正在实现FIX-013真实COMMIT确认丢失测试：仅测试loopback协议代理，在服务器返回真实COMMIT OK后丢弃确认并断连；待实际结果前不计完成。两套业务仍沿用真实工作单元/审计/CAP，不以提交后进程中断代替确认丢失。
- 实际SDK8.0.425，锁定8.0.424未验收；没有通过升级依赖或改global.json掩盖环境问题。未task-finish、commit/push或发送外部通知。

## Review 修复记录：2026-10-05 提交确认丢失双 ORM 验收

- 模式 REVIEW_FIX，reviewRound=1，fixScope=recommended；此前目标回合分类 progress：独立审查产生了最新 FreeSQL 未验收及出箱恢复缺口证据。review.md 保留为独立审查结果，整体继续 IN_PROGRESS。
- FIX-013 状态 PARTIAL：共享确认丢失用例在 EFCore 首次2/2通过；本轮 FreeSQL 首次2/2失败，故障代理未触发，等待超时。新增安全诊断仅记录异常类型、驱动 Number、事务结果和调用栈，不打印异常正文、连接或SQL。
- 根因是测试连接中的 SET 用户变量标记被 MySqlConnector 识别成未绑定参数，Number=0，StatementPreparer.GetParameterIndex 调用栈，事务在提交前已 RolledBack。该问题属于夹具，不是生产 COMMIT 恢复逻辑缺陷。
- 仅在 InventoryCommitAckLossProxy 的测试连接启用 AllowUserVariables；保留 loopback、无 TLS/压缩及禁止存储协议报文的边界，未修改生产注册或依赖。故障未触发时立即报告安全诊断，不等待60秒掩盖真实阶段。
- 修正后定向：FreeSQL2/2、EFCore2/2，artifacts/inventory-real-deadlock-fix/admin-freesql-commit-loss-fixed-net6.trx、admin-ef-commit-loss-fixed-net6.trx。首次超时及两次定位失败日志/TRX保留，不计通过。
- 两套均验证服务器成功COMMIT OK在代理被丢弃后返回Unknown，不自动重扣；独立权威写库核对原请求，恢复及重复调用不增加库存/整单/审计/出箱次数。真实断连后的清理失败保留，恢复结果附加且标明Committed，不遮蔽异常。
- 原生 CAP 后台扫描恢复子项 COMPLETED：新增共享 AdminInventoryOutboxRecoveryCases，停止故障宿主后先读取唯一原消息Id且Status=Scheduled，确认正常订阅者尚未收到；之后启动仅负责CAP原生扫描的独立直连宿主，等待同Id Succeeded与正常宿主真实RabbitMQ回调。无目标消息手工Publish、Flush、Dispatcher调用或出箱UPDATE。恢复查询及重复原请求仍保持库存14、整单/审计/出箱各1；传输回调至少一次，不宣称严格一次。
- 恢复宿主只在测试使用FailedRetryInterval=1/FallbackWindowLookbackSeconds=5；停止与释放分别单次尝试，保留原异常和清理错误。代理和作用域按既有所有权清理，不把服务器已提交状态和客户端收到确认混为一谈。
- 后台恢复真实定向：两ORM各1/1，admin-ef-outbox-background-net6.trx、admin-freesql-outbox-background-net6.trx。加上确认丢失两项，本批新增/补齐真实故障路径为每套三项。
- 最新完整MySQL/CAP/RabbitMQ门控按EFCore→FreeSQL顺序执行，EFCore32/32、FreeSQL31/31，0失败/0跳过，两套各19个必需方法全部Passed。artifacts/admin-mysqlcap-results/inventory-commit-ack-recovery/net6.0-20261005T113751407Z/，门控日志admin-commit-ack-full-gate.log，退出0；已覆盖上轮待验收31/30及新增后台恢复。
- CI最低32/31并逐方法核验19项，脚本自测126个缺失/非Passed拒绝场景通过；无关通过方法不能补缺失必需方法。资源证据依据真实原消息Id/状态、数据库数量与宿主停止，不以一次内存变化证明没有泄漏。
- 中文XML与UTF8静态核查66块通过，修正两处连接及验证回调文案；Git空白检查通过。生产符号映射、库存恢复指南已更新，历史14/16/18方法及27/26、29/28、31/30数量保留为历史批次。可机读结果在artifacts/inventory-real-deadlock-fix/commit-ack-recovery-results.json。
- 未定位历史 FreeSQL 偶发进程竞争失败根因；CAP取消确认/重投、其他组合故障、生产有序接线及其余工作包继续分别处理。实际SDK8.0.425，锁定8.0.424未验证；未task-finish或发送外部通知。

## Review 修复记录：2026-10-05 有序事务状态与 CAP 适配接线

- 模式：REVIEW_FIX；Review 仍为 `NEEDS_FIX`，完整 P0–P4 保持 `IN_PROGRESS`。本批处理有序事务保存点外状态和 CAP 适配的可执行子项，不把通用适配器测试当作 Admin 生产业务接线完成。
- `IOrderedEventTransactionState` 新增 `ActivateAmbient`、`Outcome` 和 `ResetBusinessStateAsync`。协调器在异步事务开始、业务处理前、提交前和保存点回滚后显式激活当前状态；回滚后先清理 ORM/宿主状态，再替换 CAP 缓冲，避免依赖异步方法内部对 `AsyncLocal` 的赋值回流调用方。
- `EfCoreOrderedEventTransactionFactory<TContext>` 在事务状态实现中清理 `ChangeTracker`，并记录 `Committed`/`RolledBack`；`FreeSqlOrderedEventTransactionFactory` 清理 `EntityChangeReport`，仍只释放本次工作单元，不释放宿主拥有的 `IFreeSql`。两者都保留重复释放一次和结束后拒绝重置的规则。
- 新增 `CapOrderedEventTransactionFactory` 与 `AddBingCapOrderedEventTransaction<TTransactionFactory>`。包装器只拥有当前作用域的 CAP 缓冲，底层 ORM 工厂拥有物理事务；物理提交后 CAP Flush 异常保留 `Committed`，回滚/释放异常集中附加，外部 CAP ambient 不被覆盖。显式注册入口重复调用幂等，不自动改变已有 CAP 注册。
- 新增/补充直接测试：CAP 工厂 net6/net8 各21/21（19个工厂行为项加2个注册契约项）；EFCore状态清理3/3，FreeSQL状态清理2/2。CAP生产程序集构建0错误。启用隔离 Docker MySQL/RabbitMQ 后，Admin EFCore 有序 CAP 3/3、FreeSQL 有序 CAP 3/3 通过，证据为 `artifacts/ordered-cap-state-fix/admin-efcore-cap-ordered-state-net6-final.trx` 和 `admin-freesql-cap-ordered-state-net6-final.trx`；覆盖同事务派生出箱、回滚无派生消息、3→1→2 乱序以及重复投递不重复执行处理器。此前未启用容器时的3项跳过结果仍保留。
- 文档和测试映射已补充显式 CAP 适配注册、异步 ambient 边界、物理事务所有权和当前验证限制。实际 SDK 为8.0.425，`global.json` 仍锁定8.0.424，未修改。
- 本批回归：`Bing.EventBus.Tests` net6/net8 各238/238，`Bing.EntityFrameworkCore.Tests` net6 90/90，`Bing.FreeSQL.Tests` net6/net8 各59通过、24个外部 MySQL 用例按门控跳过。首次真实 EFCore 有序 CAP 重复投递断言4失败，修正为幂等保持3后两套真实容器门控各3/3；失败 TRX 保留在同一 artifacts 目录作为测试诊断，不计生产缺陷。
- 本批仍未关闭 FIX-006：`modules/admin/src` 没有真实有序业务消费者、Store/Factory/授权器接线；也未执行专用 MySQL/CAP/RabbitMQ 的新派生出箱路径。FIX-007、008、009、011、012、013 及其环境/长期资源验收继续按审查 TODO 保留。

## Review 修复记录：2026-10-05 有序恢复服务与关键审计入口

- 执行状态：COMPLETED（恢复入口子项）；FIX-006 整体仍为 PARTIAL。
- 新增 `IOrderedEventRecoveryService` 和 `OrderedEventRecoveryService`，将状态查询、人工重试、授权器和关键操作审计集中到一个受控入口。后台有序消费者继续直接使用协调器，不会因为人工授权器阻断正常消费。
- 状态查询在开启协调器事务前执行所有授权器；人工重试沿用协调器现有授权路径，避免一次恢复触发两次授权副作用。审计记录只包含操作、消费者、业务流标识和异常类型，不包含消息正文或 SQL 参数。
- 审计失败不会覆盖原始恢复异常；恢复已提交后审计失败会标记 `Committed` 和 `Bing.OrderedEventRecoveryAuditCommitted`，不能误报为回滚。失败审计异常附加到原异常，保留原始诊断。
- 新增 `AddBingOrderedEventRecovery` 幂等注册入口；持久化审计实现由宿主显式注册 `IOrderedEventRecoveryAudit`，框架不默认把日志当作可靠审计落库。
- 定向验证：`Bing.EventBus.Tests` net6/net8 各3/3，证据为 `artifacts/ordered-cap-state-fix/ordered-recovery-service-net6.trx` 和 `ordered-recovery-service-net8.trx`。覆盖授权拒绝、成功重试审计、提交后审计失败及事务结果保留。
- 该批不宣称 Admin 生产模块已经注册有序业务消费者或授权器；跨租户授权实现、补偿操作、持久化审计配置和其余 FIX-006 交付继续保留。

## Review 修复记录：2026-10-05 EFCore 自动实体审计快照

- 执行状态：COMPLETED（EFCore 快照子项）；FIX-009 整体仍为 PARTIAL。
- `Bing.Datas.EntityFramework.Core.UnitOfWorkBase` 在存在活动 `IAuditingManager` 审计作用域时，按 Added/Modified/Deleted 捕获实体属性变化；同一实体在一个作用域内多次保存会合并变更，没有审计作用域时保持原行为。
- 快照只保留有界文本，跳过阴影属性及名称包含 Password、Token、Secret、Credential 的字段；实体类型、主键、租户和原始/新值用于后续存储层白名单过滤。捕获失败不会通过静态缓存持有实体类型。
- SQLite 直接测试覆盖新增、修改、删除、同一实体作用域隔离和敏感字段不进入内存明细；`Bing.EntityFrameworkCore.Tests` net6 全量90/90通过，证据为 `artifacts/audit-auto-snapshot/efcore-auto-audit-full-net6.trx`。
- FreeSQL 自动快照、普通 HTTP 审计数据库默认配置、保存预算/取消和生产授权查询仍是 FIX-009 未完成项；本批不把 EFCore 覆盖外推到 FreeSQL。

## Review 修复记录：2026-10-05 FreeSQL 自动实体审计快照

- 执行状态：COMPLETED（FreeSQL 快照子项）；FIX-009 整体仍为 PARTIAL。
- `Bing.Uow.UnitOfWorkBase` 在存在活动 `IAuditingManager.Current.Log` 时，将 FreeSQL `EntityChangeReport.OnChange` 接入当前工作单元；没有报告回调时回落到 `Options.OnEntityChange`，已有宿主回调继续执行。没有审计作用域时不安装采集回调，保持原保存行为。
- 快照覆盖新增、修改和删除，按映射主键、租户和实体类型归集属性变化；同一作用域内重复修改合并首个原值和最后新值。敏感属性名包含 Password、Token、Secret 或 Credential 时跳过，值使用固定区域格式并限制为 512 个字符，忽略列不进入明细。
- FreeSQL 通知发生在物理提交后且框架随后清空报告，因此该能力只负责工作单元内的自动快照收集，不能替代关键审计与业务修改同事务的显式持久化入口；普通审计存储失败仍按现有异常语义处理。
- 直接 SQLite 测试：net6 新增快照 7/7，快照与取消/事务审计组合 25/25；net8 新增快照 7/7。证据位于 `artifacts/freesql-entity-audit-snapshot/`。普通审计数据库默认配置、保存预算/取消和生产授权查询仍需单独验收。
- 随后顺序运行 FreeSQL 全量：net6 与 net8 均为 66 Passed、24 NotExecuted（外部 MySQL 门控）、0 Failed；证据为 `artifacts/freesql-entity-audit-snapshot/full-net6/freesql-full-net6-after-audit.trx` 和 `full-net8/freesql-full-net8-after-audit.trx`。实际 SDK 为 8.0.425，`global.json` 锁定 8.0.424 未验证；NU1900 仅表示漏洞元数据源不可访问。

## Review 修复记录：2026-10-06 有序消费者全局积压统计

- 执行状态：COMPLETED（FIX-012 的全局待处理数量子项）；最老事件年龄、指标导出、归档和长期容量测量仍未完成。
- 新增可选 `IOrderedEventStoreStatistics` 契约，不修改 `IOrderedEventStore`，因此旧的自定义存储无需实现新成员。内置 `DbOrderedEventStore` 按逻辑消费者查询 `Status <> 2` 的全部待处理事件数。
- `OrderedEventCoordinator.GetPendingEventCountAsync` 在存储支持该契约时使用独立事务查询；消费者后台统计失败只记录安全的错误类型并回退当前分页流数，不阻断正常业务处理。该事务是否采用只读隔离由宿主事务工厂决定。
- `BingOrderedEventMetricsSnapshot` 新增 `PendingEvents`，保留旧构造函数并将其回退值设为待处理流数；内置存储显示全局事件积压，自定义存储仍有兼容的分页回退。
- 直接验证：`DbOrderedEventStoreTest.GetPendingEventCountAsync_ShouldCountAllUnprocessedEvents` 与 `OrderedEventMetricsTest.Snapshot_ShouldExposeAggregatedCounters` 随有序定向集运行，net6 12/12、net8 12/12，证据为 `artifacts/ordered-statistics/net6/ordered-statistics-net6.trx` 和 `artifacts/ordered-statistics/net8/ordered-statistics-net8.trx`。
- 当前限制：统计使用当前事务可见快照，没有新增 `CreatedAt` 列，因此不能给出最老事件年龄；全局查询会增加一次数据库扫描，生产环境需结合索引和容量测量评估。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。
- 随后 EventBus 全量回归 net6/net8 各 242/242 通过，证据为 `artifacts/ordered-statistics/full-net6/eventbus-full-net6-after-statistics.trx` 和 `artifacts/ordered-statistics/full-net8/eventbus-full-net8-after-statistics.trx`。

## Review 修复记录：2026-10-06 FreeSQL 提交后审计快照异常隔离

- 执行状态：COMPLETED（FIX-009 的提交后采集异常子项）；普通数据库审计配置、保存预算/取消、慢存储释放和 FreeSQL 关键事务审计仍需单独验收。
- `Bing.Uow.UnitOfWorkBase.CaptureAuditChanges` 现在按实体和属性隔离反射、getter 与值格式化异常。FreeSQL 实体变化通知发生在物理提交后时，自动快照失败属性或实体会被跳过，不再把已提交业务误报为 `SaveChanges` 失败；宿主原有 `OnChange` 回调仍按原语义执行，其异常不会被吞掉。
- 新增真实 SQLite 回归：同步和异步保存均在提交 TraceAfter 后让 getter 抛错，断言业务行已落库、正常属性仍采集、宿主回调只执行一次；另验证宿主回调异常仍原样传播。
- 定向验证：`UnitOfWorkEntityAuditSnapshotTest` net6/net8 各 10/10，证据为 `artifacts/freesql-entity-audit-snapshot/freesql-entity-audit-hardened-net6.trx` 和 `freesql-entity-audit-hardened-net8.trx`。随后 FreeSQL 全量 net6/net8 各 69 Passed、24 NotExecuted（外部 MySQL 门控）、0 Failed，证据为 `artifacts/freesql-entity-audit-snapshot/full-hardening-net6/freesql-full-net6-after-audit-hardening.trx` 和 `full-hardening-net8/freesql-full-net8-after-audit-hardening.trx`。
- 该隔离策略是尽力采集：元数据或属性不可读时明细可能不完整，不替代关键审计同事务保存；实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## Review 修复记录：2026-10-06 CAP 仅发布宿主角色

- 执行状态：COMPLETED（FIX-011 的 CAP 角色接线子项）；独立消费者进程、定时任务隔离、真实预热/排空和动态切流仍未完成。
- 新增 `EnableBingCapPublisherOnly`。它按 CAP 8.0.1 的公开服务描述符移除原生消费者注册器别名和包含接收重试的 `CapProcessingServer`，保留 Dispatcher、CAP 存储和宿主启动衔接，因此 Web 角色仍可发布事务出箱；默认不调用时完整发布/消费行为不变。
- Admin EFCore 与 FreeSQL 的 `CapModule` 读取 `BING_CAP_CONSUMER_ENABLED`。未配置保持兼容；显式 `false` 才启用仅发布模式，非法值在注册期失败并指出配置名。`BING_HOST_ROLE=consumer` 不会伪造独立工作进程，部署仍需提供消费者入口。
- 直接测试：`CapHostRoleRegistrationTest` net6/net8 各3/3，证据为 `artifacts/cap-host-role/net6/cap-host-role-net6.trx` 和 `artifacts/cap-host-role/net8/cap-host-role-net8.trx`。覆盖处理器保留/移除、重复启用幂等和自定义处理器不被误删；未把描述符测试写成真实 RabbitMQ 角色验收。
- 当前限制：仅发布模式不运行 CAP 接收重试和清理处理器，接收失败消息必须由消费者角色处理；不能在 Web 进程中同时依赖 CAP 接收重试。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## Review 修复记录：2026-10-06 宿主角色边界接线

- 执行状态：COMPLETED（FIX-011 的角色隔离子项）；无 HTTP 监听的独立 Worker、真实依赖预热、消费/任务排空和双平台动态切流仍未完成。
- 新增 `BingHostRole` 与 `IConfiguration.GetBingHostRole()`，默认角色为 `web`，支持 `consumer`、`task`，非法值在服务注册阶段失败并指出 `BING_HOST_ROLE`；未配置的既有 Web 宿主行为保持不变。
- 两个 Admin `Startup` 在 `consumer` 角色跳过 Swagger 和 Hangfire 模块，避免绿版消费者启动 Dashboard、Server 和周期任务。
- 两个 `AppModule` 在 `consumer` 角色不装配 HTTP 业务中间件、不映射控制器、不标记 HTTP 流量就绪；两个 `CapModule` 禁止消费者角色关闭 CAP 消费，并在消费者角色屏蔽 CAP Dashboard。
- 直接角色解析测试 net6：5/5，证据为 `artifacts/host-role/bing-host-role-net6.trx`。Admin EFCore 与 FreeSQL 构建均为 0 错误；仍有既有 net6/依赖兼容及隐藏成员警告。
- 角色仍复用 `ConfigureWebHostDefaults`，可能启动 Kestrel；因此文档明确这是同一 Web 可执行文件的模块隔离，不宣称独立 Worker 或完全无 HTTP 监听。实际 RabbitMQ 消费切换和排空仍需外部部署验收。实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

## 2026-10-06 宿主 Task 角色接线

- `BingHostRoleExtensions.IsBingBackgroundRole` 统一识别 `consumer` 与 `task`。两个 Admin `AppModule` 对后台角色都跳过 HTTP 业务管道、控制器映射和流量就绪标记；两个 Startup 对后台角色跳过 Swagger。
- EFCore Admin 的 `HangfireModule` 仍仅由非 consumer 角色注册，因此 `task` 角色启动 Hangfire Server，`consumer` 角色不会启动 Hangfire。Task 角色只挂载 Hangfire Server，不挂载 Dashboard；Web 角色保持 Dashboard 兼容行为。FreeSQL 宿主在本轮补齐了同一 Hangfire 模块接线，角色边界与 EFCore 对齐。
- 两个 `CapModule` 按角色读取 `BING_CAP_CONSUMER_ENABLED`：Web/Consumer 未配置时保持原有消费行为，Task 未配置时默认关闭 CAP 消费；Task 只有显式设置 `BING_CAP_CONSUMER_ENABLED=true` 才接收 CAP。CAP Dashboard 仅在 Web 角色注册。
- 直接角色解析测试 `BingHostRoleTest` net6 6/6 通过，证据为 `artifacts/host-role/task-strict-net6/bing-host-role-task-strict-net6.trx`；新增数值字符串拒绝回归，避免 `1` 被枚举解析成 `consumer`。EFCore Admin 构建 0 错误、52 警告；FreeSQL Admin 构建 0 错误、51 警告。警告仍为 net6 EOL、Microsoft.Extensions 10 与 net6 不支持、Hangfire 弃用 API 及既有隐藏成员警告。
- 该接线仍复用 Web 宿主入口和 `ConfigureWebHostDefaults`，可能启动 Kestrel；没有把 Task 角色写成无 HTTP Worker，也没有替代真实 Hangfire/CAP 消费排空、依赖预热和蓝绿动态部署验收。实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

### Task 角色 Swagger 排除修正

- 两个 Admin `Startup.ConfigureServices` 的 Swagger 条件统一改为 `!configuration.IsBingBackgroundRole()`，因此 `consumer` 和 `task` 都不会注册 Swagger；两个宿主的 Task 角色均保留 Hangfire Server，Consumer 均跳过 Hangfire 模块。
- 修正后 EFCore 与 FreeSQL Admin net6 构建均 0 错误、55 警告。警告为既有 net6 EOL、Microsoft.Extensions 10/net6 不支持、Hangfire 弃用 API 和隐藏成员问题；本次没有新增错误。真实 Worker、预热、排空和动态部署仍未验收。

## 2026-10-06 HTTP 审计保存等待预算

- `BingHttpAuditingOptions.SaveTimeout` 新增可选保存等待预算，默认仍为无限等待。`BingHttpAuditingMiddleware.SaveWithBudgetAsync` 在预算耗尽时只附加超时诊断，继续等待原始 `SaveAsync` 完成，再执行作用域异步释放；不会取消保存任务，也不会提前释放仍在使用的存储连接。
- 新增保存预算边界、非法预算及超时传播测试；`BingHttpAuditingMiddlewareTest` 职责集在 net6/net8 均 20/20 通过，直接预算用例也分别为 1/1。证据为 `artifacts/audit-save-budget/full-net6/auditing-save-budget-full-net6.trx`、`artifacts/audit-save-budget/full-net8/auditing-save-budget-full-net8.trx`，以及 `artifacts/audit-save-budget/net6/auditing-save-budget-net6.trx`、`artifacts/audit-save-budget/net8/auditing-save-budget-net8.trx`。延迟存储先验证预算耗尽时请求仍未完成，再放行保存并断言保存完成；非法负值构造和 `ThrowOnStoreFailure` 的超时传播也有直接断言。
- 该能力是等待预算和诊断，不是调用方取消或后台脱离保存。普通 `IAuditingStore` 没有调用方取消契约，生产慢存储、宿主停止、取消策略、留存归档和长期资源测量仍需单独验收。实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

### 保存预算计时器清理补充

- `SaveWithBudgetAsync` 为有限预算创建可取消的计时器；保存任务提前完成或预算诊断产生后都会取消计时器，避免长预算在请求完成后继续保留定时任务。底层保存任务仍由原流程等待完成，作用域释放顺序不变。
- 修改后的 `BingHttpAuditingMiddlewareTest` 职责集在 net6/net8 均 20/20 通过，证据为 `artifacts/audit-save-budget/timer-net6/auditing-save-budget-timer-net6.trx` 和 `artifacts/audit-save-budget/timer-net8/auditing-save-budget-timer-net8.trx`。这证明测试路径没有改变保存、异常和释放语义；未把它扩展为调用方取消或宿主强停验收。

## 2026-10-06 Admin 有序事件生产基础接线

- 执行状态：COMPLETED（FIX-006 的生产基础注册子项）；业务消费者、业务处理器、租户授权、恢复审计和真实 CAP 消费链路仍未完成。
- 两个 Admin `CapModule` 增加显式 `OrderedEvents:Enabled` 开关。开关关闭时保持原服务描述；开启时注册 `DbOrderedEventStore(OrderedEventDatabase.MySql)` 和 CAP 有序事务适配器。
- EFCore 使用 `EfCoreOrderedEventTransactionFactory<AdminUnitOfWork>`，继续由 Admin 工作单元拥有数据库事务。FreeSQL 先从 `FreeSqlWrapper.Orm` 构造 scoped `FreeSqlOrderedEventTransactionFactory`，再交给 CAP 适配器；不会把根 ORM 注册为 `IFreeSql` 或由适配器释放根 ORM。
- 该接线只提供发布/接收协调所需的基础设施，不自动注册没有业务语义的 `IOrderedEventHandler` 或 `AddBingOrderedConsumer`，也不在启动时建表。三张表仍需通过 `OrderedEventSchema` 的受控迁移部署。
- Admin 注册契约测试：EFCore `AdminCapRegistrationTest` 14/14，FreeSQL `AdminCapRegistrationTest` 14/14，证据为 `artifacts/admin-ordered-registration/efcore/admin-ordered-registration-efcore-net6.trx` 和 `artifacts/admin-ordered-registration/freesql/admin-ordered-registration-freesql-net6.trx`。
- 生产构建：`Bing.Admin` 和 `Bing.Admin.FreeSQL` net6 均 0 错误；仍有既有 net6 EOL、Microsoft.Extensions 10/net6 不支持、Hangfire 弃用 API 及隐藏成员警告。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 有序事件恢复授权缺口修复

- 执行状态：COMPLETED（FIX-006 的恢复入口安全子项）；Admin 生产授权器、租户策略和持久化审计实现仍需宿主补齐。
- `OrderedEventRecoveryService` 现在要求至少注册一个 `IOrderedEventRecoveryAuthorizer`；状态查询和人工重试在进入事务前均执行授权，未配置授权器直接拒绝，避免受控恢复入口默认放行。
- 新增协调器内部无授权重试路径，恢复服务完成授权后复用该路径，避免同一授权器执行两次；直接调用底层协调器的旧契约保持不变。
- 定向验证：`Bing.EventBus.Tests` net6/net8 的 `OrderedEventRecoveryServiceTest` 各 5/5 通过，最终证据为 `artifacts/ordered-recovery-auth/final-net6/ordered-recovery-auth-final-net6.trx` 和 `artifacts/ordered-recovery-auth/final-net8/ordered-recovery-auth-final-net8.trx`；成功重试额外断言授权器只执行一次。
- 该修复不提供默认授权策略，也不将日志记录器当作可靠审计落库；生产调用仍需注册租户/消费者/业务流授权器及 `IOrderedEventRecoveryAudit`。

## 2026-10-06 Generic Host 后台模块初始化接线

- 执行状态：COMPLETED（FIX-011 的模块生命周期接线子项）；真实无 HTTP Worker、消费者/定时任务排空、依赖预热和蓝绿动态部署仍未完成。
- `EnableBingHostedInitialization()` 新增为 Core 扩展入口。它要求当前服务集合已经通过 `AddBing` 或 `AddBingApplication` 注册模块，并将本次注册标记为由 `BingModuleHostedService.StartAsync` 执行异步模块初始化；`StopAsync` 继续复用统一的 `ShutdownAsync`，不创建第二个容器，也不改变 Web 默认 `UseBing` 时机。
- EFCore 和 FreeSQL Admin 在 `consumer`/`task` 后台角色启用该入口，并跳过 Web 的 `UseBing`；Program 根据同一宿主角色配置改走 Generic Host，不注册 `ConfigureWebHostDefaults`，因此后台代码路径不再启动 Kestrel。Web 角色仍由 ASP.NET Core 管道调用 `UseBing`。
- Core 直接测试：`BingHostedLifecycleTest` net6/net8 均 2/2 通过，证据为 `artifacts/hosted-lifecycle/net6/hosted-lifecycle-net6.trx` 和 `artifacts/hosted-lifecycle/net8/hosted-lifecycle-net8.trx`。最新 Admin EFCore 构建 0 错误/52 警告，FreeSQL 构建 0 错误/51 警告；警告仍属于 net6 EOL、Microsoft.Extensions 10/net6 不支持及既有 API/隐藏成员问题。
- 该入口只解决 Generic Host 启动初始化与停止关闭的时机及宿主分支，未实现 CAP 消费者独立镜像、Hangfire/CAP 排空、就绪预热或 Docker/Kubernetes 动态切流；这些边界继续保留在集群宿主配置文档中。

### Task 角色 Hangfire 启动补齐

- 执行状态：COMPLETED（FIX-011 的 Task 角色服务启动子项）。
- 两个 Admin 的 `HangfireModule.AddServices` 在 `BING_HOST_ROLE=task` 时注册 `AddHangfireServer()`；Task 的模块初始化阶段配置 `HangfireDIActivator` 并注册 `IDebugLogJob`，不依赖 Web 的 `UseModule(IApplicationBuilder)`。Web 角色仍沿用原有 Web 管道启动 Dashboard，Consumer 角色不加载该模块。
- EFCore Admin net6 构建 0 错误/57 警告；`AdminHangfireRegistrationTest` 定向测试 2/2 通过，证据为 `artifacts/host-role/hangfire/admin-hangfire-role-net6.trx`。警告为既有 net6 EOL、Microsoft.Extensions 10/net6 不支持、Hangfire Web API 弃用和隐藏成员问题。该修复没有把 Hangfire/CAP 任务排空、真实任务执行或蓝绿部署演练标记为已验收。

## 2026-10-06 有序事件最老积压年龄指标

- 执行状态：COMPLETED（FIX-012 的基础年龄采集子项）；指标导出、告警、容量归档、长期资源探针仍未完成。
- `IOrderedEventStorePendingAgeStatistics` 作为可选存储扩展加入有序事件数据契约。`DbOrderedEventStore` 在收件箱写入 `CreatedAtUtc`，并按消费者查询未完成记录的最早入箱时间；旧的自定义存储不实现该接口时保持可运行，只报告空年龄。
- `BingOrderedEventMetricsSnapshot.OldestPendingEventAge`、`BingOrderedEventMetrics.RecordOldestPendingEventAge` 和 `OrderedEventCoordinator.GetOldestPendingEventAgeAsync` 已接入后台扫描。年龄查询失败只记录警告并清空本轮年龄，不阻断业务处理；负年龄按零处理，避免时钟回拨产生负值。
- 有序收件箱表的新增 `CreatedAtUtc` 是数据库契约变化。`OrderedEventSchema.GetCreateStatements` 只负责新表脚本，不会修改已部署表；现有环境必须通过受控迁移补充非空列并为历史记录填充明确的 UTC 值，迁移前不能直接部署新版本。
- 定向验证：`Bing.EventBus.Tests` 的 `OrderedEventMetricsTest`、`DbOrderedEventStoreTest` 与 `OrderedEventSchemaTest` 在 net6.0、net8.0 均为 16/16 通过，证据为 `artifacts/ordered-age-index/net6/ordered-age-index-net6.trx` 和 `artifacts/ordered-age-index/net8/ordered-age-index-net8.trx`。测试覆盖空值、负年龄归零、处理后仍有积压、最早记录查询以及 MySQL/SQLite 完整建表索引 SQL。
- 新增 `deploy/migrations/ordered-events/001-created-at-utc.mysql.sql`，用于旧收件箱补充 `CreatedAtUtc` 和年龄查询索引。该脚本需要停写、备份和人工核对，历史记录统一回填迁移时的 UTC 基线；不在应用启动时自动执行，也不宣称可一键回滚。
- 本批未实现 Prometheus/OpenTelemetry 导出、最老年龄告警、历史数据归档、指标权限控制和 20/100 次长期 GC 资源探针；实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

## 2026-10-06 标准 Meter 指标出口

- 执行状态：COMPLETED（FIX-012 的进程内指标出口子项）；具体 exporter、告警规则、权限控制、长期归档和 GC 资源探针仍未完成。
- `BingOrderedEventMetrics` 现在为每个逻辑消费者创建标准 `System.Diagnostics.Metrics.Meter`，名称为 `Bing.Events.Ordering`，提供待处理流/事件、最老积压年龄、在途、最近耗时以及成功、缺口、失败、重试等可观察仪表。
- 所有测量只携带固定 `consumer` 标签，不写入事件 ID、租户、实体键或消息正文；指标实例实现幂等 `Dispose`，随宿主单例释放 Meter，不建立静态 Meter 或类型缓存。
- `OrderedEventMetricsTest.Metrics_ShouldExposeObservableMeasurements` 使用 `MeterListener` 直接读取聚合值，验证 Meter 名称、消费者标签和关键仪表；net6/net8 均 5/5 通过，证据为 `artifacts/ordered-meter/net6/ordered-meter-net6.trx` 和 `artifacts/ordered-meter/net8/ordered-meter-net8.trx`。
- 该实现只提供标准进程内采集契约，不包含 OpenTelemetry/Prometheus exporter、远程指标认证、跨进程聚合、告警阈值、历史归档或容量预算；这些仍需部署平台和业务宿主分别验收。

## 2026-10-06 普通审计分批保留清理

- 执行状态：COMPLETED（P3 普通审计保留入口子项）；冷归档、删除授权、租户差异化策略和长期容量预算仍由宿主完成。
- 新增 `IAuditLogRetentionStore.DeleteBeforeAsync`。`DbAuditingStore` 使用自有连接和独立事务，要求 UTC 截止时间、每批 1 到 10,000 条，并尊重取消令牌；不自动循环，避免无界清理任务。
- 审计表新增 `IsCritical` 标记。普通 `IAuditingStore.SaveAsync` 写入 `0`，`ITransactionalAuditingStore.SaveAsync` 写入 `1`；清理条件包含 `IsCritical = 0`，不会删除关键事务审计。MySQL 建表语句增加保留索引，SQLite 建表返回额外索引语句。
- 旧部署必须先执行 `deploy/migrations/auditing/001-critical-flag.mysql.sql`。旧表无法判断历史行是否关键，迁移默认标记为普通；迁移前必须备份并复核历史数据，不能把历史分类当作自动正确。
- `AuditLogSchemaTest`、`DbAuditingStoreTest` 与 `AuditingPersistenceExtensionsTest` 的最终职责集在 net6/net8 均 27/27 通过，证据为 `artifacts/auditing-retention/final2-net6/auditing-retention-final2-net6.trx` 和 `artifacts/auditing-retention/final2-net8/auditing-retention-final2-net8.trx`。测试覆盖普通/关键隔离、截止时间、批量边界、UTC 校验、取消、连接未打开和 DI 单例注册。
- Bing.Auditing.Tests 完整回归在 net6/net8 均 158/158 通过，证据为 `artifacts/auditing-retention/full-final-net6/auditing-full-retention-final-net6.trx` 和 `artifacts/auditing-retention/full-final-net8/auditing-full-retention-final-net8.trx`。
- 该批没有实现归档到冷存储、删除授权或自动定时器；生产宿主应将清理作为受控运维任务，并保存批次审计和失败恢复依据。

## 2026-10-06 Admin 有序恢复授权与审计接线

- 执行状态：COMPLETED（FIX-006 的宿主安全接线子项）；实际业务有序消费者、恢复 HTTP 端点及真实多租户恢复演练仍未完成。
- 两个 Admin CAP 模块在 `OrderedEvents:Recovery:Enabled=true` 时注册 `IOrderedEventRecoveryService`、`AdminOrderedEventRecoveryAuthorizer` 和 `AdminOrderedEventRecoveryAudit`；默认关闭，恢复开关未同时启用 `OrderedEvents:Enabled` 时在注册阶段拒绝。
- 授权器要求当前用户已认证、拥有配置的角色、消费者位于配置白名单；租户流默认要求与 `ICurrentUser.TenantId` 严格匹配。授权不读取消息头、请求参数或恢复查询中的租户值。未配置角色或消费者白名单时拒绝解析，避免默认放行。
- 审计适配器使用现有 `IAuditingManager` 保存安全摘要，仅写入操作、消费者、流、实体键、结果和异常类型，不写消息正文或 SQL 参数。恢复事务完成后才保存审计；审计保存失败由通用恢复服务标记已提交结果，不能伪装为业务回滚。
- 新增共享 Admin 直接测试：每个宿主的注册、跨消费者/跨租户拒绝及安全审计投影各 3 项通过。EFCore 证据为 `artifacts/admin-recovery/efcore3/admin-recovery-efcore3.trx`，FreeSQL 证据为 `artifacts/admin-recovery/freesql/admin-recovery-freesql.trx`。实际 SDK 为 8.0.425；锁定 8.0.424 仍未验证。

## 2026-10-06 有序消费者有界并发

- 执行状态：COMPLETED（公平性/背压的进程内基础子项）；跨节点容量、数据库锁等待和真实业务处理吞吐仍需环境验收。
- `AddBingOrderedConsumer<THandler>` 新增可选 `maxConcurrency`，范围为 1 到 100，默认值为 1 以保持旧行为。消费者按分页结果分批启动独立作用域，不同业务流最多同时处理配置上限；单个业务流仍由持久化游标和事务锁保证串行。
- 每一批任务使用 `Task.WhenAll` 等待，任何任务取消或宿主停止都会等待同批任务结束，不把仍在执行的任务标记为已完成；处理器解析、业务异常和作用域释放仍分别记录失败和在途指标。
- 文档补充了并发参数、数据库连接/事务容量和外部副作用幂等要求：`framework/src/Bing.Events/README.md`。新增无效并发上限测试，保留默认串行及已有失败/重复/停止回归。
- 定向验证：`Bing.EventBus.Tests` `OrderedEventConsumerTest` net6.0 和 net8.0 均为 8/8 通过，证据分别为 `artifacts/ordered-concurrency/consumer-concurrency/ordered-consumer-concurrency.trx` 和 `artifacts/ordered-concurrency/consumer-concurrency-net8/ordered-consumer-concurrency-net8.trx`。新增测试验证 4 个不同业务流的并发上限确实为 2；尚未以真实 MySQL 测量跨流吞吐或锁等待。
- 实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 Admin 普通审计数据库注册

- 执行状态：COMPLETED（FIX-009 的宿主注册子项）；普通审计真实数据库写入、迁移、保留归档和长期容量仍需部署环境验收。
- EFCore 与 FreeSQL Admin 的 `AppModule` 在 `Auditing:DatabaseEnabled=true` 时注册 `DbAuditingStore`，连接来自 `ConnectionStrings:DefaultConnection`；默认关闭时保持日志存储，不创建数据库连接。缺少连接字符串会在服务注册阶段直接失败。
- 注册只传递显式允许属性和字段/负载/明细上限，使用独立 MySQL 连接，不把普通 HTTP 审计并入业务事务。关键库存审计继续使用 `SaveTransactionalAuditAsync`，不能用普通审计替代回滚依据。
- 定向验证：EFCore `AdminRateLimitingRegistrationTest` 10/10 通过，证据为 `artifacts/admin-audit-registration/efcore/admin-audit-registration-efcore.trx`；FreeSQL 同职责集 9/9 通过，证据为 `artifacts/admin-audit-registration/freesql/admin-audit-registration-freesql.trx`。覆盖默认注册、显式数据库存储和缺少连接字符串失败路径。
- 文档补充 `Auditing:DatabaseEnabled`、连接字符串、迁移和数据投影限制；应用不会自动建表、归档或清理审计数据。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 Admin 有序事件恢复 HTTP 入口

- 执行状态：COMPLETED（FIX-006 的管理端入口子项）；真实业务有序消费者、恢复多租户现场演练、可靠审计后端和 CAP 断连恢复仍未完成。
- 两套 Admin Web 项目共享 `OrderedEventRecoveryController`。恢复服务启用时提供 `GET /api/ordered-events/recovery/state` 和 `POST /api/ordered-events/recovery/retry`；恢复服务未注册时控制器返回 404，不创建隐藏的默认恢复能力。控制器只接收消费者、租户、业务流和实体键，不接收消息正文。
- 控制器使用 Admin 现有认证管道和 `AdminOrderedEventRecoveryAuthorizer`，不从请求头或查询参数绕过当前用户租户；业务恢复事务和安全审计仍由 `IOrderedEventRecoveryService` 统一处理。默认日志审计不等同于长期可靠审计，生产需配置持久化 `IAuditingStore`。
- 定向验证：EFCore `AdminOrderedEventRecoveryControllerTest` 4/4 通过，证据为 `artifacts/admin-recovery-controller/efcore-final2/admin-recovery-controller-efcore-final2.trx`；FreeSQL 同职责集 4/4 通过，证据为 `artifacts/admin-recovery-controller/freesql-final2/admin-recovery-controller-freesql-final2.trx`。覆盖关闭返回 404、状态查询、重试转发、全局空租户请求映射，以及无服务提供程序时仍能通过可选恢复服务构造控制器。
- 文档更新了端点、认证、请求字段和持久化审计边界。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 当前 TODO 复核

截至本批次，P0 的锁与本地事件快照、P2 有序基础设施与库存事务、P3 限流/审计基础实现、P4 HTTP 闸门和 Generic Host 接线均已有代码及局部直接证据；本批又完成了有序消费者有界并发、Admin 普通审计数据库注册和 Admin 有序恢复 HTTP 入口。完整 P0–P4 仍为 IN_PROGRESS，剩余项按能力边界归并如下：

- **FIX-006：有序生产与业务闭环。** Admin 已注册有序存储、CAP 事务适配器、稳定主题与生产入箱订阅、库存有序处理器、恢复授权/审计和查询/重试端点；本地还补有库存服务双主题同事务发布证据。仍缺真实 MySQL/CAP/RabbitMQ 提交、重投、跨节点竞争与恢复演练，以及 CAP/ORM 保存点外状态回收证明。
- **FIX-007：库存历史异常诊断。** 最新双 ORM 死锁和整单路径已有通过证据；历史 FreeSQL 无编号竞争失败及一次 EFCore `1062/Execute/RolledBack` 仍没有确定冲突键和可复现根因，不能把后续通过解释成已修复。
- **FIX-008：集群限流故障链路。** 组件和 Admin 注册已完成；Redis 分桶已按实际最早回收时间反馈，调用方预先取消不消耗额度，原子脚本完成后返回已提交结果，两个独立 dotnet 进程共享同一策略额度，net6/net8 本机 Redis 集成回归已有 12/12 专项和 72/72 全量证据。仍缺真实认证/可信代理 HTTP 请求、Redis 断连/恢复、主从切换和显式降级的部署环境验收。
- **FIX-009：审计生产闭环。** 两套 Admin 已增加显式 `Auditing:DatabaseEnabled` 注册并验证缺失连接失败，HTTP 审计也支持可选存储取消，数据库存储新增租户隔离的有界元数据查询；仍缺真实生产 MySQL 落库/查询演练、删除/归档授权、慢存储长期容量策略和宿主停止演练。默认日志存储不作为可靠恢复审计证据。
- **FIX-011：蓝绿运行闭环。** Generic Host 后台初始化、角色开关和 Hangfire Task 注册已有；仍缺真实依赖预热、CAP/任务排空、消费者独立进程、Compose/Nginx 与 Kubernetes 动态切流、回滚和长请求/处理中任务演练。
- **FIX-012：运行诊断与资源证据。** 有序积压年龄、全局数量、进程内 Meter 及 20/100 次模块/插件长期资源探针已有直接证据；仍缺 exporter/告警/权限控制、审计/收据/消息归档和未完成数据保护。
- **FIX-013：组合故障和活动资源。** 取消转换、确认丢失、后台出箱恢复，以及协调器层业务异常与回滚/释放同时失败的组合已有直接证据；有序消费者现在会在处理器忽略取消令牌时报告排空未完成并保留活动作用域；仍缺“出箱已写后强杀、提交后发送/Dispose 失败、CAP 与定时任务在途停止”的完整矩阵。

其他未充分考虑的边界继续保留：SDK `8.0.424` 锁定验证、专用 MySQL/CAP/RabbitMQ 多实例与 TLS 故障演练、CAP 至少一次和业务幂等留存、Redis 租期/主从切换的非独占语义、旧表迁移与新旧版本共存、可信代理/Dashboard/诊断授权、强杀或断电无法保证托管清理。以上属于验证或部署边界，当前没有新增远程可利用漏洞证据。

## 2026-10-06 有序消费者停止排空诊断

- 执行状态：COMPLETED（FIX-013 的本地在途消费者停止子项）。`ConsumerWorker` 记录扫描循环的实际退出状态，并覆写 `StopAsync`：宿主停止令牌取消而当前处理器仍未退出时，只记录消费者名称和在途数量，抛出带有 `Bing.OrderedEventConsumerDrainIncomplete` 数据标记的取消异常，不强制释放活动作用域或事务。处理器结束后可再次使用未取消令牌等待同一个后台服务完成；被取消的业务事务不会被静默标记为已处理，待处理事件仍保留。
- `OrderedEventConsumerTest.HostedWorker_WhenHandlerIgnoresCancellation_ShouldReportIncompleteDrainAndWaitLater` 与 `HostedWorker_WhenStoppedBeforeStart_ShouldNotReportIncompleteDrain` 在 `Bing.EventBus.Tests` 的 net6.0、net8.0 各 2/2 通过，前者证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-consumer-drain-net6-final.trx`、`ordered-consumer-drain-net8-final.trx`，后者证据为 `ordered-consumer-stop-before-start-net6-final.trx`、`ordered-consumer-stop-before-start-net8-final.trx`。测试使用真实 `BackgroundService`，验证活动处理期间停止报告、在途计数保持、后续释放后计数归零、事件未被错误确认，以及尚未启动时停止不会误报。
- 该修复只提供有序消费者的本地停止诊断和等待契约，不代表 CAP、Hangfire、SSE/WebSocket、脱离请求任务或生产多进程已经完成排空；强杀或断电仍无法保证托管清理。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证。

## 2026-10-06 EventBus 有序消费者停止回归

- `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 288/288 通过、0 失败、0 跳过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-consumer-drain-full-net6.trx` 和 `eventbus-consumer-drain-full-net8.trx`。
- 本次全量结果包含新增的停止排空、未启动停止和扫描快照稳定性测试；构建仍有既有 NU1900 漏洞源不可达、nullable 与 xUnit 分析器警告。该结果不替代锁定 SDK 8.0.424、EFCore 离线依赖、真实 MySQL/CAP/RabbitMQ 或蓝绿部署环境验收。

## 2026-10-07 本地事件类型键回收

- 执行状态：COMPLETED（P0 本地事件内存保留子项）。`LocalEventBus` 现在用字典级同步协调订阅、取消和快照复制；取消最后一个处理器或对未订阅类型执行 `UnsubscribeAll` 后，会删除空的事件类型列表，避免动态事件 `Type` 被长期保留。发布仍在同一快照上执行，后续订阅变更不会影响当前发布。
- `LocalEventBusConcurrencyTest.PublishAsync_ConcurrentSubscriptionChanges_UsesInitialSnapshot` 与 `UnsubscribeLastHandler_ShouldRemoveEmptyEventTypeEntry` 在 net6.0、net8.0 各 2/2 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/local-event-bus-memory-net6.trx` 和 `local-event-bus-memory-net8.trx`。
- `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 289/289 通过，证据为 `eventbus-local-snapshot-full-net6.trx` 和 `eventbus-local-snapshot-full-net8.trx`。该修复只覆盖进程内订阅字典的空键回收，不改变宿主生命周期或跨节点事件语义。

## 2026-10-06 审计快照原始值比较修复

- 执行状态：COMPLETED（FIX-009 的快照准确性修复）。EFCore 与 FreeSQL 之前先截断到 512 个字符再比较，导致仅在截断边界之后变化的长值被错误地当作未变化。
- 两套 ORM 现在先比较未截断的原始值，再生成最多 512 个字符的审计输出；字节数组按内容比较，普通值沿用类型相等语义。敏感字段过滤、输出长度限制和异常边界保持不变。
- 新增长值边界回归：EFCore 当前测试项目只声明 net6.0，`EfCoreAuditPropertySetterIntegrationTest` 2/2 通过，证据为 `artifacts/audit-snapshot-raw-compare/efcore-net6/efcore-audit-raw-compare-net6.trx`；FreeSQL net6.0 与 net8.0 的 `UnitOfWorkEntityAuditSnapshotTest` 均 11/11 通过，证据为 `artifacts/audit-snapshot-raw-compare/freesql-net6/freesql-audit-raw-compare-net6.trx` 和 `artifacts/audit-snapshot-raw-compare/freesql-net8/freesql-audit-raw-compare-net8.trx`。
- EFCore net8.0 本批未验证：该测试项目当前没有 net8.0 目标；尝试离线恢复时又缺少 `Microsoft.EntityFrameworkCore*` 包并无法访问 NuGet。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

### 数据库生成主键刷新

- EFCore 创建审计记录现在在物理保存成功后刷新主键文本。此前自增键在保存前仍为临时值，审计记录可能保存 `0`；刷新仅作用于本次保存登记的创建记录，并在成功、异常和下一次保存开始时清理暂存引用。
- `EfCoreAuditPropertySetterIntegrationTest` 新增自增主键与审计 `EntityId` 一致、多个新增实体不合并、同步保存路径的断言，net6.0 4/4 通过，最终证据为 `artifacts/audit-snapshot-generated-key/efcore-net6-final2/efcore-audit-generated-key-net6-final2.trx`。创建记录不再按保存前可能重复的临时主键合并，保存成功后再刷新数据库生成值。

### 审计快照暂存引用异常路径清理

- 保存前登记的 EFCore `EntityEntry` 与审计记录现在在配置、事务创建、物理保存、提交后通知及取消异常路径中立即清空；成功路径和下一次保存开始也会清空，避免工作单元在失败后继续保留实体图引用。
- 该清理只影响框架内部暂存状态，不撤销模块或业务代码已经产生的外部副作用，也不改变提交后通知的异常语义。
- `EfCoreAuditPropertySetterIntegrationTest` 在最新清理路径下 net6.0 5/5 通过，新增用例直接断言保存准备异常后暂存集合为空；证据为 `artifacts/audit-snapshot-generated-key/efcore-net6-final4/efcore-audit-generated-key-net6-final4.trx`。本次未声称完成长期 GC/分配探针，实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 审计保存取消传递

- 执行状态：COMPLETED（FIX-009 的请求取消适配子项）；慢存储长期容量、删除授权、冷归档和生产停止演练仍未完成。
- 新增可选 `ICancellableAuditingStore` 和 `ICancellableAuditLogSaveHandle`。旧 `IAuditingStore`、旧保存句柄及自定义审计管理器无需修改；实现可选接口时，HTTP 中间件把 `HttpContext.RequestAborted` 传递给存储，未实现时继续调用兼容的无令牌方法。
- `DbAuditingStore` 接入可选取消契约，原有数据库保存与独立连接所有权不变。保存等待预算仍只产生诊断，不会因预算耗尽提前释放；请求取消只允许底层存储结束等待，作用域释放仍等待真实保存任务完成。
- `BingHttpAuditingMiddlewareTest` net6.0 与 net8.0 均 21/21 通过，新增请求中止令牌传递和慢存储完成断言，证据为 `artifacts/audit-cancellation/net6-final/audit-cancellation-net6-final.trx` 和 `artifacts/audit-cancellation/net8-final/audit-cancellation-net8-final.trx`。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证；NU1900 仅表示当前环境无法取得 NuGet 漏洞源。

## 2026-10-06 审计元数据查询

- 执行状态：COMPLETED（FIX-009 的授权后查询数据层子项）；删除/归档授权、慢存储长期容量预算和宿主停止演练仍未完成。
- 新增 `IAuditLogQueryStore`、`AuditLogQuery` 和 `AuditLogEntry`。查询必须显式提供租户标识，支持应用、用户、关联标识、UTC 时间范围、关键标记和有界分页（单次最多 1000 条），结果按执行时间、租户和审计标识稳定排序。
- `DbAuditingStore` 同时实现 SQLite/MySQL 元数据查询；MySQL 租户使用与写入一致的严格 UTF-8 二进制参数和读取解码。结果不包含 `DetailsJson`，存储层不承担租户、角色或 HTTP 路由授权。
- `AddBingDatabaseAuditing` 为查询契约注册与存储实例共享的单例，重复注册会移除旧查询契约，保持与其它数据库审计契约一致。
- 定向验证：`DbAuditingStoreTest` 与 `AuditingPersistenceExtensionsTest` 在 net6.0、net8.0 各 27/27 通过，最终证据为 `artifacts/audit-query/final-net6`、`artifacts/audit-query/final-net8` 对应 TRX。测试覆盖租户隔离、稳定排序、过滤、关键审计、分页、非法参数、取消、连接所有权和 DI 单例映射。
- `Bing.Auditing.Tests` 全量在 net6.0、net8.0 各 161/161 通过，最终证据同为 `artifacts/audit-query/final-net6`、`artifacts/audit-query/final-net8`；构建仍报告当前环境无法访问 NuGet 漏洞源的 NU1900 警告。
- 文档已补充查询示例及授权边界。该能力只提供有界元数据读取，不实现 HTTP 查询端点、明细授权、删除/冷归档策略或长期容量预算。

## 2026-10-06 Redis 限流分桶回收等待时间

- 执行状态：COMPLETED（FIX-008 的限流反馈准确性子项）；独立进程共享额度、Redis 断连/恢复、真实认证代理链和取消后的额度行为仍需环境验收。
- Redis 令牌桶脚本在达到 `maxKeys` 且新键无法创建时，不再固定返回 1000ms。脚本使用 Redis 服务端时间计算每个现有分桶的补充等待和闲置回收等待，返回最早可回收时间；仍每秒最多执行一次分桶扫描，避免在高基数策略上无界遍历。
- 新增 `RedisTokenBucketRateLimiterTest.TryAcquireAsync_WhenMaxKeysIsFull_ShouldReturnReclaimWait` 和 `TryAcquireAsync_WhenCancelledBeforeRedisCall_ShouldNotConsumePermit`，验证容量为 1、补充速率为 0.01、闲置时间为 10 秒时拒绝结果的建议等待时间超过 8 秒，并验证调用方预先取消不会访问 Redis 或消耗额度。使用本机 Redis `127.0.0.1:6379`、`RUN_INTEGRATION_TESTS=true` 实际运行，net6.0 与 net8.0 均为 11/11 通过，证据为 `artifacts/rate-limit-reclaim/net6-enabled/redis-rate-limit-net6-enabled.trx` 和 `artifacts/rate-limit-reclaim/net8-enabled/redis-rate-limit-net8-enabled.trx`。
- 未设置 Redis 集成环境时同一职责集为 10 项跳过；该脚本仍未在 Redis 主从切换、断连恢复和多进程部署环境中验收。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证；构建有既有 NU1900 漏洞源不可达警告。

## 2026-10-06 有序消费者扫描失败指标

- 执行状态：COMPLETED（FIX-012 的有序消费者数据新鲜度子项）。`BingOrderedEventMetrics` 新增 `ScanFailureCount` 和 `RecordScanFailure()`，并发布固定低基数 Meter `bing.ordered.scan.failures`；主扫描、全局待处理数量和最老积压年龄三条失败路径都会记录该计数。
- 主扫描失败时保留上一份积压快照，单项统计失败时只保留对应的全局积压数或年龄，不再让监控把过期快照当作当前零积压；没有最老年龄时直接省略年龄测量，不发布 `-1ms` 哨兵值。
- `OrderedEventMetricsTest` net6.0/net8.0 各 6/6 通过，证据为 `artifacts/ordered-meter/scan-failure-net6/ordered-meter-scan-failure-net6.trx` 和 `artifacts/ordered-meter/scan-failure-net8/ordered-meter-scan-failure-net8.trx`。`OrderedEventConsumerTest.HostedWorker_WhenPendingStreamScanFails_ShouldRecordScanFailure`、`HostedWorker_WhenStatisticsScansFail_ShouldRecordScanFailures` 及既有消费者职责集 net6.0/net8.0 各 10/10 通过，证据为 `artifacts/ordered-consumer-scan-failure-retain/net6/ordered-consumer-scan-failure-retain-net6.trx` 和 `artifacts/ordered-consumer-scan-failure-retain/net8/ordered-consumer-scan-failure-retain-net8.trx`；测试同时断言统计失败时旧全局积压数和年龄保持不变。
- 本批只解决进程内指标的失败可观测性；OpenTelemetry/Prometheus exporter、告警规则、诊断接口授权、消息和审计归档、长期 GC/分配探针仍未完成。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证；构建继续有既有 NU1900 漏洞源不可达警告。
- 统计失败语义修正后，`Bing.EventBus.Tests` 完整回归 net6.0/net8.0 各 258/258 通过，证据为 `artifacts/eventbus-scan-metrics-final/net6/eventbus-full-scan-metrics-final-net6.trx` 和 `artifacts/eventbus-scan-metrics-final/net8/eventbus-full-scan-metrics-final-net8.trx`。

## 2026-10-06 FreeSQL 异步查询回退

- 执行状态：COMPLETED（FreeSQL 异步查询回退子项）。
- `FreeSqlAsyncQueryableProvider` 保持 `CanExecute<T>() == false`，其异步元素、数量、聚合、列表和数组方法统一执行取消前检查后对应的同步 LINQ 查询并包装为 `Task`；异常继续采用 LINQ 原生语义。
- 新增职责测试 `FreeSqlAsyncQueryableProviderTest`，覆盖元素/条件/数量、聚合/物化、预取消和空结果异常；net6.0 与 net8.0 各 4/4 通过，证据为 `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-async-provider-net6-final4.trx` 与 `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-async-provider-net8-final2.trx`。
- 本实现是同步回退，不代表 FreeSQL Provider 获得数据库异步执行能力。

## 2026-10-06 FreeSQL 回归复核

- `Bing.FreeSQL.Tests` 在 net6.0 与 net8.0 均为 74 通过、24 跳过、0 失败；证据为 `artifacts/freesql-full/root-net6/freesql-full-root-net6.trx` 与 `artifacts/freesql-full/root-net8/freesql-full-root-net8.trx`。
- 跳过项均由现有 MySQL/多进程环境门控，包含库存并发、事件消费和故障恢复场景；本次回归不能替代专用 MySQL/CAP 环境验收。
- 本次实际 SDK 为 8.0.425；仓库锁定的 8.0.424 仍不可用，未修改 `global.json`。

## 2026-10-06 Redis 限流独立进程额度验证

- 执行状态：COMPLETED（FIX-008 的跨进程共享额度验证子项）。新增 `Bing.Caching.CSRedis.RateLimitProbe` net6/net8 探针，测试工程构建后复制到独立输出目录；两个 `dotnet` 进程使用同一策略和分桶键各申请 5 次，最终允许数合计严格为容量 5，确认额度由 Redis 原子脚本共享，不随进程数量翻倍。
- net6.0 与 net8.0 定向测试均 1/1 通过，最终证据为 `artifacts/rate-limit-independent-process/final-net6/redis-rate-independent-process-final-net6.trx` 和 `artifacts/rate-limit-independent-process/final-net8/redis-rate-independent-process-final-net8.trx`。测试使用本机 `127.0.0.1:6379`，结束后删除本次唯一策略键。
- 该证据只覆盖两个独立进程的正常竞争；Redis 断连/恢复、主从切换、真实认证与可信代理 HTTP 链路、原子操作完成后的取消语义仍未验收。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。
- 在同一 Redis 环境下，CSRedis 集成工程完整回归 net6.0/net8.0 均为 72/72 通过，证据为 `artifacts/rate-limit-independent-process/full-net6/csredis-full-independent-net6.trx` 和 `artifacts/rate-limit-independent-process/full-net8/csredis-full-independent-net8.trx`。

## 2026-10-06 Redis 锁执行入口持有者契约

- 执行状态：COMPLETED（P0 锁契约修复）。`CSRedisDistributedLock.ExecuteWithLock` 及三个异步/泛型重载此前忽略调用方提供的 `value`，内部另造持有者，导致旧 `ILock` 契约的持有者语义与释放诊断不一致；现在四个入口均使用传入值完成原子获取和所有者校验释放。
- 新增 `RedisDistributedLockTest.ExecuteWithLock_ShouldHonorProvidedOwner`，并保留回调异常、释放失败及过期租约测试。真实 Redis 定向职责集 net6.0、net8.0 各 10/10 通过，证据为 `artifacts/lock-owner-contract/net6` 和 `artifacts/lock-owner-contract/net8` 对应 TRX。
- 该修复不改变新的 `TryAcquireLeaseAsync` 唯一持有者生成规则；调用方仍需为旧入口提供每次调用唯一的 `value`，租期到期或 Redis 故障切换后不应继续假定锁独占。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 锁便捷扩展唯一持有者

- 执行状态：COMPLETED（P0 锁便捷入口修复）。`LockExtensions.ExecuteWithLock*` 不再把所有自动释放调用固定为同一个持有者 `"1"`，每次调用都会生成唯一持有者；显式 `LockTak/LockRelease` 仍保持历史固定值，避免破坏需要手工配对的旧代码。
- 新增 `LockExtensionsTest.ExecuteWithLock_ShouldUseUniqueOwner` 和 `LockTakAndRelease_ShouldKeepLegacyOwner`。`Bing.Core.Tests` net6.0、net8.0 各 2/2 通过，证据为 `artifacts/lock-owner-contract/core-net6` 和 `artifacts/lock-owner-contract/core-net8` 对应 TRX。
- 变更后的 Core 全量回归 net6.0、net8.0 均为 629/629 通过，证据为 `artifacts/lock-owner-contract/core-full-net6` 和 `artifacts/lock-owner-contract/core-full-net8` 对应 TRX。

## 2026-10-06 Admin EFCore 资源排序查询

- 执行状态：COMPLETED（Admin EFCore 参考实现占位项）。`ResourcePoStore.GetMaxSortIdAsync` 不再抛出 `NotImplementedException`，改为在当前工作单元中按应用标识和父模块标识过滤后执行 EFCore `MaxAsync`；空结果保持返回 `null`，与 FreeSQL 实现语义一致。
- 新增 `ResourcePoStoreTest.GetMaxSortIdAsync_ShouldFilterApplicationAndParent` 和 `GetMaxSortIdAsync_ShouldReturnNullWhenNoMatch`，使用 SQLite 内存数据库和真实 `IAdminUnitOfWork` 作用域验证过滤与空结果。Admin EFCore net6.0 定向测试 2/2 通过，证据为 `artifacts/admin-resource-store/net6-final/admin-resource-store-net6.trx`。
- 本次只收口该参考实现占位，不代表 Admin 生产 MySQL 查询、库存并发、CAP 事务或其它模块 TODO 已完成；实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-06 Redis 限流原子提交后的取消语义

- 执行状态：COMPLETED（FIX-008 的调用方取消边界修复）。`RedisTokenBucketRateLimiter.TryAcquireAsync` 原先在 Redis 原子脚本返回后再次检查取消令牌，可能在额度已经提交后向调用方抛出取消异常；调用方重试会再次消耗额度。
- 现在取消令牌只用于阻止尚未开始的 Redis 操作。脚本完成后直接转换并返回已提交结果，不尝试伪造回滚或释放额度；Redis 故障异常仍原样传播，脚本开始前取消仍不会访问 Redis。
- 现有真实 Redis 定向职责集已覆盖预取消、并发容量、策略配置冲突、分桶回收和独立进程额度共享：net6.0、net8.0 均 72/72 全量通过，本批限流职责集各 12/12 通过。该批没有把定时取消当作稳定的“脚本完成后”测试；原子完成后的语义由实现契约和文档固定，Redis 主从切换、断连恢复及真实 HTTP 认证代理链仍待部署验收。
- 实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用，未修改 `global.json`。

## 2026-10-06 模块与插件长期资源探针

- 执行状态：COMPLETED（FIX-012 的进程内资源证据子项）。`ModuleResourceProbeTest` 对正常初始化、初始化失败、调用方取消和插件换代分别执行 20 次预热及 100 次测量，记录托管总分配、托管堆、各代 GC 和工作集，并断言模块释放次数、服务集合/容器/模块弱引用以及退休插件加载上下文可回收。
- Core `net6.0`、`net8.0` 均为 5/5 通过，证据为 `artifacts/module-resource-probe/net6/module-resource-probe-net6.trx` 和 `artifacts/module-resource-probe/net8/module-resource-probe-net8.trx`。探针只输出测量结果，不设置未经批准的内存阈值；强制 GC 仅存在于测试代码。
- 该证据不包含默认加载上下文程序集卸载、OpenTelemetry/Prometheus exporter、告警权限、审计/消息归档或生产宿主长期容量预算；FIX-012 仍保持 PARTIAL。
- 实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用，未修改 `global.json`。

## 2026-10-06 CAP 元数据边界校验

- 执行状态：COMPLETED（事件元数据输入边界子项）。`ClusterEventHeaders.Write` 对六个文本字段执行长度校验并验证正序号；`ClusterEventHeaders.Read` 另外限制序号消息头最多 20 个字符。事件 ID、租户、业务流、实体键、契约版本和跟踪 ID 分别限制为 128、200、200、200、100、128 个字符。
- 本地发送数据超限抛出 `ArgumentException`，远端读取的消息头超限抛出 `FormatException`；校验发生在构造事件元数据前，避免超大不受信任头值进入排序、幂等或审计路径。缺少这些可选字段的旧消息仍保持兼容读取。
- `ClusterEventHeadersTest` 在 net6.0、net8.0 均为 15/15 通过，证据为 `artifacts/cluster-event-headers/sequence-net6/cluster-event-headers-sequence-net6.trx` 和 `artifacts/cluster-event-headers/sequence-net8/cluster-event-headers-sequence-net8.trx`。
- 该修复只收口消息头大小边界，不实现有序生产者/消费者、CAP 重试状态清理、导出器或告警；实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用，未修改 `global.json`。

## 2026-10-06 蓝绿镜像不可变性校验

- 执行状态：COMPLETED（FIX-011 的发布输入校验子项）。`deploy/bluegreen/Test-BlueGreen.ps1 -Live` 现在要求 Compose 的蓝、绿镜像环境变量以及 Kubernetes 清单中的全部镜像使用 64 位 SHA-256 digest；Kubernetes 的 `example.invalid` 和 `replace-me` 占位值会直接拒绝。静态模式仍只验证模板结构，便于文档示例保留占位镜像。
- 本地验证：`Test-BlueGreen.ps1 -Platform Compose` 和 `-Platform Kubernetes` 静态检查均通过 9 项；当前环境没有可用 Docker/Kubernetes 服务，因此未执行 Live 就绪、切流、排空或回滚验证。传入缺少 digest 的 Compose 镜像及默认 Kubernetes 占位镜像时，Live 校验分别在启动容器/集群前失败。
- 该校验只防止发布脚本误用可变或占位镜像，不证明依赖预热、CAP/Hangfire 排空、动态切流和长请求演练已经完成；FIX-011 仍为 PARTIAL。实际 SDK 为 8.0.425，锁定的 8.0.424 仍不可用。

## 2026-10-06 审计保留清理授权入口

- 执行状态：COMPLETED（FIX-009 的删除授权本地契约子项）。新增 `IAuditLogRetentionAuthorizer`、`IAuditLogRetentionService` 和 `AuditLogRetentionService`。高层入口先校验 UTC/批量参数，再执行当前调用方授权，授权通过后才委托 `IAuditLogRetentionStore`；授权失败或调用方预取消不会打开数据库连接，也不会删除记录。
- 新增 `AddBingAuditLogRetention<TAuthorizer>`，使用 `TryAddScoped` 注册，重复调用不覆盖调用方预先注册的授权器。低层 `IAuditLogRetentionStore` 保持兼容，仍明确为基础设施契约，不自动宣称具备 HTTP 权限控制。
- `AuditLogRetentionServiceTest` 在 net6.0、net8.0 各 5/5 通过，覆盖授权顺序、授权拒绝、预取消、超过 10,000 条批量上限和幂等注册；最终证据为 `artifacts/audit-retention/net6-final/audit-retention-net6-final.trx` 和 `artifacts/audit-retention/net8-final/audit-retention-net8-final.trx`。该批未实现租户差异化策略、冷归档、生产 HTTP 管理端点或 MySQL 外部环境验收；FIX-009 仍为 PARTIAL。
- 实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用；构建继续有既有 NU1900 漏洞源不可达警告。

## 2026-10-06 审计查询授权入口

- 执行状态：COMPLETED（FIX-009 的查询授权本地契约子项）。新增 `IAuditLogQueryAuthorizer`、`IAuditLogQueryService` 和 `AuditLogQueryService`。高层入口先拒绝空查询、缺少租户和调用方预取消，再执行当前调用方授权，授权通过后才委托 `IAuditLogQueryStore`；授权失败不查询数据库。
- 新增 `AddBingAuditLogQuery<TAuthorizer>`，使用 `TryAddScoped` 注册，重复调用不覆盖调用方预先注册的授权器。低层查询存储保持兼容，继续只返回不含 `DetailsJson` 的安全元数据。
- `AuditLogQueryServiceTest` 在 net6.0、net8.0 各 4/4 通过，最终证据为 `artifacts/audit-query-service/net6-final/audit-query-service-net6-final.trx` 和 `artifacts/audit-query-service/net8-final/audit-query-service-net8-final.trx`。该批未实现宿主身份策略、明细脱敏端点、冷归档或生产 MySQL 权限环境验收；FIX-009 仍为 PARTIAL。
- 实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用；构建继续有既有 NU1900 漏洞源不可达警告。

## 2026-10-06 审计持久化最终回归

- `Bing.Auditing.Tests` 在 net6.0、net8.0 各 170 通过、0 失败、0 跳过，最终证据为 `artifacts/audit-persistence-final-v2/net6/auditing-full-final-net6.trx` 和 `artifacts/audit-persistence-final-v2/net8/auditing-full-final-net8.trx`。
- 回归覆盖数据库保存/查询/分批保留、HTTP 审计管理器、关键事务接口、资源清理以及本批新增的查询和保留授权服务。结果只证明框架本地行为，不替代 Admin 生产 MySQL、身份授权、冷归档和宿主停止演练。

## 2026-10-06 CAP 有序入箱适配器

- 执行状态：COMPLETED（FIX-006 的 CAP 入箱复用子项）；Admin 真实业务订阅、两套 ORM 生产处理器和 RabbitMQ 部署验收仍未完成。
- 新增 `CapOrderedEventIngress` 和 `AddBingCapOrderedEventIngress`。宿主继续显式声明固定 `[CapSubscribe]` 主题及稳定 CAP 消费者组，适配器统一校验 `CapHeader`、保留原始正文并等待 `OrderedEventCoordinator.ReceiveAsync` 入箱事务完成；不会自动订阅全部 CAP 主题，也不会改变 CAP 的重投语义。
- `AddBingOrderedConsumer<THandler>` 自动注册该作用域适配器，重复注册通过 `TryAddScoped` 保持幂等；调用方仍可在只有入箱订阅、尚未启动后台业务消费者的进程中单独注册。
- `CapOrderedEventIngressTest` 在 `Bing.EventBus.Tests` 的 net6.0 和 net8.0 均为 3/3 通过，最终证据为 `artifacts/cap-ordered-ingress/net6-final/cap-ordered-ingress-net6-final.trx` 和 `artifacts/cap-ordered-ingress/net8-final/cap-ordered-ingress-net8-final.trx`。覆盖真实 SQLite 收件箱、原始正文保真、输入校验和重复注册。
- 实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用；构建继续报告无法访问 NuGet 漏洞源的 NU1900 警告。
- Admin EFCore 与 FreeSQL 的 `CapModule.AddOrderedEventFoundation` 现在同时注册 `CapOrderedEventIngress`，使启用 `OrderedEvents:Enabled` 的宿主可以直接接入显式 CAP 订阅；两套 `AdminCapRegistrationTest` 的该职责集均为 14/14 通过，证据为 `artifacts/admin-cap-ingress/efcore/admin-cap-ingress-efcore-net6.trx` 和 `artifacts/admin-cap-ingress/freesql/admin-cap-ingress-freesql-net6.trx`。

## 2026-10-06 Admin 库存有序生产闭环

- 执行状态：PARTIAL（FIX-006 的生产业务接线子项）。`AdminInventoryService` 在已有整单事务回调中同时发布旧版完成主题和可选的有序完成主题；有序消息沿用当前 CAP/数据库事务，不创建第二个库存事务。`OrderedEventPublisher` 只在 `OrderedEvents:Enabled` 时注入，因此旧配置仍保持原主题行为。
- 两个 Admin 宿主在 `OrderedEvents:Enabled`、`Inventory:Enabled`、`BING_HOST_ROLE=consumer` 和 `BING_CAP_CONSUMER_ENABLED=true` 同时满足时，注册固定 CAP 主题/组、`CapOrderedEventIngress` 和 `AddBingOrderedConsumer<AdminInventoryOrderedEventHandler>("admin-inventory-v1")`；Web/Task 不启动该后台消费者。处理器校验业务流、契约版本、租户和订单身份，只在有序处理事务中写入 `bing_inventory_ordered_projection`，不会再次扣减库存。
- 新增 SQLite 直接证据：`InventoryOrderProjectionStoreTest.SaveAsync_ShouldUseCallerTransactionAndPreservePayload`、`AdminInventoryOrderedEventHandlerTest` 的有效事件、身份不匹配、版本/状态拒绝；FreeSQL 定向 `AdminInventoryOrderedEventHandlerTest|AdminCapRegistrationTest` net6.0 为 19/19 通过，证据为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-ordered-handler-freesql-net6.trx`。EFCore 同一过滤在本机被缺失的 EFCore/Pomelo 离线包阻断，未将构建失败计为源码失败。
- 投影表随 Admin 库存集成夹具按租户清理，避免跨用例残留。生产 MySQL/CAP/RabbitMQ 仍未执行：尚未证明真实网络到达、CAP 重投、跨节点有序消费、消费恢复和多租户授权；FIX-006 继续保持 PARTIAL。实际 SDK 为 8.0.425，锁定的 8.0.424 仍不可用。

## 2026-10-06 Admin 库存双主题事务直接验证

- 执行状态：COMPLETED（FIX-006 的本地发布事务证据）。新增 `AdminInventoryOrderedEventPublishingTest.ExecuteAsync_WhenOrderedPublisherIsPresent_ShouldPublishBothTopicsInOneTransaction`，使用 CAP 事务上下文、库存事务回调和序号存储的同一 `DbTransaction`，验证旧完成主题与有序完成主题均在同一事务内发布，并断言租户、业务流、实体键、序号、契约版本和跟踪标识。
- `Bing.Admin.FreeSQL.Tests` net6.0 定向集为 20/20 通过，证据为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-ordered-publishing-freesql-net6-single.trx`。该测试为共享夹具，已链接到 EFCore 测试项目；本机 EFCore 项目仍因缺少离线 EFCore/Pomelo 包无法执行。
- 该证据只验证应用服务的事务身份和双主题发布调用，不替代真实 MySQL/CAP/RabbitMQ 的提交、重投、跨节点竞争、消费恢复与多租户授权验收；FIX-006 继续保持 PARTIAL。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用。

## 2026-10-06 Admin 有序库存正文大小预检

- 执行状态：COMPLETED（FIX-006 输入边界子项）。`AdminInventoryOrderedEventHandler` 在 `JsonDocument.Parse` 前按 UTF-8 字节数限制正文不超过 65536 字节，与 `DbInventoryOrderProjectionStore` 的持久化限制一致；超限消息不会进入 JSON 解析或投影存储。
- `AdminInventoryOrderedEventHandlerTest.HandleAsync_WhenPayloadExceedsLimit_ShouldRejectBeforeParsingOrSaving` 已加入两套 Admin 共享测试；FreeSQL net6.0 有序定向集 21/21 通过，证据为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-ordered-publishing-freesql-net6-single.trx`。EFCore 仍受本机离线 EFCore/Pomelo 包缺失阻断。
- 该限制只保护 Admin 库存处理器的解析和投影边界，不限制通用有序事件正文；真实 CAP/RabbitMQ 的代理消息大小、重投和跨节点资源上限仍需专用环境验收。
## 2026-10-06 Admin 有序库存投影状态白名单

- 执行状态：COMPLETED（FIX-006/P2 数据完整性子项）。`DbInventoryOrderProjectionStore.SaveAsync` 现在在打开写入命令前只接受 `Applied` 和 `Insufficient` 两个库存终态；直接绕过 `AdminInventoryOrderedEventHandler` 的调用不能写入 `Pending` 或其他未知状态。
- 新增 `InventoryOrderProjectionStoreTest.SaveAsync_WhenStatusIsUnsupported_ShouldRejectBeforeWriting`，验证拒绝发生在插入前且当前事务中没有产生投影记录。`InventoryOrderProjectionStoreTest` 在 `Bing.EventBus.Tests` 的 net6.0、net8.0 均为 3/3 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/inventory-projection-status-net6-final.trx` 和 `framework/tests/Bing.EventBus.Tests/TestResults/inventory-projection-status-net8-final.trx`。
- 该修复只约束 Admin 库存投影的终态数据，不改变通用有序事件存储；真实 MySQL 字段校验、CAP 重投和跨节点消费仍属于 FIX-006 的环境验收。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用。

## 2026-10-06 有序消费者身份边界

- 执行状态：COMPLETED（FIX-006/FIX-012 输入身份一致性子项）。消费者名称现在统一拒绝空值、超过 200 个字符以及首尾空白；校验覆盖数据库存储、协调器、CAP 入箱、消费者注册、恢复服务和指标实例，避免存储哈希身份与下游文本唯一键出现不同解释。
- 新增 `OrderedEventConsumerTest.AddBingOrderedConsumer_WhenConsumerHasBoundaryWhitespace_ShouldReject`、`CapOrderedEventIngressTest.ReceiveAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeOpeningTransaction` 和 `DbOrderedEventStoreTest.EnqueueAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeWriting`。EventBus 有序消费者职责集在 net6.0、net8.0 各为 27/27 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-consumer-identity-net6.trx` 和 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-consumer-identity-net8.trx`。
- 该边界修复不改变合法消费者名称的哈希和排序语义，也不把名称自动规范化；已有包含首尾空白的配置需要改为稳定的无空白名称。真实 MySQL 字符串排序、CAP/RabbitMQ 多节点组名和蓝绿部署仍需专用环境验收。

## 2026-10-06 恢复审计消费者身份边界

- 执行状态：COMPLETED（FIX-006/FIX-012 输入身份一致性补充入口）。`OrderedEventRecoveryAuditEntry` 现在复用 `OrderedEventConsumerName.Validate`，公共审计记录构造也拒绝首尾带空白的消费者名称，避免绕过恢复服务入口后写入不同文本身份。
- `OrderedEventRecoveryServiceTest.RecoveryAuditEntry_WhenConsumerHasBoundaryWhitespace_ShouldReject` 在 net6.0、net8.0 各 7/7 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-recovery-identity-net6.trx` 和 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-recovery-identity-net8.trx`。
- 该补充只收口恢复审计对象的本地输入边界，不改变合法名称语义；MySQL 字符串排序、CAP/RabbitMQ 多节点消费组和蓝绿切换仍需专用环境验收。实际 SDK 为 8.0.425，锁定的 8.0.424 仍不可用。

## 2026-10-06 Admin 恢复消费者精确授权

- 执行状态：COMPLETED（FIX-006 的授权身份一致性子项）。`AdminOrderedEventRecoveryAuthorizer` 原先以 `OrdinalIgnoreCase` 查找消费者白名单，而有序事件存储、游标和指标使用 `Ordinal` 身份；现改为精确大小写匹配，并在构造授权器时拒绝超过 200 个字符或首尾带空白的消费者配置。
- `AdminOrderedEventRecoveryTest.Authorizer_ShouldRejectConsumerWithDifferentCasing` 和 `Authorizer_WhenConsumerConfigurationHasBoundaryWhitespace_ShouldReject` 已加入共享测试。FreeSQL Admin net6.0 定向集 5/5 通过，证据为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-recovery-identity-freesql-net6.trx`。
- EFCore Admin 同一测试未能构建：本机离线缓存缺少 `Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational` 和 `Pomelo.EntityFrameworkCore.MySql`，且 NuGet 源不可达；未将该环境阻断计为源码失败。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证。
- 该修复只统一授权白名单与有序消费者的精确身份语义；不替代真实 MySQL/CAP/RabbitMQ 多节点恢复、身份系统和部署验收。

## 2026-10-06 EventBus 有序身份完整回归

- `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 各 283/283 通过、0 失败、0 跳过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-recovery-identity-full-net6.trx` 和 `eventbus-recovery-identity-full-net8.trx`。
- 回归包含消费者注册、CAP 入箱、数据库存储、协调器、恢复服务和新增恢复审计身份边界；仅有既有 NU1900 漏洞源不可达、nullable/xUnit 分析器等警告。
- 该结果不替代 EFCore Admin 环境构建、锁定 SDK 8.0.424、真实 MySQL/CAP/RabbitMQ 多节点和蓝绿部署验收。

## 2026-10-06 有序协调器组合清理故障

- 执行状态：COMPLETED（FIX-013 的本地组合故障证据子项）。扩展 `OrderedEventAtomicFailureTest.RecordingFactory` 注入外层回滚异常，并新增业务异常、失败标记提交失败、外层回滚失败、事务释放失败同时发生的职责测试。
- `ProcessNextAsync_WhenBusinessRollbackAndDisposeFail_ShouldPreserveOriginalAndCleanupErrors` 在真实 SQLite 有序存储上断言：原始业务异常对象保持不变，`TransactionOutcome` 为 `Unknown`，`Bing.OrderedEventFailureRecordingException`、`Bing.TransactionRollbackException` 和 `Bing.TransactionCleanupException` 分别保留对应异常引用，提交/回滚/释放各执行一次，业务修改未提交且事件仍待处理。
- `OrderedEventAtomicFailureTest` 定向测试在 net6.0、net8.0 均为 7/7 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-combined-cleanup-net6-final.trx` 和 `ordered-combined-cleanup-net8-final.trx`。测试使用 SDK 8.0.425；锁定的 8.0.424 仍不可用，构建仅有既有 NU1900、nullable 和 xUnit 分析器警告。
- 该证据只覆盖协调器的本地异常聚合，不替代出箱已写后强杀、CAP 异步发送完成语义及生产在途消费/任务排空；FIX-013 继续保持 PARTIAL。

## 2026-10-06 EFCore 审计值格式化容错

- 执行状态：COMPLETED（FIX-009 的本地审计可靠性子项）。`UnitOfWorkBase` 对实体标识、租户值和属性快照使用逐值容错格式化；值对象读取或 `ToString`/`IFormattable.ToString` 抛错时记录有限上下文并跳过对应审计值，正常属性继续采集，业务数据库保存不被该类审计格式化异常阻断。数据库、事务、审计管理器和宿主实体回调异常仍按原语义传播，不被此处吞掉。
- `EfCoreAuditPropertySetterIntegrationTest.SaveChangesAsync_WhenAuditValueFormattingThrows_ShouldKeepBusinessSaveSuccessful` 使用 SQLite `ValueConverter` 映射一个会在 `ToString` 抛错的值对象，断言业务行提交、正常属性仍有快照、异常属性不进入快照。
- 本机 EFCore 定向测试因离线缓存缺少 `Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational` 及 Pomelo 包且 NuGet 源不可达，未形成新的通过证据；改动已完成 UTF-8、差异空白检查。实际 SDK 为 8.0.425，锁定的 8.0.424 仍不可用。
- 该子项明确把值格式化从 fail-fast 调整为 best-effort；不解决生产 MySQL、审计删除/冷归档授权、慢存储容量和宿主停止演练，FIX-009 继续保持 PARTIAL。

## 2026-10-06 有序消费者全局待处理流指标

- 执行状态：COMPLETED（FIX-012 的积压流数量精度子项）。新增可选 `IOrderedEventStorePendingStreamStatistics`，内置 `DbOrderedEventStore` 按消费者统计仍有未完成事件的不同业务流；`OrderedEventCoordinator` 和后台消费者优先使用全局值。
- 未实现该可选契约的旧自定义存储继续回退到最近一页扫描数量；统计异常时保留上一份指标快照并增加既有 `ScanFailureCount`，不把失败误报为零积压。
- `Bing.EventBus.Tests` 有序存储、协调器和消费者职责集在 net6.0、net8.0 各 35/35 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-global-stream-net6-final.trx` 和 `ordered-global-stream-net8-final.trx`。新增消费者夹具验证三条积压流在 `batchSize=2` 时指标仍为全局 3。
- 本地验证使用 SDK 8.0.425；锁定 8.0.424 仍不可用。该改动只修正进程内指标数量，不提供 OpenTelemetry/Prometheus exporter、告警规则、归档或长期容量预算。
- 随后 `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 285/285 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-global-stream-full-net6.trx` 和 `eventbus-global-stream-full-net8.trx`。

## 2026-10-07 CAP 有序事务适配器注册冲突

- 执行状态：COMPLETED（FIX-006 事务适配器注册边界子项）。`AddBingCapOrderedEventTransaction<T>` 现在在服务集合中记录已选底层工厂：同一工厂重复调用保持幂等；另一底层工厂不再追加第二个 `IOrderedEventTransactionFactory`，而是在注册阶段报告两种工厂的完整类型名。这样避免 EFCore/FreeSQL 适配器同时存在时由 DI 的最后注册规则静默覆盖事务实现。
- 新增 `CapOrderedEventTransactionRegistrationTest.AddBingCapOrderedEventTransaction_WhenDifferentFactoryIsRegistered_ShouldRejectWithoutPartialRegistration`，断言异常包含已注册和冲突工厂，并且冲突调用没有添加第二个工厂描述。
- `CapOrderedEventTransactionRegistrationTest` 在 `Bing.EventBus.Tests` 的 net6.0、net8.0 各为 3/3 通过，证据为 `artifacts/cap-ordered-registration/net6/cap-ordered-registration-net6.trx` 和 `artifacts/cap-ordered-registration/net8/cap-ordered-registration-net8.trx`。该修复只约束同一服务集合的注册一致性，不替代真实 MySQL/CAP/RabbitMQ 多节点事务验收；实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证。

## 2026-10-07 CAP 注册一致性完整回归

- `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 各 `290/290` 通过、0 失败、0 跳过，证据为 `artifacts/eventbus-cap-registration-full/net6/eventbus-cap-registration-full-net6.trx` 和 `artifacts/eventbus-cap-registration-full/net8/eventbus-cap-registration-full-net8.trx`。
- 回归包含 CAP 有序事务适配器注册冲突、本地事件订阅快照与空键回收、有序消费、收据和故障清理测试；构建仍有既有 NU1900、nullable 和 xUnit 分析器警告。该结果不替代锁定 SDK 8.0.424、真实多节点基础设施或蓝绿部署验收。

## 2026-10-07 蓝绿消费者就绪预检

- `deploy/bluegreen/Test-BlueGreen.ps1` 新增可选 `-RequireConsumer`。静态模式继续校验 Compose/Kubernetes 的消费者角色、启用开关和稳定消费组；Compose 实时模式要求 `consumer-blue` 或 `consumer-green` 至少一个服务处于运行状态，Kubernetes 实时模式要求两个消费者 Deployment 的 `readyReplicas` 合计至少为 1。
- `deploy/bluegreen/README.md` 的启动示例显式使用 `-RequireConsumer`，并说明该检查只证明消费者进程/副本就绪，不证明消息排空、单次提交、切换或回滚。默认不传该参数时仍保持 Web-only 的兼容检查。
- `& .\deploy\bluegreen\Test-BlueGreen.ps1 -Platform Compose`：11/11 静态断言通过；未执行 `-Live`，因为当前环境没有可作为证据的 Compose/Kubernetes 蓝绿基础设施。该预检不改变 P4 的动态部署验收状态。

## 2026-10-07 CAP 消费组配置边界

- 两套 Admin `CapModule` 对消费者角色增加 `BING_CAP_CONSUMER_GROUP` 校验：未配置时沿用稳定部署约定 `bing-admin`；显式空值、首尾空白、版本化或其他组名在注册阶段直接失败。校验只防止配置漂移，不动态改写各个 `[CapSubscribe]` 的组名；订阅特性仍必须保持稳定。
- `AdminCapRegistrationTest.AddServices_WhenConsumerGroupIsBlankOrMismatched_ShouldRejectWithoutExposingValues` 在 `Bing.Admin.FreeSQL.Tests` net6.0 通过 18/18（含原有注册职责集），证据为 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-cap-registration-group-freesql-net6-final.trx`。
- EFCore 同名定向测试未进入执行阶段：构建时本机 NuGet 源不可达，`Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 报 `NU1301`。该项保留为环境阻断，不将 FreeSQL 结果扩展为 EFCore 通过。实际 SDK 为 8.0.425，锁定的 8.0.424 仍不可用。

## 2026-10-07 有序消费者指标快照提供程序

- 执行状态：COMPLETED（FIX-012 的进程内诊断汇总子项）。新增 `IOrderedEventMetricsSnapshotProvider`、`BingOrderedEventMetricsSnapshotProvider` 和 `AddBingOrderedEventMetricsDiagnostics`。提供程序在构造时复制指标来源，按消费者名称使用 `Ordinal` 排序，拒绝空来源、非法身份和重复身份；每次读取返回新的只读快照集合，并校验自定义指标的来源名称与快照名称一致。
- 该入口不保存根容器、不建立静态类型缓存、不包含事件正文/事件 ID/实体键，也不实现 HTTP 暴露、身份授权、告警节流或归档。调用方预先注册的自定义提供程序由 `TryAddSingleton` 保留；没有消费者时返回空集合。
- `OrderedEventMetricsTest` 的指标职责集在 net8.0、net6.0 各 12/12 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-metrics-provider-net8-final-v3-retry.trx` 和 `ordered-metrics-provider-net6-final-v3.trx`。覆盖稳定排序、只读集合、非法身份、重复消费者、快照身份冲突、默认空集合和注册幂等。
- 该子项只补进程内诊断读取契约，不替代 OpenTelemetry/Prometheus exporter、授权端点、告警规则、消息归档或长期容量/GC 测量；实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证。

- `Bing.EventBus.Tests` 全量回归在 net8.0、net6.0 各 296/296 通过，证据为 `eventbus-metrics-provider-full-net8-final.trx` 和 `eventbus-metrics-provider-full-net6-final.trx`。该回归只证明 EventBus 现有本地/CAP/有序职责未受影响，不替代 Admin EFCore 离线依赖、真实多节点部署和锁定 SDK 验证。

## 2026-10-07 租户范围审计保留

- 执行状态：COMPLETED（FIX-009 的租户删除隔离子项）。新增独立的 `IAuditLogTenantRetentionAuthorizer`、`IAuditLogTenantRetentionStore`、`IAuditLogTenantRetentionService` 及 `AddBingAuditLogTenantRetention<TAuthorizer>`，不修改原全局保留契约，避免破坏已有自定义实现。
- `DbAuditingStore` 实现租户范围删除时使用参数化 `TenantId` 条件，并沿用 UTC、批量上限、关键审计排除、自有连接和事务清理规则；租户标识必须非空、不得全部为空白、不超过 128 个字符，并按原值匹配，首尾空白和 Unicode 大小写不会被修剪或规范化。保存、查询、高层授权和低层删除共用严格 UTF-8 校验；授权器和低层存储收到同一个租户标识，调用方预取消不会触发依赖。
- 建表语句新增 `(TenantId, IsCritical, ExecutionTimeUtc, AuditId)` 租户清理索引，并提供 `deploy/migrations/auditing/004-tenant-retention-index.mysql.sql` 与 SQLite 迁移脚本；该索引匹配租户条件、关键标记、截止时间和批量稳定顺序。
- `AuditLogTenantRetentionServiceTest`、`DbAuditingStoreTest` 和 `AuditLogSchemaTest` 的修正定向集在 net8.0、net6.0 各 11/11 通过，证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-tenant-retention-net8-corrected.trx` 和 `audit-tenant-retention-net6-corrected.trx`；覆盖授权拒绝、孤立代理字符、首尾空格精确隔离及完整 Schema 索引字符串。`DbAuditingStoreMySqlCommandTest` 在两个框架各 1/1 通过，证据为 `audit-mysql-command-net8-final.trx` 和 `audit-mysql-command-net6.trx`，固定 MySQL 删除语句及 `VARBINARY` 租户参数。最终完整 `Bing.Auditing.Tests` 在两个框架各 179/179 通过，证据为 `auditing-tenant-retention-full-net8-final-v3.trx` 和 `auditing-tenant-retention-full-net6-final-v3.trx`。
- `docs/guides/auditing-persistence.md` 与 `Bing.Auditing/README.md` 增加租户范围用法和授权边界。该子项不实现冷归档、删除调度、HTTP 权限端点、跨节点留存协调或锁定 SDK 8.0.424 验证；实际测试使用 SDK 8.0.425，仍有既有 NU1900 警告。

## 2026-10-07 Prometheus 指标文本导出

- 执行状态：COMPLETED（FIX-012 的文本导出子项）。新增 `BingOrderedEventMetricsPrometheusExporter.Export`，消费 `IOrderedEventMetricsSnapshotProvider` 的只读快照，按消费者名称使用 `StringComparer.Ordinal` 稳定排序，输出 Prometheus 0.0.4 文本格式。
- 导出器只使用 `consumer` 一个固定低基数标签，转义反斜杠、双引号、回车和换行；年龄指标按非负毫秒向上取整，缺少年龄时不输出样本；重复或非法消费者名称、null 快照会在导出前拒绝。输出不包含事件 ID、实体键或消息正文。
- `OrderedEventMetricsTest.PrometheusExporter_ShouldWriteStableLowCardinalityText`、`PrometheusExporter_WhenProviderReturnsDuplicateNames_ShouldReject` 及四个输入边界用例在 net8.0、net6.0 各 18/18 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-metrics-prometheus-boundary-net8.trx` 和 `ordered-metrics-prometheus-boundary-net6.trx`。
- `Bing.EventBus.Tests` 完整回归在 net8.0、net6.0 各 302/302 通过、0 失败、0 跳过，证据为 `eventbus-metrics-exporter-full-net8-final.trx` 和 `eventbus-metrics-exporter-full-net6-final.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍不可用；构建继续报告 NuGet 漏洞源不可达的 NU1900 及既有分析器警告。
- 该导出器不创建 HTTP 端点，也不负责认证、授权、告警、归档或长期容量测量；宿主暴露监控端点时必须自行保护访问。FIX-012 的告警、授权端点、长期归档/GC 和容量预算仍保持 PARTIAL。

## 2026-10-07 有序消费者指标告警判定

- 执行状态：COMPLETED（FIX-012 的进程内告警判定子项）。新增 `BingOrderedEventAlertRule`、`BingOrderedEventMetricsAlertEvaluator` 和 `IOrderedEventMetricsAlertEvaluator`，支持待处理事件数、最老待处理事件年龄及扫描失败累计值增长三类规则。
- 规则名称使用 `Ordinal` 判重并限制长度和控制字符；快照身份、数值和年龄在评估前校验。扫描失败规则以累计值增量为触发条件，首个采样只建立基线；待处理数量和年龄恢复到阈值以下后清除告警状态。
- 冷却状态按消费者和规则隔离，并在采样时间倒退时允许重新发布；状态只存在评估器实例内，未写入静态缓存或外部存储。评估器只返回告警结果，不发送通知、不提供 HTTP 端点、不负责鉴权、跨节点去重或归档。
- `OrderedEventAlertingTest` 在 net6.0、net8.0 各 7/7 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-event-alerting-net6-final.trx` 和 `ordered-event-alerting-net8-final.trx`。`Bing.EventBus.Tests` 完整回归在两个目标框架各 309/309 通过，证据为 `eventbus-alerting-full-net6.trx` 和 `eventbus-alerting-full-net8.trx`。
- 实际验证使用 SDK 8.0.425，仓库锁定的 8.0.424 仍不可用；构建继续有 NuGet 漏洞源不可达的 NU1900 及既有测试分析器警告。FIX-012 的跨节点告警聚合、通知鉴权端点、长期容量/GC 测量和归档保护仍保持 PARTIAL。



## 2026-10-07 锁便捷入口与租约兼容修复

- 执行状态：COMPLETED（P0 本地锁便捷入口与租约契约子项）。`LockTak/LockTakAsync` 不再使用固定 owner `1`；每次成功获取生成唯一持有者，并在同步包装建立的异步上下文中关联对应释放。旧持有者的延迟或重复释放不会再误删后继持有者。
- 新增独立 `ILockLease` 与 `ILockLeaseProvider`。`LocalLock` 支持可取消等待、一次性同步/异步释放和引用计数回收；Redis 实现保留原子 `SET NX PX` 与持有者校验释放，并保持 `IDistributedLock` 原有继承契约，避免强迫外部实现新增成员。自定义锁未提供租约时，泛型注册不隐式失败，调用租约扩展会明确报告不支持。
- `LockExtensionsTest` 锁扩展与本地锁定向回归：SDK 实际 8.0.425 下 net8.0、net6.0 各 21/21 通过，证据为 `framework/tests/Bing.Core.Tests/TestResults/locks-net8-registration-boundary.trx` 与 `locks-net6-registration-boundary.trx`。Core 全量回归随后在 net8.0、net6.0 各 633/633 通过，证据为 `framework/tests/Bing.Core.Tests/TestResults/core-lock-compat-full-net8.trx` 与 `core-lock-compat-full-net6.trx`；锁定 SDK 8.0.424 仍未验证。
- 本批只覆盖进程内所有权、取消和注册兼容性；Redis 故障切换、租约丢失后的业务补偿、长时间负载和跨节点独占仍属于 P0/P1 后续验收。

## 2026-10-07 有序事件恢复授权缺省拒绝

- 执行状态：COMPLETED（恢复安全边界子项）。`OrderedEventCoordinator.RetryAsync` 在开启恢复事务前拒绝空授权器配置；显式调用协调器不能绕过 `IOrderedEventRecoveryAuthorizer`。授权检查通过后仍保持原有事务、提交结果和重试指标语义。
- 新增 `RetryAsync_WhenAuthorizationIsMissing_ShouldRejectBeforeOpeningTransaction`，断言事务工厂没有新增开启、失败流仍保持阻塞状态且重试指标不增加；原有允许、拒绝、未知提交、提交后释放失败和原子失败测试改为显式授权。
- `Bing.EventBus.Tests` 有序恢复定向测试在 net8.0、net6.0 各 19/19 通过；全量回归在两个目标框架各 310/310 通过。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍未验证；构建继续有 NU1900（NuGet 漏洞源不可达）及既有分析器警告。
- 本修复只保护协调器人工恢复入口；审计记录器为空、跨节点授权聚合、MySQL/CAP/RabbitMQ 实际故障和生产 HTTP 鉴权仍需宿主显式配置与后续环境验收。

## 2026-10-07 HTTP 客户端地址信任边界

- 执行状态：COMPLETED（P3 HTTP 输入信任子项）。`BingHttpContextExtensions.GetRemoteIpAddress` 不再直接读取客户端提供的 `X-Forwarded-For`，默认只使用 `Connection.RemoteIpAddress`；代理转发地址必须先经过 ASP.NET Core 受信 `ForwardedHeaders` 中间件处理。
- 新增 `BingHttpContextExtensionsTest.GetRemoteIpAddress_WhenForwardedHeaderIsUntrusted_ShouldUseConnectionAddress` 和 `GetRemoteIpAddress_WhenConnectionAddressIsMissing_ShouldReturnNull`，固定伪造转发头不会改变地址来源。
- MVC 定向测试在 net8.0、net6.0 各 2/2 通过；完整 `Bing.AspNetCore.Mvc.Tests` 在两个目标框架各 140 通过、1 跳过、0 失败。实际 SDK 为 8.0.425，构建仍有 NU1900（NuGet 漏洞源不可达）及既有分析器警告；锁定 8.0.424 未验证。
- 该修复不替代代理拓扑配置；`KnownProxies`/`KnownNetworks`、多级转发链和边缘代理配置仍由宿主负责，未在本地测试中宣称已验证。

## 2026-10-07 有序恢复审计缺省拒绝

- 执行状态：COMPLETED（FIX-006 关键恢复审计安全子项）。`OrderedEventRecoveryService` 在授权成功后、读取状态或开启恢复事务前检查审计记录器；没有 `IOrderedEventRecoveryAudit` 时拒绝操作，避免人工恢复成功但没有可追溯记录。授权失败仍优先返回原始授权异常。
- 新增 `GetStateAsync_WhenAuditIsMissing_ShouldRejectAfterAuthorization` 和 `RetryAsync_WhenAuditIsMissing_ShouldRejectBeforeRetry`，验证授权调用顺序、恢复状态不变及事务前拒绝；已有成功恢复和审计失败后 `Committed` 语义继续覆盖。
- 该约束只要求至少一个审计记录器，不替代生产持久化、权限模型、审计容量/归档和跨节点审计一致性；默认日志记录器仍不等同可靠恢复审计。

## 2026-10-07 恢复审计完整回归

- `Bing.EventBus.Tests` 在新增恢复审计前置检查后，net8.0、net6.0 均为 312/312 通过；覆盖恢复服务、协调器授权、审计缺失、成功提交、审计失败后 `Committed`、未知提交和释放失败路径。
- 本次使用任务专属隔离输出目录、`UseSharedCompilation=false` 和 `-maxcpucount:1`，规避其他并行构建占用共享 `obj` 的环境问题；实际 SDK 为 8.0.425，构建仍报告 NU1900、既有 nullable/analyzer 及 NETSDK1206 警告，锁定 8.0.424 未验证。
- FIX-006 仍为 PARTIAL：本批只保证本地恢复服务不会在缺少授权或审计时执行操作，不替代真实 Admin 多租户、持久化审计、CAP 断连恢复和多节点竞争验收。

## 2026-10-07 Admin 有序指标受保护端点

- 执行状态：COMPLETED（FIX-012 的宿主诊断授权子项）。新增 `OrderedEventMetricsController` 和 `AdminOrderedEventMetricsAuthorizer`。端点 `/internal/metrics/ordered-events` 默认不启用；只有有序事件已启用、`BING_HOST_ROLE=consumer` 且 `OrderedEvents:Metrics:Enabled=true` 时才注册快照提供程序和授权器，Web/Task 角色配置会在模块注册阶段失败。
- 指标读取角色由 `OrderedEvents:Metrics:Role` 显式配置，未认证或缺少角色时拒绝；输出使用已有 Prometheus 导出器，只包含固定 `consumer` 标签，不包含消息正文、事件 ID 或实体键。控制器未注册诊断依赖时返回 404，保持默认宿主兼容。
- `AdminOrderedEventMetricsTest` 在 FreeSQL Admin net6.0 定向职责集 4/4 通过，证据为 `artifacts/admin-metrics-freesql-net6-final`。测试覆盖禁用 404、授权拒绝、成功文本、缺少角色，以及 Consumer/Web 角色注册边界。EFCore 同名职责集因 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 `NU1301`（当前 NuGet 源不可达）未形成通过证据。
- 两套 Admin 的指标端点代码和配置文档已同步；本地证据不替代真实认证网关、Prometheus 抓取、多节点告警聚合、动态蓝绿部署或锁定 SDK 8.0.424 验证。FIX-012 仍为 PARTIAL。
### Admin 有序指标受保护端点结果更正（2026-10-07）

上一节记录的 FreeSQL 4/4 是新增注册边界测试补入前的中间结果。补齐 `CapModule` Web/Consumer 角色注册断言后，`AdminOrderedEventMetricsTest` 在 `Bing.Admin.FreeSQL.Tests` 的 net6.0 最终结果为 6/6，通过证据为 `artifacts/admin-metrics-freesql-net6-final`。`Bing.Admin.EFCore.Tests` 仍因 `Bing.EntityFrameworkCore` 与 `Bing.EntityFrameworkCore.MySql` 还原访问 `https://api.nuget.org/v3/index.json` 失败（NU1301）未形成通过证据。

`Bing.Admin.FreeSQL` net6.0 生产工程随后构建成功（0 错误）；`Bing.Admin` EFCore 工程同样在编译到受影响项目后被上述 NU1301 阻断。构建使用 SDK 8.0.425，未修改仓库锁定的 8.0.424。

## 2026-10-07 Admin 审计元数据查询入口

- 执行状态：COMPLETED（FIX-009 的查询授权子项）。两套 Admin 的 `AppModule` 仅在 `Auditing:DatabaseEnabled=true` 且 `Auditing:Query:Enabled=true` 时注册 `IAuditLogQueryService` 和 `AdminAuditLogQueryAuthorizer`；缺少查询角色或数据库审计配置会在注册阶段拒绝。
- 新增 `/internal/auditing/logs` 受保护端点，默认未启用时返回 404。授权器要求认证用户具备独立角色，默认严格匹配当前租户；`AllowCrossTenant` 只能显式开启。查询结果使用 `AuditLogEntry` 元数据投影，不包含明细 JSON、请求正文或异常正文。
- `AdminAuditLogQueryTest` 在 FreeSQL Admin net6.0 定向职责集最终 7/7 通过，证据为 `artifacts/admin-audit-query-freesql-net6-final-v2`；FreeSQL 生产工程构建到测试通过。EFCore 对应测试和构建仍受 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 NU1301 阻断。
- 本地测试不替代真实 MySQL 查询、认证网关 ACL、跨租户运维审批、归档/留存和长期容量验证；SDK 实际为 8.0.425，锁定 8.0.424 未验证。

### Admin 审计查询结果更正（2026-10-07）

上一节的 FreeSQL 7/7 是补充缺少租户条件测试前的中间结果。修正后，`AdminAuditLogQueryTest` 在 `Bing.Admin.FreeSQL.Tests` net6.0 定向职责集为 12/12 通过，证据目录为 `artifacts/admin-audit-query-freesql-net6-final-v4`。新增断言覆盖空租户拒绝、跨租户角色缺失拒绝，以及两套 Admin `AppModule` 的查询禁用和数据库未启用边界。

同时收紧 `AdminAuditLogQueryAuthorizer`：查询必须提供具体租户；`AllowCrossTenant=true` 时必须配置 `CrossTenantRole`，并且跨租户请求必须具备该角色。当前端点不支持全局审计查询，避免授权语义与数据库查询的 `TenantId=@tenant` 条件不一致。EFCore 对应职责集和生产构建仍受 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 NU1301 阻断；实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

### 审计查询高层校验（2026-10-07）

`AuditLogQueryValidator` 将租户标识、分页上限、UTC 时间范围和文本长度校验前移到 `AuditLogQueryService`，自定义 `IAuditLogQueryStore` 也不能绕过这些边界；空字符串仍保留为非租户审计范围，空白租户被拒绝。`DbAuditingStore` 复用同一校验器，避免不同存储实现出现不同的请求上限。

`Bing.Auditing.Tests` net6.0、net8.0 全量各 180/180 通过，证据为 `artifacts/audit-query-validation-net6` 和 `artifacts/audit-query-validation-net8`。FreeSQL Admin 审计查询职责集在修正控制器空白租户响应后为 13/13，通过证据为 `artifacts/admin-audit-query-freesql-net6-final-v5`。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

模块注册边界随后补充了 `AllowCrossTenant=true` 缺少 `CrossTenantRole` 的启动期拒绝；FreeSQL Admin 审计查询职责集最终为 14/14，通过证据为 `artifacts/admin-audit-query-freesql-net6-final-v6`。两套 Admin 的 EFCore 对应职责集仍受 NuGet 还原不可达阻断。

审计校验器补充超长文本条件后重新执行，`Bing.Auditing.Tests` net6.0、net8.0 仍各 180/180 通过；最终目录为 `artifacts/audit-query-validation-net6-final` 和 `artifacts/audit-query-validation-net8-final`。

## 2026-10-07 Hangfire 宿主角色隔离

- 执行状态：COMPLETED（FIX-011 的角色注册子项）。`Bing.Admin.Modules.HangfireModule` 现在只在 `BING_HOST_ROLE=task` 注册 `AddHangfireServer()`；Web 角色仅在 `UseModule` 中装配 Dashboard，Task 角色通过 Generic Host 的托管服务启动 Server 和周期任务，Consumer 角色不加载 Hangfire 模块。
- Task 角色经过 Web 管道入口时不再调用 `UseHangfireServer()`，避免服务注册阶段和管道阶段重复启动两个 Server。Web 角色不会因 `UseModule` 启动后台任务，Consumer 角色不会意外拥有 Hangfire Server。
- 新增 `AdminHangfireRegistrationTest`，覆盖 Task/Web/Consumer 的服务注册、Consumer 模块排除及 Task 管道重复装配边界。`Bing.Admin.EFCore.Tests` net6.0 定向集最终 5/5 通过，证据为 `artifacts/admin-hangfire-role-net6-final/admin-hangfire-role-net6.trx`。
- `Bing.Admin` EFCore 生产工程在允许 NuGet 还原后构建成功，0 个错误；实际 SDK 为 8.0.425，构建仍有 net6 生命周期、NU1900 漏洞源不可达和既有隐藏成员警告，仓库锁定的 8.0.424 未验证。
- 本批先完成进程角色与 Hangfire Server 注册隔离；Dashboard 独立认证授权在后续同日子项补齐。CAP 消费工作进程的真实排空、Task/CAP 动态切流和 Compose/Kubernetes 在线回滚仍未完成，FIX-011 继续保持 PARTIAL。

## 2026-10-07 Hangfire Dashboard 默认拒绝与角色授权

- 执行状态：COMPLETED（FIX-011 的 Dashboard 访问控制子项）。新增 `AdminHangfireDashboardAuthorizationFilter`，Web 角色装配 Dashboard 时绑定授权过滤器；未配置 `Hangfire:Dashboard:Role`、请求未认证或角色不匹配时拒绝访问，匹配 `role_code` 或标准角色声明时才放行。
- 授权过滤器不读取客户端自定义请求头，不保存用户或服务提供程序引用；角色值按逗号拆分后使用不区分大小写的精确匹配，避免前缀匹配绕过。
- `AdminHangfireRegistrationTest` 新增匿名、缺少角色和匹配角色用例。`Bing.Admin.EFCore.Tests` net6.0 定向集最终 8/8 通过，证据为 `artifacts/admin-hangfire-role-auth-net6/admin-hangfire-role-auth-net6.trx`。
- `Bing.Admin` 与测试项目构建成功，0 个错误；实际 SDK 为 8.0.425，仍有既有 net6 生命周期和 NU1900 警告，锁定 8.0.424 未验证。
- CAP Dashboard 仍需宿主配置认证中间件和网络 ACL；本批不把 Admin 的本地角色策略扩展为跨节点权限中心，也不替代真实反向代理 ACL、CAP 消费排空和蓝绿在线回滚。FIX-011 继续保持 PARTIAL。

## 2026-10-07 CAP Dashboard 默认拒绝与角色授权

- 执行状态：COMPLETED（FIX-011 的 Dashboard 访问控制子项）。EFCore Admin 与 FreeSQL Admin 的 CAP 模块现在都默认关闭 Dashboard；显式启用后设置 `AuthorizationPolicy`，并强制 `AllowAnonymousExplicit=false`。策略要求用户已认证，且 `role_code` 或标准角色声明按不区分大小写的完整值匹配 `CAP:Dashboard:Role`。
- 两套 Admin 共用 `AdminRoleAuthorization` 的角色解析逻辑，不读取请求自定义头，不保存服务提供程序或用户引用；缺少角色配置、匿名请求和不匹配角色均拒绝，避免把 CAP 运维面板误暴露为匿名入口。CAP 8.0.1（EFCore）和 CAP 7.1.4（FreeSQL）均使用同一公开选项契约。
- `Bing.Admin.EFCore.Tests` 的 `AdminCapRegistrationTest` net6.0 定向集 22/22 通过，证据为 `artifacts/admin-cap-dashboard-auth-efcore-net6-v2/admin-cap-dashboard-auth-efcore-net6-v2.trx`；`Bing.Admin.FreeSQL.Tests` net6.0 定向集 22/22 通过，证据为 `artifacts/admin-cap-dashboard-auth-freesql-net6-v2/admin-cap-dashboard-auth-freesql-net6-v2.trx`。新增用例覆盖缺少角色时默认拒绝，以及 Consumer/Task 角色不会注册 Dashboard。
- 本地测试未覆盖真实认证网关、反向代理 ACL、MySQL/RabbitMQ CAP 消费、跨节点权限中心及蓝绿在线回滚；其他 CAP 宿主仍需自行配置认证授权。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证，FIX-011 继续保持 PARTIAL。

## 2026-10-07 Exceptionless 凭据配置化

- 执行状态：COMPLETED（日志配置安全子项）。`Bing.Admin`、`Bing.Admin.FreeSQL` 和 `Bing.Samples.Hangfire` 的日志模块已移除源码中的 Exceptionless API Key 与内网端点，统一读取 `Exceptionless:ApiKey`、`Exceptionless:ServerUrl`；空 API Key 时不注册远程客户端或远程 Sink，仍保留本地日志。
- FreeSQL Admin 与 EFCore Admin 现在使用一致的条件客户端注册，避免同一配置在两套宿主中产生不同的生命周期行为。配置分支测试覆盖空 Key、有效 Key、客户端配置和 Serilog 全局 logger 释放。
- `AdminLogConfigurationTest` 在 EFCore Admin net6.0 通过 2/2，证据为 `artifacts/admin-log-config-efcore/admin-log-config-efcore.trx`；FreeSQL Admin net6.0 通过 2/2，证据为 `artifacts/admin-log-config-freesql-v2/admin-log-config-freesql-v2.trx`。Admin EFCore、Admin FreeSQL 和 Hangfire Sample 生产构建均为 0 错误。
- 构建和测试使用 SDK 8.0.425；仓库锁定的 8.0.424 仍不可用，构建继续有 net6 生命周期、NU1900 漏洞源不可达及既有警告。未连接真实 Exceptionless 服务，未据此声称远程投递已验收；Git 历史中的旧凭据仍需轮换。数据库、Redis、RabbitMQ、JWT 等其他配置中的敏感值仍是独立待办。

## 2026-10-07 Admin 示例凭据清空

- 执行状态：COMPLETED（示例配置安全子项）。两套 Admin 的 `appsettings.json` 已删除 MySQL 用户密码、旧连接串注释和 JWT Secret，保留空的 `ConnectionStrings:DefaultConnection`、`ConnectionStrings:RedisConnection` 与 `JwtOptions:Secret` 配置键；RabbitMQ 本来已由 `CAP:RabbitMQ` 配置读取并在缺失时 fail-fast。
- 两个 JSON 文件使用 UTF-8 解析检查均通过；仓库源码、样例和文档中不再出现本次清除的旧密码、JWT Secret 或 Exceptionless 内网端点。部署必须使用环境变量、用户机密或外部密钥服务注入，未配置时预期启动失败，不再回退到共享默认凭据。
- 本批未修改 Git 历史，也未验证真实数据库、Redis、RabbitMQ、JWT 或 Exceptionless 服务连通性；历史提交中的凭据轮换和密钥扫描仍需运维执行。SDK 实际为 8.0.425，锁定 8.0.424 仍未验证。

## 2026-10-07 审计保留后台调度

- 执行状态：COMPLETED（FIX-009 的可选调度子项）。新增 `AuditLogRetentionScheduleOptions`、`AuditLogRetentionHostedService` 和 `AddBingAuditLogRetentionScheduler<TAuthorizer>`。调度默认关闭；启用后每轮只通过 `IAuditLogRetentionService` 删除一个有界批次，仍执行宿主提供的后台授权器，不直接解析低层存储。
- 调度器校验正间隔、正保留时长和 1 到 10000 的批量上限；支持启动后立即执行一次。停止时取消下一轮等待，但会等待当前删除任务和作用域实际结束，不因宿主停止令牌提前释放存储资源；失败只记录安全日志并等待下一轮。
- `AuditLogRetentionHostedServiceTest` 在 net8.0、net6.0 各 4/4 通过，覆盖默认关闭、启动立即执行、无效间隔和停止等待不响应取消的存储调用；最终证据为 `artifacts/audit-retention-scheduler/net8-v3/audit-retention-scheduler-net8-v3.trx` 和 `artifacts/audit-retention-scheduler/net6-v2/audit-retention-scheduler-net6-v2.trx`。`Bing.Auditing.Tests` 在新增停止等待用例后的完整回归于两个目标框架各 184/184 通过，最终证据为 `artifacts/audit-retention-scheduler/full-net8-v2/auditing-full-with-scheduler-net8-v2.trx` 和 `artifacts/audit-retention-scheduler/full-net6-v2/auditing-full-with-scheduler-net6-v2.trx`；此前 183/183 结果保留为历史中间证据。
- 调度入口只负责普通审计分批清理，不实现冷归档、跨节点选主、租户差异化删除或关键审计删除；多实例生产部署仍需外部调度/租约协调。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍未验证，构建继续有 NU1900。

### 协调租约启动校验结果更正（2026-10-07）

协调租约缺少 `ILockLeaseProvider` 时已改为 `StartAsync` 阶段直接拒绝，避免后台持续记录错误却让宿主误以为清理正常运行；已注册提供程序但本轮未获取租约仍只跳过本轮。修正后的定向测试在 net8.0、net6.0 各 7/7 通过，证据为 `artifacts/audit-retention-scheduler/coord-start-net8/coord-start-net8.trx` 和 `artifacts/audit-retention-scheduler/coord-start-net6/coord-start-net6.trx`；`Bing.Auditing.Tests` 全量在两个目标框架各 187/187 通过，证据为 `artifacts/audit-retention-scheduler/full-start-net8/full-start-net8.trx` 和 `artifacts/audit-retention-scheduler/full-start-net6/full-start-net6.trx`。

上段测试方法重命名后重新执行结果不变：定向证据更新为 `artifacts/audit-retention-scheduler/coord-start-final-net8/coord-start-final-net8.trx` 和 `artifacts/audit-retention-scheduler/coord-start-final-net6/coord-start-final-net6.trx`；全量证据仍为 `full-start-net8`、`full-start-net6`，两个目标框架均 187/187。

## 2026-10-07 审计保留协调租约

- 执行状态：COMPLETED（FIX-009 的多实例调度协调子项）。`AuditLogRetentionScheduleOptions` 新增可选 `UseCoordinationLease`、协调键、最长等待时间和可选租期；默认关闭，不改变单节点原有调度行为。
- `AuditLogRetentionHostedService` 在启用协调后通过当前 `ILockLeaseProvider` 获取租约。没有提供程序、获取失败或租约为空时不会调用删除服务；成功路径在作用域清理后异步释放租约，释放异常仍进入后台诊断日志。等待时间不允许无限等待，分布式部署可指定有限租期；本地锁应保持租期为空。
- `AuditLogRetentionHostedServiceTest` 新增租约不可用、成功释放一次和提供程序缺失用例。定向测试在 net8.0、net6.0 各 7/7 通过，证据为 `artifacts/audit-retention-scheduler/coord-net8/coord-net8.trx` 和 `artifacts/audit-retention-scheduler/coord-net6/coord-net6.trx`。`Bing.Auditing.Tests` 全量回归在两个目标框架各 187/187 通过，证据为 `artifacts/audit-retention-scheduler/full-coord-net8/full-coord-net8.trx` 和 `artifacts/audit-retention-scheduler/full-coord-net6/full-coord-net6.trx`。
- 协调租约只降低多个节点同时调度同一批次的概率，不提供租约过期后的 fencing，也不替代存储幂等、审计授权、冷归档和关键审计留存策略。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 仍未验证，构建继续有 NU1900；真实 Redis 故障切换和多节点清理仍需专用环境验收。

## 2026-10-07 FreeSQL/EFCore Docker MySQL-CAP-RabbitMQ 有序事务验证

- 执行状态：COMPLETED（FIX-006 的真实 ORM/CAP 有序事务子项）。使用隔离 Docker MySQL 8.4、RabbitMQ 4.3 管理镜像，分别运行 `Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest` 与 `Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest` 的 `net6.0` 集成测试。
- FreeSQL 首次回滚重试运行暴露测试库中的旧 `bing_event_inbox` 表缺少当前代码要求的 `CreatedAtUtc` 列；没有修改生产启动流程，先在隔离测试库执行显式兼容迁移并重新验证。迁移后回滚重试 1/1 通过，完整 FreeSQL 有序集成类 3/3 通过，证据分别为 `artifacts/docker-cap-validation/freesql-ordered-retry-migrated-net6/freesql-ordered-retry-migrated-net6.trx` 和 `artifacts/docker-cap-validation/freesql-ordered-full-migrated-net6/freesql-ordered-full-migrated-net6.trx`。
- EFCore 有序集成类在同一隔离 MySQL/RabbitMQ 环境通过 3/3，证据为 `artifacts/docker-cap-validation/efcore-ordered-net6/efcore-ordered-net6.trx`。覆盖提交后按序处理、回滚后序号重试、CAP 重复投递去重和乱序到达补齐。
- 这组测试证明两套 ORM 的业务事务、CAP 出箱、RabbitMQ 接收及有序入箱链路在该隔离环境可工作；旧表必须按 `OrderedEventSchema` 执行显式部署迁移，框架不在生产启动时自动修改表结构。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有 NU1900、net6 生命周期及既有警告。真实多节点竞争、Redis 故障切换、CAP 断连恢复和蓝绿切流仍未由本次单机 Docker 验证覆盖。

## 2026-10-07 Redis 限流与分布式锁真实集成验证

- 执行状态：COMPLETED（FIX-008 限流跨进程子项及 P0 锁行为验证）。使用隔离 Redis Docker 实例，`RedisTokenBucketRateLimiterTest` 在 net8.0、net6.0 各 12/12 通过，证据为 `artifacts/redis-rate-limit-validation/redis-tokenbucket-net8/redis-tokenbucket-net8.trx` 和 `artifacts/redis-rate-limit-validation/redis-tokenbucket-net6/redis-tokenbucket-net6.trx`。
- 限流测试实际启动两个独立 dotnet 探针进程，验证同一策略和分桶不会因进程数增加而获得两份容量；同时覆盖并发申请、取消不消耗额度、闲置键回收、补充时间和策略配置冲突。
- `RedisDistributedLockTest` 在 net8.0、net6.0 各 10/10 通过，证据为 `artifacts/redis-lock-validation/redis-lock-net8/redis-lock-net8.trx` 和 `artifacts/redis-lock-validation/redis-lock-net6/redis-lock-net6.trx`。覆盖原子获取、租期超过一分钟、竞争、错误持有者释放、重复释放和回调异常释放。
- 本地 Redis 证据不覆盖主从故障切换、网络断连/恢复、HTTP 真实认证代理链路、限流显式降级配置和多节点审计协调；实际 SDK 为 8.0.425，锁定的 8.0.424 未验证，构建仍有 NU1900 与 net6 生命周期警告。

## 2026-10-07 有序消费者运行时排空契约

- 执行状态：COMPLETED（FIX-011/FIX-013 的有序消费者排空子项）。`AddBingOrderedConsumer<THandler>` 现在为每个稳定逻辑消费者注册一个 `IOrderedEventConsumerRuntime`，同时保留原有 `IHostedService` 注册。运行时契约提供 `ConsumerName`、`IsRunning`、`IsDraining`、`InFlightCount`、`BeginDrain()` 和 `WaitForDrainAsync(...)`，部署脚本或运维协调器可先请求停止扫描，再等待后台循环及在途处理真实退出。
- 消费者 `StopAsync` 复用同一 `BeginDrain` 路径；排空期间不会开始新的扫描，已进入处理器的业务仍按原取消和数据库事务规则运行。等待超时或取消只结束当前等待，不释放活动作用域；原有 `Bing.OrderedEventConsumerDrainIncomplete` 诊断和重复停止行为保持不变。
- 新增 `OrderedEventConsumerTest.ConsumerRuntime_WhenBeginDrain_ShouldWaitForActiveHandlerWithoutForcingRelease`，验证不响应取消的处理器在排空等待超时后仍保持在途计数，释放处理器后才报告真实退出。该测试在 net6.0、net8.0 各 1/1 通过；完整 `OrderedEventConsumerTest` 在两个目标框架各 16/16 通过，证据为 `artifacts/ordered-consumer-runtime7/consumer-runtime-net6.trx`、`consumer-runtime-net8.trx`、`ordered-consumer-full-net6.trx` 和 `ordered-consumer-full-net8.trx`。
- 该契约只覆盖 Bing 有序消费者，不自动接管 CAP 原生回调、Hangfire、SSE/WebSocket 或其他脱离请求的后台任务；完整 Compose/Kubernetes 动态切流、消费者版本切换和跨节点排空仍未验收。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证，构建继续存在 NU1900 警告。

## 2026-10-07 Admin 库存竞争验收补齐

- 执行状态：COMPLETED（FIX-007 的同作用域及跨进程竞争子项）。使用隔离 Docker MySQL 8.4（宿主端口 33306）和 RabbitMQ 4.3（宿主端口 35672），FreeSQL 与 EFCore 各执行同订单作用域竞争、不同订单作用域竞争、同订单跨进程竞争、不同订单跨进程竞争四个用例，均为 4/4 通过。
- FreeSQL 证据：`artifacts/inventory-competition-freesql-net6-migrated/inventory-competition-freesql-net6-migrated.trx`；EFCore 证据：`artifacts/inventory-competition-efcore-net6-migrated/inventory-competition-efcore-net6-migrated.trx`。
- 首次运行暴露测试库 `bing_audit_log` 是旧结构，缺少当前生产 SQL 使用的 `IsCritical`；按 `deploy/migrations/auditing/001-critical-flag.mysql.sql` 和 `004-tenant-retention-index.mysql.sql` 在隔离库执行显式迁移后重跑通过。该结果固定了生产部署必须先完成审计结构迁移的要求，生产启动不会隐式修改表结构。
- 通过结果断言同订单只有一个非重复结果、不同订单竞争库存不为负且审计/出箱数量正确；跨进程探针使用独立进程，不等同单进程多作用域测试。
- 验证使用 SDK 8.0.425，仓库锁定的 8.0.424 仍未验证；构建有既有 NU1900、net6 生命周期和分析器警告。该批仍不覆盖 MySQL 主从故障切换、CAP 断连后的提交确认丢失及蓝绿动态切换。

## 2026-10-07 Admin 库存故障恢复验收补齐

- 执行状态：COMPLETED（FIX-007/FIX-013 的真实故障子项）。在同一隔离 Docker MySQL 8.4、RabbitMQ 4.3 环境中，FreeSQL 与 EFCore 各执行五个故障用例，均为 5/5 通过：一次 InnoDB 死锁后使用新作用域重试、持续死锁达到四次上限、提交确认丢失后的 Unknown 结果查询、恢复时清理异常聚合，以及 CAP 出箱后台扫描恢复。
- FreeSQL 证据：`artifacts/inventory-fault-recovery-freesql-net6/inventory-fault-recovery-freesql-net6.trx`；EFCore 证据：`artifacts/inventory-fault-recovery-efcore-net6/inventory-fault-recovery-efcore-net6.trx`。
- 测试断言死锁回滚后只保留一次业务/审计/出箱提交；提交确认丢失时按原租户和订单读取首次结果，不换身份重做；后台恢复把原出箱记录推进到成功且不重复扣减。清理未完成或 Unknown 状态不会被伪装为普通成功。
- 该批仍不覆盖 MySQL 主从故障切换、CAP 网络断连后真实确认丢失注入、RabbitMQ 重启期间的恢复以及蓝绿动态切流。验证使用 SDK 8.0.425，仓库锁定的 8.0.424 仍未验证；构建有既有 NU1900、net6 生命周期和分析器警告。

## 2026-10-07 CAP 原生消费回调排空契约

- 执行状态：COMPLETED（FIX-011/FIX-013 的 CAP 调用器排空子项）。`BingSubscribeInvoker` 新增 `IBingCapConsumerRuntime`，在通过 `ISubscribeInvoker` 执行回调时记录在途数量；`BeginDrain` 后拒绝新的调用并进入 CAP 失败路径，已经开始的回调继续运行，直到真实退出。
- `EnableBingCapConsumerCancellationRetry()` 以 `TryAdd` 暴露与 `ISubscribeInvoker` 相同生命周期的运行时实例，重复启用和预先注册的自定义运行时不被覆盖。等待超时或调用取消只结束当前等待，不强制释放 CAP 作用域；直接调用 `BingSubscribeInvoker.InvokeAsync` 仍保留原生基类语义。
- 作用域边界修正：当调用方注册继承 `BingSubscribeInvoker` 的 `Scoped` 调用器时，`IBingCapConsumerRuntime` 跟随调用器生命周期注册，不从根容器捕获 scoped 服务；`ValidateScopes` 作用域解析测试已通过。
- 宿主停止只接入 Singleton 调用器的 `BingCapConsumerHostDrain`；Scoped 调用器不注册单例宿主适配器，避免 `CapHostedLifecycleService` 构造时解析 scoped 运行时。新增 `Enable_WhenScopedInvokerIsUsedByCapHost_ShouldBuildWithValidateScopes` 验证宿主在 `ValidateScopes` 下可构造。
- `BingSubscribeInvokerTest.ConsumerRuntime_WhenDraining_ShouldWaitForInFlightAndRejectNewInvocation` 与 `EnableCancellationRetry_ShouldExposeInvokerRuntime` 在 net6.0、net8.0 的定向集均通过，`BingSubscribeInvokerTest` 各 12/12；运行时 SDK 为 8.0.425，锁定的 8.0.424 未验证。
- 后续作用域隔离修正后的 `Bing.EventBus.Tests.Cap` 完整定向集在 net6.0、net8.0 各 64/64 通过，覆盖 Scoped 调用器宿主构造；TRX 为 `artifacts/cap-consumer-drain-net6/bing-eventbus-cap-net6.trx` 和 `artifacts/cap-consumer-drain-net8/bing-eventbus-cap-net8.trx`，运行时 SDK 为 8.0.425。
- 该契约只覆盖 Bing 适配器进入的 CAP 回调，不代表 RabbitMQ 已停止拉取、CAP 原生处理器、Hangfire 或其他后台任务已排空；完整 Compose/Kubernetes 动态切换、连接故障恢复和跨节点排空仍需专用环境验证。

## 2026-10-07 Admin Hangfire 应用层任务排空

- 执行状态：PARTIAL（FIX-011/FIX-013 的 Hangfire 应用层子项）。Task 角色新增 `IBingHangfireRuntime`、`BingHangfireDrainFilter` 和宿主停止适配器。任务创建过滤器和执行过滤器均接入排空状态：开始排空后既拒绝新的任务写入 Hangfire 存储，也拒绝进入执行区；已进入执行区的任务继续运行，直到 `WaitForDrainAsync` 观察到计数归零。
- `HangfireModule` 只为 `BingHostRole.Task` 注册该运行时、过滤器和 Hangfire Server；Web/Consumer 不注册任务运行时。`BingHangfireDrainHostedService.StopAsync` 与 `ApplicationStopping` 共用 `BeginDrain`，并使用 Generic Host 传入的关闭令牌等待在途任务归零；等待取消或超时会抛出带在途数量的诊断异常，不强制释放作用域或根容器。释放时退订停止回调并移除该过滤器。
- `AdminHangfireRegistrationTest` 的任务创建拒绝、排空过滤器、宿主等待、取消诊断、`ApplicationStopping` 回调和角色注册回归在 net6.0 通过 13/13，证据为 `modules/admin/tests/Bing.Admin.EFCore.Tests/TestResults/admin-hangfire-drain-net6.trx`。新增用例确认排空期间不会把新任务写入存储，宿主进入停止阶段会开始排空但仍保留在途任务；构建使用实际 SDK 8.0.425，仓库锁定的 8.0.424 未验证，仍有 net6 生命周期、Microsoft.Extensions 10/net6 和 NU1900 警告。
- 该实现不控制 Hangfire 内部轮询器、服务器选举、存储中的任务状态或多个 Task 节点之间的排空顺序。生产切流仍需先停止任务准入，再通过 Hangfire 原生宿主停止、稳定队列/幂等键和部署编排确认恢复；动态 Compose/Kubernetes 任务切换、任务重试恢复和跨节点排空仍未验收。

## 2026-10-07 蓝绿 Task 部署槽位补齐

- 执行状态：PARTIAL（FIX-011 的部署模板子项）。`deploy/bluegreen/compose.yaml` 新增 `task-blue` 和 `task-green` profile，使用 `BING_HOST_ROLE=task` 且默认关闭 CAP 消费；`kubernetes.yaml` 新增对应蓝绿 Deployment，绿版默认 `replicas: 0`，与 Consumer 使用独立的进程槽位和终止宽限期。
- `Test-BlueGreen.ps1` 静态检查新增 Task 角色、消费关闭、蓝绿槽位和绿版停放断言；`-RequireTask` 可在 Live 模式要求 Compose 至少有一个运行中的 Task 服务，或 Kubernetes 至少有一个就绪的 Task 副本。Compose 静态检查共 15/15 通过，命令为 `& .\deploy\bluegreen\Test-BlueGreen.ps1 -Platform Compose`。
- 使用蓝绿镜像摘要执行 `docker compose -f .\deploy\bluegreen\compose.yaml config --quiet` 返回 0；Docker 客户端输出本机配置文件访问警告，但未阻断配置解析。当前没有真实蓝绿镜像、HTTP 服务或 Kubernetes 集群，未执行 `-Live`、任务排空、切流和回滚。
- README 已明确 Task 与 Consumer 分开启动、排空和回滚；`-RequireTask` 只证明进程/副本就绪，不证明 Hangfire 原生队列已排空、调度唯一性、跨节点任务恢复或动态发布成功。
- Task 槽位现适用于包含同一 Hangfire 排空接线的 EFCore 和 FreeSQL Admin 镜像；FreeSQL 注册测试已通过，但真实 Hangfire Server、任务存储迁移、多节点排空和动态切换仍未验收。

## 2026-10-07 有序收件箱安全留存清理

- 执行状态：COMPLETED（P2 有序消费的显式留存子项）。新增可选 `IOrderedEventStoreRetention` 和 `OrderedEventCoordinator.DeleteProcessedBeforeAsync`，内置 MySQL/SQLite 存储按消费者、截止时间和批大小只删除已处理记录；待处理和失败记录不会进入删除条件，清理在协调器事务内提交。
- `DbOrderedEventStoreTest.DeleteProcessedBeforeAsync_ShouldKeepPendingAndFailedRecords` 验证已处理记录删除、待处理记录保留及失败状态保留；`OrderedEventCoordinatorTest.DeleteProcessedBeforeAsync_ShouldDeleteCompletedRecordsOnly` 验证协调器路径清理后游标仍可查询。两个直接测试在 `Bing.EventBus.Tests` 的 net6.0 均通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-net6-v2.trx` 和 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-coordinator-only.trx`；同一测试筛选在 net8.0 以 2/2 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-net8-retry.trx`。
- `OrderedEventRetentionHostedServiceTest` 在 net6.0 和 net8.0 各 6/6 通过，覆盖默认关闭、多消费者立即清理、未获取协调租约跳过、缺少消费者配置拒绝、缺少租约提供程序启动拒绝，以及清理事务忽略取消时停止等待真实完成；证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-scheduler-net6-v5.trx` 和 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-scheduler-net8-v5.trx`。租约提供程序按清理作用域解析，测试在 `ValidateScopes=true` 下通过，避免单例托管服务捕获 scoped 资源。
- 本批同时新增默认关闭的 `AddBingOrderedEventRetentionScheduler`，支持多消费者、有界批次和可选协调租约；不提供归档存储或默认保留期，宿主必须先保留游标、审计和业务幂等依据，再启用清理。自定义存储若未实现可选契约会返回未支持结果，不影响既有 `IOrderedEventStore` 消费契约。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；net8.0 重跑使用 `UseSharedCompilation=false` 规避测试 DLL 文件锁，构建仍有既有 NU1900 和分析器警告。
- 存储、协调器和调度器的有序留存组合筛选在 net6.0、net8.0 各 8/8 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-full-net6-v2.trx` 和 `ordered-retention-full-net8-v2.trx`。

## 2026-10-07 有序收件历史去重保护

- 执行状态：COMPLETED（P2/P4 有序收件清理安全边界子项）。`DbOrderedEventStore` 新增 `bing_event_inbox_archive` 历史表：已处理收件记录在同一事务内先归档再从收件箱删除；后续接收会同时检查收件箱和历史表，避免清理后重复事件 ID 被当作新事件接受。历史表不由现有留存调度器自动删除，必须由部署方按重放窗口、业务幂等和审计留存策略另行管理。
- `OrderedEventSchema` 的 MySQL 建表脚本现在包含四张表，SQLite 包含四张表及两个年龄索引；新增 `deploy/migrations/ordered-events/002-inbox-archive.mysql.sql`。已有部署必须在启用清理前执行该迁移，应用启动不会自动修改生产表结构。
- `DbOrderedEventStoreTest.DeleteProcessedBeforeAsync_ShouldKeepPendingAndFailedRecords` 新增历史记录数量及清理后重复事件校验：原事件可幂等重投，复用相同事件 ID 但改变序号或正文会拒绝。`OrderedEventSchemaTest` 对 MySQL/SQLite 的完整 SQL 和语句数量做精确断言。`Bing.EventBus.Tests` 的归档定向集在 net6.0、net8.0 各 10/10 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-archive-net6.trx` 和 `ordered-retention-archive-net8.trx`。
- Admin EFCore、FreeSQL 及 FreeSQL MySQL 有序测试清理路径均包含历史表，避免集成测试之间残留归档记录。`Bing.Data` 生产工程编译通过，实际 SDK 为 8.0.425；仓库锁定的 8.0.424 未验证，MySQL 归档 INSERT/DELETE 语法仍需在真实 MySQL 环境重跑确认。
- 该历史表只保留事件身份和正文用于去重，不替代外部副作用的业务幂等记录；过早删除历史表记录会重新打开重复事件入口，生产归档策略必须先确定允许重放范围。
- `Bing.Admin.EFCore.Tests` 与 `Bing.Admin.FreeSQL.Tests` 的 net6.0 测试工程在 `BuildProjectReferences=false`、`GenerateDocumentationFile=false` 下分别编译成功，均为 0 错误；完整构建仍可能受到仓库中已有 XML/资源输出文件锁影响。当前 Docker API 返回权限拒绝，未在本轮重新执行 MySQL 归档 SQL。
- `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 均 326/326 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-archive-eventbus-full-net6.trx` 和 `ordered-archive-eventbus-full-net8.trx`。输出仍有既有 NU1900（NuGet 漏洞源不可达）警告，未将其误判为归档功能失败。

## 2026-10-07 有序历史表显式留存清理

- 执行状态：COMPLETED（P4 消息归档容量治理子项）。新增独立 `IOrderedEventStoreArchiveRetention`，不改变原有 `IOrderedEventStoreRetention` 契约；`DbOrderedEventStore` 和 `OrderedEventCoordinator` 提供按消费者、UTC 截止时间和有界批次删除历史表的入口。
- 历史清理不接入默认 `OrderedEventRetentionHostedService`，避免框架在未知重放窗口下自动删除事件 ID 去重依据。调用方必须先确认重放范围、业务幂等记录和审计留存，再显式调用 `DeleteArchivedBeforeAsync`；确认相同前置条件后，也可以显式配置归档保留期限和消费者启用可选调度；删除后超出窗口的事件可能再次入箱。
- `DbOrderedEventStoreTest.DeleteArchivedBeforeAsync_ShouldDeleteOnlyExpiredHistory` 与 `OrderedEventCoordinatorTest.DeleteArchivedBeforeAsync_ShouldDeleteExpiredHistoryOnly` 验证 SQLite 历史清理的批量、时间和协调器事务边界；归档/历史调度定向集在 net6.0、net8.0 各 12/12 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-archive-retention-net6-v1.trx` 和 `ordered-archive-retention-net8-v1.trx`。新增接口后的 EventBus 全量回归在 net6.0、net8.0 各 328/328 通过，证据为 `ordered-archive-history-full-net6-v1.trx` 和 `ordered-archive-history-full-net8-v1.trx`。
- 真实 MySQL 历史删除语法、跨节点历史清理协调和容量预算仍需专用环境与部署策略验证；归档调度默认关闭，启用后的单进程 SQLite 行为另有后续证据，不能替代生产验收。

## 2026-10-07 有序历史清理取消与回滚边界

- 执行状态：COMPLETED（历史清理异常路径补测）。`DbOrderedEventStore.DeleteProcessedBeforeAsync` 和 `DeleteArchivedBeforeAsync` 在执行数据库命令前检查调用方取消令牌；协调器继续在开启事务前检查取消，避免取消请求产生新的清理事务。
- `DbOrderedEventStoreTest.DeleteArchivedBeforeAsync_WhenTransactionRollsBack_ShouldKeepHistory` 验证删除操作回滚后历史记录仍存在；`DeleteArchivedBeforeAsync_WhenCanceled_ShouldKeepHistory` 验证已取消令牌不会删除历史。`OrderedEventCoordinatorTest.DeleteArchivedBeforeAsync_WhenCanceled_ShouldNotBeginTransaction` 进一步断言协调器没有开启事务。包含原有正常路径在内的五项定向测试在 net6.0、net8.0 各 5/5 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-archive-cancel-rollback-net6-v2.trx` 和 `ordered-archive-cancel-rollback-net8-v2.trx`。
- 取消只表示本次等待停止；数据库驱动若已进入执行阶段仍由事务回滚保证原子性。真实 MySQL 取消、网络断连和跨节点历史清理协调仍需专用环境验证；实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证。
- 取消边界补测后的 `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 均 331/331 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-archive-history-full-net6-v2.trx` 和 `ordered-archive-history-full-net8-v2.trx`；结果仍包含既有 NU1900 与分析器警告，不影响测试通过结论。

## 2026-10-07 审计日志显式归档

- 执行状态：COMPLETED（P4 审计容量治理的显式归档子项）。新增 `IAuditLogArchiveStore`、`IAuditLogArchiveService`、`AuditLogArchiveService` 和 `AddBingAuditLogArchive<TAuthorizer>()`。归档复用保留授权器，但默认不注册后台调度，也不改变原有 `DeleteBeforeAsync` 删除语义。
- `DbAuditingStore.ArchiveBeforeAsync` 在自有连接的单一事务内先把过期普通审计写入 `bing_audit_log_archive`，再从主表删除；取消、插入或删除失败都会回滚两步。关键审计不会归档，归档表保留明细和 `ArchivedAtUtc`；如果主表身份已在归档表存在，唯一约束冲突会回滚并保留主表，避免审计身份复用时静默丢失新内容。
- `AuditLogSchema` 已为 MySQL/SQLite 增加归档表和时间、租户索引；新增 `deploy/migrations/auditing/005-audit-log-archive.mysql.sql` 与对应 SQLite 脚本。已有部署必须显式执行迁移，生产不自动建表或自动删除归档历史。
- `DbAuditingStoreTest`、`AuditLogArchiveServiceTest`、`AuditLogSchemaTest`、`DbAuditingStoreMySqlCommandTest` 直接覆盖归档过滤、批次、身份冲突保护、回滚、预取消、授权顺序及完整 SQL。该段记录的是身份冲突保护补测前的中间结果；最新测试结果见下方“审计归档历史显式清理”章节。
- 当前仅在 SQLite 和 MySQL SQL 文本录制夹具验证；Docker API 当前无权限，未重新执行真实 MySQL 归档迁移/事务。归档表历史删除、跨节点协调、冷存储备份、容量预算及 SDK 8.0.424 仍需专用环境验证；实际本地 SDK 为 8.0.425，另有既有 NU1900 警告。

## 2026-10-07 审计归档历史显式清理

- 执行状态：COMPLETED（P4 审计容量治理的二次清理子项）。新增 `IAuditLogArchiveRetentionStore`、`IAuditLogArchiveRetentionAuthorizer`、`IAuditLogArchiveRetentionService` 和 `AddBingAuditLogArchiveRetention<TAuthorizer>()`。该授权器与归档写入授权器分离，降低误删归档历史的权限复用风险。
- `DbAuditingStore` 通过显式接口实现归档历史清理，按 `ArchivedAtUtc`、租户和审计标识稳定排序，单批最多 10,000 条；只删除归档表中的普通记录，不触及主审计表。事务失败、回滚和取消不会报告为已清理，默认不注册后台调度。
- `DbAuditingStoreTest` 新增过期/新记录/关键记录过滤、批量限制、回滚、UTC/批量预检和取消测试；`DbAuditingStoreMySqlCommandTest` 固定 MySQL 完整删除 SQL；`AuditLogArchiveRetentionServiceTest` 覆盖授权顺序、拒绝、取消和幂等注册。
- 归档相关定向集在 net6.0、net8.0 各 24/24 通过；审计项目完整回归在两个目标框架各 204/204 通过，TRX 为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-archive-retention-target-net6.trx`、`audit-archive-retention-target-net8.trx`、`audit-archive-retention-full-net6.trx` 和 `audit-archive-retention-full-net8.trx`。
- 该能力只提供显式、有界删除，不判断监管留存、外部备份或业务重放窗口；真实 MySQL 事务、跨节点清理协调、冷存储和 SDK 8.0.424 仍需专用环境验证。实际 SDK 为 8.0.425，构建仍有既有 NU1900 警告。

## 2026-10-07 审计归档历史可选调度

- 执行状态：COMPLETED（P4 审计容量治理的调度便利性子项）。新增 `AuditLogArchiveRetentionScheduleOptions`、`AuditLogArchiveRetentionHostedService` 和 `AddBingAuditLogArchiveRetentionScheduler<TAuthorizer>()`。调度默认关闭，启用后每轮只经独立 `IAuditLogArchiveRetentionService` 删除一个有界批次；不会绕过授权器，也不会自动创建冷存储备份。
- 调度器支持启动立即执行、有限间隔、1 到 10,000 的批量上限及可选协调租约；归档历史使用独立默认锁键 `bing:audit-archive-retention`，避免与普通审计清理混用。启用协调却未注册 `ILockLeaseProvider` 时在 `StartAsync` 直接拒绝；未获取租约时跳过本轮。
- 停止和释放会等待当前存储调用实际结束，不使用宿主停止令牌强制提前释放作用域；归档历史的重放窗口、外部备份、跨节点租约故障和真实 MySQL 事务仍由宿主环境验收。
- `AuditLogArchiveRetentionHostedServiceTest` 在 net6.0、net8.0 各 7/7 通过；`Bing.Auditing.Tests` 在两个目标框架各 209/209 通过。证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-archive-retention-scheduler-net6-final.trx`、`audit-archive-retention-scheduler-net8-final.trx`、`audit-archive-scheduler-full-net6.trx` 和 `audit-archive-scheduler-full-net8.trx`。实际 SDK 为 8.0.425，锁定 8.0.424 未验证，构建仍有 NU1900 警告。

## 2026-10-07 审计调度作用域租约修正

- 执行状态：COMPLETED（审计调度器生命周期安全修正）。普通审计保留调度器和归档历史清理调度器不再在单例构造函数中捕获可选的 `ILockLeaseProvider`；每次批处理在独立异步作用域内解析租约提供程序和清理服务，租约、清理调用及作用域按同一批次释放。
- 启用协调租约时，`StartAsync` 先在临时作用域验证提供程序是否注册；缺失注册会立即拒绝启动。调度器停止或释放时仍等待实际存储操作结束，不因宿主停止令牌提前释放活动作用域。
- `AuditLogRetentionHostedServiceTest.Scheduler_WhenCoordinationProviderIsScoped_ShouldBuildWithValidateScopes` 与 `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenCoordinationProviderIsScoped_ShouldBuildWithValidateScopes` 验证 `ValidateScopes=true`、`ValidateOnBuild=true` 下的构建和租约释放路径。普通及归档调度器合计定向测试在 net6.0、net8.0 各 16/16 通过，证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-retention-scoped-net6.trx` 和 `audit-retention-scoped-net8.trx`。
- 作用域修正后的 `Bing.Auditing.Tests` 全量回归在 net6.0、net8.0 各 213/213 通过，证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-retention-scoped-full-net6.trx` 和 `audit-retention-scoped-full-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900 警告。
- 本修正只覆盖进程内调度器的 DI 作用域和停止等待，不替代真实 Redis 多节点租约、MySQL 归档事务、冷存储备份、重放窗口治理及蓝绿动态部署验收。

## 2026-10-07 审计调度启动预检

- 执行状态：COMPLETED（审计调度配置错误快速失败子项）。普通审计保留调度器和归档历史清理调度器在启用后于 `StartAsync` 预检对应高层清理服务；服务或其构造依赖缺失时立即抛出配置异常，不创建后台循环，也不把错误延迟为周期日志。
- 新增 `AuditLogRetentionHostedServiceTest.Scheduler_WhenRetentionServiceMissing_ShouldRejectStartup` 与 `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenArchiveRetentionServiceMissing_ShouldRejectStartup`。结合既有协调租约缺失、作用域租约和停止等待测试，两类调度器定向测试在 net6.0、net8.0 各 18/18 通过，证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-scheduler-startup-validation-net6.trx` 和 `audit-scheduler-startup-validation-net8.trx`。
- 预检补丁后的 `Bing.Auditing.Tests` 全量回归在 net6.0、net8.0 各 215/215 通过，证据为 `framework/tests/Bing.Auditing.Tests/TestResults/audit-scheduler-startup-full-net6.trx` 和 `audit-scheduler-startup-full-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900 警告。
- 该预检只保证进程内服务图在后台启动前完整；不替代真实 MySQL 归档事务、Redis 多节点租约、冷存储备份、重放窗口治理或蓝绿动态部署验收。

## 2026-10-07 有序事件留存启动预检

- 执行状态：COMPLETED（P4/P2 有序留存调度配置错误快速失败子项）。`OrderedEventRetentionHostedService.StartAsync` 在启用后于临时异步作用域预检 `OrderedEventCoordinator`；缺少协调器会直接抛出配置异常，不创建后台清理循环。协调租约仍在同一作用域校验，实际批次继续使用该批次作用域解析协调器和租约。
- 新增 `OrderedEventRetentionHostedServiceTest.Scheduler_WhenCoordinatorMissing_ShouldRejectStartup`。有序留存调度定向集在 net6.0、net8.0 各 7/7 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-startup-validation-net6.trx` 和 `ordered-retention-startup-validation-net8.trx`。
- 预检补丁后的 `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 332/332 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-startup-full-net6.trx` 和 `ordered-retention-startup-full-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900、nullable 和分析器警告。
- 该预检只保证进程内有序清理依赖图在宿主启动前完整；不替代真实 MySQL 事务、Redis 多节点协调、消息重放窗口、CAP 故障恢复或蓝绿动态部署验收。

## 2026-10-07 FreeSQL 存储自动登记领域事件

- 执行状态：COMPLETED（P1 事件与工作单元闭环的 FreeSQL 本地路径）。`StoreBase<TEntity,TKey>` 现在在新增、更新、删除及对应异步路径中自动登记实现 `IHasDomainEvents` 的聚合；调用方仍可显式调用 `UnitOfWork.RegisterDomainEventSource`，不会改变既有显式注册契约。删除路径先登记调用方传入的原实体，再按标识加载持久化实体，避免原聚合上产生的领域事件因重新加载而丢失。
- 新增 `StoreChanges_WithDomainEventEntity_ShouldDispatchWithoutExplicitRegistration` 和 `StoreChanges_WithDomainEventEntityCollection_ShouldDispatchWithoutExplicitRegistration`，使用 SQLite 真实存储、事务工作单元和领域事件分发器，覆盖单实体及批量新增、更新、异步删除路径，并断言事件分发、事件源清空和数据删除。该定向测试在 net6.0、net8.0 各 2/2 通过，证据为 `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-store-domain-events-net6-v2.trx` 和 `freesql-store-domain-events-net8-v2.trx`。
- 修正 `FreeSqlOrderedEventTransactionFactoryTest.Coordinator_WhenBusinessFails_ShouldRollbackSavepointAndBlockStream` 的测试夹具：现行恢复流程要求显式 `IOrderedEventRecoveryAuthorizer`，测试现在注册允许授权器并验证授权调用一次；这保持生产恢复接口的安全拒绝行为，不放宽未授权恢复。
- `Bing.FreeSQL.Tests` 全量回归在 net6.0、net8.0 各 76/76 通过、24 项外部 MySQL 集成测试跳过、0 失败；证据为 `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-domain-event-full-net6-v3.trx` 和 `freesql-domain-event-full-net8-v3.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900（NuGet 漏洞源不可达）和分析器警告。
- 本批证明的是进程内 FreeSQL 领域事件登记与工作单元路径；真实 MySQL 事务、CAP/RabbitMQ 多节点投递、进程中断恢复、跨节点顺序和蓝绿切换仍需专用环境验证。FreeSQL 通用异步查询同步回退已在前序批次完成，未纳入本批。

## 2026-10-07 CAP 消费收据显式留存清理

- 执行状态：COMPLETED（P1 业务幂等收据容量治理的本地可验证子项）。`EventConsumptionStore` 新增可选 `IEventConsumptionRetentionStore`，在调用方拥有的事务内按稳定消费者、UTC 截止时间和有界批次删除过期收据；MySQL 使用有序 `DELETE ... LIMIT`，SQLite 使用事务内 `rowid` 子查询，单批上限为 10,000 条。
- `EventConsumptionStoreTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceiptsAndRespectBatch` 验证只删除目标消费者的过期收据、保留其他消费者和新收据，并遵守批量上限；`DeleteBeforeAsync_WhenTransactionRollsBack_ShouldKeepReceipts` 验证回滚保留；`DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotDeleteReceipts` 验证取消、非 UTC 截止时间和非法批量在执行前拒绝。net6.0、net8.0 定向集各 3/3 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-net6.trx` 和 `event-consumption-retention-net8.trx`。
- 该接口不注册后台调度器、不执行授权、不归档业务幂等依据；调用方必须先确认重放窗口、审计和外部副作用的保护仍然有效。CAP 自身消息表清理不能替代该收据策略。真实 MySQL 删除事务、跨节点清理协调、长周期容量和 SDK 8.0.424 仍需专用环境验证；本次使用 SDK 8.0.425，构建保留既有 NU1900 警告。
- 新增 `EventConsumptionMySqlIntegrationTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceipts`，在专用 MySQL 环境门控下固定 MySQL `DELETE ... ORDER BY ... LIMIT` 路径；本机无可用外部数据库时 net6.0/net8.0 各 0 通过、1 跳过，TRX 为 `framework/tests/Bing.FreeSQL.Tests/TestResults/event-consumption-mysql-retention-net6.trx` 和 `event-consumption-mysql-retention-net8.trx`。该跳过只表示环境未执行，不计入 MySQL 验收通过。

## 2026-10-07 CAP 收据 SQLite 时间格式固定

- 执行状态：COMPLETED（收据留存边界补强）。SQLite 收据写入和清理截止时间现在都使用固定的 UTC round-trip ISO 文本；MySQL 继续使用原生 `DateTime` 参数。这样不依赖 Microsoft.Data.Sqlite 对 `DateTime` 的默认文本格式，避免 `T`/空格格式差异造成过期判断错误。
- `EventConsumptionStoreTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceiptsAndRespectBatch` 保留一条由正常登记路径写入、未手工改时间的新收据，验证它不会因驱动默认格式而被误删。该定向集在 net6.0、net8.0 各 3/3 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-net6-v2.trx` 和 `event-consumption-retention-net8-v2.trx`。
- SQLite 时间格式修正后的 EventBus 全量回归在 net6.0、net8.0 各 335/335 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-full-net6-v2.trx` 和 `event-consumption-retention-full-net8-v2.trx`。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证；构建保留既有 NU1900、nullable 和分析器警告。

## 2026-10-07 CAP 收据高层授权入口

- 执行状态：COMPLETED（P1 业务幂等收据治理的授权边界子项）。新增 `IEventConsumptionRetentionAuthorizer`、`IEventConsumptionRetentionService`、`EventConsumptionRetentionService` 和 `AddBingEventConsumptionRetention<TAuthorizer>()`。服务保留调用方事务，按“参数预检 → 取消检查 → 授权 → 低层删除”顺序执行，不自动创建数据库连接、第二事务或后台调度器。
- `EventConsumptionRetentionServiceTest.DeleteBeforeAsync_ShouldAuthorizeBeforeStore` 验证授权先于存储；`DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotCallStore` 验证拒绝无低层副作用；`DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotCallDependencies` 验证取消和非法参数在授权前拒绝；`AddBingEventConsumptionRetention_ShouldKeepExistingAuthorizer` 验证重复注册不覆盖已有授权器。net6.0、net8.0 各 4/4 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-service-net6-v2.trx` 和 `event-consumption-retention-service-net8-v2.trx`。
- 高层服务仍要求宿主显式注册 `IEventConsumptionRetentionStore`，授权器必须由宿主根据身份、租户和重放策略实现；本批不引入默认授权、不提供后台调度、不把 CAP 消息表清理或本地测试等同于业务幂等验收。真实 MySQL 事务、跨节点协调、重放窗口和 SDK 8.0.424 仍待专用环境验证。
- 高层服务接入后的 EventBus 全量回归在 net6.0、net8.0 各 339/339 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-service-full-net6.trx` 和 `event-consumption-retention-service-full-net8.trx`。实际 SDK 为 8.0.425，构建保留既有 NU1900、nullable 和分析器警告。

## 2026-10-07 CAP 收据表结构与迁移契约

- 执行状态：COMPLETED（P1 收据部署一致性子项）。新增 `EventConsumptionSchema.GetCreateStatements`，统一生成 MySQL/SQLite 收据表和留存索引脚本；新增 `deploy/migrations/events/001-event-consumption.mysql.sql` 与 `001-event-consumption.sqlite.sql`。MySQL 使用 `utf8mb4_bin` 身份列、`datetime(6)` 和 `(Consumer, ProcessedAtUtc, EventId)` 留存索引；SQLite 使用 BINARY 文本列和同等索引。
- `EventConsumptionSchemaTest` 精确断言两套数据库的完整 SQL 以及非法数据库类型；`EventConsumptionStoreTest` 和 MySQL 集成夹具改为复用生产 schema 契约。schema/消费定向集在 net6.0、net8.0 各 17/17 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-schema-net6.trx` 和 `event-consumption-schema-net8.trx`。
- `EventConsumptionMySqlIntegrationTest` 在 net6.0 已按新 schema 编译并执行环境门控，5 项均跳过（无外部数据库），不计入 MySQL 验收；真实 MySQL 迁移、索引计划、跨节点协调和 SDK 8.0.424 仍需专用环境验证。应用不会在启动时自动修改生产数据库。
- schema 接入后的 `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 342/342 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-schema-full-net6.trx` 和 `event-consumption-schema-full-net8.trx`。FreeSQL MySQL 集成夹具在 net8.0 同样编译并执行门控，5 项均跳过；net6.0/net8.0 的 MySQL TRX 分别为 `framework/tests/Bing.FreeSQL.Tests/TestResults/event-consumption-schema-mysql-net6.trx` 和 `event-consumption-schema-mysql-net8.trx`。

## 2026-10-07 CAP 收据授权入口源码路径修正

- 执行状态：COMPLETED（交付完整性修正）。将 `EventConsumptionRetentionExtensions.cs` 从错误的 `ConsumptionEventConsumptionRetentionExtensions.cs` 路径移动到 `Consumption/EventConsumptionRetentionExtensions.cs`，删除错误路径，避免构建依赖旧输出程序集中的扩展方法。
- 正确源码路径下重新编译并运行 `EventConsumptionRetentionServiceTest`，net6.0/net8.0 各 4/4 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-service-net6-v3.trx` 和 `event-consumption-retention-service-net8-v3.trx`；最终 EventBus 全量 net6.0/net8.0 各 339/339 通过，证据为 `event-consumption-retention-service-full-net6-v2.trx` 和 `event-consumption-retention-service-full-net8-v2.trx`。
- `git diff --check` 对本批改动返回 0。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证，NU1900 等既有警告仍存在。

## 2026-10-07 真实 IP 可信代理边界

- 执行状态：COMPLETED（FIX-008 的 HTTP 身份来源安全子项）。`RealIpMiddleware` 不再无条件采信 `X-Forwarded-For`；只有连接对端命中 `RealIpOptions.TrustedProxies` 时才解析转发链，可信代理名单为空或对端不匹配时保留连接地址。
- 转发链按从右向左跳过已配置的可信代理，并使用第一个非可信地址；无效地址只记录诊断并继续查找，不把请求头中的 `unknown` 当作客户端地址。IPv4、IPv4 映射 IPv6 和普通 IPv6 比较分别处理，避免不同 IPv6 地址被错误折叠。
- `UseRealIp(IApplicationBuilder, ...)` 和 `UseRealIp(IWebHostBuilder, ...)` 保留原有调用入口，并允许通过配置委托设置可信代理；旧的无名单调用迁移后会安全地忽略转发头。`GetRemoteIpAddress` 默认只读取连接对端地址的行为不变。
- `RealIpMiddlewareTest` 在 `Bing.AspNetCore.Mvc.Tests` net6.0 5/5 通过，证据为 `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/real-ip-trust-net6-final.trx`。覆盖未信任对端、可信代理链、全可信链、无效配置和 IPv6 比较边界。
- README、集群一致性指南和安全指南已说明 `TrustedProxies` 配置、转发头信任边界及“真实 IP 解析不等于 IP 准入控制”。该测试未覆盖真实反向代理、认证网关、代理地址轮换和多跳 ForwardedHeaders 生产配置；实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证，构建继续有既有 NU1900 警告。

## 2026-10-07 CAP Trace 日志正文脱敏

- 执行状态：COMPLETED（FIX-012 的消息诊断安全子项）。`MessageEventBus` 的 Trace 日志不再序列化完整事件正文和请求头；只记录事件名称、回调名称、头数量和开始/结束阶段，避免密码、令牌或业务载荷进入日志系统。
- 发布事务、CAP 头写入和消息正文传递行为保持不变；该修改只减少诊断输出，不把日志脱敏误认为业务数据加密或传输安全。
- `MessageEventBusLoggingTest.PublishAsync_WhenTraceEnabled_ShouldNotLogEventPayload` 在 `Bing.EventBus.Tests` net6.0、net8.0 各 1/1 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/cap-logging-redaction-net6.trx` 和 `cap-logging-redaction-net8.trx`。测试使用真实 `MessageEventBus` 与 CAP 发布调用，断言敏感正文和 `EventData` 不出现在 Trace 文本中。
- 其他 CAP、RabbitMQ、业务日志和第三方 Sink 仍需按实际配置审查；本修复不覆盖外部日志采集系统的访问控制、历史日志轮换和已有日志中的旧敏感内容。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证，构建继续有既有 NU1900 警告。
- 脱敏改动后的 `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 均为 343/343 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-redaction-full-net6.trx` 和 `eventbus-cap-redaction-full-net8.trx`。

## 2026-10-07 消费身份首尾空白边界

- 执行状态：COMPLETED（FIX-006/FIX-012 的消费身份一致性子项）。`EventConsumptionStore` 现在在写入和留存清理入口拒绝带首尾空白的消费者名称或事件 ID，避免应用层 `Ordinal` 身份与 MySQL 文本主键的尾部空格比较规则出现分歧；内部空白仍保留为合法标识，未引入隐式 `Trim`。
- 新增 `EventConsumptionStoreTest.TryRecordAsync_UnicodeIdentities_ShouldRemainDistinct`，验证预组合与分解形式的 Unicode 标识按原始精确值分别记录；`TryRecordAsync_InvalidKeyOrCancellation_ShouldNotWriteReceipt` 和 `DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotDeleteReceipts` 覆盖消费者及事件 ID 的首尾空白拒绝。
- 重新编译后的消费存储定向测试在 net6.0、net8.0 各 11/11 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-identity-net6-final.trx` 和 `event-consumption-identity-net8-final.trx`。`Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 344/344 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-consumer-identity-full-net6.trx` 和 `eventbus-consumer-identity-full-net8.trx`。
- 该修复不会自动清理既有数据库中的空白身份；生产迁移前仍需扫描并处理历史数据，真实 MySQL `utf8mb4_bin` 的 PAD SPACE 行为、迁移锁等待和跨节点旧版本混跑仍需专用环境验收。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；NU1900 警告仍因漏洞源不可达存在。

## 2026-10-07 可信代理日志回显边界

- 执行状态：COMPLETED（FIX-008 的请求头诊断安全子项）。`RealIpMiddleware` 不再在 Trace 中记录完整转发头；解析失败时也不回显客户端提供的非法值，只记录固定诊断文本。真实 IP 解析和可信代理判断逻辑保持不变。
- `RealIpMiddlewareTest.InvokeAsync_WhenForwardedHeaderContainsInvalidValue_ShouldNotLogHeaderValue` 使用自定义日志捕获器传入包含 `secret-token` 的转发头，验证任何日志文本都不包含原始值。该测试与既有可信代理、IPv6 和无效配置边界在 net6.0、net8.0 各 6/6 通过，证据为 `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/real-ip-logging-net6-final2.trx` 和 `real-ip-logging-net8-final2.trx`。
- 该修复只约束 Bing 中间件的诊断输出；ASP.NET Core ForwardedHeaders、网关、认证组件和第三方日志 Sink 仍需按实际部署审查。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；NU1900 警告仍因漏洞源不可达存在。

`Bing.AspNetCore.Mvc.Tests` 在该日志改动后完整回归 net6.0/net8.0 各 146 项通过、1 项既有上传测试跳过、0 失败，证据为 `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/aspnetcore-mvc-logging-full-net6.trx` 和 `aspnetcore-mvc-logging-full-net8.trx`。

## 2026-10-07 Admin MySQL/CAP 真实门控复验

- 执行状态：COMPLETED（FIX-006/FIX-007/FIX-013 的现有真实门控回归子项）。在隔离 Docker MySQL 8.4 和 RabbitMQ 4.3 容器中，使用 `eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1` 顺序运行两套 Admin net6.0 测试；诊断文件确认数据库和 RabbitMQ 均可达，连接使用专用 `bing_cluster_test` 测试库，脚本退出码为 0。
- EFCore `Bing.Admin.EFCore.Tests` 为 34/34 通过、0 失败、0 跳过，TRX 为 `artifacts/admin-mysqlcap-results/consumer-identity-final/net6.0-20261007T123203391Z/admin-efcore-mysqlcap.trx`；FreeSQL `Bing.Admin.FreeSQL.Tests` 为 33/33 通过、0 失败、0 跳过，TRX 为同目录下的 `admin-freesql-mysqlcap.trx`。该门控覆盖库存幂等、同/不同订单并发、取消、进程中断前后提交、InnoDB 死锁有界重试、提交确认丢失恢复、出箱后台恢复、消费取消及有序 CAP 到达乱序路径。
- 本次只证明当前门控列出的真实数据库/RabbitMQ 用例在单次隔离环境中通过；Redis 主从故障切换、跨独立部署版本的消息契约兼容、长时间网络分区、蓝绿动态切流和锁定 SDK 8.0.424 仍未验收。实际 SDK 为 8.0.425，构建保留 net6 EOL、Microsoft.Extensions 10/net6 兼容性和 NU1900 漏洞源不可达警告。
- 仓库脚本及两套 Admin 测试工程当前只声明 `net6.0`，`-Framework net8.0` 会在 PowerShell 参数校验阶段拒绝；因此本批没有把 net8.0 写成外部门控结果，后续若工程增加 net8 目标需另行扩展脚本和测试输出路径。

## 2026-10-07 审计归档批次一致性

- 执行状态：COMPLETED（审计归档并发一致性本地修复）。`DbAuditingStore.ArchiveBeforeAsync` 仍在同一事务内先复制、后删除，但删除条件现在绑定本次批次写入的 `ArchivedAtUtc` 和 `(TenantId, AuditId)` 身份；不再按第二次独立的截止时间加 `LIMIT` 重新选一批源记录。MySQL 使用带归档表连接的删除语句，SQLite 使用 `EXISTS` 子查询。
- 归档写入和源表删除的影响行数必须一致，否则抛出批次不一致异常并回滚事务；这避免源行在两步之间被更新、删除或失去资格时被静默误处理。归档批次继续使用受限批量和 UTC 截止时间，未改变关键审计不参与普通归档的规则。
- `DbAuditingStoreMySqlCommandTest.ArchiveBeforeAsync_WhenMySql_ShouldUseInsertThenDeleteSql` 现在精确断言按批次标记删除及同一 `@archived` 参数；`ArchiveBeforeAsync_WhenBatchCountsDiffer_ShouldRollback` 固定影响行数不一致时的回滚。SQLite 的正常归档、批量限制、重复身份、删除失败回滚及源记录资格变化测试也全部通过。
- 审计定向测试在 net6.0、net8.0 各 37/37 通过；`Bing.Auditing.Tests` 全量回归在 net6.0、net8.0 各 217/217 通过。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建继续存在既有 NU1900（NuGet 漏洞源不可达）警告。
- 本修复证明 SQLite 和 SQL 语句级批次绑定行为；真实 MySQL 高并发写入、归档索引计划、备份/恢复窗口及跨节点清理协调仍需专用数据库环境验证。
- 使用现有隔离 Docker MySQL 8.4 的 `bing_cluster_test` 测试库临时表执行同一批次 SQL，实际得到 `inserted=2`、`deleted=2`、源表剩余 1 条、归档表 2 条；临时表已清理。该验证只覆盖 SQL 语法和单事务批次行为，不代表高并发锁等待、故障恢复或生产容量验收。

## 2026-10-07 本地事件递归发布深度保护

- 执行状态：COMPLETED（P1 事件生命周期的递归保护子项）。`LocalEventBusOptions.MaxPublishDepth` 默认限制为 64；`LocalEventBus` 使用当前异步执行流记录发布链深度，处理器递归发布超过上限时抛出明确异常，且在成功或失败后恢复上一级上下文。无效上限在处理器执行前拒绝，不会产生处理器副作用。
- 新增 `LocalEventBusRecursionTest.PublishAsync_WhenDepthExceeded_ShouldFailAndRestoreContext` 与 `PublishAsync_WhenMaxDepthIsInvalid_ShouldFailBeforeHandlers`，并补充默认值契约测试。定向测试在 net6.0、net8.0 各 19/19 通过；全量回归证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-net6-recursion.trx` 和 `eventbus-net8-recursion.trx`。
- `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 347/347 通过，0 失败、0 跳过。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建保留既有 NU1900、nullable 和分析器警告。
- 该保护只限制同一本地发布链的递归深度，不替代业务事件幂等、CAP 重试上限、跨节点消息环检测或异步后台任务生命周期治理；跨总线循环仍需在分布式消费层设置消息标识和重试策略。

## 2026-10-07 审计留存与查询真实 MySQL 门控

- 执行状态：COMPLETED（审计普通留存和安全投影查询的真实数据库证据）。`DbAuditingStoreMySqlIntegrationTest` 新增普通审计 `DeleteBeforeAsync` 和 `IAuditLogQueryStore.QueryAsync` 门控：留存清理只删除普通记录并遵守批量上限，关键记录和未到期记录保留；查询按租户、应用、分页读取元数据投影，不返回明细正文。
- Docker MySQL 8.4、专用 `bing_cluster_test` 中，net6.0/net8.0 的归档、留存和查询用例各 3/3 通过；TRX 为 `framework/tests/Bing.Auditing.Tests/TestResults/db-auditing-mysql-retention-query-net6.trx` 和 `db-auditing-mysql-retention-query-net8.trx`。测试数据使用随机租户并在 `finally` 中清理。
- 无外部门控时审计全量回归在 net6.0/net8.0 各 217 通过、3 跳过、0 失败、总计 220；证据为 `audit-full-after-mysql-retention-net6.trx` 和 `audit-full-after-mysql-retention-net8.trx`。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证，构建保留 NU1900 警告。
- 该门控覆盖单实例 SQL、租户二进制身份、分页和关键审计保护；多实例清理协调、锁等待/断连恢复、长期容量、备份恢复和生产授权仍属于部署级 TODO。

## 2026-10-07 DbAuditingStore 真实 MySQL 集成门控

- 执行状态：COMPLETED（真实存储路径）。新增 `DbAuditingStoreMySqlIntegrationTest.ArchiveBeforeAsync_WhenMySql_ShouldMoveExactBatch`，通过真实 `MySqlConnection`、生产 `AuditLogSchema` 和 `DbAuditingStore` 归档实现，使用随机租户隔离数据并在 `finally` 中删除自身记录。`MySqlConnector` 仅加入 `Bing.Auditing.Tests` 测试项目，未进入生产依赖。
- 在 Docker MySQL 8.4、专用 `bing_cluster_test` 数据库中，net6.0 和 net8.0 各 1/1 通过；TRX 为 `framework/tests/Bing.Auditing.Tests/TestResults/db-auditing-mysql-net6.trx` 和 `db-auditing-mysql-net8.trx`。测试验证两条过期记录按批次归档、当前记录留在主表、归档身份与主表身份集合准确对应。
- 未设置门控时，审计全量回归在 net6.0/net8.0 各 217 通过、1 跳过、总计 218；设置门控时只执行新增 MySQL 用例。所有运行使用 SDK 8.0.425，仓库锁定的 8.0.424 仍未验证，构建保留 NU1900 漏洞源不可达警告。
- 该门控仍不覆盖 MySQL 多实例并发归档、锁等待超时、连接中断恢复、备份/恢复窗口和跨节点调度协调；这些继续作为部署级 TODO。

## 2026-10-07 LockExtensions 异步上下文所有权隔离

- 执行状态：COMPLETED（P0 本地锁便捷入口并发修复）。`LockExtensions` 原先把可变持有者字典放在 `AsyncLocal` 容器中；从同一父执行上下文派生的并发任务会共享该容器，后获得同一键的任务可能覆盖前一个任务的持有者，导致重复释放误释放后继锁。
- 现在在异步执行上下文切换时复制持有者容器；异步获取在等待前捕获当前容器并在同一容器内登记，保证调用方续体仍能释放原持有者。释放后清空空容器，避免继续通过租约索引引用锁实例。
- `LockExtensionsTest.LockTakAndRelease_WhenOwnerGenerationChanges_ShouldNotReleaseSuccessor` 增加父上下文预热，使两个 `Task.Run` 任务继承同一原始上下文，直接覆盖共享容器路径。`LockExtensionsTest` 与 `LocalLockTest` 在 net6.0、net8.0 各 21/21 通过；实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证。
- 该修复只改变便捷扩展记录持有者的并发隔离，不改变 `ILock` 的公开签名、Redis 租期语义或本地锁的兼容释放返回值。一个异步流同时对同一锁键持有多个未释放便捷租约仍不受支持，应使用 `ILockLease` 保存显式租约。

## 2026-10-07 CAP dispatcher 入队完成等待

- 执行状态：COMPLETED（CAP 有序事务适配的入队完成子项）。CAP 8.0.1 的 `CapTransactionBase.Flush()` 会调用 `IDispatcher` 的发布、执行和调度入口，但丢弃返回的 `ValueTask`；原适配器因此只能证明调用已经发起，不能证明异步入队任务已经结束。
- `CapOrderedEventTransactionFactory` 现在为每个自有 CAP 缓冲安装事务级 `TrackingDispatcher`，转发 dispatcher 生命周期方法并收集三类入队任务。物理事务提交后，适配器等待已发起任务完成；同步刷新错误与异步入队错误分别保留，已提交结果仍标记为 `Committed`，不将失败误报为数据库回滚。
- 新增 `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenNativeDispatchIsIncomplete_ShouldWaitForDispatchCompletion`，用未完成 `ValueTask` 证明提交任务不会提前完成；net6.0/net8.0 的工厂定向测试各 20/20 通过。随后 EventBus 完整回归在 net6.0/net8.0 各 348/348 通过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-dispatch-full-net6.trx` 和 `eventbus-cap-dispatch-full-net8.trx`。该测试使用职责替身，不代表 RabbitMQ broker 确认或网络投递完成。
- `IDispatcher.Start`、`Dispose` 继续转发给 CAP 宿主，跟踪器不会接管宿主 dispatcher 所有权。等待不使用调用方取消令牌，物理提交完成后必须等待已经发起的入队任务结束；超时、broker 确认、后台扫描和重试仍需按 CAP 运行时单独验收。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

## 2026-10-07 CAP dispatcher 同步完成与异步故障路径

- 执行状态：COMPLETED（CAP 入队跟踪的分配与故障诊断子项）。`TrackingDispatcher` 对同步完成的 `ValueTask` 直接返回，不再为每条同步入队创建临时 `Task` 并放入事务级待完成列表；未完成操作仍保留原任务直到物理提交后的等待阶段，避免用优化改变入队完成语义。
- 新增 `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenNativeDispatchFailsAsynchronously_ShouldKeepCommittedOutcome`，验证异步入队失败时保留原始异常、`TransactionOutcome.Committed`、消息观察结果及单次物理释放；原有未完成等待测试继续覆盖提交不会提前结束。CAP 工厂定向测试在 net6.0/net8.0 各 21/21 通过。
- `Bing.EventBus.Tests` 全量回归在 net6.0/net8.0 各 349/349 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-dispatch-v2-net6.trx` 和 `eventbus-cap-dispatch-v2-net8.trx`。构建仍有既有 NU1900、nullable 和分析器警告；实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证。
- 该修正只覆盖适配器对 CAP dispatcher 返回任务的等待和异常保留，不代表 RabbitMQ broker 确认、网络发送完成、后台重试或跨节点故障切换；这些仍需真实 CAP/RabbitMQ 环境验收。

## 2026-10-07 CAP dispatcher 多故障聚合

- 执行状态：COMPLETED（CAP 入队故障诊断子项）。`TrackingDispatcher.WaitForCompletionAsync` 在多个异步入队同时失败时收集全部 faulted task 的原始异常；单个异常保持原对象和堆栈，多个异常抛出 `AggregateException`，取消且没有 fault 时保持原取消语义。
- `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenMultipleNativeDispatchesFail_ShouldAggregateAllErrors` 验证两个消息入队失败都可定位，外层物理事务仍为 `Committed`，消息已进入 dispatcher，且底层事务只提交和释放一次。CAP 工厂定向测试在 net6.0/net8.0 各 22/22 通过。
- `Bing.EventBus.Tests` 全量回归在 net6.0/net8.0 各 350/350 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-dispatch-multi-full-net6.trx` 和 `eventbus-cap-dispatch-multi-full-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900、nullable 和分析器警告。
- 该修正只完整保留本次 dispatcher 入队调用返回的异常，不代表 RabbitMQ broker 确认、网络故障恢复、CAP 后台重试或跨节点消费排空；这些仍属于部署级 TODO。

## 2026-10-07 CAP 宿主排空等待预算

- 执行状态：COMPLETED（FIX-011/FIX-013 的宿主排空诊断子项）。`BingCapConsumerOptions.DrainTimeout` 新增为可选配置，默认保持 `Timeout.InfiniteTimeSpan`；`EnableBingCapConsumerCancellationRetry()` 的无参入口和既有二进制签名保留，新增委托重载只配置由 Bing 接管的 CAP 宿主排空。
- `BingCapConsumerHostDrain` 现在按当前 Options 调用 `IBingCapConsumerRuntime.WaitForDrainAsync`。有限预算返回 `false` 时，`CapHostedLifecycleService` 抛出带 `Bing.CapConsumerDrainIncomplete` 数据标记的 `TimeoutException`；调用方停止等待或超时不会强制终止回调、事务或作用域，后续仍可使用未取消令牌等待同一个关闭任务。
- `CapHostedLifecycleTest.StopAsync_WhenConfiguredDrainTimeoutExpires_ShouldReportIncomplete` 验证有限预算、排空调用参数、诊断标记和重复停止行为；`BingCapConsumerCancellationRegistrationTest.Enable_WhenDrainTimeoutIsInvalid_ShouldRejectHostResolution` 验证非法时间在宿主解析阶段拒绝。CAP 宿主定向测试在 net6.0、net8.0 各 20/20 通过，注册边界测试各 11/11 通过，完整 `Bing.EventBus.Tests` 在 net6.0、net8.0 各 351/351 通过。TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/cap-host-drain-timeout-net6.trx`、`cap-host-drain-timeout-net8.trx`、`cap-registration-timeout-net6.trx`、`cap-registration-timeout-net8.trx`、`eventbus-cap-drain-full-net6-v2.trx` 和 `eventbus-cap-drain-full-net8-v2.trx`。
- 运行使用 SDK 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有既有 NU1900、nullable 和分析器警告。该配置只覆盖 `BingSubscribeInvoker` 的宿主排空，不代表 CAP 原生处理器、Hangfire、定时任务、RabbitMQ 确认、跨节点切流或故障恢复已经完成。

## 2026-10-07 Compose 蓝绿后台角色编排入口

- 执行状态：PARTIAL（FIX-011 的 Compose 编排子项）。新增 `deploy/bluegreen/Invoke-ComposeBlueGreenCutover.ps1`，在目标 Web 就绪后可按显式开关启动目标 Consumer/Task，切换 Nginx，再以 `DrainTimeoutSeconds` 停止旧角色；切换或停止失败时尝试恢复旧 Web 路由、旧后台角色和候选角色。
- 旧 `Switch-ComposeTraffic.ps1` 无参行为保持不变；新入口支持 `-DryRun`，用于在没有 Docker 的环境查看动作顺序。稳定 CAP 组名、任务幂等键和应用自身排空仍由宿主负责，脚本不会强制终止活动工作。
- `Test-BlueGreen.ps1 -Platform Compose` 静态检查新增编排脚本存在及关键回滚/排空参数断言；PowerShell 解析、`Invoke-ComposeBlueGreenCutover.ps1 -Target green -SwitchConsumer -SwitchTask -DryRun` 和 Compose 静态检查均通过。
- 当前没有替换为真实镜像并运行完整 Compose 蓝绿环境，因此本批不证明 RabbitMQ 消费切换、Hangfire 存储任务恢复、长请求排空、动态回滚或多节点一致性；实际 SDK 与锁定 SDK 的差异仍为 8.0.425/8.0.424。

## 2026-10-07 Kubernetes 蓝绿后台角色编排入口

- 执行状态：PARTIAL（FIX-011 的 Kubernetes 编排子项）。新增 `deploy/bluegreen/Invoke-KubernetesBlueGreenCutover.ps1`，查询当前 `bing-web` Service 颜色，等待目标 Web Deployment 就绪，可选扩容目标 Consumer/Task，切换 Service selector，再等待旧后台 Deployment 副本归零；异常时尝试恢复旧 selector、旧角色副本和候选角色。
- 保留 `Switch-KubernetesTraffic.ps1` 的只切 Web 行为；新入口支持 `-DrainTimeoutSeconds` 和 `-DryRun`。缩容等待以 Deployment `status.replicas=0` 为完成条件，不强制终止超过预算的 Pod，超时会保留失败状态并进入回滚尝试。
- PowerShell AST 解析、`Invoke-KubernetesBlueGreenCutover.ps1 -Target green -SwitchConsumer -SwitchTask -DrainTimeoutSeconds 30 -DryRun` 以及 `Test-BlueGreen.ps1 -Platform Compose/Kubernetes` 均通过；静态检查现为 17 项。
- 当前没有可用 Kubernetes 集群、不可变生产镜像和真实 CAP/Hangfire 任务，因此未证明 Service 切换、Pod 排空、消息组竞争、任务恢复或跨节点回滚；实际 SDK 与锁定 SDK 的差异仍为 8.0.425/8.0.424。
- 使用两个测试用 SHA-256 digest 环境变量运行 `docker compose -f deploy/bluegreen/compose.yaml config --quiet`，退出码为 0；Docker CLI 仅报告本机 `C:\Users\jianx\.docker\config.json` 访问警告，未启动或拉取任何服务。该命令只证明 Compose YAML 可解析，不替代真实镜像启动和切流验收。

## 2026-10-07 有序消费者页面内槽位补位

- 执行状态：COMPLETED（FIX-006/FIX-011 的本地调度子项）。`ConsumerWorker` 不再按固定并发批次等待 `Task.WhenAll` 后才启动下一组；当前扫描页面使用有界在途槽位，任一流完成后立即补充后续流。页面全部处理完成后，分页键仍取源扫描顺序的最后一项，不使用异步完成顺序，避免慢流隔离修复引入分页跳过。
- 新增 `OrderedEventConsumerTest.HostedWorker_WhenOneStreamIsSlow_ShouldRefillAvailableSlot`：第一个业务流保持等待、并发上限为 2 时，第二个快速流完成后第三个流必须在慢流释放前进入处理；同时断言三条流最终完成。该测试还覆盖每个流独立作用域和正常排空。
- 有序消费者定向测试在 net6.0、net8.0 各 17/17 通过；`Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 353/353 通过，0 失败、0 跳过。本次命令使用实际 SDK 8.0.425，仓库锁定 SDK 8.0.424 仍未验证；构建保留 NU1900 漏洞源不可达及既有分析器警告。
- 该改动只改善单次扫描页面内的槽位利用率；跨页面公平调度、处理器永久不响应取消、CAP/RabbitMQ broker 确认和多节点动态排空仍属于部署级 TODO，不通过扩大并发或强制终止活动作用域解决。

## 2026-10-07 有序事件逻辑消费者全局容量闸门

- 执行状态：COMPLETED（P2 容量控制的数据库存储子项）。`DbOrderedEventStore` 新增可选 `maxPendingPerConsumer`，以 `bing_event_consumer_capacity` 按逻辑消费者锁定并重算所有业务流的未完成收件数量；默认值为空，旧注册行为不变。
- 入箱先执行事件 ID/业务流序号幂等判断，再检查全局容量，避免容量已满时把合法重复消息误报为超限。插入、状态推进和游标更新均在调用方事务中完成；失败记录继续计数，成功处理在状态与游标更新后刷新计数。容量行缺失时由启用容量的存储按当前收件箱重建，便于受控迁移后的首次运行。
- `OrderedEventSchema` 增加容量表建表语句；新增 `deploy/migrations/ordered-events/003-consumer-capacity.mysql.sql` 和 `003-consumer-capacity.sqlite.sql`，应用启动不会自动修改生产数据库。容量统计查询在容量行已经锁定后使用普通聚合查询，避免对 MySQL 聚合语句附加不必要的 `FOR UPDATE`。
- 直接测试：`DbOrderedEventStoreTest.EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAcrossStreamsAndReleaseAfterProcessing`、`EnqueueAsync_WhenCapacityIsEnabledAfterExistingMessages_ShouldReconcilePendingCount`、`Constructor_WhenConsumerCapacityIsInvalid_ShouldReject`、`Constructor_ShouldKeepLegacySignatureAndSupportCapacityOverload`；`OrderedEventSchemaTest` 同步覆盖 MySQL 五条、SQLite 七条完整建表语句。容量定向测试 net6.0/net8.0 各 21/21 通过；旧三参数构造签名仍可编译调用。
- 全量回归：`Bing.EventBus.Tests` net6.0、net8.0 各 357/357，0 失败、0 跳过；最终 TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-global-capacity-final-net6.trx` 和 `eventbus-global-capacity-final-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；仍有 NU1900 漏洞源不可达及既有 nullable/分析器警告。
- 边界：该闸门只覆盖同一 `DbOrderedEventStore` 数据库中的逻辑消费者，不提供 Redis、跨数据库或 CAP broker 级全局配额；跨页面公平调度、真实 MySQL 并发容量压测、RabbitMQ 确认/断连恢复和蓝绿动态切换仍未完成。启用参数前必须执行容量表迁移并纳入备份、容量告警和归档计划。

## 2026-10-07 有序事件容量 MySQL 实驱验证

- `Bing.FreeSQL.Tests.Events.OrderedEventCapacityMySqlTest.EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAndReleaseAfterProcessing` 使用已有隔离 Docker MySQL 8.4（`bing_cluster_order_mysql`，专用数据库 `bing_cluster_test`）执行，net6.0 和 net8.0 各 1/1 通过。
- 测试通过 `OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql)` 创建容量表，使用同一逻辑消费者的三条业务流验证前两条占满、第三条在容量满时被拒绝、第一条成功处理后第三条可入箱，并直接读取 `PendingCount=2`。该路径实际经过 MySQL 容量行锁、聚合统计和事务提交，不使用 SQLite 替代。
- 测试连接受 `IntegrationDatabaseSafetyValidator` 约束，只使用以测试命名的专用数据库，并在 `finally` 中删除三条流的收件箱、归档、游标、序号和容量行；没有启动、停止或删除容器。
- 该证据仍不是多实例高并发容量压测，也不证明跨数据库/Redis 全局配额、MySQL 主从故障、RabbitMQ 确认或蓝绿动态切换。实际 SDK 为 8.0.425，仓库锁定 SDK 8.0.424 未验证；构建保留 NU1900 漏洞源不可达警告。

## 2026-10-08 Admin 宿主消费者容量配置

- 执行状态：COMPLETED（宿主配置接线与注册边界）。`Bing.Admin.Modules.CapModule` 和 `Bing.Admin.FreeSQL.Modules.CapModule` 现在读取可选 `OrderedEvents:MaxPendingPerConsumer`，正整数通过命名参数传给 `DbOrderedEventStore`；未配置时传入 `null`，保持历史行为。非数字、零和负数在注册阶段拒绝，错误只包含配置键。
- `AdminCapRegistrationTest.AddServices_WhenOrderedEventCapacityIsConfigured_ShouldApplyCapacity` 通过服务描述符和测试反射断言容量值为 3；`AddServices_WhenOrderedEventCapacityIsInvalid_ShouldReject` 覆盖 0、负数和非数字。EFCore、FreeSQL 的 net6.0 定向注册测试各 26/26 通过，TRX 为 `modules/admin/tests/Bing.Admin.EFCore.Tests/TestResults/admin-cap-registration-efcore-net6.trx` 和 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-cap-registration-freesql-net6.trx`。
- `docs/guides/ordered-events.md` 已补充 Admin 配置键、环境变量写法和启用前迁移要求。当前两个 Admin 注册工程只声明 net6.0，未伪造 net8.0 结果；实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证，构建仍有既有 NU1900、net6 EOL 和 Microsoft.Extensions 10 兼容性警告。
- 该配置只把数据库内的积压上限接入宿主，不改变 CAP broker、Redis 或跨数据库的配额语义；默认没有配置时不会自动启用容量闸门。

## 2026-10-08 SQLite 容量迁移实执行

- 执行状态：COMPLETED（本地迁移脚本证据）。`OrderedEventSchemaTest.ConsumerCapacityMigration_WhenExistingInbox_ShouldBackfillPendingCount` 在内存 SQLite 中先创建不含容量表的历史结构，写入状态为待处理、失败和已完成的三条收件记录，再以 UTF-8 读取并执行仓库中的 `deploy/migrations/ordered-events/003-consumer-capacity.sqlite.sql`。
- 迁移执行后查询容量表得到 `PendingCount=2`，仅统计 `Status <> 2` 的两条记录；`OrderedEventSchemaTest` 在 net6.0、net8.0 各 3/3 通过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-schema-migration-net6.trx` 和 `ordered-schema-migration-net8.trx`。
- 该测试证明 SQLite 脚本的建表和回填语义，不代表生产大表回填性能、备份恢复、并发写入停机窗口或 MySQL 迁移回滚；这些仍需部署演练。

- 新增迁移测试后重新执行 `Bing.EventBus.Tests` 全量回归：net6.0、net8.0 各 358/358 通过、0 失败、0 跳过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-after-sqlite-migration-net6.trx` 和 `eventbus-after-sqlite-migration-net8.trx`。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。
- FreeSQL 全量回归在 net6.0/net8.0 各 76/76 通过、26 项 MySQL/外部环境用例按门控跳过；新增容量夹具在门控关闭时不会阻断本地回归。
- 迁移脚本实驱：在同一隔离 MySQL 8.4 测试库直接执行 `deploy/migrations/ordered-events/003-consumer-capacity.mysql.sql`，成功创建 `bing_event_consumer_capacity`（`varchar(64) ascii_bin` 主键、`bigint PendingCount`）并完成当前积压回填，当前容量行数为 0。首次命令因未把测试密码传入 `docker exec` 被拒绝，未触及数据库；随后使用容器环境变量重试成功。

## 2026-10-08 有序消费者跨页公平回绕

- 执行状态：COMPLETED（FIX-006/FIX-011 的跨页调度子项）。`ConsumerWorker` 新增连续页预算，默认 `maxPagesPerScan=16`；达到预算后将稳定游标回绕到首页。旧的五参数注册入口保持签名兼容并采用默认预算，新重载允许宿主按负载设置预算，范围为 1 到 1000。
- 新增 `OrderedEventConsumerTest.HostedWorker_WhenPagesRemainFull_ShouldWrapCursorForFairness`：首流保持缺口、后续页面持续有积压，并在首次扫描后插入一个排序更小的新流；配置预算为 2 页，断言新流处理时旧高键流完成数不超过 2，证明不会等待整批后续页面耗尽。`OrderedEventConsumerTest` 定向测试在 net6.0/net8.0 各 1/1 通过。
- `Bing.EventBus.Tests` 全量回归在 net6.0/net8.0 各 359/359 通过，0 失败、0 跳过；TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cross-page-fairness-full-net6.trx` 和 `eventbus-cross-page-fairness-full-net8.trx`。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证；构建仍有 NU1900、nullable、分析器及 net6 生命周期警告。
- 该预算只改变扫描游标回绕，不终止不响应取消的处理器，不改变数据库顺序、幂等、失败重试或容量闸门；真实 CAP/RabbitMQ broker 确认、跨节点动态排空和生产负载下预算调优仍需专用环境验收。

## 2026-10-08 集群指南审计状态收口

- 执行状态：COMPLETED（文档事实修正）。`docs/guides/cluster-consistency.md` 原先仍把普通审计数据库落库、留存和归档描述为“仍需完善”，与 `DbAuditingStore` 实现及隔离 MySQL 门控证据不一致；现已改为说明这些能力已提供，并明确生产侧仍需配置保留窗口、备份恢复、跨节点清理协调、容量告警和长期容量验证。
- 本轮仅修改文档，不改变生产代码、公开契约或测试夹具；既有审计测试证据可按未变化的生产源码范围复用。没有因文档修正重复运行代码测试。
- 真实多实例审计清理、备份恢复、长期容量、锁定 SDK 8.0.424 和动态蓝绿仍分别属于 `BLOCKED_EXTERNAL` 或部署验证项，不转化为本地实现缺陷。

## 2026-10-08 有序消费者失败流退避与人工恢复

- 执行状态：COMPLETED（有序消费失败风暴的本地生命周期子项）。`ConsumerWorker.ProcessStreamAsync` 现在按业务流稳定键记录失败探测截止时间；已持久化失败的流在退避期间只读取状态，不重复执行 `ProcessNextAsync`，处理器构造、依赖解析和业务处理的持续异常也会登记退避。显式 `RetryAsync` 清除持久化失败状态后，下一次探测继续处理；失败探测缓存最多保留 4096 条，满载且没有到期条目时使用 100ms 全局窗口，避免异常流数量导致无界内存或轮询热循环。
- 新增 `OrderedEventConsumerTest.HostedWorker_WhenStreamFailed_ShouldBackoffUntilExplicitRetry`，验证失败流在 250ms 内只执行一次处理，状态探测发生，人工重试后真实业务事务提交；新增 `HostedWorker_WhenHandlerConstructionKeepsFailing_ShouldBackoff`，验证处理器持续构造失败时 250ms 内最多四次尝试且不进入业务处理；新增 `ConsumerWorker_WhenManyFailuresArriveConcurrently_ShouldBoundProbeCache`，并发登记 5000 个失败键后断言缓存不超过 4096 条。三条定向测试在 net6.0、net8.0 各 3/3 通过；随后 `Bing.EventBus.Tests` 全量在 net6.0、net8.0 各 364/364 通过，0 失败、0 跳过，TRX 为 `ordered-failure-backoff-cap-net6.trx`、`ordered-failure-backoff-cap-net8.trx`、`eventbus-net6-failure-backoff-final.trx` 和 `eventbus-net8-failure-backoff-final.trx`。
- 退避缓存按消费者工作实例存在，不持有静态插件类型或根容器，也不跨节点同步；节点重启、其他节点和跨进程统一失败探测仍由数据库失败状态、业务幂等和恢复授权保证。实际测试使用 SDK 8.0.425，仓库锁定的 8.0.424 未验证；构建保留既有 NU1900、nullable 和分析器警告。

## 2026-10-08 消费收据多字节身份边界

- 执行状态：COMPLETED（消费幂等身份边界测试子项）。`EventConsumptionStore.ValidateKey` 继续按 Unicode 字符数限制消费者和事件标识，并拒绝首尾空白；新增 `EventConsumptionStoreTest.TryRecordAsync_NonBmpAndUtf8Boundary_ShouldKeepOrdinalIdentity`，覆盖两个不同非 BMP 消费者、200 个多字节消费者字符、128 个多字节事件字符，以及超出字符上限的拒绝。
- `EventConsumptionStoreTest` 定向测试在 net6.0、net8.0 各 12/12 通过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-identity-net6.trx` 和 `event-consumption-identity-net8.trx`。本轮只修改测试夹具，未改变生产契约或 SQL；实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。
- SQLite 直接测试证明 API 层的精确身份与字符边界；MySQL `utf8mb4_bin` 生产排序、旧表迁移和多实例重复竞争仍需专用数据库门控，不把 SQLite 证据外推为 MySQL 集群验收。

## 2026-10-08 CAP 事务状态同步边界

- 执行状态：COMPLETED（本地并发状态修复）。`CapOrderedEventTransactionFactory.TransactionScope` 现在通过 `_gate` 同步写入 `_outcome` 与 `_finished`，`Outcome` 读取与 `ActivateAmbient` 的结束检查使用同一同步边界；提交成功后再次激活会明确拒绝，避免并发读取到过期的事务状态。
- 新增 `CapOrderedEventTransactionFactoryTest.ActivateAmbient_WhenCommitCompleted_ShouldRejectReactivation`，并在隔离测试工程中运行该适配器全部 23 项定向测试，net6.0 为 23/23、0 失败、0 跳过；隔离生产源码编译 0 警告、0 错误，TRX 为 `.tmp-cap-test/TestResults/cap-state-sync-isolated.trx`（临时工程已清理，保留结果摘要）。标准工程仍存在 `obj` 输出被外部进程锁定的环境限制，不以该失败命令替代隔离测试证据。
- 本修复只覆盖进程内事务状态可见性，不代表 CAP broker 确认、RabbitMQ 断连恢复、提交确认丢失或跨节点排空已解决；这些仍属于 `BLOCKED_EXTERNAL` 部署验收项。实际环境使用 SDK 8.0.425，锁定 SDK 8.0.424 仍未验证。

## 2026-10-08 当前计划差异与待办清单

以下清单按当前源码、测试映射和执行证据整理，不把历史 `review.md` 中已被后续记录关闭的条目重复计入。

### BLOCKED_EXTERNAL

- [ ] 使用仓库锁定的 SDK 8.0.424 重新运行受影响项目；当前所有本地证据使用 SDK 8.0.425。
- [ ] 在专用多实例 Redis 环境验证主从切换、租期到期、旧持有者释放和锁安全边界。
- [ ] 在真实 MySQL/RabbitMQ 环境注入提交后断连、broker 确认丢失、取消确认和进程中断，验证 CAP 重试、Unknown 恢复与重复消费。
- [ ] 用真实镜像执行 Compose 与 Kubernetes 蓝绿切流、预热、角色排空、长请求、消费者恢复和回滚。
- [ ] 验证多实例审计清理、归档、备份恢复、锁等待和跨节点调度协调。

### DEFERRED

- [ ] 将失败流退避从进程内缓存提升为跨节点共享退避与立即唤醒协议，并定义节点重启后的恢复语义。
- [ ] 为事件序号、收据、审计和出箱数据建立长期归档、容量预算、分区与恢复演练；当前实现只提供有界批量清理和指标基础。
- [ ] 建立独立的生产指标导出、告警和权限控制，覆盖出箱积压、乱序缺口、消费延迟、限流拒绝、审计失败和排空状态。

### PLAN_PARTIAL

- [x] 完成专用 Docker MySQL/RabbitMQ 中 CAP 新入口的发布进程中断、提交确认丢失、授权查询/恢复、失败清理和有序乱序测试；生产 broker 网络分区与多实例确认仍属于外部门禁。
- [x] 完成 EFCore/FreeSQL 库存整单 Unknown/重试故障恢复、多 SKU 固定顺序和双进程并发门控；数据库条件更新仍只保证数据库层原子性，生产多节点容量与故障演练仍待验证。
- [x] 完成 Admin 两套 ORM 宿主组合的 HTTP+Redis 单进程故障链路；`AppModule` 注册后的真实 Redis `200 → 429`、无监听端口断连 `503`、替换限流器异常 `503` 均有直接测试，框架 TestServer 的 `FailOpen` 和关键审计回滚也已有直接证据。多实例故障切换仍属外部门禁。
- [ ] 将蓝绿后台角色与 HTTP 进程的稳定组名、幂等键、定时任务和数据契约兼容性纳入真实切换演练。

### NOT_YET_CONSIDERED

- [ ] 明确过滤事件后序号是否仍连续，避免订阅方因不可见事件产生永久缺口。
- [ ] 明确事件契约版本兼容、broker 侧消息头大小、签名/来源校验和敏感字段脱敏策略（应用侧 16 KiB 头集合边界已完成）。
- [ ] 明确幂等记录、失败收件箱、审计日志和 CAP 消息表的保留窗口必须覆盖最长允许重放时间。
- [ ] 明确 Redis 锁的 fencing token、主从故障切换和时钟漂移边界；库存正确性不能只依赖锁。
- [ ] 明确租户、消费者组和诊断接口的授权隔离，避免跨租户读取收据、库存、审计或消息正文。
- [ ] 明确毒性消息、死信、人工重放、错误分类和重复副作用的处置流程。
- [ ] 明确扩展/迁移/删除的数据库变更顺序、在线回滚限制、备份校验和大表停写窗口。
- [ ] 明确宿主停止期限、取消只停止等待还是停止工作、强制终止后的出箱恢复和资源所有权报告。
- [ ] 明确限流身份对可信代理、IPv6、NAT、租户配额、键数量上限和 Redis 故障降级的统一配置。

本清单不将本地事件跨进程广播、CAP 至少一次投递、强杀进程托管清理和数据库内部锁标记为缺陷；这些是已公开的能力边界。

### 有序消费者失败探测缓存满载保护（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `ConsumerWorker.ProcessStreamAsync`：失败探测缓存达到上限时，先按短窗口查询持久化状态，未知流可恢复处理，仍失败的流继续退避 | `OrderedEventConsumerTest.HostedWorker_WhenFailureProbeCacheIsFull_ShouldProbeStateBeforeRetryingBusiness` | 隔离测试工程 net6.0 1/1 通过；测试填充 4096 条失败探测记录并验证额外流的业务处理只调用一次 |
| `ConsumerWorker.IsFailedStreamOverflowProbeDue` / `ScheduleNextOverflowProbeIfNeeded`：缓存满载时保持有界探测节奏，不因缓存溢出而每轮重复执行业务 | 同上及同文件回归 | 同文件有序消费者测试 net6.0 24/24 通过；生产 `Bing.Events` 源码编译 0 警告、0 错误 |

该修复只约束单进程缓存满载时的状态探测和业务尝试次数，不提供跨节点共享退避、进程重启恢复或 broker 重投控制；这些仍属于 `DEFERRED` 或 `BLOCKED_EXTERNAL` 范围。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### 有序消费者缺口告警节流（2026-10-08）

- 执行状态：COMPLETED（本地诊断有界性子项）。`ConsumerWorker` 对同一业务流的缺口告警使用 30 秒节流窗口；告警缓存上限为 4096 条，缓存满载且没有过期条目时使用 1 秒全局窗口，避免持续缺号造成日志和高基数键无限增长。
- 缺口恢复为 `Processed`、失败或空结果时清除对应告警状态；不改变缺口指标、游标、事务和人工恢复语义。
- 新增 `OrderedEventConsumerTest.ConsumerWorker_WhenGapRepeats_ShouldThrottleAndBoundWarnings`；隔离 `OrderedEventConsumer` 测试 net6.0 为 25/25 通过，生产 `Bing.Events` 源码编译 0 警告、0 错误。
- 该节流只限制进程内诊断日志，不替代跨节点告警聚合、缺口恢复授权和消息序号治理；实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### CAP 消息头总大小边界（2026-10-08）

- 执行状态：COMPLETED（消息头输入输出边界子项）。`ClusterEventHeaders.Read` 在识别 Bing 事件标识后，按 UTF-8 字节统计全部消息头名称和值，超过 16 KiB 时在读取业务字段前拒绝解析；`Write` 在写入已知字段后对最终集合执行同一上限校验。未知头也计入上限，避免扩展头造成无界解析开销。
- 单字段字符上限、有序序号和旧消息缺少 Bing 事件标识的兼容行为保持不变；写入端公开 API 签名未改变，但最终消息头集合超过上限时现在会拒绝发布。
- `ClusterEventHeadersTest.Read_WhenHeaderCollectionExceedsByteLimit_ShouldReject` 与现有 15 项头部契约测试组成隔离测试，net8.0 共 16/16 通过；独立运行程序也验证有效头解析和超大集合拒绝。
- `ClusterEventHeadersTest.Write_WhenHeaderCollectionExceedsByteLimit_ShouldReject` 覆盖调用方预置未知头的出站路径；最小 net6.0 运行器验证写入拒绝、读取拒绝和有效消息往返均通过。
- 该边界不提供消息签名、来源认证、契约版本求解或敏感字段脱敏；这些仍保留在 `NOT_YET_CONSIDERED`，实际 CAP broker 的头大小限制仍需部署环境核对。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### 有序事件归档保留调度（2026-10-08）

- 执行状态：COMPLETED（显式归档调度子项）。`OrderedEventRetentionScheduleOptions` 新增可选 `ArchiveRetentionPeriod` 与 `ArchiveConsumers`；两项必须同时配置，默认不自动删除 `bing_event_inbox_archive`。
- `OrderedEventRetentionHostedService` 在同一协调租约和有界批次内先执行已处理收件清理，再按归档消费者调用 `OrderedEventCoordinator.DeleteArchivedBeforeAsync`；待处理和失败记录不会进入归档删除路径。没有归档实现的自定义存储只记录跳过，不改变默认清理结果。
- 新增 `OrderedEventRetentionHostedServiceTest.Scheduler_WhenArchiveRetentionIsConfigured_ShouldDeleteArchivedRecords`、`Scheduler_WhenArchiveOnly_ShouldIgnoreProcessedRetentionPeriod`、`Scheduler_WhenArchiveConfigurationIsIncomplete_ShouldRejectConfiguration` 和 `Scheduler_WhenArchiveConsumersAreMissing_ShouldRejectConfiguration`；net6.0 隔离精准测试 4/4 通过，覆盖归档专用配置、两种不完整配置和实际 SQLite 收件归档调度删除。隔离源码构建 0 错误，使用 SDK 8.0.425。
- 该能力不代表归档窗口、幂等记录、审计留存、备份恢复、跨节点容量和生产大表清理已经验收；这些继续属于部署和长期治理范围。

### Admin MySQL/RabbitMQ 单实例集群门控（2026-10-08）

- 执行状态：COMPLETED（专用 Docker 单实例 CAP/库存门控）。使用现有 MySQL 8.4 容器的隔离数据库 `bing_cluster_test`，临时启动 RabbitMQ 3.13 management 容器并映射本机 5673；测试结束后已删除临时 RabbitMQ 容器，未修改现有 MySQL/Redis 容器。
- `Invoke-AdminMySqlCapIntegrationTests.ps1 -Framework net6.0` 预检通过，随后按脚本声明的必需方法顺序运行：Admin EFCore `34/34`、Admin FreeSQL `33/33`，均 0 失败、0 跳过。覆盖库存重复与跨作用域并发、双 SKU 不足回滚、真实死锁重试、进程提交前后中断、提交确认丢失、出箱后台恢复、消费取消和有序事件 3→1→2 到达。
- 恢复授权、恢复审计和审计查询定向测试：EFCore `19/19`、FreeSQL `19/19`，TRX 位于本次运行目录的 `recovery-unit` 子目录。完整证据位于 `artifacts/admin-mysqlcap-results/rabbit-20261008/net6.0-20261008T072313184Z/`。
- 该批次证明真实单实例 MySQL/RabbitMQ 连接、CAP 出箱/消费和两套 ORM 业务路径；不证明生产网络分区、broker 确认丢失的真实传输形状、多实例竞争、Redis 故障切换、RabbitMQ 集群故障或 SDK 8.0.424。实际 SDK 为 8.0.425，构建保留既有 net6 支持、NU1900 和分析器警告。

### Redis 限流与分布式锁单实例门控（2026-10-08）

- 执行状态：COMPLETED（本地 Redis 集成子项）。使用现有 Redis 5.0.12 容器的 `127.0.0.1:6379`，受 `RUN_INTEGRATION_TESTS=true` 门控运行 `RedisTokenBucketRateLimiterTest` 与 `RedisDistributedLockTest`；net6.0 和 net8.0 均为 `22/22`，0 失败、0 跳过。
- 限流覆盖独立服务提供程序和两个独立 dotnet 进程共享额度、同键并发不超配、闲置键回收、策略配置冲突、同步/异步状态一致、调用方提前取消及真实 Redis 脚本路径；分布式锁覆盖跨线程/租约、错误所有者释放、过期后释放、同步/异步回调异常和释放失败诊断。
- TRX 位于 `framework/tests/Bing.Caching.CSRedis.Tests.Integration/TestResults/redis-local-net6/redis-lock-rate-limit.trx` 和 `redis-local-net8/redis-lock-rate-limit.trx`。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证；构建仍报告 NuGet 漏洞源不可达及 net6 生命周期警告。
- 该门控只证明单 Redis 实例上的原子脚本、租期和共享配额；Redis 主从切换、脑裂、fencing token、时钟漂移及生产多节点故障仍属于 `BLOCKED_EXTERNAL`/`NOT_YET_CONSIDERED`，库存正确性不能只依赖 Redis 锁。

### Admin 宿主 HTTP/Redis 限流链路（2026-10-08）

- 执行状态：COMPLETED（宿主组合本地子项）。共享测试夹具通过两套 `AppModule.AddServices` 注册路径和真实 `ApplicationBuilder` 管道，连接现有 Redis 5.0.12 后验证同一客户端 `200 → 429` 及 `Retry-After`；再切换到无监听端口验证真实 Redis 连接失败返回 `503`；EFCore、FreeSQL 的 net6.0 定向测试均为 `3/3`，0 失败、0 跳过。
- 同一夹具额外注册失败限流器验证 Admin 默认失败闭合，HTTP 返回 `503`；它与框架 TestServer 的 FailOpen/FailClosed 测试互补，证明宿主不会绕过限流中间件。
- TRX 位于 `modules/admin/tests/Bing.Admin.EFCore.Tests/TestResults/rate-http-net6-v2/admin-rate-http-v2.trx` 和 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/rate-http-net6-v2/admin-rate-http-v2.trx`。实际 SDK 为 8.0.425，仍有 NuGet 漏洞源不可达、net6 生命周期和 Microsoft.Extensions 10/net6 兼容性警告。
- 该子项不等同于 Redis 主从故障切换、多实例额度一致性或进程级恢复；这些继续由外部故障演练验收。
