# GC Task 诊断工具限定修正（Review 4 · FIX-003）

- Task：`20261023-module-lifecycle-gc`
- 授权：`continuation-authorization.md`（用户“好的，继续修正功能”）
- 范围：仅 Review 4 唯一剩余 `FIX-003`（SHOULD_FIX）；FIX-001/002 保持 CLOSED，不重开。
- 生产：**未修改** Bing.Core 生产、默认关闭测试 hook、旧 Stage B 报告或 `review.md`。
- GC 状态：`ROOT_CAUSE=NOT_DETERMINED`，`GC_PROBLEM=UNRESOLVED`；当前正式 solution/Release FAIL 保留。

## CIA

- ChangedFiles：`artifacts/gc-capture-tools/orchestrate_capture.py`、`artifacts/gc-capture-tools/run_timeout_control.py`、`artifacts/gc-capture-tools/virtual_deadline_confirm.py`；`monitor.ps1` 当前 hash 与 Review4 受保护输入一致，本轮未改。
- ChangedProjects：无生产项目；仅诊断工具/artifacts。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：诊断 runner 的最后准入、已创建进程所有权/异常清理、共享有界清理预算、实际剩余 wait。
- ChangedProviders：无。
- ChangedTFMs：无。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：否（工具修正报告）。
- AffectedDependents：仅未来 GC 采证 runner；不影响产品测试或 GC 根因结论。
- RiskLevel：LOW（工具代码，无真实运行）；不影响正式 solution FAIL/Release。

## FIX-003 AC 映射

| Review4 AC | 实现差异 | 证据 |
| --- | --- | --- |
| 1. build 目录/日志/环境准备后，真实 Popen 前检查同一 deadline；非正 0 build；创建后按当时正剩余 wait | `run_process` 在打开 stdout/stderr 后调用 `before_start`；Popen 后 `timeout_provider` 重新取剩余；`build_before_start` 抛 `DeadlineReached`，main 写 `TIMEOUT_NOT_STARTED`，不创建 build/monitor/test。 | `orchestrate_capture.py:75-87`、`:310-321`；虚拟 `buildPreparationOverrun` 0 启动 |
| 2. monitor 创建后的 trx.mkdir/日志打开准备异常，try/finally 清理 monitor；build 普通 wait 异常清理 build 树；正常退出不重复杀 | monitor Popen 后进入 try，`trx.mkdir`/test 日志/测试 Popen 都在 try 内；`finally: cleanup_created()` 按本次 `monitorProc`/`testProc`/`buildProc` 清理；`terminate_process` 先 `poll`，已退出记 `already-exited`。`run_process` 非 Timeout 异常先清理 build 再抛出。 | `orchestrate_capture.py:130-167`、`:261-290`、`:370-471`；虚拟 `pidPublicationError`、`buildWaitError` |
| 3. 所有 taskkill 有独立短截止，多个 owned PID 共用剩余预算；单条失败/timeout 不阻断其它、不覆盖原异常；记录命令发出/确认退出 | `orchestrate_capture.py` 的 `CleanupBudget` 固定 5 秒，`terminate_process` 每条 taskkill 传 `timeout=remaining`；`run_timeout_control.py` 外层 45 秒超时后使用同一 5 秒语义清理本次 test PID，记录 `commandIssued`/`exitConfirmed`/`cleanup-timeout`，清理失败不覆盖原超时。 | `orchestrate_capture.py:130-167`、`:219`、`:287-300`；`run_timeout_control.py` 清理路径；虚拟 `cleanupBudgetTimeout`、supplement `timeout-control-confirm.json` |
| 4. 实际 wait 使用正剩余与既定上限较小值；截止后只做有限清理；合同不变 | build wait 由 `timeout_provider` 在 Popen 后计算；test/monitor/collector wait 均按实际剩余；hook30s/collector20s/formal15min/timeout-control45s 未改。 | `orchestrate_capture.py:86-87`、`:421-450`；`monitor.ps1:151-160`；虚拟三场景 |

## 静态与有限虚拟验证

- `py_compile`：`orchestrate_capture.py`、`run_timeout_control.py`、`virtual_deadline_confirm.py` exit 0。
- PowerShell Parser：`monitor.ps1`、`collector.ps1` 无语法错误。
- `static-assertions.json`：build 前/后准入、build ownership、共享 cleanup budget、taskkill timeout/exitConfirmed、test try/finally、collector PID/finally 均为 true。
- 纯虚拟确认：`artifacts/review-fix-round-4/virtual-deadline-confirm/confirm.json`，15 秒 faulthandler 外截止，无真实 dotnet/monitor/collector，`contractSatisfied=true`：
  - build 899.5s + 8 hash 越界：0 monitor/test；
  - build 准备跨截止：0 build/monitor/test；
  - 目录准备跨截止：仅 monitor，test 0，`MONITOR_STARTED_TEST_NOT_STARTED` 并清理；
  - test-pid OSError：清理 test/monitor，原 OSError 保留；
  - build wait OSError：清理已记录 build PID，原异常保留；
  - cleanup taskkill timeout：记录 `cleanup-timeout`，后续预算耗尽记 `skipped-no-budget`，原 OSError 保留。
- timeout-control 补充虚拟：artifacts/review-fix-round-4/supplement/timeout-control-confirm.json，假进程 45 秒外层超时；taskkill 使用 <=5 秒预算；成功路径 commandIssued=true/exitConfirmed=true，清理超时路径 cleanup-timeout/exitConfirmed=false，原 	imedOut=true/exitCode=null 保留。
- 本轮未运行真实 build/Core/solution/GC/并发/压力/dump；未补跑历史控制。

## 文件与边界

- 修改：`orchestrate_capture.py`、`run_timeout_control.py`、`virtual_deadline_confirm.py`；新增 `artifacts/review-fix-round-4/` 报告/原始虚拟证据；顶层本文件。
- 未修改：Bing.Core 生产、ModuleLifecycleTest/GcCaptureHandshake、`monitor.ps1`、`review.md`、Stage B/父路线图。工具修正内容集中在本文件与 `artifacts/review-fix-round-4/`。
- 未 git add/commit/push/reset/restore/clean；未发送外部通知；未运行 task-finish。
- **FIX-003 仍待 Reviewer 独立判定**；本文件不修改 review.md、不宣称 CLOSED。Astra 的 GC 调查不在此文件范围。
