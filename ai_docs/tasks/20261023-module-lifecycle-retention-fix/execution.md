<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261023-module-lifecycle-retention-fix
AI_EXECUTION_FINISHED_AT: 2026-10-23T18:57:00+08:00

# 实施执行报告

## 执行结论

实现与测试已通过 Review2（FIX-001 CLOSED，OPEN_ACTIONABLE=0），Admin InventoryProbe 构建恢复也通过 Sol/high 专项验收。原 checkpoint 的 `CS2012` 失败原样保留；在两个 csproj 修复后的新候选中，`dotnet build Bing.All.slnx -c Release --no-restore` exit 0，默认全量测试 exit 0，14008 个 UnitTestResult 为 0 Failed（net6.0 6722 Passed/593 NotExecuted；net8.0 6172 Passed/521 NotExecuted）。当前状态为“实现 PASS、Review PASS、构建恢复 PASS、本地 build/默认全量 PASS、外部系统未验证”；不宣称修复 DI6 的通用泄漏或改写历史归因。

## 任务信息

- 任务：`20261023-module-lifecycle-retention-fix`。
- 实施输入：[plan.md](plan.md)；根因依据：[Astra 调查](../20261023-module-lifecycle-gc/astra-diagnosis.md)第 5 节及其未应用补丁。
- 起始 HEAD：`3919c1509907d661cfef3679dad79368c8b9cb39`。
- 起始工作区不干净：任务开始前已存在 `BingModuleManager.cs` 之外的 GC 采证/测试改动、旧任务文件和无关 whitespace 差异；本任务未 reset/restore/clean，保留这些修改。
- 起点源码、旧 `Bing.Core`/`Bing.Core.Tests` Release 二进制及哈希保存在 `artifacts/startpoint/manifest.json` 与 `artifacts/startpoint/files/`。
- 最终相关源码/二进制身份保存在 `artifacts/final-identity.json`。
- 未修改旧 GC 调查、Astra 原始 artifact、旧 review、Stage B 或父路线图。

## 计划执行情况

| 计划项 | 状态 | 说明 |
|---|---|---|
| 保存起点与二进制身份 | COMPLETED | `artifacts/startpoint/` |
| 惰性缓存 `IServiceScopeFactory`，初始化/关闭共用 | COMPLETED | 仅改 `BingModuleManager`，不新增锁或协议 |
| `Create` helper 使用 Recorder singleton factory | COMPLETED | 保持实例身份，避免第二次解析触发点 |
| 直接回归覆盖解析一次、两 scope、异常/取消/Dispose、自定义 provider | COMPLETED | 新增 `ModuleLifecycleRetentionTest` |
| 复制 Astra 探针并做两项原测试 TFM 确认 | COMPLETED | `artifacts/astra-probe-final/candidate/summary.json` |
| 完整 Core.Tests net6/net8 Release | COMPLETED | 各 645 passed |
| 直接消费者回归 | COMPLETED | `Bing.Tests` net6/net8 各 112 passed |
| 执行报告、traceability、使用边界 | COMPLETED | 本文件与 [traceability.md](traceability.md) |

## 变更摘要

- `BingModuleManager` 新增按应用惰性缓存的 `_scopeFactory` 与私有 `CreateLifecycleScope()`；初始化和显式关闭阶段均从该工厂创建独立 scope。
- `ModuleLifecycleTest.Create<T>` 预先创建 `Recorder`，通过 singleton factory 注册，保持实例身份不变。
- 新增 `ModuleLifecycleRetentionTest`，覆盖正常生命周期、初始化失败、取消清理、provider Dispose、Recorder 身份，以及自定义 `IServiceProvider`/工厂下“只解析一次、两个不同 scope 均释放”的契约。
- 无公共 API、依赖、TFM、构建图、Shutdown 发布协议或数据库 Provider 创建链改动。

## Change Impact Analysis

| 项目 | 影响 |
|---|---|
| ChangedFiles | 1 个生产文件、1 个既有测试 helper、1 个新增测试文件、本任务文档/证据 |
| ChangedProjects | `Bing.Core`、`Bing.Core.Tests`；实际消费者 `Bing.Tests` |
| ChangedPublicContracts | 无；`BingModuleManager` 为 internal |
| ChangedRuntimePaths | 模块初始化/显式关闭的 scope 创建由重复解析改为复用当前应用工厂；provider Dispose 回调仍不创建 scope |
| ChangedTFMs | 本地验证 net6.0/net8.0 Release |
| ChangedBuildPackaging | 无 |
| ChangedProviders | 无；本次不改变 SQL/Provider 创建链，因此未追加数据库测试 |
| RiskLevel | LOW-MEDIUM；低频生命周期路径，需独立 Review 核对缓存边界与测试证据 |

## 测试结果

### 精准回归与直接覆盖

| 项目/TFM | 结果 | 证据 |
|---|---:|---|
| `ModuleLifecycleRetentionTest` / net8.0 Release | 6 passed, 0 failed | `artifacts/review-fix-round-1/l1-net8-final/` |
| `ModuleLifecycleRetentionTest` / net6.0 Release | 6 passed, 0 failed | `artifacts/review-fix-round-1/l1-net6-final/` |

Review1 指出上一版测试只读取缓存字段、手工调用私有 helper 并自行 Dispose，不能证明真实生命周期所有权。本轮已改为通过真实 `IBingModuleManager.Initialize/InitializeAsync` 与 `Shutdown`/provider `Dispose` 观测自定义 provider 的 factory 解析数、scope 身份及释放计数；私有 helper 测试保留但明确标注为独立单元契约。历史 `l1-retention-*` 结果保留为上一轮证据，不再作为 FIX-001 的最终所有权证据。

### 两项原测试的受控确认

| 原测试 | net6.0 | net8.0 | 证据 |
|---|---:|---:|---|
| `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` | 1 passed | 1 passed | `artifacts/candidate-repeated-net6-final/`、`candidate-repeated-net8-final/` |
| `RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` | 1 passed | 1 passed | `artifacts/candidate-retained-net6-final/`、`candidate-retained-net8-final/` |

每个原测试在每 TFM 最终只确认一次；没有因首次结果追跑。原断言未修改，未增加 GC 轮次。

### 复制探针的因果确认

- 探针复制到 `artifacts/astra-probe-final/`，只扩展有限监听为 `IServiceScopeFactory` 与 `Recorder`，没有修改原测试断言；Astra 原始 artifact 未改。
- `artifacts/astra-probe-final/candidate/summary.json`：4/4 `passed=true`，两项原测试在两 TFM 均为 `original-result=Passed`，监听到的 `events` 均为空数组。
- 该探针只对原候选 DLL 执行；Review1 FIX-001 本轮未针对新测试 DLL 重跑探针，不能把零事件当作解析次数或所有权证据。
- 这证明在原候选冻结二进制和受控窗口内，两项原测试通过且未触发这两个后台编译事件；不把它扩大为任意应用或永久泄漏结论。

### 完整与消费者回归

| 项目/TFM | 结果 | 证据 |
|---|---:|---|
| `Bing.Core.Tests` / net8.0 Release | 645 passed, 0 failed, 0 skipped | `artifacts/full-core-net8-final/` |
| `Bing.Core.Tests` / net6.0 Release | 645 passed, 0 failed, 0 skipped | `artifacts/full-core-net6-final/` |
| `Bing.Tests` / net8.0 Release | 112 passed, 0 failed, 0 skipped | `artifacts/consumer-bing-tests-net8-final/` |
| `Bing.Tests` / net6.0 Release | 112 passed, 0 failed, 0 skipped | `artifacts/consumer-bing-tests-net6-final/` |

`Bing.Tests` 是除 `Bing.Core.Tests` 外直接引用 `Bing.Core` 的现有测试项目。没有 SQL/Provider 创建链变化，未无因追加数据库测试；未运行全量 solution，留给主线程最终检查点。

## Build/Typecheck/Lint/Format

- 上述 `dotnet test -c Release` 已完成编译；无编译错误。
- 未发现独立 lint/format 命令，未运行额外类型检查。
- `git diff --check` 仅报告任务开始前已有的无关 trailing whitespace（旧任务 plan 与 EF 测试支持文件）；本任务改动未新增 trailing whitespace。
- net6 构建保留既有 `Microsoft.Extensions.Options 10.0.0` TFM 支持警告；测试源码保留既有 nullable/xUnit analyzer 警告；均未导致失败。

## 计划偏差

1. 真实生命周期用例通过反射构造 internal `BingModuleRegistration`/`BingModuleManager`，但调用真实 `IBingModuleManager` 初始化/关闭及 provider `Dispose`；私有 `CreateLifecycleScope` helper 测试单独保留并明确边界。仅测试代码，无生产契约变化。
2. 两项原测试按“每测试每 TFM 单独一次”执行，共四次进程；没有合并过滤器，避免 PowerShell 管道解析干扰。仍保持每项一次确认，没有压力矩阵。
3. 复制探针后新增本任务 runner；原探针和原 runner 未修改。复制版本只调整监听类型和有界运行目录，未修改原测试方法或断言。
4. 消费者选择 `Bing.Tests`；没有运行 SQL/Provider 套件，因为本次不触碰 SQL 工厂或 Provider 创建链。

## 基线问题

- 任务开始前工作区已有 `ModuleLifecycleTest.cs` 的 GC 采证/`ITestOutputHelper` 改动、`GcCaptureHandshake.cs` 等未跟踪文件，以及多个无关文件差异；本任务只追加 `Create` helper 的 singleton factory 修改，没有覆盖或回退这些内容。
- `BingModuleManager.cs` 的 CRLF 提示为仓库既有换行策略提示，不是本轮功能问题。
- net6 的 `Microsoft.Extensions.Options` TFM 警告和 xUnit/netnullable 警告为既有基线。

## 已知问题

- 本修复只消除框架生命周期自身重复解析 `IServiceScopeFactory` 的触发点；DI6 后台编译闭包在其它代码路径上的通用保留行为未被修改。
- 不承诺任意应用在 `Dispose` 后立即回收，也不声称历史自然失败只有这一个原因。
- 缓存字段在 manager 生命周期内保留工厂；provider Dispose 回调仍不创建 scope，legacy 分支未改变。

## 风险与回归关注点

- 独立 Review 应重点核对初始化与显式关闭的 scope 是否仍彼此独立、异常/取消路径是否仍保留原传播、provider Dispose 路径是否继续避免创建 scope。
- 并发初始化/关闭由现有状态机约束；本改动没有新增锁或改变关闭协议。
- 受控证据只覆盖当前冻结候选二进制；不得把探针的零事件结果表述为性能提升或永久回收保证。

## Reviewer 注意事项

- FIX-001 的真实计数证据见 `artifacts/review-fix-round-1/l1-net8-final/` 与 `l1-net6-final/`；请直接核对 provider 解析数、scope 身份及每 scope 释放计数。
- 原测试断言和 GC 次数未改；`artifacts/astra-probe-final/candidate/summary.json` 仅证明原候选的零监听事件，不能替代本轮新测试 DLL 的计数断言。
- 请区分“候选实现完成”与“历史失败唯一归因”；本报告只支持前者及受控窗口内的因果验证。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`；Review 文件：`ai_docs/tasks/20261023-module-lifecycle-retention-fix/review.md`。
- Fix Scope：`recommended`。
- 本轮范围：仅修改 `ModuleLifecycleRetentionTest.cs` 及本任务 execution/traceability/artifacts；生产源码、原弱引用测试、Astra 资料/工具、旧 review、Stage B 未改。

#### FIX-001

- 严重程度：`MEDIUM`；处理要求：`SHOULD_FIX`；执行状态：`COMPLETED`。
- 根因：上一版正常/失败/取消/Dispose 用例只读取 manager 缓存字段；唯一的计数测试直接反射调用私有 `CreateLifecycleScope` 后由测试自行 Dispose，不能证明 manager 在真实初始化/关闭阶段拥有并释放两个 scope。
- 修复：
  - 新增共享 `LifecycleObservation`，反射构造 internal `BingModuleRegistration`/`BingModuleManager`，但通过真实 `IBingModuleManager.Initialize`/`InitializeAsync`、`Shutdown` 和 provider `Dispose` 执行生命周期。
  - 自定义 `ObservedServiceProvider` 记录 factory 解析次数；`ObservedScopeFactory` 记录每个 scope 身份与创建/释放顺序；`ObservedScope` 记录释放次数。
  - 正常路径断言初始化返回时首个 scope 已释放一次；显式关闭后解析仍为 1、scope 为 2 且不同、两者各释放一次；重复 `Shutdown`/`Dispose` 不增加计数。
  - 初始化钩子已创建 scope 后抛错：保留原 `InvalidOperationException`，失败清理 scope 先释放、初始化 scope 后释放，各一次。
  - 进入初始化后使用 `Entered` 信号再取消，不使用固定 sleep；保留原 OCE，清理与释放顺序同上。
  - 初始化成功后 provider `Dispose` 不创建关闭 scope，解析/创建计数不变。
  - 私有 helper 测试改名为 `PrivateHelperContract_...`，明确只覆盖 helper 单元契约；Recorder 身份只覆盖本测试 helper，不冒充原 helper 证据。
- 验证：
  - `ModuleLifecycleRetentionTest` / net8.0 Release：6 passed、0 failed、0 skipped；`artifacts/review-fix-round-1/l1-net8-final/`。
  - `ModuleLifecycleRetentionTest` / net6.0 Release：6 passed、0 failed、0 skipped；`artifacts/review-fix-round-1/l1-net6-final/`。
  - 失败尝试保留在 `l1-net8-attempt1-compile-failure/`、`l1-net8-attempt2-test-failure/`，不计入最终通过。
- 证据：
  - CIA：`artifacts/review-fix-round-1/cia.md`。
  - 测试起点：`artifacts/review-fix-round-1/startpoint/`。
  - 新测试源码/二进制身份：`artifacts/review-fix-round-1/test-identity.json`。
  - 验证摘要：`artifacts/review-fix-round-1/validation.md`。

### Round 1 汇总

- MUST_FIX：0。
- SHOULD_FIX：1，FIX-001 `COMPLETED`。
- PARTIAL/BLOCKED/FAILED：0。
- 回归验证：仅本轮定向职责用例 net6/net8 Release；生产源码未变，完整 Core、消费者和原候选 Probe 证据按范围复用，未重跑。
- 下一步：交回主线程安排独立复 Review；不自行将 `review.md` 改为 PASS。

## Final Candidate Checkpoint

- 授权范围：Review2 PASS 后唯一一次最终 Release solution build/默认测试检查点；不重跑旧 Probe、性能、外库或 targeted 修复。
- 位置：`artifacts/final-candidate-checkpoint/`；CIA：`cia.md`；冻结：`freeze.json`；结果：`checkpoint-summary.json`。
- 绝对预算：冻结时起 15 分钟；build/test 共用，不增加、不重跑。
- 环境：独立子进程；清除/隔离 BING_/RUN_/连接/Broker/CAP/Redis 等门控变量（只记录键名）；保留默认 SQLite、本地 Sample、并行和 net6/net8；未启用 filter、全局 `-f`、隐式 runsettings、GC capture、诊断 hook、监视器或 dump。
- 命令与结果：
  - `dotnet build Bing.All.slnx -c Release --no-restore`：exit 1；PID `50940`；16.41 秒；未超时。
  - 失败：`CSC : error CS2012`，`Bing.Admin.FreeSQL.InventoryProbe` 的 `obj/Release/net6.0/...dll` 正被另一个进程占用；未推断占用者归属、未按名称杀进程、未重试。
  - `dotnet test Bing.All.slnx -c Release --no-build --no-restore ...`：`NOT_STARTED`；没有 TRX。
- 证据：`build/command.json`、`build/stdout.txt`、`build/stderr.txt`、`build/result.json`、`checkpoint-summary.json`、`post-build-identity.json`（标记构建后身份不可用）、`git-status-after.txt`、`source-diff-after.patch`。
- 结论：**本地默认门禁未通过**；实现 PASS、独立 Review PASS、外部系统独立未验证。历史正式 FAIL 和 Stage B 旧候选不被本结果改写；历史排他归因仍未知。下一步交主线程处理，禁止本执行者 targeted 追绿或扩修。

### CS2012 构建阻断恢复

- 只在 `artifacts/build-lock-diagnosis/` 做有界诊断；Restart Manager 当前 `NO_LOCKERS`，不归因锁进程；原 checkpoint 失败原样保留。
- 静态证据：单 TFM InventoryProbe 同时作为 solution 根项目及带显式 `TargetFramework=net6.0` 的测试 ProjectReference/AfterTargets `MSBuild` 实例，两套全局属性对应同一 `obj/bin` 输出。
- 仅修改 `Bing.Admin.FreeSQL.Tests.csproj`、`Bing.Admin.EFCore.Tests.csproj`，移除冗余 `SetTargetFramework` 和 AfterTargets `TargetFramework` 传递；保留 solution 入口、ProjectReference、Configuration、probe 输出复制和 TFM。
- 最小四项目并行 Release 构建一次：exit `0`（PID `46968`，7.50 秒），无 `CS2012`；两个测试 `InventoryProbe` 输出 hash 与对应 probe 一致。
- 证据：`build-lock-diagnosis/diagnosis.md`、`build-recovery-plan.md`、`recovery-result.json`、`recovery-diff.patch`、`minimal-build/`。
- 该恢复步骤当时未重跑完整 solution/checkpoint；随后主线程授权的新候选检查点已在 `artifacts/final-candidate-after-build-recovery/` 通过，原 checkpoint 仍保留 FAIL/NOT_STARTED。

## Final Candidate After Build Recovery

- 授权范围：Admin InventoryProbe 构建恢复专项验收 PASS 后，对两个 csproj 改动形成的新候选执行一次最终 L4；不是原 checkpoint 盲目重试。
- 位置：`artifacts/final-candidate-after-build-recovery/`；`freeze.json` 冻结起点，`cia.md` 说明范围，`checkpoint-summary.json` 汇总。
- 绝对预算：15 分钟，build/test 共用；未扩展、未重跑。起点 identity/SDK/runtime/Git 状态在 `freeze.json`、`environment.txt`、`git-status-before.txt`。
- 环境：独立子进程；清除/隔离外部数据库、CAP、缓存、Broker、Probe、GC capture 等门控；保留默认 SQLite、本地 Sample、并行和 net6/net8；无 filter、全局 `-f`、隐式 runsettings、监视器或 dump。
- Build：`dotnet build Bing.All.slnx -c Release --no-restore`，exit `0`，PID `31924`，6.21 秒，未超时；完整日志在 `build/`。
- Test：build 成功后 `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <本轮 trx>`，exit `0`，PID `61944`，76.58 秒，未超时；完整日志在 `test/`。
- TRX 逐项聚合：103 个 TRX，6 个空 TRX，14008 个 `UnitTestResult`，0 Failed；net6.0 6722 Passed / 0 Failed / 593 NotExecuted，net8.0 6172 Passed / 0 Failed / 521 NotExecuted。NotExecuted 未计为通过；空 TRX 列表在 `checkpoint-summary.json`。
- 专项结果：两个 GC 原方法在 net6/net8 均 Passed；`ModuleLifecycleRetentionTest` 六项在 net6/net8 均 Passed。
- 身份：`freeze.json` 起点与 `source-identity-after.json` 结束源身份一致；关键构建输出 hash 在 `post-build-identity.json`；`source-diff-after.patch` 保留本轮相关差异。
- 结论：**新候选本地 Release build/默认全量 PASS**；原 `final-candidate-checkpoint` 的 build FAIL/test NOT_STARTED、历史正式 FAIL、历史排他归因未知、永久泄漏未证明均独立保留。外部数据库/CAP/缓存等未验证。

## Git 状态

- 原实施修改：`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs`、`framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs`。
- 原实施新增：`framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleRetentionTest.cs`、`ai_docs/tasks/20261023-module-lifecycle-retention-fix/`。
- Review1 FIX-001 本轮只修改 `framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleRetentionTest.cs`，并新增 `artifacts/review-fix-round-1/`；生产源码未改。
- Build recovery 只修改 `modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj`、`modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj`；新增 `artifacts/build-lock-diagnosis/` 与 `artifacts/final-candidate-after-build-recovery/`。
- 未执行 `git add`、commit、push、reset、restore、clean、PR 或 API baseline/包版本修改。
