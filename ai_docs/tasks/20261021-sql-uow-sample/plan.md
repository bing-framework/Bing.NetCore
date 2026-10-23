# 多数据库工作单元 Sample 与端到端验收计划

Task ID：20261021-sql-uow-sample
状态：APPROVED；用户已明确授权实施此计划。
日期：2026-10-21。实施 GPT-6-Luna/max，独立验收 GPT-6.1-Sol/high。

## 1. 验收结论与目标

前置任务 `20261021-sql-multidb-unit-of-work` 独立审查 PASS。规划阶段重新执行 net8 Release 的同库共享事务回滚、双库提交、双库回滚测试，3/3 通过，无跳过。

现有后台消费者测试只模拟 DI scope，没有启动 BackgroundService 或 Hangfire Server。本次新增可运行 Sample，验证 HTTP 请求、后台服务和 Hangfire 作业中的真实使用方式。同时修正 `framework/src/Bing.Uow/README.md` 中遗漏的查询参数和错误的迁移文档链接。

## 2. 示例结构与使用方式

新增 `samples/Bing.Samples.SqlUnitOfWork` 与 `samples/Bing.Samples.SqlUnitOfWork.Tests`，加入 `Bing.All.sln` 及 Samples 索引。

- Sample 使用 Web SDK、net6.0；测试覆盖 net6.0/net8.0，沿用仓库测试配置。
- 引用当前框架源码，沿用现有 EF、SQLite、Hangfire 与 MemoryStorage 版本，不升级依赖。
- 一个项目内按 Controllers、Application、Repositories、Jobs、Infrastructure 分目录，展示完整调用链。
- 每次启动创建专属临时目录，包含 orders.db、reporting.db 两个真实数据库；初始化完成后才接受业务和作业。退出释放资源后清理本次创建的文件。
- 提供 Dapper、EfBridge 两种启动配置，默认 Dapper。EfBridge 显式将 orders 映射到同一个 Scoped EF context；两种配置分别验收。
- 提供 README 和 .http 请求文件，包含启动命令、场景步骤、预期结果及生命周期说明。
- 框架公共 API 不新增、不变更。

## 3. 业务与宿主实现

### 跨层与多数据库

- 应用服务和 orders 仓储分别通过构造函数注入 ISqlQuery，默认数据源明确设置为 orders；写入使用 ISqlExecutor。
- reporting 仓储通过工厂创建具名 Query/Executor，展示多库选择方式。
- 应用服务负责 Begin/Complete，仓储参与当前工作单元；分别持有的 Query 保持独立查询状态，同源资源共享事务。
- DI 注入对象由 DI scope 释放；工厂创建对象使用 using。循环操作使用工厂，不在长生命周期业务 scope 中反复从 DI 解析可释放的 Transient 对象。

### 三个真实入口

- HTTP：POST /api/scenarios/{scenario} 同步等待场景执行。
- BackgroundService：POST /api/worker/scenarios/{scenario} 投递内存队列；后台服务每条任务创建独立异步 DI scope，再解析业务服务。
- Hangfire：POST /api/jobs/scenarios/{scenario} 实际入队，由 MemoryStorage 和真实 Hangfire Server 执行；Job 构造函数注入业务服务，使用 ASP.NET Core 作业作用域。
- 两种后台入口返回 operationId，使用 GET /api/operations/{operationId} 查询完成或失败结果。队列仅传递场景和标识，不捕获请求 scope 或 Query。
- 验收场景关闭 Hangfire 自动重试，避免失败被重复执行掩盖；不增加周期性自动写入任务。

### 固定场景

| 场景 | 验证行为 |
| --- | --- |
| 同库跨层提交/回滚 | 应用层和仓储层能读取同一事务内写入，最终结果符合提交或回滚 |
| 双库提交/回滚 | 两库分别使用本地事务，最终行集正确 |
| 捕获执行异常 | 业务捕获 SQL 异常后，root 仍为 rollback-only，不能提交此前写入 |
| 频繁创建与释放 | 同一 root 内创建、使用、释放 100 个工厂 Query，后续查询仍可使用共享资源 |
| EF 与 Dapper 混用 | EF SaveChanges 和 Dapper 写入共同提交或回滚；Complete 不代替 SaveChanges |
| 作用域隔离 | 不同请求、后台任务和 Job 使用不同业务 scope/root |

## 4. 自动测试与通过标准

配套测试实际启动本地 HTTP 宿主、BackgroundService 和 Hangfire Server。Hangfire 宿主测试串行运行，避免进程级配置相互干扰。

- 两种启动配置均开启 ValidateScopes 和 ValidateOnBuild。
- HTTP 覆盖全部场景；Worker 和 Hangfire 分别覆盖提交、回滚、异常，以及连续两次操作的 scope 隔离。
- 每次操作完成后，用独立 SQLite 连接验证两个数据库的准确行集，不以日志、状态码或作业成功状态代替数据库断言。
- 跨层测试确认 Query 实例不同、查询状态独立，但能读取同事务未提交的数据。
- 并发隔离使用只读场景；写入验收顺序执行，避免 SQLite 写锁影响生命周期判断。
- 队列测试采用有截止时间的状态等待，超时报告操作状态；不使用固定长时间 sleep。
- 宿主停止后验证任务退出、监听端口释放、数据库文件可删除。
- 新 Sample 构建及配套 net6/net8 Release 测试全部通过；现有相关回归按影响范围执行，未改热路径则复用既有性能证据。

## 5. 实施任务与证据

| Task | 目标与范围 | 实施与依赖 | 验收 |
| --- | --- | --- | --- |
| T01 | 冻结当前工作区，创建任务记录 | 读取 AGENTS、GOAL_RULES、execute-plan；记录 HEAD、status、diff 范围；注册任务状态 | 起点可追溯，所有 F008/前置 Uow 修改受保护 |
| T02 | 新建 Sample、数据库与双配置 | T01 后实现宿主、临时目录、初始化、DI 与 EF 映射；引用真实框架项目和既有依赖版本 | 两种配置构建并通过严格 DI 校验，独立两文件 |
| T03 | 应用层、仓储与固定场景 | T02 后实现跨层注入、具名工厂、实际事务与诊断结果 | 全部场景有真实调用与独立连接行集断言，不伪造 PASS |
| T04 | HTTP、Worker 与 Hangfire | T03 后实现真实队列/Server、业务 scope、状态查询和有序停止 | 三入口实际执行，失败可观察，队列不携带 scoped 对象 |
| T05 | 端到端测试 | 覆盖上述验收矩阵，真实随机本地 HTTP 端口，两个 TFM Release | 测试不跳过关键场景，零失败，关闭/清理可验证 |
| T06 | 文档、解决方案与交付 | README/.http、samples 索引、Uow README 修正、符号到测试映射 | 命令/请求可复现，链接正确，UTF-8 与 diff check 通过 |
| T07 | Sol/high 独立验收 | 阅读全部计划、execution、源码、差异与原始证据，按影响范围验证 | 独立 review.md；仅 OPEN_ACTIONABLE 触发必要修复 |

测试/构建命令从仓库现有配置核对，主要入口：

```powershell
dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net8.0
git diff --check
```

测试前记录简明 Change Impact Analysis。保存实际命令、配置、退出码、日志与 TRX 到本任务 artifacts，新失败和后续成功分别保存，不覆盖历史。运行必要 SQLite 本地集成；外部数据库门控保持关闭。无生产热路径改动时不重跑 Benchmark，不宣称性能提高。

## 6. 交付边界和风险

- 双 SQLite 验证多数据库行为，不宣称异构数据库互操作通过，不提供跨库原子事务。Hangfire 内存存储不承担持久化调度能力。
- SQLite 并发写锁、异步 scope 传播、DI dispose 与临时文件清理、Hangfire 进程级状态是重点回归风险。
- 如发现框架缺陷，先形成可复现失败用例，再处理与本次验收直接相关的最小修复；同步记录 CIA 与新增回归。禁止公共 API 变更。
- 保留全部现有未提交修改、先前计划和审查证据；不 git add/commit/push/PR/reset/clean，不调整版本、API baseline 或性能预算。
- 所有文本显式 UTF-8。无外部消息通知，本任务 task-finish 使用 --no-notify。
- 实施写 execution.md、traceability.md 和 artifacts；独立 Reviewer 写 review.md；主代理整合 final-report.md。执行者不代替 Reviewer 宣告验收。
- 自动 Review/Fix 最多 3 轮，遵守 GOAL_RULES 的 No Progress Guard。只有 OPEN_ACTIONABLE > 0 才继续修复；外部环境/预算状态不触发循环。

## 7. 已核对参考

- samples/Bing.Samples.WebApi.Tests：真实 HTTP 启停 smoke 与测试项目接入。
- samples/Bing.Samples.Hangfire：既有 Hangfire.AspNetCore 1.7.16、Hangfire.MemoryStorage 1.7.0；新 Sample 使用作业 scope，不照搬根容器直接解析。
- framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteUnitOfWorkContractTest.cs：真实双库事务与失败语义。
- framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqliteUnitOfWorkTestSupport.cs：EF 映射 context、必要服务注册与 SQLite 文件配置。
- ai_docs/tasks/20261021-sql-multidb-unit-of-work：前置实现、迁移、审查及未改范围证据。
