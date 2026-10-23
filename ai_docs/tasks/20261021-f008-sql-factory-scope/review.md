<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261021-f008-sql-factory-scope
AI_REVIEWED_AT: 2026-10-21T01:51:32.941156+00:00

# F008 独立验收，Round 2

**PASS。FIX-001、FIX-002、FIX-003 全部 CLOSED；OPEN_ACTIONABLE=0；F008_STATUS=CLOSED。** 原两类 root provider/过滤状态故障已修复，批准的 scope、缓存和兼容注册契约已闭环。本地 F008 不再需要 Fix 或兼容审批。整体遗留报告中的 F008 状态由父会话更新，未修改旧 merged-code-audit。

## 当前状态

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS_LOCAL_REQUIRED_SCOPE
PERFORMANCE_EVIDENCE_STATUS: PASS_LOCAL_PROBE_AND_DRY_SMOKE_ONLY
RESOURCE_EVIDENCE_STATUS: NOT_RUN_OUT_OF_SCOPE
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL_GLOBAL_GATES
RELEASE_STATUS: LOCAL_F008_PASS_GLOBAL_RELEASE_GATES_INDEPENDENT
GOAL_STATUS: COMPLETED
OPEN_ACTIONABLE: 0
F008_STATUS: CLOSED
NEXT_ACTION: STOP
```

COMPLETED 的范围是本任务批准的本地交付与验收；不表示外部实库/CI/CodeSmith、正式性能资源门禁或其他整体遗留项完成。Scoped 兼容方案已批准，没有本地 BLOCKED_APPROVAL。

## 候选、规则与 CIA

- 当前 HEAD 仍为任务起点 `5065c38ced05a95088e17d89d96c9a1610e37159`，暂存区为空。核查 AGENTS、用户规则、GOAL_RULES、review-code 技能、批准计划、execution 末尾 Review 修复 Round 1、fix-round-1/evidence-index 和实际 diff/新增文件。
- 与 Round 1 的 candidate.json 逐项比对：原 13 个源码/测试/harness 文件全部哈希未变。仅新增五个测试文件，扩展 `MultiProviderRegistrationTest.cs`；生产、fixture、消费者、benchmark、公共签名、TFM、版本、API baseline、预算均未改。当前完整 diff/status/untracked 与源码哈希见本轮 `candidate.json`、`unstaged.txt`、`staged.txt`、`status.txt`、`untracked.txt`。实际构建后又校验源码无漂移，见 `source-check.json`。
- 已在写新报告之前将旧 review/final-report 原始字节保存为 `artifacts/review-round-2/round-1-review.md`、`round-1-final-report.md`。SHA-256 分别为 `a1802d8879aafba84042fb2ebe0dc173a262d137d5a1658e5a0f66d32e83110e`、`45d752e6f16852e8a57273ca3c7c1dcc1bcb14f3464b20dafaae607e673c6092`，与 Fix 记录一致。
- 测试前 CIA：ChangedFiles=5 新测试+1 扩展测试；ChangedProjects=Core.Tests、EF.Tests、SqlServer.Tests、SQLite.Integration；ChangedPublicContracts/RuntimePaths/Providers/TFMs/BuildPackaging/BenchmarkHarness=无生产变化；AffectedDependents=无新增消费端影响；RiskLevel=MEDIUM（验收覆盖与并行诊断捕获）。先最小新增真实链，再受影响 suite；未机械扩大到五 Provider 全矩阵或全仓。
- Reviewer 仅写本轮原始证据与当前两份报告；保留 plan/execution/migration、Luna 证据和其他历史 artifacts。命令显式 UTF-8；dotnet 每条最多180秒、test hang90秒、超时 taskkill /T；stdout/stderr、退出码、TRX 分别保留。本轮均无超时、退出0，不使用 --no-build。

## FIX-001：CLOSED

原严重程度 MEDIUM / SHOULD_FIX；原根因是事务孩子与 EF adapter 缺少 scoped filter 联合回归。

直接证据：

- `SqlTransactionScopeFilterContractTest.Begin_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope`。
- `SqlTransactionScopeFilterContractTest.BeginAsync_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope`。
- `EfCoreSqlQueryFactoryScopeFilterContractTest.Create_WhenScopesAreValidated_ShouldKeepFilterStatePerAdapterScope`，Shared/Independent 两个理论用例。

事务测试使用真实 ValidateScopes/ValidateOnBuild 的独立 A/B scope、独立 SQLite 数据源及不同的 first/second 行内容。Begin("first") 后切换 ambient 到 second，事务 typed Query 仍读 first，B 显式读 second。切换 A filter 时 A 完整 SQL 改变且恢复，B SQL 独立；事务创建的实体 Delete/AsyncDelete 对已删除行返回启用0/禁用1。同步与异步均回滚，后续可读两库行集。

证据边界准确解释：Delete 是软删除已删除行，0→1 是匹配影响行和过滤状态的证据，不是物理删除或可观察的状态变化；新增测试最后行集本身不能单独证明“曾改变数据后再回滚”。真正改变数据的提交/回滚/异常释放由未变的既有 SQLite transaction tests 验证，本轮 full 亦执行。Executor 固定上下文同时核对真实源码：SqlTransactionScope.CreateExecutor 使用 CreateForTransaction(_context)，并通过 BindTransactionContext/SetTransactionContext 绑定同一连接、事务、快照和 lease；既有共享事务/快照测试继续有效。新 Query 的不同库行集是实际路由判别，不把两个库同为 Id=2 的 Mutation 行数冒充单独的路由判别。

EF 测试从 A/B 解析真实 IEfCoreSqlQueryFactory，各建实际 UnitOfWork，两个连接模式均执行 enabled→A disabled→restore→B disabled→restore，逐次断言完整 SQL、真实行集、参数名/值及连接 Source/Ownership。Shared 的连接在 Query Dispose 后保持 Open；Independent 声明 Owned，实际释放沿用既有 EF 资源测试。

失败迭代修正没有弱化关键断言：diagnostic observer 的 BeginCapture 同步设置 AsyncLocal，在执行 await 前生效，消息来自 BeforeExecute，Assert.Single 仍保留，隔离其他并行测试的逻辑调用；启用时严格断言 `@_p_0=false`。禁用时完整 SQL 不含谓词、返回两行，并严格检查公开诊断表示为 ReadOnlyCollection<SqlParam> 的 Count=0。核对未变 SqlQueryBase.CreateParameterDiagnostics：元数据空时回退公开属性，因此这里的 Count 是空参数源描述，不是发给数据库的名为 Count 的 SQL 参数。没有把错误参数隐藏为“忽略参数”。事务 Query 执行后改用公开 ToSql，避免冻结后误取草稿 Builder；SQL 字面量和真实执行断言保留。

独立最小 SQLite3/EF4 均通过；独立 SQLite net8 full167、EF实际net6 full100通过；net6 SQLite同候选167复用。满足原验收条件，不需要变异或生产修改。

## FIX-002：CLOSED

原严重程度 MEDIUM / SHOULD_FIX；原根因是 resolver 职责证据与 Scoped factory 创建链脱节。

`SqlFactoryCacheScopeContractTest.TypedRendering_WhenScopesShareCache_ShouldReuseMappingsAndReadCurrentFilters` 在新容器、唯一实体上，经 Scoped ISqlQueryFactory 创建 typed Query：首次渲染观测 miss/entry 增长，同 scope 新 Query 热命中，B scope 共享同一 Singleton resolver/cache、hit 增长且 entry 不增。不同 DbKey/Profile 映射到不同表，新增 miss/entry，返回不同实体行；再次创建隔离路由 Query 又命中且 entry 稳定。

命中后 A/B 的 filter 独立，反复切换严格断言完整 SQL 与参数单项/空集合；filter 渲染前后 miss/entry 不增加，实际行集分别为一行/两行。计数用增量关系而非内部 Resolve 调用次数，未通过预先 Resolve 人工暖缓存替代 factory 冷渲染。新链提供联合证据；未变 resolver/key/Clone 测试继续作为各自职责证据，不互相冒充。

独立 SQLite 最小链及 full net8通过；同候选 net6 full复用。满足原验收，缓存算法或键无需改动。

## FIX-003：CLOSED

原严重程度 MEDIUM / SHOULD_FIX；原根因是自定义兼容仅测试 Query、旧多 Provider 测试只从 root 固定次序解析。

- `SqlFactoryRegistrationContractTest.AddSqlCore_WhenEachFactoryIsPreRegistered_ShouldPreserveDescriptorAndResolvedInstance` 循环四工厂，分别覆盖 Singleton type/factory/instance 的描述符、生命周期、实际解析身份；type 注册还验证跨 scope Same。无资源 stub 只用于 DI 注册身份，不充当执行实现。
- 同类 `AddSqlCore_WhenFactoriesAreRegisteredAfterCore_ShouldResolveLastAndRetainCollectionEntries` 检查四工厂 last-service 和 GetServices 两项。
- `EfCoreSqlQueryFactoryRegistrationContractTest` 两方法覆盖 EF 预注册三种形式和后注册/集合，Singleton 不被默认 Scoped 替换。
- `MultiProviderRegistrationTest.AddProviders_WhenOrderIsReversedAndRegistrationsRepeat_ShouldKeepScopedFactoryContracts` 的正/逆序两用例重复 core 和五 Provider 注册，ValidateScopes 子 scope 验证四工厂单条 Scoped、同 scope Same/异 scope NotSame；元数据、resolver/cache/registry跨 scope Same；五 Provider 单条注册。MySQL/PostgreSQL 两具名路由生成不同方言的完整 typed SQL 字面量，未连接外库。原来五 Provider/SPI/Doris/Oracle 职责证据保持有效。

独立 Core net8 full168、SqlServer net8 full297和EF net6 full100通过；同候选Core/SqlServer net6 full分别168/297复用。满足原验收，不要求穷举所有Provider排列。

## 本轮 Fresh 与同候选 Fix 证据

| 套件/TFM/Release | passed/failed/skipped | 本轮最终选用证据 |
|---|---:|---|
| Core net8 | 168/0/0 | fresh `review-round-2/core-full-net8/` |
| Core net6 | 168/0/0 | REUSED_UNCHANGED_SCOPE `fix-round-1/core-net6-release/` |
| EF actual net6 | 100/0/0 | fresh `review-round-2/ef-full-net6/` |
| SqlServer（含多 Provider）net8 | 297/0/0 | fresh `review-round-2/sqlserver-full-net8/` |
| SqlServer（含多 Provider）net6 | 297/0/0 | REUSED_UNCHANGED_SCOPE `fix-round-1/multiprovider-net6-release-final/` |
| SQLite integration net8 | 167/0/1 gated | fresh `review-round-2/sqlite-full-net8/` |
| SQLite integration net6 | 167/0/1 gated | REUSED_UNCHANGED_SCOPE `fix-round-1/sqlite-integration-net6-release-final2/` |

独立最小链 `sqlite-new-net8`=3/0/0、`ef-new-net6`=4/0/0，已包含在 full，用于先否定回归，不额外累计通过数。SQLite gated case 是未变的 Provider evidence contract；原 net6/net8 受控运行各1通过可复用。因此当前每TFM最多168个不同SQLite integration case具有执行证据，不能再叠加第一轮164或最小链13/3。

Luna 其余4个最终Release日志与上述独立full计数一致；完整7个最终目录、命令/status/TFM/TRX、新源码关联及失败迭代审计见本轮 `evidence-audit.md`。原早期编译/诊断/accessor失败保持历史证据，未计入最终通过。新候选源码写入早于最终所选构建；独立fresh命令重建当前候选，不依赖未经确认的 --no-build 产物。

## REUSED_UNCHANGED_SCOPE 与未运行范围

| 未变套件 | TFM | passed/failed/skipped | 最终有效来源 |
|---|---|---:|---|
| Data.Tests | net6/net8 | 各157/0/0 | review-round-1 `data-full-net6/net8` |
| Data.Sql.Tests | net6/net8 | 各1317/0/0 | 原 artifacts/trx/final `data-sql-net6/net8` |
| CustomProvider.Tests | net6/net8 | 各21/0/0 | 原 trx/final `custom-provider-net6/net8` |
| MySql.Tests | net6/net8 | 各180/0/0 | R1独立net6 + 原final2 net8 |
| PostgreSql.Tests | net6/net8 | 各138/0/0 | R1独立net6 + 原final2 net8 |
| Oracle.Tests | net6/net8 | 各93/0/0 | R1独立net6 + 原full net8 |
| SQLite unit | net6/net8 | 各112/0/0 | 原 trx/full `sqlite-unit-net6/net8` |
| SQLite gated evidence contract | net6/net8 | 各1/0/0 | 原 provider-contract/net6、net8；与full的skip对应 |
| FreeSQL.Tests | net6/net8 | 各76/0/26 | 原full net6/net8-final |
| Admin.EFCore.Tests | 实际net6 | 140/0/36 | 原full admin-efcore-net6 |
| Admin.FreeSQL.Tests | 实际net6 | 150/0/35 | 原full admin-freesql-net6 |

复用来源配置与身份边界沿用 Round1证据审计：旧Provider/Custom等部分为Debug，其他为Release，不拼成单一Release二进制。本轮无相关生产/测试/运行时/environment class变化；Round1源码哈希、Git范围与现候选对比支持scope复用。旧日志没有冻结程序集哈希的局限仍保留；最新关键链有独立Release构建。FreeSQL/Admin 的原日志包含消费者和传递项目成功编译输出，仍可作为构建/装配证据。迁移示例Round1已实际编译0warning/0error，源码及所依赖生产scope未变，不重复编译。

未再运行五Provider全矩阵、85项目build、11796旧矩阵、无关consumer、probe或24Dry smoke。未运行外部实库、外部CI、CodeSmith、正式资源/性能矩阵；这些全局门禁独立，不转为本地代码 finding。仅测试变化无需重建缓存/格式化/Clone算法baseline；SQL AGENTS在Round1已经逐项核查的未变职责不重新打开。

## 性能、迁移和最终计划核对

生命周期probe与E2E smoke源码/harness/生产注册均和Round1一致，REUSED_UNCHANGED_SCOPE。net8 Release/ValidateScopes=true，两个场景各warmup100+3×1000，总0.4127652秒、零异常、零连接resolver调用。首场景四工厂解析+typed渲染+释放，同scope场景重复Query.Create并释放；记录成本，不宣称性能预算、P95或性能提升。原probe外层退出码缺失的已知证据限制不补造。24/24 Dry smoke使用真实修改后的benchmark基类和显式scope，cleanup次序已验收。

原两故障的before TRX仍为2failed；当前SQLite full重新执行原ValidateScopes=true/false typed渲染回归并通过。IDataFilter保持Scoped，无状态metadata/registry/cache保持Singleton；Factory绑定解析scope，A带到B不会重绑；Query/Executor上下文快照与动态filter分离。Clone仍隔离参数/子句，共享原scope filter和既有executor，未增加并发安全承诺。

迁移契约保持：Singleton/后台每业务使用IServiceScopeFactory创建scope；Create产物由调用方释放；scope覆盖执行、await、枚举、reader与释放；事务孩子在事务结束前完成。直接DI transient由scope管理；EF Shared外部连接不误释放、Independent自有连接释放。TryAdd保留自定义Singleton，由注册者负责依赖契约。raw SQL/Multiple不自动过滤；Oracle Multiple无runtime、Doris只读无事务边界保留。不扩展EF StoreBase重构。

计划7阶段最终结果：基线/失败回归、注册/创建链、过滤/事务/cache/Clone、Provider职责、SQLite/消费者/资源、probe、迁移/追溯均PASS_LOCAL_SCOPE（7/7）。原三项验收缺口均有真实测试与原始证据；没有新OPEN_ACTIONABLE，不仅依据COMPLETED自报收口。

## Round索引与分组TODO

- R1：首次独立验收提出三个SHOULD_FIX；原文快照在本轮round-1-review.md/final-report.md，原raw evidence在review-round-1。
- Fix1：Luna仅补测试；原始过程在fix-round-1，未覆盖失败记录。
- R2：本报告；candidate/source-check、run_checks.py、6组独立命令stdout/stderr/status/TRX、evidence-audit为新证据。

Completed：FIX-001/002/003 CLOSED；本地Implementation/Test PASS；计划7/7及三个修复项3/3验收完成。

Open Actionable：none。

Blocked Approval：本地F008 none；正式性能/资源预算不在本任务批准范围。

Blocked External：外部CI/实库/CodeSmith及整体发布证据，仍由父会话独立跟踪。

Not Applicable：重建未变算法baseline、重复全仓/全Provider/全consumer矩阵、重复probe/smoke。

Accepted Limitations：Oracle/Doris支持边界、raw SQL语义、scope/产物所有权、共享executor不承诺并发安全。

Verified Boundaries：既有probe在60秒窗口内完成且无DB；Dry smoke仅证实执行。

Deferred：退款、全局规则其他缺口/候选、全局发布收口，留在原整体任务；本任务无新延后本地事项。

No-Progress Check：CHANGED，原三个根因取得新增直接证据并关闭；未达到无进展条件。当前第2轮，未超过3轮上限。

Next Action：STOP。父会话可据本报告更新整体遗留F008为CLOSED；Reviewer停止循环，不重写旧整体审查历史。
