<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-COMMON-ATTRIBUTES-DECOUPLE-20260930
AI_REVIEWED_AT: 2026-10-03T22:44:58.0121310+08:00

# Docker 验收后的计划对照审查

本轮只审查，不修改生产代码或测试。对照 plan.md、用户批准的完整计划、当前源码、Git 改动、execution.md、test-mapping.md 和两份 Docker TRX。未重跑测试；以前的完整审查保存在 review-before-docker-result-audit-20261003.md。

## 当前结论

公共特性、类型转发、AOP 执行器、独立工作单元适配包及主要迁移均已实现。仍有 **4 组本地可执行 TODO，以及 2 组验证门槛**；这不是缺少 4 个公共 API，不用测试总数计算完成百分比。

| 计划项 | 状态 | 依据与剩余差异 |
| --- | --- | --- |
| T1 特性契约抽离 | PASS | 沿用已核查的中立类型、类型转发及旧消费者证据；包源依赖图验收另列 |
| T2 AspectCore 适配 | PASS | 沿用此前参数验证、忽略规则和属性注入测试证据；本轮未发现需要重新打开的适配缺陷 |
| T3 工作单元 | PARTIAL | 异步代理真实事务通过；直接同步保存、事务开始失败与清理边界仍有缺口 |
| T4 依赖与框架恢复 | PARTIAL | 源码迁移已落地；完整默认还原图与 Admin 两宿主最终构建证据尚未收口 |
| T5 测试和交付 | PARTIAL | Docker 六项已通过，资源故障直接测试及文档当前状态需补齐 |

## 已确认的验证证据

- artifacts/admin-mysqlcap-results/net6.0-20261003T142701992Z/ 的 EFCore、FreeSQL TRX 各为 3/3，通过、无失败、无跳过。覆盖成功业务提交/出箱/消费、主键冲突回滚、出箱动作后取消回滚。
- 使用 MySQL 8.4、CAP 8.0.1、RabbitMQ 4.3，SDK 8.0.425，net6.0。该结论证明本机容器环境，不证明锁定 SDK 或所有部署环境。
- EFCore 最后一次连接生命周期调整发生在上述组合 TRX 后；execution.md 记录随后单独 3/3 通过，但没有对应的新组合 TRX。不能把旧 TRX 当作最终文件版本的完整快照。
- 本地 EFCore 8 通过/3 门控跳过、FreeSQL 9 通过/3 门控跳过、预检脚本 5/5 是上轮结果；本轮没有重复执行。Core/AOP/MVC/Sample 的历史通过结果不冒充本轮测试。
- 旧 EXT-001 本机 MySQL/RabbitMQ 不可用阻断已解除。FIX-024、025、027 的实现缺口已修复；FIX-023、026 的余项归入下面更具体的边界项，FIX-028 的交付余项归入 FIX-032。

## FIX-029：直接同步提交没有建立 CAP 事务

- 状态：OPEN_ACTIONABLE；严重程度：HIGH；处理要求：MUST_FIX。
- 问题与证据：两个 AdminUnitOfWork 只重写 SaveChangesAsync。同步 Commit()/SaveChanges() 分别继承 framework/src/Bing.FreeSQL/Bing/Uow/UnitOfWorkBase.cs:224 和 framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs:430 的普通数据库事务路径。framework/src/Bing.Events/Cap/MessageEventBus.cs:74 注册的动作不使用传入的 transaction，而是调用 CAP publisher；同步路径没有设置 CAP 当前事务。
- 影响：直接同步保存包含延迟消息时，消息不能由该路径保证加入业务事务，业务保存失败存在出箱先行的风险。这是源码调用链发现，尚未增加真实同步失败复现。同步业务方法经 AOP 最终调用 CommitAsync，不等于直接同步入口已覆盖。
- 修复目标与要求：优先为两套 Admin 同步入口提供与异步等价的 CAP 事务适配；若确实不能支持，应在任何事务动作前明确拒绝并文档化，不能静默执行非原子消息发布。
- 验证方式：真实 MySQL/CAP 下直接 Commit()/SaveChanges() 的成功、重复键失败；按唯一负载断言业务与出箱共同提交/回滚，不能只测试同步代理方法。

## FIX-030：FreeSQL 开始事务失败会留下提前设置的 CAP 状态

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：modules/admin/src/Bing.Admin.Data.FreeSQL/CapUnitOfWorkExtensions.cs:13-17 先构造 MySqlCapTransaction、写 publisher.Transaction.Value，再调用 GetOrBeginTransaction。后者抛异常时方法无法返回，调用方 AdminUnitOfWork.cs:46 的 using 未建立，且不在其后续 try/catch 内。
- 影响：失败的 CAP 包装对象没有在此路径清理，当前调用上下文的 publisher 事务状态可能残留，妨碍同一上下文后续使用。不能据此直接宣称进程级永久内存泄漏。
- 修复目标与要求：事务创建成功后再发布有效状态，或在失败路径释放新对象并恢复原状态；清理失败不得覆盖事务开始异常，不能误释放外部拥有的资源。
- 验证方式：注入 GetOrBeginTransaction/事务适配开始失败，断言当前事务状态恢复、清理次数和原始异常；正常开始路径仍保持单一所有权。

## FIX-031：补齐事务释放与提交后失败的直接验收

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：FreeSQL AdminUnitOfWork.cs:46-67 的 using 与 EFCore AdminUnitOfWork.cs:45 事务 using 的 Dispose 在现有回滚异常聚合之外；Dispose 如再抛出，语言层面的 finally 可以覆盖正在传播的原始异常。当前 RollbackBoth/TryRollbackAsync 测试只验证委托，不验证整个事务所有权流程。FreeSQL 数据库提交后的 Flush 失败分支也没有直接测试。
- 影响：尚不能兑现完整的“原始异常、清理异常同时定位，资源按正确所有权释放”验收。未证明当前正常 Provider 会发生重复释放或内存泄漏，不把测试缺失等同于已发生泄漏。
- 修复目标与要求：补充可控事务故障测试，覆盖业务失败+回滚失败+Dispose 失败、取消+清理失败、开始前取消、提交成功后 Flush 失败；按实际复现修复异常保留与所有权。提交后失败不得伪称已回滚，不自动重放业务写入。
- 验证方式：断言原始异常保留、其他应清理资源仍获清理、包装对象与底层事务的所有权/释放次数；EF 连接在异常后仍由 EF 正确管理；提交后 Flush 失败时数据库/出箱结果及 CommitAfter 行为明确。优先可控故障注入，真实 Provider 验证持久化结果，不要求通过制造真实数据库崩溃证明每种委托异常。

## FIX-032：交付记录和最终依赖闭包证据收口

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：test-mapping.md:6 仍标“真实 CAP 8 提交待外部测试”，与后续行及 Docker TRX 矛盾；连接所有权映射只有集成成功证据，没有独立所有权断言。execution.md 中保留离线 EfCore6Version/NoWarn 验证及完整 Admin 宿主构建缺包记录。
- 修复目标与要求：纠正映射的当前状态，区分旧记录、最终源码测试和完整默认依赖还原；记录 CAP MySql 对齐 8.0.1 的影响闭包，补齐当前源码对应的可保存验收结果。完整构建若因源/包不可用而失败，只报告环境门槛，不循环修改业务代码。
- 验证方式：生产符号对应真实测试方法和执行结果；两套 Admin 宿主的默认还原、构建与运行时依赖图有记录。对于用户所指 Bing.Utils 1.5.0，以实际选用包源/nupkg/还原图核实，不将某份本机缓存依赖推广为所有来源的包事实，不未经需求修改版本。

## 验证门槛与非本期事项

- VERIFY-001：锁定 SDK 8.0.424 未验收，实际 8.0.425；不得修改 global.json 绕过。
- VERIFY-002：默认依赖图的干净还原及完整 Admin 两宿主最终构建未形成成功证据；历史离线覆盖参数和已有资产不能代替。该项的记录任务见 FIX-032，环境恢复本身不算代码缺陷。
- 非新增必做功能：Autofac、NativeAOT、不可信插件隔离仍在计划之外；跨库强一致、恰好一次消费也不是此次 CAP 出箱测试的保证。强杀进程不保证托管清理。
- Docker 启停一键化可作为后续便利改进，当前已有运行脚本且实际容器验收通过，不将缺少 Compose 文件单独判为功能缺陷。

## 当前状态

OPEN_ACTIONABLE = 4
REVIEW_STATUS = NEEDS_FIX
TEST_STATUS = PASS_SCOPED_WITH_GAPS
RELEASE_STATUS = NOT_FULLY_VERIFIED

建议顺序：FIX-029 → FIX-030 → FIX-031 → FIX-032。只针对明确缺陷和上述验收缺口继续推进；不因历史 PARTIAL 状态重复执行全量审查修复。