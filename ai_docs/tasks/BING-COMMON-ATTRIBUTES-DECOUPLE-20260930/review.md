<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-COMMON-ATTRIBUTES-DECOUPLE-20260930
AI_REVIEWED_AT: 2026-10-03T23:45:43.0732803+08:00

# 同步 CAP 与资源清理改动后的计划复核

本轮只读核对生产源码、测试、Git 差异、计划和已保存的 TRX，未重新运行测试。上轮报告保存为 review-before-sync-cap-recheck-20261003.md。主要公共能力已实现；剩余为 2 组行为/测试 TODO、1 组文档 TODO、2 组环境验证，以及 1 项投递波动观察。

## 计划对照

| 计划项 | 状态 | 当前结论 |
| --- | --- | --- |
| T1 中立特性、类型转发 | PASS | 定义已迁移；普通标记用法和旧消费者证据沿用历史已核查结果 |
| T2 参数验证与 Ignore 适配 | PASS | 沿用前轮已核查的真实代理测试，本轮未发现重新打开这些项的依据 |
| T3 工作单元执行器及 Admin 事务 | PARTIAL | 同步、异步主要路径均已接入；开始阶段的局部失败资源清理仍有缺口 |
| T4 依赖与框架迁移 | PARTIAL | 已有源码迁移；默认依赖图和两个完整 Admin 宿主构建仍未验证通过 |
| T5 测试、Sample、文档 | PARTIAL | Sample 已有 HTTP 业务值与 StopAsync 测试；组合清理异常和当前报告仍需补齐 |

## 已完成与证据复用

- FIX-029 CLOSED：两套 Admin 重写 SaveChanges，同步待执行事务动作进入 CAP 路径；Commit 通过虚方法分派进入 SaveChanges。直接同步成功与重复键失败测试均通过，不能再列为未实现。
- FIX-030 部分关闭：底层数据库事务开启本身失败时不会发布新 CAP 状态，已有直接测试；新发现的“数据库事务已开启，后续 CAP 准备失败”见 FIX-033。
- FIX-031 的代码部分已落实：释放错误聚合、提交后 Flush 状态标记、当前 CAP 事务恢复均已接入。测试完整性余项保留在下面。
- 最新 Docker TRX：artifacts/admin-mysqlcap-results/net6.0-20261003T152807178Z/；EFCore、FreeSQL 各 total=5、passed=5、failed=0、notExecuted=0。覆盖直接同步成功/失败、代理异步成功、业务冲突、执行中取消；同步成功/失败断言当前 CAP 事务恢复。
- 执行记录中的定向 FreeSQL 辅助测试 8/8、EFCore 回滚测试 2/2、脚本 5/5 属历史执行证据，本轮只核实测试内容和已有结果。实际 SDK 8.0.425，不等于锁定版本通过。
- SampleWebApiSmokeTest.LazyEndpoint_ShouldResolveBusinessValueAndReleasePortOnStop 已包含真实 HTTP 200、lazy-service-ready 和端口释放断言；本轮未重跑，不重新列为缺失功能。

## FIX-033：数据库事务开启后 CAP 准备失败未完整清理

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：modules/admin/src/Bing.Admin.Data.FreeSQL/CapUnitOfWorkExtensions.cs:16 先调用 GetOrBeginTransaction，:18 才解析 IDispatcher 和构造 CAP 包装对象，而且这两步在 :19 的 try 外。若依赖缺失或工厂构造抛异常，数据库事务可能已经开启。调用方 AdminUnitOfWork.cs:74 的 capTransaction == null 分支仅保存异常，没有回滚新开的底层事务。
- 影响：失败后仍保留工作单元时，新开事务可能持续占用连接/锁直到工作单元释放。不能由此宣称永久内存泄漏，也不能将调用方已有事务一律回滚。
- 修复目标：先完成必要的 CAP 依赖准备，再取得业务事务；将部分创建失败纳入明确的资源所有权清理路径，区分本方法新开与调用方已有事务。
- 验证方式：在事务开启后注入 CAP 依赖/准备失败，断言原异常保留、新建资源清理、原有 CAP 值恢复；另测已有事务不会被误释放。不要只测试 GetOrBeginTransaction 第一行直接抛错。

## FIX-031：完整工作单元的组合清理故障测试仍需补齐

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：CapUnitOfWorkExtensionsTest:36 的 RollbackBoth 测试调用委托，:101/:117/:129 的 DisposeAndCapture 测试调用共享辅助方法；AdminUnitOfWorkRollbackTest 测试 TryRollbackAsync。它们没有在一次实际 AdminUnitOfWork 调用中组合触发业务/取消、CAP 回滚、数据库回滚和 Dispose 故障。Commit_WhenFlushFails_ShouldPreserveCommittedDatabaseResult 使用缺少 Flush 的 Mock，未验证工作单元 catch/finally 与真实出箱的组合结果。
- 影响：辅助函数正确不能完全证明生产调用者在所有失败位置都执行了它们，当前仍不能确认所有部分创建、关闭及重复释放边界。
- 修复目标：增加可控事务适配/故障夹具，在完整生产调用链测试原始异常、所有清理尝试、资源释放次数和当前 CAP 状态；优先复用真实 Provider 并在事务包装边界注入异常。
- 验证方式：业务错误+回滚错误+释放错误、取消+清理错误、CAP Begin 错误、提交后 Flush 错误。断言根因可定位、剩余清理继续、次数正确、提交后不伪称回滚。无需制造真实服务器崩溃，亦无需为测试建立新的通用事务框架。

## FIX-034：统一报告当前状态与历史证据

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：execution.md:11、:28、:149 仍把 MySQL/CAP 列为不可用或唯一外部阻断，后文却已有 Docker 两套 5/5 和剩余代码/测试差距；当前映射与最新附录较准确，但报告首页会误导读者。
- 修复目标：当前 execution.md 首页和验收矩阵只表达当前状态；旧轮次归档并链接保留。区分历史本地全量、当前定向、最终 Docker、缺包构建和 SDK 未验证。
- 验证方式：从报告首页即可得到与最新 TRX 一致的结论，不把跳过算通过，也不写“只剩外部环境”。无需因纯文档修改再跑代码测试。

## 环境验证 TODO

- VERIFY-001 / BLOCKED_EXTERNAL：锁定 SDK 8.0.424 验证，现有结果基于 8.0.425，不改 global.json。
- VERIFY-002 / BLOCKED_EXTERNAL：取得可用包源后按默认参数还原、构建两套完整 Admin 宿主并核查运行时依赖图。历史 EFCore 宿主缺 Serilog/Exceptionless；FreeSQL 宿主现有资产缺新适配项目的信息。先修复还原条件，不把 --no-restore 失败直接归为源码缺陷。契约包不传递 AspectCore 的最终包验收以实际包源和还原图为准，不能从某份本机 Bing.Utils 缓存推导全部来源的事实。

## 观察项与边界

- OBS-001 / DEFERRED：FreeSQL 中间轮次出现一次消费者等待 30 秒超时，随后单项和最终组合通过；保留失败证据。后续如再现，优先采集出箱/收件状态、消费者启动及 broker 信息，不直接延长超时或凭一次成功宣称彻底解决。
- ACCEPTED_LIMITATION：提交成功后 Flush 失败无法回滚数据库；CAP 出箱不证明跨库强一致或恰好一次消费。
- Autofac、NativeAOT、不可信隔离仍不在本期计划中，不计为漏实现。

## 状态

OPEN_ACTIONABLE = 3（FIX-033、FIX-031、FIX-034）
IMPLEMENTATION_STATUS = NEEDS_FIX
TEST_STATUS = PASS_SCOPED_WITH_GAPS
EXTERNAL_GATE_STATUS = BLOCKED_EXTERNAL
RELEASE_STATUS = NOT_FULLY_VERIFIED

建议顺序：先修 FIX-033，并由 FIX-031 的完整调用链故障用例固定行为；随后整理 FIX-034，最后在环境具备时执行两个验证门槛。本轮未修改业务源码、测试或 execution.md。