<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: 20261022-full-suite-recovery
AI_EXECUTION_FINISHED_AT: 2026-10-22T10:22:17Z

# 全量测试恢复执行报告

## 执行结果

Round 0 已完成 T01–T04、隔离真实数据库验收、一次本地 L4 全量测试及本任务 Docker 资源清理。任务开始时工作区已有大量未提交和未跟踪内容；执行期间予以保留，未暂存或提交。

Round 0 原报告曾宣称原 181 项均已语义闭环。独立 Review 发现其中 AOP 两项只覆盖替换后的信号等待路径，不能证明原始无参快速取消回归，因此该声明过早。本报告保留 Round 0 的历史计数和证据，并以当前 AOP 候选的同名实测结果修正原失败映射。最新 Sol/high 独立复验为 PASS，`CACHE_RETRY_STATUS=PASS`、`OPEN_ACTIONABLE=0`，FIX-001..004 全部 CLOSED。缓存限定测试修复后已刷新 Release solution build 与默认 L4，当前检查点见报告末尾；此前 net6 单项失败检查点仍保留为历史。`review.md` 保持独立证据，没有被实施者改写或关闭。

### 上一候选最终检查点 CIA（历史）

- ChangedFiles：本轮冻结生产/业务测试源码；只新增最终构建/L4 证据、skip 清单和任务报告。
- ChangedProjects：Fresh build 覆盖 `Bing.All.slnx` 的 159 个 solution project；Fresh L4 覆盖 solution 中所有实际测试项目。
- ChangedPublicContracts / RuntimePaths / Providers / BuildPackaging / BenchmarkHarness：无代码变更。
- ChangedTFMs：net6.0、net8.0；ChangedDocsOnly：本轮源码不变，仅更新报告与证据。
- AffectedDependents：solution 内测试消费者由 Fresh L4 检查。
- RiskLevel：HIGH（用户授权的 final candidate checkpoint；源码哈希需与 Sol PASS 候选一致）。
- 执行边界：外部 Provider/CAP/cache 测试门控在独立子进程显式关闭，连接串环境变量清空；不传 `--settings`，不隐式加载本地 runsettings。SQLite 本地集成路径保留执行。

## T01–T04 实施

- **T01 失败基线与配置来源**：保留迁移任务原始 181 项失败详情及 TRX；确认 MySQL、PostgreSQL、SQLite、SQL Server 四个集成测试项目通过 `RunSettingsFilePath` 自动加载含 `RUN_INTEGRATION_TESTS=true` 的本地设置，造成默认测试误启外部数据库。任务开始时的文件清单和工作区状态保存在 `artifacts/baseline/worktree/`。
- **T02 默认外库门控与作用域**：从四个集成测试 `.csproj` 移除隐式 runsettings 加载；文档改为显式传入设置，provider runner 继续仅接受 `integration.runsettings.local` 白名单文件并将结果限制在仓库 `artifacts/provider-test-results`。四份既有 `integration.local.runsettings` 均未改写。MySQL/SQL Server fixture 开启 `ValidateScopes`，查询、执行器、事务工厂均通过显式 scope 创建和释放；无数据库注册测试位于不加入数据库 collection 的独立测试类。
- **T03 Logging 自包含**：测试使用 `HostBuilderContext.Configuration` 和 host-owned Serilog logger，默认使用内存 sink；移除固定 D 盘路径、默认 Seq、静态 logger 与 `ProcessExit` 清理。文件 sink 测试使用专属临时目录，并验证 Host 关闭后文件可清理。
- **T04 AOP 取消传播（Round 1 修正）**：在现有 UOW 拦截器中继续调用并等待真实目标/提交结果，将取消异常作为故障任务传回 AspectCore 3.0.0，保留异常和令牌，取消时不执行后续提交后回调；不替换全局代理或升级依赖。已移除按参数预取消状态提前拒绝目标的策略。
- **T04 EventBus 退避（复用）**：任务起点快照已包含 `Environment.TickCount64`、三次尝试和 Stop Host 后相邻间隔至少 100 ms 的确定性测试。本轮未改 EventBus；复用先前测试结果，不将其描述为本轮新改动。

关键测试追溯：Round 1 AOP 覆盖原始 `Cancellation_ShouldNotCommit`、四种 no-token 已取消返回、目标内取消、快速/异步提交取消、忽略已取消令牌、预取消令牌下的普通业务异常和 source-backed ValueTask 单次消费；预取消参数的四种返回测试仍保留，但不单独作为闭环依据。EventBus 测试为 `OrderedEventConsumerTest.HostedWorker_WhenHandlerConstructionKeepsFailing_ShouldBackoff`（本轮复用）；MySQL 独立注册测试为 `MySqlServiceRegistrationTest.ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection`；SQL Server 事务回归为 `SqlServerExecutionContractTest.Transaction_WhenCompletedOrRolledBack_ShouldKeepExpectedVisibility` 和 `Transaction_WhenScopeIsDisposedWithoutCompletion_ShouldRollbackChanges`。完整方法级映射见 CSV。

## 真实数据库验收

仅启动专属 Compose project `bing-full-suite-recovery`，使用回环动态端口：MySQL `mysql:8.4` / 8.4.11 / 50754，PostgreSQL `postgres:16` / 16.14 / 50753，SQL Server `2022-latest` / 16.0.4265.3 / 50752。脱敏身份、镜像版本及端口记录在 `artifacts/environment/provider-environment.json`，不含凭据。

| 验收项 | net6.0 | net8.0 |
| --- | ---: | ---: |
| MySQL provider runner（含跨库用例） | 59/59 通过 | 59/59 通过 |
| SQL Server provider core | 22 通过、1 可选用例跳过 | 22 通过、1 可选用例跳过 |
| 三 Provider `MultiProviderQueryTest` 独立进程 | 1/1 通过 | 1/1 通过 |

MySQL 的跨库专项已实跑。PostgreSQL 只参与三 Provider 混合测试，没有扩展为 PostgreSQL 全套专项；SQL Server 的全局门控仅在 `MultiProviderQueryTest` 专用子进程内开启。MySQL 与 SQL Server provider TRX 的测试结果均符合上述计数；runner 随后的 release evidence 校验因要求 clean source 而返回非零，工作区当时保留着未提交改动，因此不报告 release matrix PASS。

## Round 0 历史验证结果

Release solution build：`dotnet build Bing.All.slnx -c Release --no-restore --nologo`，0 错误、118 警告。最终 L4 仅执行一次，清除全局集成门控和连接串、不传 `--settings`，命令为：

```powershell
dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/full-suite-recovery/l4/446a8a4cccda4e3494971a3ef67932c8
```

进程退出码为 0，103 个 TRX 的计数如下。`NotExecuted` 单独统计，不作为通过。

| TFM | 总数 | 通过 | 失败 | 未执行 |
| --- | ---: | ---: | ---: | ---: |
| net6.0 | 7,075 | 6,487 | 0 | 588 |
| net8.0 | 6,535 | 6,018 | 0 | 517 |

该 L4 是 Round 0 的历史检查点，运行时早于本轮 AOP 生产代码修复；这些计数只描述修复前候选，不代表当前工作区 binary 的全量结果。本轮按 Review Fix 范围仅运行 AOP L1→L2，没有重跑 L4、Provider 测试或重建数据库；数据库与 Provider 源码未变，旧证据按计划复用。

Round 0 本地 L2 专项：AOP 111/111、EventBus 385/385、Logging 13/13、SQLite 232/232，以上每个 TFM 均全部通过；这些 AOP 计数属于修复前检查点。Review Fix Round 1 当前 L1/L2 新计数见下方专节。MySQL 与 SQL Server 默认门控也单独验证：MySQL 每个 TFM 为 1 通过、58 未执行；SQL Server 每个 TFM 为 4 通过、19 未执行。

Round 0 L4 的 1,105 项未执行按 TRX 程序集分类：每个 TFM 的 MySQL 58、PostgreSQL 41、SQL Server 19；SQLite evidence-only 1；Redis/缓存/FreeSQL/EasyCaching 集成 net6.0 为 404、net8.0 为 369；其他可选集成或环境门控 net6.0 为 65、net8.0 为 29。Round 0 原始列表见 `artifacts/final-local-suite-skips.csv`；这 1,105 项均未计入通过数。当前候选的独立 skip 列表见最终检查点专节。

## 原失败闭环与资源清理

原始失败清单共 net6.0 90 项、net8.0 91 项。Round 0 曾将第 25、26 项映射到不充分的替代路径；追加 Review Fix 后改由原始无参 `Task.Yield` 后抛取消回归证明。CSV 181 项逐项保留证据映射；当前映射与整体 L4 结果分开报告，不能把行级已有 Passed 证据解读为整套 L4 通过。之前的八项预取消参数测试继续保留，不能替代真实管线路径。MySQL 注册测试按职责迁移后的独立测试类名映射，SQL Server 事务项由专门 L1 TRX 覆盖；详见 `artifacts/original-181-closure.csv` 与本报告最终检查点专节。

专属 Compose project 已执行 `down --volumes --remove-orphans`：移除 3 个任务容器、1 个网络、2 个数据卷，剩余任务资源为 0。清理证据在 `artifacts/environment/resource-cleanup.json`；清理后原有 6 个容器仍保持运行，见 `artifacts/environment/preexisting-container-state-after-cleanup.json`。现有数据库容器未被操作。

Round 0 历史 TRX 位于仓库根目录 `artifacts/test-results/full-suite-recovery/l4/446a8a4cccda4e3494971a3ef67932c8/`；Provider TRX 位于 `artifacts/provider-test-results/full-suite-recovery/`。当前候选独立复验与最终 L4 结果见文末；`git diff --check` 的最终结果也在那里记录。

## Review Fix Round 1

本轮只处理 FIX-001、FIX-002 及直接测试依赖。Reviewer 的两 TFM probe 显示 AspectCore 3.0.0 的 `RuntimeAspectContext.Complete()` 会先 await 目标返回值，成功后才设置 `ReturnValue`；目标快速取消时，上下文完成任务先变为 Canceled。`AspectActivator` 对已完成取消任务的快路径会跳过等待并读取未赋值结果，因此调用者观察成功。原补丁的参数预取消扫描绕开目标，不能修复实际管线，而且会破坏“忽略 token 正常成功”和“目标抛普通业务异常”的语义。

修复在 `BingUnitOfWorkInterceptor` 内为 `next` 与提交后的完成状态使用 `TaskCompletionSource`：真实目标和提交仍按原顺序执行；捕获的异常通过 `TrySetException` 传递，使 AspectCore 快路径进入 faulted 分支并向调用者传播原取消异常/令牌。异常时不提交或不运行回调；source-backed ValueTask 不在拦截器中转换或二次读取。未改全局代理、依赖版本或第三方源码。

| 验证级别 | net6.0 | net8.0 | 证据 |
| --- | ---: | ---: | --- |
| L1 `UnitOfWorkAdapterTest` | 33 通过，0 失败，0 跳过 | 33 通过，0 失败，0 跳过 | `artifacts/fix-review-round-1-20261022T0722Z-aop/L1-final2/` |
| L2 完整 `Bing.Aop.AspectCore.Tests` | 122 通过，0 失败，0 跳过 | 122 通过，0 失败，0 跳过 | `artifacts/fix-review-round-1-20261022T0722Z-aop/L2-final2/` |

新增正式回归包括 `NoTokenCanceled_*` 四种返回类型、恢复后的 `Cancellation_ShouldNotCommit`、`CancelInsideTarget_ShouldPropagateCancellation`、`CommitAlreadyCanceled_ShouldPropagateCancellationOnce`、`CommitSignalledCancellation_ShouldPropagateCancellationOnce`、`IgnoreCanceledToken_ShouldCompleteAndCommit`、`PreCanceledToken_TargetFailure_ShouldPreserveBusinessException` 和 `SourceBackedValueTaskCancellation_ShouldConsumeResultOnce`。每个目标取消用例断言目标实际执行、Commit=0、Callback=0；提交取消断言提交只尝试一次、Callback=0；忽略 token 的用例断言目标/提交/回调各一次。未以人为 `Task.Yield` 或延长等待规避快速完成。

执行报告当前状态：本轮两项 finding 的实现与验证已完成；整体任务待独立 Sol/high 复审。`review.md` 保留 Reviewer 原始 `NEEDS_FIX` 证据，本轮没有编辑或关闭该报告。数据库、Provider、门控、Logging、SQL、EventBus 均未在本轮改动；本轮未触及 Compose 资源。

## Review Fix Round 2：FIX-003

本轮按用户要求只处理最新 Review 的 FIX-003，不重新打开 FIX-001/FIX-002。根因由 Round 2 source-confirmation 明确：AspectCore 3.0.0 的 `RuntimeAspectContext.Complete()` 等待原 ValueTask 后仍把原结构赋给 `ReturnValue`；随后 `AspectActivator.InvokeValueTask<TResult>` 又读取它。单次消费 source 的成功返回因此在 Target→Commit→Callback 后被再次读取并抛错。该失败在任务起点也存在，不归因为 Round 1 的取消补丁。对应 probe 和源确认保存在 `artifacts/reviewer-round-2/`。

在现有 UOW 拦截器内增加私有 `UnitOfWorkAspectContext` 适配器。它仅在目标返回 `ValueTask` / `ValueTask<T>` 时接管终端 `Complete()`：目标方法调用一次，将原值任务转为 Task 并等待一次，再把同一个 Task 包回对应 ValueTask 供 AspectCore 上游读取；`T` 通过原 `Task<T>` 保留。其它返回类型仍委托原上下文的 `Complete()`。同步目标异常从 `TargetInvocationException` 还原原异常；异步异常/取消由 `AsTask()` 原样完成传播，之后的 UOW 提交和回调继续走原链。适配器转发上下文属性与 `next` 链，不改全局代理、依赖版本、第三方源码、公开 API 或数据库路径；没有吞异常、替换默认结果或增加延迟。

测试使用实现 `IValueTaskSource` 与 `IValueTaskSource<int>` 的真实单次消费源，覆盖非泛型/泛型 ValueTask × 已完成/信号完成四条成功路径。四项均断言 GetResult=1、目标/提交/回调各一次且顺序为 Target→Commit→CommitAfter；泛型返回值精确为 123。source-backed 非泛型和泛型取消均断言原 `OperationCanceledException` 实例及 token、GetResult=1、零提交/回调。Round 1 的取消、业务异常、忽略 token、scope 与回调场景由完整 L2 再次覆盖。

| 验证级别 | net6.0 | net8.0 | 证据 |
| --- | ---: | ---: | --- |
| L1 `UnitOfWorkAdapterTest` | 38 通过，0 失败，0 跳过 | 38 通过，0 失败，0 跳过 | `artifacts/fix-review-round-2-20261022T0755Z-aop/L1-final/` |
| L2 完整 `Bing.Aop.AspectCore.Tests` | 127 通过，0 失败，0 跳过 | 127 通过，0 失败，0 跳过 | `artifacts/fix-review-round-2-20261022T0755Z-aop/L2-final/` |

L1/L2 TRX 计数已从 XML `Counters` 复核，新增六个 source-backed 成功/取消方法在两 TFM 均 Passed。net6 构建只显示 SDK 对 net6.0 的 `NETSDK1138` 生命周期提示，没有新增 nullable 编译警告。执行命令均使用 Release、`--no-restore`，运行前移除进程级 `RUN_INTEGRATION_TESTS`；无数据库、Provider 测试或全量 L4。

原 181 项 CSV 未修改。AOP #25/#26 仍映射原始 `Cancellation_ShouldNotCommit` 的 Round 1 同名方法与 TRX；本轮新增 source-backed 回归是 FIX-003 的直接证据，不冒充原始 181 项之一。MySQL/SQL Server/PostgreSQL、Logging、SQL 与 EventBus 源范围未变，前轮 Provider / 资源清理证据按 `REUSED_UNCHANGED_SCOPE` 复用；本轮 EventBus 修复也仅复用任务起点已有测试。

### Round 2 汇总

- Fix Scope：按用户指令限于 FIX-003。
- 完成实现与验证：FIX-003 局部 ValueTask 结果转交及 source-backed 成功/取消回归。
- 保持状态：FIX-001、FIX-002 继续按 Review Round 2 结论为 CLOSED；不改 `review.md`。
- Review 状态：`review.md` 仍为 `NEEDS_FIX`，FIX-003 状态由独立 Reviewer 在 Round 3 决定；本报告不宣称该 finding 已被 Review 关闭。
- 在 Round 3 收口时，当前候选最终 Release + 默认 L4 尚待独立复审；随后 Sol/high 复验通过，现已执行并在文末记录结果。Round 0 旧 L4 仍仅为修复前检查点。
- 下一步：交父任务安排同一 Sol/high 第 3 轮独立复审；不启动数据库、不重跑 L4。

## Round 3 后限定追加 Fix：FIX-003、FIX-004

### 授权、状态与影响分析

第三轮 Review 后，用户在 `continuation-approval.md` 明确同意一次限定追加 Fix → 独立 Review；此授权不重置三轮自动循环，也不授权实施者安排 Reviewer。本次继续使用 review-fix 状态，注册器报告已有活动状态并复用原 `reviewRound=3`，没有递增轮数。只处理 FIX-003、FIX-004；FIX-001、FIX-002 保持 Sol 已确认的 CLOSED 行为。`review.md` 未修改，FIX-003/004 的 Review 状态仍由独立复审决定。

变更影响分析：生产文件为 `Bing.Ddd.Application.AspectCore` 的局部 UOW 拦截器，测试文件为其 AOP 测试项目；未改变公共 API、依赖、Provider、数据库、TFM、构建打包、API baseline 或性能预算。运行时影响限于 UOW 标记 ValueTask 的结果转交和目标调用分派；net6.0/net8.0 均直接受影响。风险为 MEDIUM。测试前先执行两个 FIX 的失败回归，再按 L1 → L2 验证。

### FIX-003：嵌套 scope 下 source-backed ValueTask 单次消费

Round 3 的 source-confirmation 与 probe 证明，默认嵌套 scope 会抑制内层 `IScopeInterceptor`，所以只依附提交拦截器的结果兼容无法运行；内层 source-backed ValueTask 会被上游再次 `GetResult`。本次为每个带 UOW 标记的 ValueTask 方法增加独立于 scope 提交拦截器的私有结果适配器；原提交拦截器仍保留原 scope 规则。适配器通过 AspectCore 的 `MethodReflector` 调用目标，将 source-backed ValueTask 转成可重读 Task 一次，在 await 前把该 Task-backed ValueTask 写回上下文，避免成功和取消路径再次消费源。

先运行的两 TFM 红回归均命中 nested source-backed 场景；当时四种嵌套组合调用失败或被 source 的二次消费保护捕获。正式回归覆盖无泛型/泛型 × 已完成/信号完成，均断言内外目标各一次、source `GetResult=1`、一个外层提交/回调及顺序正确，泛型结果精确为 123。原直接 source 成功/取消、异常/token 与 scope 回归继续保留。

### FIX-004：类代理虚方法终端分派

Round 3 证实 `MethodInfo.Invoke` 会对生成的类代理虚方法再次做虚分派：默认 scope 下外围 Before/After 重入，`Scope.None` 下持续进入代理链。适配器现用 AspectCore `MethodReflector` 及其终端 `Call`/`Callvirt` 约定：类实现方法用非虚 `Call` 直接到真实目标；显式接口私有 final 虚实现和接口声明保留 `Callvirt`。这避免代理重入并保留参数数组、目标异常和上下文/next 转发，不改全局代理或第三方实现。

两 TFM 正式回归覆盖虚方法类代理的默认及 None scope，结果 123、Before/After/目标/提交/回调各一次；取消令牌与业务异常回归验证异常语义且不提交。显式接口另有正式测试。独立 context probe 在两 TFM 的 13 个模式全部通过，包含 ForwardTask、ValueTask、Break、ref/out、闭合泛型、显式接口、原异常、source fault/cancel、普通及 source-backed 嵌套、虚方法默认 scope 和 None；上下文对象/元数据关联保持，scheduler 调用结束后无残留。

### 验证与证据

| 验证 | net6.0 | net8.0 | 结果文件 |
| --- | ---: | ---: | --- |
| L1 `UnitOfWorkAdapterTest` | 49 Passed / 0 Failed / 0 未执行 | 49 Passed / 0 Failed / 0 未执行 | `artifacts/fix-review-continuation-round-3-20261022T082941Z-aop/L1-final-current/` |
| L2 `Bing.Aop.AspectCore.Tests` | 138 Passed / 0 Failed / 0 未执行 | 138 Passed / 0 Failed / 0 未执行 | `artifacts/fix-review-continuation-round-3-20261022T082941Z-aop/L2-final-current/` |
| 当前候选 context probe | 13/13 Passed | 13/13 Passed | `artifacts/fix-review-continuation-round-3-20261022T082941Z-aop/context-probe/context-probe-net{6.0,8.0}-current.json` |

计数由四份 L1/L2 TRX 的 XML `Counters` 与单项 `UnitTestResult` 核对；每份均零失败、零跳过/未执行。原 181 映射第 25/26 项 `Cancellation_ShouldNotCommit` 已指向当前候选 L2 的同名实际 Passed 结果：net6.0 为 `.../L2-final-current/net6.0/jianx_DESKTOP-8CUU71G_2026-10-22_16_47_45_net6.0.trx`，net8.0 为 `.../L2-final-current/net8.0/jianx_DESKTOP-8CUU71G_2026-10-22_16_48_01_net8.0.trx`。历史 Round 1/2 报告和 TRX 均保留。

数据库、Provider、门控、Logging、SQL 与 EventBus 源范围均未变化；既有真实 Provider/资源清理结果按 `REUSED_UNCHANGED_SCOPE` 复用，本轮未启动数据库、未重跑 Provider 或 L4。Round 0 的 103 份旧 L4 仍仅是修复前检查点。追加 Fix 的代码与直接测试已完成，但 FIX-003/004 尚无本轮独立 Review 结论；待同一 Sol/high 限定复审，若通过再由父任务安排当前候选 Release 构建及默认 L4。没有宣称 181 项整体语义闭环或当前全量通过。

### 本次收口

- 执行范围：用户授权的单次追加 Fix，仅 FIX-003、FIX-004；不启动新的自动 Review/Fix 循环。
- FIX-003：实现与 L1/L2 验证完成，等待独立 Review。
- FIX-004：实现与 L1/L2 验证完成，等待独立 Review。
- FIX-001、FIX-002：保持 CLOSED，不重新打开。
- `review.md`：保持 Reviewer 独立证据，未修改。
- 本节记录 Round 3 当时的待办状态；随后独立 Review PASS，当前候选 Release + 默认 L4 结果见下方最终检查点。
- Git：未暂存、未提交、未推送；无外部通知。

## 上一候选最终 Release 与默认 L4（历史检查点）

### 检查点与候选身份

Sol/high 独立复验为 `IMPLEMENTATION_STATUS=PASS`、`OPEN_ACTIONABLE=0`、FIX-001..004 全部 CLOSED。按用户原计划和 `continuation-approval.md` 授权，于 2026-10-22 刷新最终检查点。此前 3 次 runner 预检错误都发生在 build/test 前；原因、纠正及记录见本检查点 evidence directory 的 `runner-preflight-attempt-{1,2,3}.log`。之后实际 Release build 与默认 L4 各执行一次，没有重试测试。

工作区 HEAD 为 `5065c38ced05a95088e17d89d96c9a1610e37159`，SDK 为 `10.0.400`。复验通过的生产源 SHA256 为 `4C4DB60B5092FC14FDE8F23AA6E7FD429BE4D35C31C1F760A0016F2843C927A6`，AOP 正式测试源 SHA256 为 `87069F7AF7BD229761E3153CC2081954A76F93417D35225D1BDDBC1FE2D70E20`，运行前后匹配。net6/net8 Release 输出中的生产 DLL 哈希均为 `2DA882C4E8BB17B7268624CEABE6C6FB46C8ED35CAECFD3014D0EF3EC5DDFE64`；AOP 测试 DLL 哈希分别为 `5A1532CBF1B6CFC4AE10EF5A5CA0A979ED379D23DF536AB2CCC491007899A592`、`CEC55FC0A6098C5CE1E7C7C97325D7BD9A8EB039BB95EB41A8BE8A390B743A6F`。逐项身份见 `candidate-binary-identities.json` 与 `run-manifest.json`。

本轮 CIA 已在报告开头记录：生产和业务测试源码不变，新增/更新的只有 runner、XML 汇总与覆盖核验脚本、证据、CSV 和任务报告。扫描 `Bing.All.slnx` 共 159 个项目，MSBuild 成功评估 159 项，其中 `IsTestProject=true` 为 54 项；net6.0/net8.0 共 103 个项目-TFM 对。test stdout 记录了 103 次项目调用，103 份 TRX 对应全部项目-TFM 对；97 对发现了测试，另有 `Bing.Localization.Resources`、`Bing.TestShare.MySql`、`Bing.TestShare` 各自两 TFM 没有可发现测试，并产生空 TRX。覆盖差异为零，详细核验见 `solution-test-project-coverage.json`。

门控在独立 PowerShell 子进程中显式设置为 false；连接串、`BING_TEST_`、`BING_INTEGRATION_`、`BING_CAP_` 等进程环境配置被移除，`RUN_METADATA_PATH` 清除，环境设为 Production。四个 Provider 集成项目均无隐式 runsettings 引用；命令未传 `--settings`，没有覆盖 xUnit 默认并行设置。SQLite 本地集成仍实跑：每个 TFM 231 Passed、1 evidence-only test skipped。没有启动数据库或外部服务；此前真实 Provider TRX 与 Compose 清理证据按未变范围复用。

### Release Build 与默认 L4

| 检查 | 命令 | 开始时间（+08:00） | 结束时间（+08:00） | 退出码 |
| --- | --- | --- | --- | ---: |
| Release solution build | `dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1` | 2026-10-22 17:18:17.079 | 2026-10-22 17:18:51.686 | 0 |
| 默认 solution L4 | `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <本次唯一 l4-trx 目录>` | 2026-10-22 17:18:51.700 | 2026-10-22 17:19:41.984 | 1 |

Build 完成 159 个 solution 项目，0 error、118 warnings，使用 `-m:1` 避免输出竞争。默认 L4 退出码为 1；没有 filters、没有 runsettings、没有重试。103 份 TRX 按 `UnitTestResult` outcome 汇总如下。TRX 的 `ResultSummary/Counters.notExecuted` 原始字段为 0，但测试项 outcome 明确含 1,105 个 `NotExecuted`；本报告据逐项 outcome 统计跳过，且 `total = passed + failed + skipped` 完全相符。原始 TRX 未修改。

| TFM | 总数 | 通过 | 失败 | 未执行/跳过 |
| --- | ---: | ---: | ---: | ---: |
| net6.0 | 7,102 | 6,513 | 1 | 588 |
| net8.0 | 6,562 | 6,045 | 0 | 517 |
| 合计 | 13,664 | 12,558 | 1 | 1,105 |

唯一失败是 net6.0 `Bing.Data.Sql.Tests.EntityMappingCacheIsolationTest.Resolve_WhenConcurrentMetadataFailureIsRetried_ShouldAllowAllWaitersToRetry`：等待者数断言期望 8、实际 7，定位在 `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs:410`。当时该轮只保留诊断，未修改业务测试或开启修复循环。完整错误与堆栈见 `final-local-suite-failures.csv`、`test.stdout.log` 和失败 TRX。逐项跳过在 `final-local-suite-skips.csv`；逐文件计数在 `trx-inventory.json`；原始 stdout/stderr 和每份 TRX 均保存在唯一 evidence directory `artifacts/final-candidate-release-l4-20261022T091033Z/`。

### 原 181 项映射

原始 181 行均保留 `CLOSED_PASSED` 状态，但这表示每项有其相应的实际 Passed 证据，不代表当前整套 L4 通过。29 项已更新为当前候选 L4 中精确 TFM、程序集、方法名匹配的 Passed TRX（Logging 24、AOP 2、EventBus 1、MySQL scope 2）；148 项外部 Provider 行保持已独立验收的真实 TRX（MySQL 116、SQL Server 30、MultiProvider 2）；4 项当前外库门控跳过的 SQL Server 事务用例继续指向其真实 SQL Server L1 Passed TRX。映射总数和类别核验见 `original-181-refresh.json` 与 `artifacts/original-181-closure.csv`。EventBus backoff 测试为任务起点已有修复，本轮复用源与既有证据；新 L4 为其补充当前 Passed 结果。

### 结论与状态

独立实现审查 PASS、四项 FIX 全部 CLOSED；数据库验收与资源清理证据未变并继续有效。当前候选 Release build PASS，但默认全量 L4 因上述单项失败而 FAIL，所以本次最终全量不是 PASS。正式 Release evidence 门禁仍为 `BLOCKED`：clean-source 条件未满足；本轮按授权不提交、不清理工作区，也不放宽门禁。`review.md` 保持独立审查原件，未修改。

## 用户授权追加执行：元数据并发重试测试

### 授权、状态与影响分析

用户在 `cache-retry-continuation.md` 明确授权仅处理最终 L4 唯一失败的 `Resolve_WhenConcurrentMetadataFailureIsRetried_ShouldAllowAllWaitersToRetry`，并要求给出功能建议。最新 `review.md` 仍为 PASS、`OPEN_ACTIONABLE=0`，没有改写或伪造 NEEDS_FIX。本次按 `execute-plan` 语义重新登记为 `plan-execution`；这是用户批准的限定执行阶段，不重置此前 Review/Fix 轮数。

测试前 CIA：ChangedFiles=`framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`；ChangedProjects=`Bing.Data.Sql.Tests`；ChangedPublicContracts=无；ChangedRuntimePaths=无；ChangedProviders=无；ChangedTFMs=`net6.0`、`net8.0`；ChangedBuildPackaging=无；ChangedBenchmarkHarness=无；ChangedDocsOnly=否；AffectedDependents=元数据缓存职责测试及其完整测试项目；RiskLevel=MEDIUM。生产 resolver 未改，未启动数据库或外部服务。

新证据目录为 `artifacts/cache-retry-followup-20261022T094923Z/`。`baseline-manifest.json` 记录了本次改动前测试源、生产 resolver 源及原失败 TRX/清单的 SHA256 快照；原 L4 失败 TRX 也完整复制为 `original-net6-failure.trx`，历史目录中的原文件未覆盖。

### 根因、修改与设计建议

`DefaultEntityMappingResolver.GetCachedModelMetadata` 通过 `ConcurrentDictionary<RuntimeTypeHandle, Lazy<EntityModelMetadata>>` 的 `ExecutionAndPublication` 共享单次加载，并只在缓存仍持有完全相同的 key/value 时移除失败 Lazy。原测试的 `ready.Signal()` 发生在进入 resolver 之前；`FirstCallEntered` 只证明首个 provider 调用已阻塞，随后立即释放并不能证明其余 7 个调用已取得旧 Lazy。旧 L4 的 7 次异常、1 次迟到成功符合该并发语义，不构成生产缓存缺陷。

测试现在为每个并发调用收集最终映射和实际尝试次数。只有观察到指定 `InvalidOperationException` 的调用才执行一次显式业务重试；其它调用可直接成功。断言确认 8 个调用最终返回同一映射、至少一次受控失败被实际传播、观察到失败的调用与显式重试一一对应、provider 总加载严格两次，随后缓存命中不再加载。另增 `Resolve_WhenCallerArrivesAfterMetadataFailureIsRemoved_ShouldLoadSuccessfully`：先等待首次失败从 resolver 传播完毕，再发起迟到调用，验证失败项已移除且后续加载与缓存命中成功。同步等待均有截止，finally 释放信号并汇合任务，helper 的同步对象在任务完成后释放。

功能建议：保留“失败向首次调用者传播、失败 Lazy 按 key/value 精确移除”的库内契约；是否重试由调用方业务策略决定。调用方选择重试时，应针对明确可恢复的失败执行有界的一次业务重试，避免 resolver 静默吞掉首次故障或把永久错误转成隐式重复工作。并发测试应检验观察到的失败、重试和最终缓存身份，不应假定同时放行就代表每个调用者取得了同一个 Lazy。当前无证据要求修改生产缓存实现。

### 验证证据

使用 SDK `10.0.400`、Release、`--no-restore`。下列命令未传 runsettings；L1 只过滤 `EntityMappingCacheIsolationTest`，L2 覆盖整个 `Bing.Data.Sql.Tests` 项目。每项只运行一次，无测试框架重试。

| 级别 / TFM | 命令 | 退出码 | 结果 | TRX |
| --- | --- | ---: | --- | --- |
| L1 / net6.0 | `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --filter FullyQualifiedName~EntityMappingCacheIsolationTest --logger 'trx;LogFileName=cache-retry-focused-net6.0.trx' --results-directory <evidence>/L1/net6.0` | 0 | 25 通过、0 失败、0 未执行 | `L1/net6.0/cache-retry-focused-net6.0.trx` |
| L1 / net8.0 | `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --filter FullyQualifiedName~EntityMappingCacheIsolationTest --logger 'trx;LogFileName=cache-retry-focused-net8.0.trx' --results-directory <evidence>/L1/net8.0` | 0 | 25 通过、0 失败、0 未执行 | `L1/net8.0/cache-retry-focused-net8.0.trx` |
| L2 / net6.0 | `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --logger 'trx;LogFileName=Bing.Data.Sql.Tests-net6.0.trx' --results-directory <evidence>/L2/net6.0` | 0 | 1,318 通过、0 失败、0 未执行 | `L2/net6.0/Bing.Data.Sql.Tests-net6.0.trx` |
| L2 / net8.0 | `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --logger 'trx;LogFileName=Bing.Data.Sql.Tests-net8.0.trx' --results-directory <evidence>/L2/net8.0` | 0 | 1,318 通过、0 失败、0 未执行 | `L2/net8.0/Bing.Data.Sql.Tests-net8.0.trx` |

所有计数已由 TRX XML `Counters` 与 `UnitTestResult` 重核；四份当前候选 TRX 均确认并发重试方法与迟到调用方法 Passed。完整命令输出在每个对应 `console.log`，时间、源码/二进制身份和逐项摘要在 `verification-summary.json`。生产源 SHA256 与起点一致；只有测试文件和本任务执行/最终报告变化。

最终生产符号到测试方法映射：`DefaultEntityMappingResolver.GetCachedModelMetadata` → `Resolve_WhenConcurrentMetadataFailureIsRetried_ShouldAllowAllWaitersToRetry`、`Resolve_WhenCallerArrivesAfterMetadataFailureIsRemoved_ShouldLoadSuccessfully`、`Resolve_WhenModelMetadataProviderFailsOnce_ShouldRemoveFailureAndAllowRetry`、`Resolve_WhenMappingIsCached_ShouldNotCallModelMetadataProviderAgain`；`RemoveModelMetadataCacheEntryIfCurrent` → `RemoveModelMetadataCacheEntryIfCurrent_WhenValueIsStale_ShouldKeepCurrentValue`。前两个方法具有本轮 net6/net8 L1 与 L2 Passed TRX；后两个及 stale-entry 保护在同一份完整 L2 TRX 中通过。

### 收口状态

本次限定范围的测试修复及 L1/L2 已完成；生产实现不变。此次 L4 唯一失败不计入也不增加原 181 项闭环映射，`artifacts/original-181-closure.csv` 保持 181 项。`review.md` 未改；本段记录该限定测试阶段完成时等待独立复验与最终 L4 的状态，后续结果见下节。之前的 Release build/L4 仍是上一候选检查点，L4 退出码 1 的历史结果保持记录；没有重跑 AOP、Provider、性能或数据库测试。未暂存、提交、推送或发送外部通知。

## 缓存追加复验后的最终 Release 与默认 L4（当前候选）

### CIA 与候选身份

用户授权见 `cache-retry-continuation.md`。Sol/high 独立复验已给出 `IMPLEMENTATION_STATUS=PASS`、`CACHE_RETRY_STATUS=PASS`、`OPEN_ACTIONABLE=0`，FIX-001..004 全部 CLOSED；本次只执行最终检查点，不重开 finding。CIA：本轮业务生产/测试源码冻结；新增内容为本目录的 runner/汇总/覆盖/方法核验脚本、原始输出与 TRX、映射刷新记录和任务报告。最终 build/L4 影响整个 `Bing.All.slnx`，覆盖 net6.0/net8.0；风险 HIGH。未启动或操作数据库容器。

当前候选 HEAD=`5065c38ced05a95088e17d89d96c9a1610e37159`，SDK=`10.0.400`，安装并用于目标测试的 .NET runtime 包含 6.0.36、8.0.27/8.0.30。相对上一失败检查点，候选已因用户授权的缓存测试改动而变化；当前缓存测试源码 SHA256=`122F84838B2DC3499FA0DA3472E3452FBB0FE6453C8EDCE1C8D2332940D5F15C`，与 Sol 复验身份一致。生产 resolver 源码未变，SHA256=`BE4E43C6CD015CFCF4B93019D2C960B381BB9A333C285435E59EA2E0A3BD453E`；AOP 生产/测试源哈希也与独立复验一致。缓存测试程序集 net6.0/net8.0 SHA256 分别为 `DBDC7B93E8F5BD1369072189B66E001C0CC5991983CB8EBEC8CB86B3490AFA60`、`9E557E51377A72DA1690DA788E903EC1DB7DF465C42D733BFB000E04160F513D`。完整候选与二进制身份在本目录 `run-manifest.json`。

### 环境、Build 与 L4

沿用上一已修正 runner，在独立 PowerShell 子进程显式将源代码与继承环境中的 `RUN_*` 外部 Provider/CAP/cache 门控设为 `false`，清除 `RUN_METADATA_PATH` 及 `BING_TEST_`、`BING_INTEGRATION_`、`BING_CAP_`、连接串和 Provider 配置环境变量；报告只保存环境键名，不含值。`ASPNETCORE_ENVIRONMENT` 与 `DOTNET_ENVIRONMENT` 为 Production。runner 检查四个 Provider 集成项目均无隐式 runsettings 引用；没有传入 settings、没有覆盖 xUnit 并行选项。SQLite 本地测试保持启用。门控和清理键完整清单见 `run-manifest.json`。

| 检查 | 命令 | 开始 / 结束（+08:00） | 退出码 |
| --- | --- | --- | ---: |
| Release solution build | `dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1` | 2026-10-22 18:13:27.716 / 18:14:05.290 | 0 |
| 默认 solution L4 | `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <本目录>/l4-trx` | 2026-10-22 18:14:06.109 / 18:15:14.001 | 0 |

Build 为 0 error、118 warnings。L4 原始 stdout/stderr、103 份 TRX、SDK/runtime 清单及精确开始/结束时间均保存在 `artifacts/final-candidate-cache-retry-release-l4-20261022T101104Z/`。逐份 TRX 的每个 `UnitTestResult` outcome 汇总如下，`NotExecuted` 不计入 Passed：

| TFM | UnitTestResult | Passed | Failed | NotExecuted/Skipped |
| --- | ---: | ---: | ---: | ---: |
| net6.0 | 7,103 | 6,515 | 0 | 588 |
| net8.0 | 6,563 | 6,046 | 0 | 517 |
| 合计 | 13,666 | 12,561 | 0 | 1,105 |

项目覆盖核验评估 solution 中 159/159 项目，54 个测试项目、103 个项目-TFM 对全部出现于 runner 输出并有 103 份 TRX；97 个项目-TFM 对发现测试，三个资源/共享测试项目的六个 TFM 对产生空结果，缺失调用和结果均为零，`coverageComplete=true`。SQLite 集成每个 TFM 实际 231 Passed、0 Failed，另 1 项 evidence-only skip。AOP 项目每个 TFM 138/138 Passed；`key-regression-results.json` 对缓存两个回归、AOP 25 个方法及理论用例（共 54 个指定 TFM 检查）逐个匹配 TRX，全部 Passed。当前全量失败清单为空；完整 1,105 项未执行清单在 `final-local-suite-skips.csv`，不得计为通过。

### 原 181 项与边界

`artifacts/original-181-closure.csv` 仍为 181 行，全部保持 `CLOSED_PASSED`，没有增加第 182 项。当前 L4 精确刷新 29 个本地原失败方法的 Passed TRX；148 个 MySQL/SQL Server/MultiProvider 真实外库结果保持已独立验收的 TRX；4 个 SQL Server 事务测试因默认外部门控而在 L4 未执行，继续引用其真实 SQL Server L1 Passed TRX。逐项记录及未执行的四行见 `original-181-refresh.json`。缓存两项属于本次追加验收范围，单独记录其当前 L4 与 L1/L2 证据，不混入原 181。

真实 MySQL、SQL Server、三 Provider 混合测试以及本任务 Compose 资源清理证据按未变范围复用；本次未启动数据库。未运行外部 CI 或 IDE 验证；L4 中所有 skip/NotExecuted 仍未执行，不宣称通过。最终本地 Release build 和默认 L4 均 PASS。正式 clean-source Release evidence 门禁仍为 `BLOCKED`，本轮未提交、清理工作区或放宽门禁。
