<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261027-resilience-boundaries
AI_REVIEWED_AT: 2026-10-27T07:36:01Z

# 关系型存储边界独立审查

## 范围与结论

审查 BND-01 的 SQLite/MySQL 迁移和结构校验、EF Core/FreeSQL 事务桥接、Dapper ADO.NET 事务复用，以及 SQLite 订单补偿样例。两项此前发现的问题均已修复并取得定向测试与真实 SQLite 探针证据。未修改生产代码或既有测试。

## Findings

### FIX-REL-01：SQLite 校验接受改变身份比较语义的排序规则

- 状态：`CLOSED`
- 严重度：`SHOULD_FIX`
- 证据：[SQLite DDL 排序规则检查](../../../framework/src/Bing.Resilience.Operations.Relational/RelationalOperationStore.Schema.cs:79)、[索引 key collation 检查](../../../framework/src/Bing.Resilience.Operations.Relational/RelationalOperationStore.Schema.cs:133)、[按 ID 查询](../../../framework/src/Bing.Resilience.Operations.Relational/RelationalOperationStore.cs:128)
- 原触发：SQLite 允许 `COLLATE 'NOCASE'`，旧扫描逻辑先移除单引号内容，导致该形式绕过校验；普通 `id/state` 与索引的 NOCASE 结构也曾被接受。
- 修复与验证：当前实现按词法 token 识别 collation 名称，保留单引号名称并跳过注释、字符串及带引号标识符中的 `COLLATE` 文本；索引校验读取 key 列 collation。`SqliteSchemaRejectsCaseInsensitiveIdentityAndState` 覆盖普通、注释/方括号及单引号 NOCASE；有效迁移测试覆盖 `COLLATE 'BINARY'` 与默认字符串 `DEFAULT 'COLLATE NOCASE'` 不被误拒绝。
- 真实 SQLite 探针结果：`sqliteNoCaseIdRejected=True; sqliteBracketNoCaseStateRejected=True; sqliteSingleQuotedNoCaseStateRejected=True; sqliteNoCaseUniqueIndexRejected=True`。探针源码保留在 [CompensationKeyMismatchProbe.csproj](evidence/review/CompensationKeyMismatchProbe.csproj) 与 [Program.cs](evidence/review/Program.cs)。

### FIX-SAGA-01：重复补偿未核对已提交的补偿幂等键

- 状态：`CLOSED`
- 严重度：`SHOULD_FIX`
- 证据：[补偿重放键核验](../../../samples/Bing.Samples.ResilienceExtensions/SqliteOrderWorkflow.cs:83)、[换键确认语义](../../../samples/Bing.Samples.ResilienceExtensions/SqliteOrderWorkflow.cs:62)、[重复补偿测试](../../../framework/tests/Bing.Resilience.Extensions.Tests/OrderWorkflowSampleTests.cs:50)
- 触发：首次使用 `compensation-key-1` 完成补偿但响应丢失；随后对相同 `ForwardKey` 和业务输入改用 `compensation-key-2` 重试。
- 修复与验证：已补偿时读取 `demo_compensations`，要求同一 `ForwardKey` 对应当前 `CompensationKey`；换键重试抛出冲突，换键确认返回 `Unknown`。`DuplicateForwardAndCompensationDoNotRepeatBusinessEffectsAndConfirmUsesCorrectLedger` 增加了换键断言。真实 SQLite 探针输出 `retryRejected=True; confirm=Unknown; ledgerRows=1`。

## 已确认边界

- MySQL 校验已区分 `INT` 与 `BIGINT`。真实 MySQL 测试将 `attempts.started_at` 改成 `INT` 后验证拒绝，恢复 `BIGINT` 后通过；同时验证唯一键缺失和非 InnoDB 拒绝。[MySqlMigrationAndReadOnlyValidationPreserveAtomicJournalRollback](../../../framework/tests/Bing.Resilience.Extensions.Tests/RelationalBoundaryTests.cs:18) 纳入当前关系型边界职责类定向运行；`sqlite-collation-final` 的 net6/net8 结果各为 12/12（这是整个职责类的用例数，不是该单个 MySQL 方法的用例数）。
- EF Core、FreeSQL 和 Dapper 均在 SQLite 实际事务内覆盖提交与回滚。EF/FreeSQL 桥接要求已有宿主事务；桥接会话释放后宿主连接仍可用，Journal 不自行提交或回滚。[RelationalBoundaryTests.cs](../../../framework/tests/Bing.Resilience.Extensions.Tests/RelationalBoundaryTests.cs:84)
- `Bing.Resilience.Operations.Relational` 项目本身只依赖 Operations；EF Core 与 FreeSQL 驱动分别留在两个可选桥接包，关系型包未传递引入数据库驱动。[Relational csproj](../../../framework/src/Bing.Resilience.Operations.Relational/Bing.Resilience.Operations.Relational.csproj:4)、[EF Core bridge csproj](../../../framework/src/Bing.Resilience.Operations.EntityFrameworkCore/Bing.Resilience.Operations.EntityFrameworkCore.csproj:4)、[FreeSQL bridge csproj](../../../framework/src/Bing.Resilience.Operations.FreeSql/Bing.Resilience.Operations.FreeSql.csproj:4)
- SQLite 脚本和 MySQL 脚本均嵌入到关系型包，资源名与运行时代码一致；迁移指引要求由宿主迁移流程执行，不在应用启动时自动建表。[嵌入资源声明](../../../framework/src/Bing.Resilience.Operations.Relational/Bing.Resilience.Operations.Relational.csproj:6)、[迁移说明](../../../docs/migrations/resilience-extensions/mysql/001-journal.sql:1)

## 验证

- 定向复核测试证据：最终 `sqlite-collation-final` net6/net8 各 12/12 通过（[net6 TRX](evidence/tests/sqlite-collation-final_net6.0_20261027153054.trx)、[net8 TRX](evidence/tests/sqlite-collation-final_net8.0_20261027153055.trx)）；该结果替换此前 11 项结果并包含新增的单引号 `NOCASE` 用例。此前 `storage-review-fixes` net6/net8 各 13/13（[net6 TRX](evidence/tests/storage-review-fixes_net6.0_20261027152428.trx)、[net8 TRX](evidence/tests/storage-review-fixes_net8.0_20261027152427.trx)）。扩展完整项目后续 net6/net8 各 174/174（[net6 TRX](evidence/tests/extensions-complete_net6.0_20261027152839.trx)、[net8 TRX](evidence/tests/extensions-complete_net8.0_20261027152846.trx)）；先前的 172/172 是历史阶段结果。
- 本次真实 SQLite 探针命令：`dotnet run --project ai_docs/tasks/20261027-resilience-boundaries/evidence/review/CompensationKeyMismatchProbe.csproj --configuration Release`，退出码 0。输出：`first=Unknown; retryRejected=True; confirm=Unknown; ledgerRows=1`；`sqliteNoCaseIdRejected=True; sqliteBracketNoCaseStateRejected=True; sqliteSingleQuotedNoCaseStateRejected=True; sqliteNoCaseUniqueIndexRejected=True`。
- 全回归首轮曾有一个 net8 `Bing.Resilience.Tests.ResilienceStrategyBoundaryTests.FallbackActionIsCancelledByExecutionBudget(providerName: "BuiltIn")` 250ms 预算用例同步超时；随后 Provider 边界用例定向复跑 3/3 通过（[TRX](evidence/tests/core-budget-recheck.trx)）。当前 [validation-summary.json](evidence/validation-summary.json) 按稳定 `testId` 合并后为 805/805，`unresolvedFailures` 为 0；TRX 的 `testName` 可能被截断，不能按名称去重。此前 799/799 是历史滚动汇总。原始首次失败证据保留；本次没有重跑全量测试，也没有修改该核心测试或实现。
- EF Core/FreeSQL 桥接在真实 SQLite provider 上验证了事务归属；本轮没有在 MySQL 上运行 ORM 桥接组合测试。

当前 `FIX-REL-01` 与 `FIX-SAGA-01` 均为 `CLOSED`，本范围没有剩余 `OPEN_ACTIONABLE`。
