# 多源 Uow、CAP 事件与诊断 Sample 实施方案

Task ID：20261021-multisource-cap-diagnostics
状态：APPROVED；用户已明确要求实施本文方案。日期：2026-10-21。
实施：GPT-6-Luna/max；独立验收：GPT-6.1-Sol/high。

## 1. 目标与设计结论

新增按数据源隔离的 CAP 事务适配，不能仅注册 CAP 就宣称业务与消息可靠提交。沿用现有事件体系：

| 类型 | 使用入口 | 验证重点 |
| --- | --- | --- |
| 内存事件 | ISimpleEventBus | 当前进程内处理、异常传播、发布时机 |
| 消息事件 | IMessageEventBus、MessageEvent<T> | 事件标识、消息头、Outbox 持久化 |
| 分布式事件 | CAP + RabbitMQ 实际消费 | 投递、重试、幂等、跨进程处理 |

不补齐尚无实现的旧 IDistributedEventBus。消息事件与分布式事件分别展示发布契约和跨进程执行，不重复发布。两个 MySQL 数据源各自拥有业务表及 CAP Outbox，覆盖 Dapper 和 EF；每个源的业务与本库 Outbox 同事务，多源 Uow 仍顺序提交且可能部分提交。参考：https://cap.dotnetcore.xyz/user-guide/en/cap/transactions/ 。

## 2. 多数据源事务适配

### 资源与发布入口

- 保留现有单源 IMessageEventBus 行为，新增 IUnitOfWorkMessagePublisher.PublishAsync(dataSourceKey, message, cancellationToken)，复用现有消息类型和元数据。
- 新入口要求活动事务型业务 Uow；拒绝未知数据源、非事务 root、终态 root，以及 Send=true 独立发布请求。
- 新增按数据源取得当前 Uow 连接资源的适配契约；Dapper 与 EF 共用现有资源匹配逻辑，避免另建连接/事务或重复登记；借用者不拥有物理提交和释放权。
- CAP 存储配置必须与实际资源数据库一致，不能把消息默认写入其他库。

### CAP 隔离与提交

- 每源独立 CAP 运行时和服务容器，隔离存储配置、发布缓冲、重试及生命周期；不能在同容器重复 AddCap 冒充多源支持。
- 宿主统一启动/关闭运行时；每次业务操作在对应运行时创建 Scoped 发布器，不捕获业务根容器或跨操作缓存发布器。
- 发布借用 Uow 实际物理事务写 CAP Outbox；返回只表示写入当前未提交事务，不表示投递或消费。
- Uow 为唯一物理事务所有者。CAP 借用事务、保存消息、管理缓冲；发布调用退出恢复事务上下文，防止跨 await、嵌套和数据源污染。
- 资源提交成功后才刷新 CAP 缓冲；回滚丢弃缓冲。提交后刷新失败保留提交结果，通过持久 Outbox 恢复，不重复提交业务事务。
- 恢复、状态更新、重试始终使用所属数据源。逐库记录已提交、已回滚、提交结果未知、提交后通知失败，不笼统显示全部回滚。
- 先用 CAP 8.0.1 真实 MySQL Provider 完成最小双源验证再接入 Sample；不升级 CAP、不复制其存储协议、不解除现有 CAP 自管事务保护性拒绝。

## 3. Sample 与独立诊断页面

保留现有双 SQLite 示例，新增显式 CAP/MySQL 模式及独立消费者宿主。提供隔离 MySQL/RabbitMQ Compose 与启动脚本；默认不连接外部服务。HTTP、BackgroundService、Hangfire 继续调用同一业务服务。

必须提供的真实场景：

- 内存事件即时处理，以及 root 完成回调触发的提交后处理。
- orders/reporting 各自写业务和消息，正常提交或整体提交前回滚。
- 首库提交、第二库失败，展示准确部分提交。
- RabbitMQ 暂不可用，业务与 Outbox 提交后恢复发送。
- 消费失败重试、重复消息幂等、发布端或消费端重启恢复。

消费者以来源数据源 + EventId + 消费者名称建立持久幂等约束，幂等记录和消费业务修改同事务。不同源明确主题与稳定消费组，不串库。

新增 /diagnostics/events，轻量 HTML/JavaScript，不引入前端框架：

- operationId、EventId、TraceId、数据源、状态筛选。
- 展示请求、Uow、逐库提交、Outbox、投递、消费尝试及消费业务结果。
- 区分业务 EventId、CAP MessageId、root ID、Job ID、消费者身份。
- 来源为真实 CAP 存储、消费业务记录、结构化日志；无证据显示未观测/未知。
- 日志保留失败/回滚过程，但不是业务提交证明；显示数据来源和刷新时间。
- 只读排查及 CAP Dashboard 跳转，不增加手工重发/补偿按钮。

## 4. 测试与验收

### 契约与生命周期

- 注册幂等、冲突拒绝、默认单源入口兼容。
- Dapper-first、EF-first、消息-first 共用正确资源；EF SaveChanges 不提前提交。
- 嵌套 Uow、独立 root、取消、异常、释放后 CAP 上下文无泄漏。
- 每个物理事务仅提交/回滚一次，CAP 不释放借用连接。

### 真实 MySQL 与 RabbitMQ

- 两库业务行与本库 Outbox 同事务，提交前独立连接不可见、提交后可见、回滚后不存在。
- 第二库失败时核对首库业务/消息保留，准确核对未提交库结果。
- 停代理后提交、恢复后消费；重启发布进程仍恢复持久消息。
- 消费失败和重复投递不产生重复业务副作用。
- 独立进程消费者；不能直接调用处理器代替传输。

### 页面与回归

- 页面定位成功、回滚、待投递、消费失败、重试、部分提交，核对业务表、CAP 表、消费记录一致。
- 保留现有 Sample net6/net8 Release；运行受影响 Uow、事件、Dapper、EF、Provider 直接测试。
- 新增默认实现具备职责级直接测试。SQL 输出改动断言完整字符串；资源/工厂路径变动包含 SQLite 本地集成及资源共享、上下文、隔离回归。按 AGENTS 维护生产符号到测试映射。
- 外部集成显式门控；环境未启动标未验证，skip 不计 PASS。只有实际热路径变化才补有界性能验证，不改性能预算。
- 页面必须实际浏览器检查筛选、错误/空状态、数据显示与控制台；不能仅以返回 HTML 200 视为 UI 验收。

## 5. 顺序、任务与交付

| Task | 目标/依赖 | 实施范围 | 验收 |
| --- | --- | --- | --- |
| T01 | 冻结起点与CAP协议 | 完整规则、当前源码、CAP8.0.1与既有有序事件适配；最小双源失败回归、资源所有权说明 | 真实失败证据与可实施适配路径，无复制CAP协议 |
| T02 | Uow资源访问/增量契约，依赖T01 | 共享Dapper/EF资源匹配、入口校验、直接测试 | 三种首次执行顺序及既有单源兼容 |
| T03 | 多源CAP事务运行时，依赖T02 | 独立容器生命周期、发布事务借用、缓冲、恢复、失败状态 | 真实双库事务与上下文/所有权验证 |
| T04 | Sample与消费者，依赖T03 | 显式CAP模式、三入口、独立消费者、幂等、Compose与脚本 | 真实跨进程收发及故障恢复；默认SQLite不受影响 |
| T05 | 诊断页面，依赖T04 | 查询API、结构化关联、只读页面、Dashboard跳转 | 浏览器与存储事实一致，不把日志当事务证据 |
| T06 | 验证/文档交付，依赖T02-T05 | 受影响Release矩阵、E2E、README、execution、traceability、artifacts | 计划覆盖与实际命令/退出码/TRX可审计，外部阻塞单列 |
| T07 | Sol/high独立验收，依赖T06 | 完整计划/真实源码/diff/原始证据/必要独立测试 | reviewer独立报告，只有OPEN_ACTIONABLE继续修复 |

Luna/max 实施，Sol/high 独立验收。禁止以测试替身通过声称真实MySQL/RabbitMQ可靠集成已验证。若环境受阻，先完成不依赖环境的代码/测试/脚本，精确记录阻塞和未验证场景，不能伪造PASS或无进展重试。

## 6. 执行边界与环境

- 不实现分布式原子提交、自动补偿、全局顺序、旧接口统一。增量接口已获用户批准，但不修改已批准API baseline或包版本；新增依赖使用现有CAP8.0.1等相容版本，不升级既有依赖。
- 根HEAD起点 5065c38ced05a95088e17d89d96c9a1610e37159；工作区含F008、前置Uow、已验收Sample等未提交修改。实施前保存status和本次会改文件起点，保护所有先存内容，不覆盖历史报告。前置任务：20261021-f008-sql-factory-scope、20261021-sql-multidb-unit-of-work、20261021-sql-uow-sample。
- Windows PowerShell，文本显式UTF-8；读取AGENTS、CODEX_HOME/GOAL_RULES及execute-plan。测试前CIA，原始失败/成功证据分目录保存，不覆盖；记录实际配置、命令、退出码、计数和复用范围。
- 本轮只读预检Docker Server 29.6.1可达。允许启动和停止本任务专属隔离Compose服务、容器和测试进程，使用专属项目名/库名/端口，不干扰现有容器或数据库。停止RabbitMQ故障注入只能针对本任务服务；重启恢复测试完成前保留其持久数据。不得访问生产库。
- 不暂存/提交/推送/PR/reset/clean，不改预算。外部消息通知禁用，task-finish使用 --no-notify。
- 自动Review/Fix最多3轮，遵守No Progress Guard与有界实验；仅OPEN_ACTIONABLE触发修复。
- 交付plan、execution、traceability、API增量清单、资源所有权说明、README/脚本、原始验证证据；Reviewer独立写review，主代理整合final-report。
