# Module Lifecycle GC 排查：符号 → 测试 → 证据

## 生产符号（只读，未修改）

| 符号 / 行为 | 关联测试 | 本轮证据 |
| --- | --- | --- |
| `BingModuleRegistration.ReleaseAsync`：清除 owned/released/modules、descriptor 实例和 owned descriptors | `ModuleLifecycleTest.ProviderDispose_ReleasesOwnedModulesOnce`、`RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`、完整 Bing.Core.Tests | `final2-full-net8/`、`final2-full-net6/`、`run-summary.json` |
| `BingModuleManager.CleanupAsync` / `ShutdownCore`：清理 started/initializationOrder，释放注册记录 | `ModuleLifecycleTest.*`、`ModuleResourceRegressionTest.RetainedServiceCollection_*` | 最终 Core 两 TFM 639/0/0 |
| `BingModuleDescriptor.ClearInstance` | `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` | 5,000 循环 net6/net8；临时夹具在 `gc-stress-source/` |
| `BingLoader.RegisteredCollections`（ConditionalWeakTable） | 源码检查；`ModuleLifecycleTest` | `diagnosis.md`：未发现可确认静态实例根，不是全面排除 |
| `ServiceLocator`（legacy only） | `RepeatedClosedApplications...` 诊断输出 | `probe-net8-01/`、`probe-full-net8-01/`：某时点 `ServiceLocator enabled before GC: False` |

## 测试夹具（本轮增强）

| 符号 / 行为 | 测试 | 证据 |
| --- | --- | --- |
| `ModuleLifecycleTest.CreateClosedApplication` 返回 `TrackedReference[]`，记录 provider/manager/recorder/module 类型 | `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` | `artifacts/review-fix-round-1/startpoint-posthoc/`、`traces`/diff |
| `TrackedReference` 构造时缓存类型字符串；`Describe` 在失败时只读缓存值，不读取 `WeakReference.Target` | 同上 | 失败时 TRX `StdOut` 将包含 `provider:<type>` 等；不保存 target 强引用 |
| 标准 3 轮 GC 仍为断言依据；额外 3 轮仅记录第二观察窗口 | 同上 | 原 `alive.ShouldBeEmpty()` 保留；不推断永久性 |
| 临时 5,000 循环压力夹具 | `GcStressDiagnosticTest.DiagnoseRepeatedClosedApplications`（已移出测试项目） | `gc-stress-source/`、`gc-stress-5000-net6/`、`gc-stress-5000-net8/` |

## 验证矩阵（当前正式候选 + 首轮历史）

| 层级 | 范围 | 结果 | 证据 |
| --- | --- | --- | --- |
| 当前正式 build | `gc-capture-20261023-01` Release solution | exit 0；201 warnings / 0 errors；52.5 秒 | `gc-capture-20261023-01/logs/build-result.json` |
| 当前正式 solution | `gc-capture-20261023-01` 默认完整 test | exit 1；103 TRX、6 空；12,881/1/1,114（net6 6,715/1/593，net8 6,166/0/521） | `gc-capture-20261023-01/` |
| 当前正式目标 | `RepeatedClosedApplications...` net6/net8 | 均 Passed；无 ready/dump；目标 NOT_REPRODUCED | `target-results.json` |
| 当前正式相关失败 | `RetainedServiceCollection_NormalShutdown...` net6 | provider alive；正式后精准单跑 1/0/0 不冲销 FAIL | `failures.json`、`formal-failure-target-net6/` |
| 首轮历史精准 | GC 单测试 net8 | 1/0/0，exit 0 | `final-target-net8/` |
| 首轮历史完整 | Bing.Core.Tests net8/net6 | 各 639/0/0，exit 0 | `final2-full-net8/`、`final2-full-net6/` |
| 调查历史 | loop10、Production5、6/12 并发、5000 循环 | 各自通过；不拼成当前候选 | 对应目录 |
| 无效/失败中间 | `full-net8-01` 0 tests；`probe-full-net8-02` 编译失败 | 不计通过 | 对应目录 |

## 结论

生产符号未改变。调查已交付，但 `ROOT_CAUSE = NOT_DETERMINED`，`GC_PROBLEM = 未解决`；H1–H4 未被全面排除，H5 不是唯一剩余假设。原始 raw artifacts 与历史 Stage B 证据未改写。

## Step 2：采证 hook / 正式运行

| 符号 / 行为 | 测试 / 工具 | 结果 | 证据 |
| --- | --- | --- | --- |
| `GcCaptureHandshake.Publish`：默认关闭，首快照非空时 atomically ready，等待同 nonce ack 最多30秒 | `GcCaptureControlTest`（临时） | ack 成功；无 monitor 时父命令 elapsed 30.909 秒、TRX 方法约 30.017 秒；原失败均保留 | `gc-control-ack-12/`、`gc-control-timeout-01/` |
| `monitor.ps1`：验证 runId/nonce/PID/StartTime/进程树后启动隐藏 collector | control/formal | 最终 control 校验通过；正式无 ready | `gc-capture-tools/monitor.ps1`、`gc-control-ack-12/logs/monitor-result.json` |
| `collector.ps1`：SDK DiagnosticsClient Full dump，20秒 watchdog | control | captured 0.59s；163,458,799 bytes；SHA256 已保存 | `gc-control-ack-12/logs/collector-result.json`、`capture/.../first-failure.dmp` |
| 默认完整 full-solution 候选采证 | `dotnet test Bing.All.slnx -c Release --no-build --no-restore` | 103 TRX/6 空；12,881/1/1,114；target net6/net8 Passed；CAPTURE_INCOMPLETE | `artifacts/gc-capture-20261023-01/` |
| 相关弱引用失败 | `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` net6 | provider alive；无目标 dump；NOT_DETERMINED | `gc-capture-20261023-01/failures.json`、`target-results.json` |
| shutdown 完成时序候选 | `BingModuleManager.ShutdownCore`/`CompleteShutdownAsync` 源码 | 未确认；未做生产修补 | `diagnosis.md` |

正式运行只一次；未追加 full/压力。目标测试未复现，相关失败保留为证据。

## Review Fix Round 2：符号 → 纯虚拟 AC → 证据

| 符号 / 行为 | 纯虚拟/静态 AC | 结果 | 证据 |
| --- | --- | --- | --- |
| `orchestrate_capture.main` 目录/日志准备后，在实际 build/hash/monitor/test 创建入口检查同一冻结 deadline | build 899.5s + hash 越界后 0 次 monitor/test；目录准备跨截止时仅 monitor、test 0 次，状态 `MONITOR_STARTED_TEST_NOT_STARTED`/`startedWork=true` | PASS | `artifacts/review-fix-round-3/virtual-deadline-confirm/confirm.json` |
| `orchestrate_capture.main` 已启动 test 的 PID 发布异常/finally 清理 | test-pid.json OSError 后清理 test/monitor 两树，保留原 OSError，`TEST_ERROR`/`startedWork=true` | PASS | 同上 |
| `monitor.ps1` collector 启动前 deadline 检查；`min(20s, 正剩余)` | 静态源码/语法检查 | PASS（未真实启动 collector） | `gc-capture-tools/monitor.ps1` |
| `run_timeout_control.py` 独立 45s 外层 watchdog + 本次树清理 | 静态检查；不重跑历史真实控制 | PASS（静态）；旧 30.909s 记录保留 | `gc-capture-tools/run_timeout_control.py`、`gc-control-timeout-01/result.json` |
| 原目标/相关失败/历史候选归属 | 当前正式候选 `gc-capture-20261023-01`；首轮 final 历史；正式后单跑不冲销 | PASS（静态报告） | `execution.md`、`diagnosis.md`、`traceability.md` |
| Describe/构造缓存/不读 Target；49TRX vs 编译失败分列 | 静态源码/证据核对 | PASS | `ModuleLifecycleTest.cs`、`run-summary.json`、`probe-full-net8-02/` |

正式运行仍为一次，未新增真实 build/test/GC；FIX-003 仅工具截止控制。

## Review Fix Round 3：AC → 差异 → 证据

| AC | 实际差异 | 确认 |
| --- | --- | --- |
| FIX-002/1 49TRX/编译失败分列 | diagnosis 删除“run-summary 包含编译失败”；execution/traceability 同步 | 静态 |
| FIX-002/2 ack 分类 | ack-01 缺结果；02–05/10 invalid-target；06–09/11 monitor-error；12 成功控制 | 静态 |
| FIX-002/3 30.909/30.017 | 全部相关句标为父命令/TRX 方法，不称 hook 实测 | 静态 |
| FIX-003/1 最后准入 | 目录/日志/参数准备后检查；build/hash/monitor/test 入口；过期 0 启动 | `confirm.json` build/hash、late-preparation 场景 |
| FIX-003/2 真实启动状态 | PID/started 立即记录；test 超时 `TEST_STARTED_TIMEOUT`；有 monitor 无 test `MONITOR_STARTED_TEST_NOT_STARTED` | `confirm.json` late-preparation |
| FIX-003/3 try/finally/collector | test/monitor 树清理；monitor collector PID/活动清理 | `confirm.json` pid-publication-error；`static-assertions.json` |
| FIX-003/4 实际剩余 wait | 移除 max 续期；collector wait 实际剩余 min(20s) | `static-assertions.json` |
| FIX-003/5 有限虚拟确认 | 15s 外截止，无真实进程；`contractSatisfied=true` | `artifacts/review-fix-round-3/virtual-deadline-confirm/confirm.json` |
