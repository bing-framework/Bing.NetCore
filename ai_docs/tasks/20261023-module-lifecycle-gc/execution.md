<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261023-module-lifecycle-gc
AI_EXECUTION_STARTED_AT: UNRECORDED
AI_EXECUTION_FINISHED_AT: 2026-10-23T16:53:18+08:00
AI_EXECUTION_UPDATED_AT: 2026-10-23T16:53:18+08:00

# 执行记录

## 当前状态

- `INVESTIGATION_DELIVERABLE = 已交付`
- `ROOT_CAUSE = NOT_DETERMINED`
- `GC_PROBLEM = 未解决`
- 生产代码未修改；仅增强测试失败诊断并修正本任务报告。
- 历史 Stage B 最后 L4：12881 Passed / 1 Failed / 1114 NotExecuted，FAIL 与 `NOT_DETERMINED` 均未改写。

## 时间、起点与来源边界

| 项目 | 记录 |
| --- | --- |
| 任务真实开始时间 | **未记录**（没有可信的事前时间戳） |
| 机器头 `AI_EXECUTION_STARTED_AT` | 已撤销原先手工填写的 12:10；当前记为 `UNRECORDED` |
| 首批可观测 TRX 时间窗 | 2026-10-23 13:42:07.710 – 13:55:22.459 +08:00（仅证明这些运行的时间窗，不证明任务开始） |
| 原先报告中的 14:20 完成时间 | 手工声明/占位，**不是实际观测到的完成时间**；Reviewer 在 14:06:58 已读取该文件，故不能作为完成时刻 |
| `t00` 捕获时间 | 2026-10-23T13:56:17+08:00，晚于最后 TRX；**事后收口捕获，不是任务开始前快照** |
| 所捕获 HEAD | `3919c1509907d661cfef3679dad79368c8b9cb39`；任务开始 HEAD 与完整 dirty 起点未记录 |
| HEAD 变化归因 | 不推断提交主体；没有证据证明由哪个线程/作者在本任务期间执行 |
| 本次 FIX 修正前快照 | `artifacts/review-fix-round-1/startpoint-posthoc/`，明确标记为 post-hoc 捕获 |

`artifacts/t00-startpoint/` 只能作为**相对所捕获 HEAD 的收口比较**；不能补足事前起点。八个受检生产文件与该捕获 HEAD/副本一致，但这不是任务开始时的冻结证据。

## 源码分析（只读）

已检查 `BingModuleRegistration`、`BingModuleManager`、`BingModuleDescriptor`、`BingModuleLoader`、`BingModuleAssemblyFinder`、`BingLoader`、`ServiceLocator`、`StartupLogger` 的释放链和静态状态：

- registration/manager/descriptor 为实例级；`ReleaseAsync` 清空 owned/released/modules 并清除 descriptor 实例；manager 清空 started/initializationOrder。
- `BingLoader` 用 `ConditionalWeakTable` 弱键；`ServiceLocator` 仅 legacy 入口；测试在某个时点观察到 `ServiceLocator enabled before GC: False`。
- 上述是“未发现可确认静态持有”的观察，不是对全部进程状态/调度下根因的排除证明。

## 实际执行偏差账本（保留，不删除原始证据）

原计划要求“一次诊断 + 一次正式确认”，但实际执行明显超出，且脚本没有 timeout/绝对截止：

| 类别 | 实际记录 | 偏差 |
| --- | --- | --- |
| 完整 Core net8 重复 | `full-net8-loop-01..10` 10 次；`full-net8-prod-01..05` 5 次；`full-net8-02`、`final-full-net8-01`、`final2-full-net8` 等 | 超出预设次数，无事前截止 |
| 完整 Core net6 | `final-full-net6-01`、`final2-full-net6` | 事后补跑 |
| 并发 | 6 个独立进程一批；12 个独立进程一批 | 超出一次诊断+一次确认；独立进程不证明进程内污染 |
| 压力 | 5,000 次循环 net6/net8 | 超出原计划；临时夹具已移出测试项目，源码保存于 `artifacts/gc-stress-source/` |
| 无效运行 | `full-net8-01`：空 filter 参数导致 0 tests、exit 0 | 不是 Pass，保留 |
| 编译失败 | `probe-full-net8-02`：诊断代码读取静态事件引发编译错误、exit 1、无 TRX | 保留，不能从失败记录中消失 |
| 临时 640 项运行 | 12 进程批次的 640 项包含已移除的压力夹具 | 不与最终 639 项候选相加 |

这些记录说明调查执行发生了重复和超限；**不能以更多测试修复流程偏差**。后续只做一次有界采证，不再追加同假设运行。

## 首轮历史候选（旧，不作为当前）

| 运行 | 项目 / TFM | 结果 | 证据 |
| --- | --- | --- | --- |
| `final-target-net8` | Bing.Core.Tests / net8 | 1/0/0，exit 0 | `artifacts/final-target-net8/` |
| `final2-full-net8` | Bing.Core.Tests / net8 | 639/0/0，exit 0 | `artifacts/final2-full-net8/` |
| `final2-full-net6` | Bing.Core.Tests / net6 | 639/0/0，exit 0 | `artifacts/final2-full-net6/` |

以上三组早于本轮采证 hook，只属于首轮历史候选；不能证明当前正式候选，也不再作为“当前源码”证据。早期 loop/prod/并发/5000 结果同样只作调查历史。

## 当前正式候选（Review Round 2）

- runId：`gc-capture-20261023-01`；这是 hook 加入后的当前正式候选，不是首轮 `final2-full-*`。
- 正式 build：exit 0，201 warnings / 0 errors，52.5 秒；solution test：exit 1，103 TRX、6 空，12,881 / 1 / 1,114（net6 6,715/1/593，net8 6,166/0/521）。
- 原目标 `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` net6/net8 均 Passed；没有 ready/ack/dump，目标 `CAPTURE_INCOMPLETE`。
- 正式唯一失败：net6 `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection`，provider 弱引用仍 alive；正式后精准单跑该失败方法一次为 1/0/0，不能冲销正式 FAIL。
- 源/DLL 关联：正式 build/test 命令、`binary-identity.json` 记录的 DLL/PDB hash 与时间关联可核对；**没有在 build 前同步封存 source hash**，因此不称完整 source-at-build 冻结。
- 当前结论保持 `TARGET_TEST_REPRODUCTION=NOT_REPRODUCED`、`RELATED_WEAK_REFERENCE_FAILURE=OBSERVED`、`ROOT_CAUSE=NOT_DETERMINED`、`GC_PROBLEM=未解决`。

## Review Fix Round 1

- Review 状态：NEEDS_FIX；仅 FIX-001/FIX-002（均 SHOULD_FIX）；0 项生产修复要求。
- 本轮为报告/证据账本修正，不做新增 Core/并发/压力/full-solution 测试。
- FIX-001：收窄所有“已排除/唯一剩余/永久强引用”表述；明确调查已交付、根因未定、GC 问题未解决；H1–H4 未全面排除，H5 不是唯一剩余假设；测试注释改为只描述第二观察窗口。见 `diagnosis.md`。
- FIX-002：更正 t00 为事后捕获；任务开始 HEAD/时间未记录；删除未证实的线程归因；区分手工时间与 TRX 观测；完整记载 loop/prod/并发/5000 偏差及 0-test/编译失败；修复 `plan.md` 中的 U+0007；首轮 final 标为历史，当前正式候选改为 `gc-capture-20261023-01`。见本文件及 `plan.md`、`traceability.md`。
- 原始 artifacts、Reviewer `review.md` 和旧 Stage B 证据均未改写。

## 原下一步（历史，已执行）

按 `artifacts/review-round-1/next-capture-plan.md` 准备一次有界 full-solution 失败采证；该正式批次已执行一次，停止重复采证。

## Review Fix Round 1（FIX-001/FIX-002）静态修正

- FIX-001：`diagnosis.md`、`execution.md`、`traceability.md`、`plan.md` 已统一为“未复现/已检查路径未发现可确认持有”，不再把 H1–H4 写成已排除，不再称 H5 是唯一剩余假设，不再用额外 GC 窗口推断永久强引用。
- FIX-002：`t00` 明确为事后收口捕获；任务开始 HEAD/时间标为未记录；删除“其它线程推进 HEAD”归因；区分手工声明与 TRX 观测；记录 loop10、Production5、6/12 进程、5000 循环、0-test、编译失败等偏差；`plan.md` 的 U+0007 已修复；首轮 final-target/final2 标为历史，当前正式候选只引用 `gc-capture-20261023-01`。
- 两项均为 SHOULD_FIX、报告/注释级；未新增 Core/并发/压力/full-solution 测试。
- UTF-8/非空白控制字符检查已通过；post-hoc 快照保留修正前的旧 U+0007 原始副本。

## Step 2：失败采证工具链与有限对照

### 测试 hook

新增测试诊断文件：`framework/tests/Bing.Core.Tests/Modularity/GcCaptureHandshake.cs`。
`ModuleLifecycleTest.TrackedReference` 在构造时保存 appId/role/完整类型字符串；失败路径不再读 `WeakReference.Target`。hook 仅在 `BING_GC_CAPTURE=1` 且首快照非空时运行，写 `ready.json` 后最多等 ack 30 秒；原断言不变。临时控制测试 `GcCaptureControlTest` 仅用于对照，已从测试项目移除，源码保存在 `artifacts/gc-capture-tools/GcCaptureControlTest.removed.cs`。

### 对照结果

- `gc-control-ack-12`：monitor PID/StartTime/进程树校验通过；collector Full dump 0.59 秒，163,458,799 bytes，ack=`captured`；控制测试仍 Failed，原失败未变绿。
- `gc-control-timeout-01`：无 monitor；**父命令 elapsed 30.909 秒**，TRX 测试方法约 30.017 秒；ready 存在、ack 不存在、hook-error 存在，测试 exit1；原失败保留。
- 强持有/释放后成功/首快照不被额外 GC 冲销：复用 Review 已接受的 `artifacts/review-round-1/diagnostic-controls` 三组对照，不重跑。
- 早期 `gc-control-ack-01..11` 是 monitor 时区/字段/调试问题，均保留；不作为产品证据。

## Step 2：正式 full-solution 采证

- runId：`gc-capture-20261023-01`；绝对 deadline `2026-10-23T15:01:16.139+08:00`（从正式 build 启动前写入，未递增）。
- Build：`dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1`；exit 0，201 warnings / 0 errors，52.5 秒。
- Test：`dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory ...`；exit 1，103 TRX、6 空；12,881 Passed / 1 Failed / 1,114 NotExecuted；net6 6,715/1/593，net8 6,166/0/521。
- 目标测试 `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`：net6/net8 均 Passed；无 ready/ack/dump，`CAPTURE_INCOMPLETE`。
- 唯一失败：`Bing.Core.Tests.Modularity.ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection`（net6），provider 弱引用仍 alive。该测试不在目标 hook 范围，未产生目标 dump；正式后精准单跑该失败方法一次（1/0/0，exit 0），未复现，不作因果证明。
- 正式运行只执行一次；没有第二次 full-solution、没有新增压力循环、没有无限 GC。
- 证据：`artifacts/gc-capture-20261023-01/`（deadline、build/test 日志、binary identity、monitor/collector、TRX、summary、failures、target-results）。

## 候选根因（未确认）

`BingModuleManager.ShutdownCore` 以 fire-and-forget 启动 `CompleteShutdownAsync`，而 `CompleteShutdownAsync` 在 `completion.TrySetResult/TrySetException` 之后才进入 `finally`。只有 Cleanup 真正挂起或存在并发 Shutdown 时，公开完成才可能早于私有尾部；两个实际同步夹具（同步 UseBing/ShutdownBing/Dispose、无未完成 await/并发证据）不具备该前提，不能据此解释其失败或支持生产修复。当前没有目标失败 dump/引用根，候选保持未确认，未做生产修补。

## 最终状态

- `INVESTIGATION_DELIVERABLE = 已交付`
- `TARGET_TEST_REPRODUCTION = NOT_REPRODUCED`
- `RELATED_WEAK_REFERENCE_FAILURE = OBSERVED`
- `ROOT_CAUSE = NOT_DETERMINED`
- `GC_PROBLEM = 未解决`
- 生产代码未改；测试仅新增默认关闭的采证 hook/诊断；旧 Stage B Review4、旧 L4 12881/1/1114、Review 文件不改写。
- 未 git add/commit/push/reset/restore/clean；未发送外部通知。

## Review Fix Round 2（FIX-002 / FIX-003）

- Review 状态：NEEDS_FIX；FIX-001 CLOSED 不重开；仅 FIX-002/FIX-003，均 SHOULD_FIX，0 生产修复。
- 本轮只改报告、诊断 runner/monitor/collector/timeout 脚本和纯虚拟确认脚本；未改 Bing.Core 生产、原 GC 测试行为、Stage B 报告或 `review.md`。
- 修改前实际副本：`artifacts/review-fix-round-2/startpoint-current/`（post-hoc，不是任务开始或 source-at-build 冻结）。

### FIX-002（报告/证据归属）

- 首轮 `final-target-net8`、`final2-full-net8`、`final2-full-net6` 已标为**历史候选**；当前正式候选为 `gc-capture-20261023-01`。
- 当前正式候选：build exit 0（201 warnings/0 errors，52.5 秒）；solution exit 1（12,881/1/1,114；net6 6,715/1/593，net8 6,166/0/521）；原目标两 TFM Passed；net6 `RetainedServiceCollection...` provider alive 失败；目标 `CAPTURE_INCOMPLETE`。
- 正式后精准单跑 `formal-failure-target-net6` 为 1/0/0；只记录为未复现，不覆盖正式 FAIL。
- 源/DLL 关联明确区分 build/test 命令、mtime、事后 manifest 与已归档 binary hash；没有 build 前同步 source hash，不称完整 source-at-build 冻结。
- `TrackedReference` 表述改为构造时缓存 appId/role/类型字符串；`Describe` 失败路径不读 `WeakReference.Target`。
- `run-summary.json` 的 49 TRX 与无 TRX 的 `probe-full-net8-02` 编译失败分列，不把后者算入 JSON 或通过。
- 旧“下一步再准备一次采证”标为历史且已执行；明确停止重复采证。
- 工具迭代分类：ack-01 缺测试/monitor结果；02–05、10 `invalid-target`；06–09、11 `monitor-error`；12 人工控制成功（dump 163,458,799 bytes、SHA-256 已保存）；timeout-01 与 formal 后精准单跑分别归档。未逐版本封存早期脚本，不补造版本证据。
- H6 收窄为真正异步挂起/并发协议候选；两个实际同步夹具没有未完成 await/并发证据，不能解释其失败或支持生产修复。父命令 30.909 秒与 TRX 方法 30.017 秒分开。

### FIX-003（runner 绝对截止与 timeout 外层 watchdog）

- `orchestrate_capture.py`：目录/日志/参数准备完成，再在实际 build/hash 后/monitor/test 创建入口检查同一冻结 deadline；build 日志打开后、Popen 前再次检查，非正时不创建 build/monitor/test。
- 已创建进程立即登记 PID/startedAt；build 记录 PID/started。build 实际 wait 由 `timeout_provider` 在创建后按当时正剩余计算，不使用准备前 timeout。test 启动后超时写 `TEST_STARTED_TIMEOUT`/真实 PID/清理；有 monitor 但 test 未启动写 `MONITOR_STARTED_TEST_NOT_STARTED`；无 monitor 写 `TIMEOUT_NOT_STARTED`。
- 创建后 PID 文件发布、trx.mkdir、日志打开、wait 等异常由覆盖 monitor 创建后阶段的 try/finally 清理本次 build/test/monitor 树；保留原 OSError/失败信息和 cleanup。build 普通 wait 异常清理已记录 build PID；正常已退出进程只记 `already-exited`。
- 清理使用独立 5 秒 `CleanupBudget`，多个本次 PID 共用剩余预算；每条 taskkill 传 `timeout=remaining`，记录 `commandIssued`/`exitConfirmed`；单条 timeout/失败不阻断其它尝试，预算耗尽记 `skipped-no-budget`，不覆盖原异常。
- `monitor.ps1`：先准备 collector 目录/日志/参数，再检查 deadline；启动后立即登记 collector PID；wait 使用实际开始时 `min(20s, 正剩余)`；finally 仅停止本次活动 collector。
- 合同保持：hook 30 秒、collector 20 秒、正式 15 分钟、timeout-control 45 秒；未增加依赖/预算。
- 纯虚拟确认：`artifacts/gc-capture-tools/virtual_deadline_confirm.py`，15 秒外部 watchdog，无真实 dotnet/monitor/collector；Python `py_compile` 与 pwsh Parser 静态检查 exit 0：
  - build 899.5 秒 + 8 次 hash 后越界：`launches=[]`、`TIMEOUT_NOT_STARTED`、`startedWork=false`，test 启动 0 次。
  - build 准备跨截止：0 build/monitor/test；`TIMEOUT_NOT_STARTED`、`startedWork=false`。
  - 目录准备跨截止：仅 monitor 创建（897.0 秒），test 创建 0 次；`MONITOR_STARTED_TEST_NOT_STARTED`、`startedWork=true`，并清理本次 monitor。
  - test-pid.json 发布 OSError：test/monitor 均已创建，二者均被清理，原 `OSError` 保留，`status=TEST_ERROR`、`startedWork=true`。
  - build wait OSError：已记录 build PID 被清理，原异常保留，`status=BUILD_ERROR`。
  - 清理阻塞：首个 taskkill timeout 记 `cleanup-timeout`，后续预算耗尽记 `skipped-no-budget`，原 `OSError` 不被覆盖。
  - 证据：`artifacts/review-fix-round-4/virtual-deadline-confirm/confirm.json`；静态断言：`artifacts/review-fix-round-4/static-assertions.json`。

### 边界与状态

- 未运行真实 build/Core/solution/GC/并发/压力/dump；未补跑历史控制。
- `review.md` 未修改；旧 raw/Stage B 证据未改写。
- `ROOT_CAUSE=NOT_DETERMINED`，`GC_PROBLEM=未解决`；FIX-003 只是工具截止修复，不是 GC 修复。
- 未 git add/commit/push/reset/restore/clean；未发送外部通知。

## Review Fix Round 3（限定收尾）

- Review 状态：NEEDS_FIX；仅 FIX-002/FIX-003，均 SHOULD_FIX；FIX-001 CLOSED 不重开；0 生产修复要求。
- 本轮只修报告与诊断工具/纯虚拟确认脚本，未改 Bing.Core 生产、原测试 GC 行为、默认关闭 hook、Stage B 报告或 `review.md`。
- FIX-002：统一 `diagnosis.md` 的 49 TRX/无 TRX 编译失败分列、ack-01 缺结果/02–11 分类、30.909 秒父命令与 30.017 秒 TRX 方法；execution/traceability 同步。当前正式候选、source/binary 限制、H6 与停止边界保持。
- FIX-003：
  - 目录/日志/参数准备先完成；实际 build/hash/monitor/test 创建入口检查同一冻结 deadline；过期 0 次新启动。
  - 创建即登记 PID/started；test 启动后超时 `TEST_STARTED_TIMEOUT`，有 monitor 但 test 未启动 `MONITOR_STARTED_TEST_NOT_STARTED`，无 monitor `TIMEOUT_NOT_STARTED`。
  - 创建后 PID 发布/等待异常由 try/finally 清理本次 test/monitor 树并保留原 OSError；`monitor.ps1` 记录 collector PID、按实际剩余等待并在 finally 清理活动 collector。
  - 移除 `max(1/max(5))`；wait 使用正剩余与上限较小值；45 秒 timeout-control 保留。
- 纯虚拟确认：`artifacts/review-fix-round-3/virtual-deadline-confirm/confirm.json`、`static-assertions.json`。build+hash 越界 0 次启动；目录准备跨截止仅 monitor、test 0 次并清理；test-pid OSError 清理两树并保留异常。`contractSatisfied=true`。
- 静态：Python `py_compile`、PowerShell Parser、UTF-8/非空白控制字符、diff 检查通过。
- 未运行真实 build/Core/solution/GC/并发/压力/dump；未补跑历史控制；未增加预算/依赖。
- `ROOT_CAUSE=NOT_DETERMINED`，`GC_PROBLEM=未解决`，正式 solution FAIL 保留。停止，不自动第四轮。
