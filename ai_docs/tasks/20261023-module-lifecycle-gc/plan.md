# Module Lifecycle GC 根因排查计划

- Task ID: `20261023-module-lifecycle-gc`
- 日期：2026-10-23
- 状态：用户已授权继续排查（无需再等计划许可）
- 实现模型：OpenCode `deepseek-v4.1-flash`
- 独立复核：主线程后续交原 Sol/high

## 1. 背景

`framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs` 的
`RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` 在默认完整 Release
测试中跨 net6/net8 间歇失败。最新 net8 TRX 中 36 个 `WeakReference` 未全部回收，
但原断言没有类型/阶段信息。历史单独运行通过不能证明全量失败无问题。

本任务只排查该 GC 存活根因；不修改阶段 B（FIX-005）生产逻辑，不扩大到 Uow/CAP/FreeSQL
或其它功能。历史 Stage B final 结果不可被本轮结果覆写。

## 2. 范围

- 允许：只读源码分析、Bing.Core 精准/完整测试、受控诊断夹具、必要时最小测试夹具修复。
- 不允许：改动 SDK/TFM/依赖包/API baseline/性能预算；改用户配置；停止无关进程；
  `git add/commit/push/reset/restore/clean`；外部通知；委派其它模型实现。
- 若确认是生产引用泄漏：先形成可重复失败回归，再最小生产修复并验证正常/失败/取消生命周期。
- 若证据不足：保持 `NOT_DETERMINED`，交付已排除/未排除假设和明确下一步，不做猜测性修补。

## 3. 假设

- H1：测试自身/JIT/断言表达式（LINQ 闭包、局部变量生命周期）在断言时仍强持有对象。
- H2：框架静态缓存/事件/单例在应用关闭后仍持有 provider/module/recorder。
- H3：DI 容器/模块 manager/registration 的释放顺序或未释放字段导致对象不可回收。
- H4：并行其它测试通过静态状态（如 `ServiceLocator`、`BingLoader.RegisterType`、
  `Singleton<T>`）或线程池/后台任务临时持有本测试对象，造成污染。
- H5：GC 时序/终结器/弱引用观察方式导致假泄漏。

## 4. 受控验证步骤

1. 记录工作区起点、候选文件 hash；保留现有 dirty 改动。
2. 源码分析：模块注册、manager、registration、ServiceLocator、静态缓存、测试静态状态。
3. 精准基线：单独运行失败测试（net6/net8 Release，有界次数），记录结果。
4. 全项目基线：完整 `Bing.Core.Tests`（net6/net8 Release，默认并行、无额外门控），
   记录逐 `UnitTestResult`。
5. 诊断夹具：在测试内增加类型化弱引用/阶段记录，失败时报告存活对象类型；
   诊断不得永久强持有被测对象，弱引用/日志不能制造假泄漏。
6. 对照实验：原路径 vs 最小候选；一次诊断 + 一次正式确认/同假设；预先定义运行次数与截止。
   不靠加长 sleep、无限 GC 重试、关闭并行/优化/tiered compilation 或跳过断言。
7. 若确认生产泄漏：最小修复 + 正常/失败/取消生命周期回归 + 完整 `Bing.Core.Tests` 两 TFM。
8. 交付 plan/execution/diagnosis/traceability、原始命令/输出/TRX/退出码、文件清单与影响。

## 5. 停止条件

- 找到可复现根因并给出最小已验证修复；或
- 证据不足，明确 `NOT_DETERMINED`、已排除/未排除假设与下一步；或
- 受控工具/环境阻塞且不属于实现缺陷。

同一假设最多一次诊断运行 + 一次正式确认运行。不得无限重试或自行多轮 Fix。

## 6. 首轮执行结果（历史）

- 状态：NOT_DETERMINED（目标测试正式运行未复现；正式运行出现相关弱引用失败，但没有目标 dump/根链）。
- 生产代码：未修改。
- 测试代码：仅增强 ModuleLifecycleTest 的类型化弱引用/失败诊断；断言未放宽。
- 验证：精准 net6/net8、完整 Bing.Core.Tests 两 TFM、12 进程并发、5,000 循环压力均通过；明细见 execution.md、diagnosis.md 和 artifacts/run-summary.json。
- 原下一步（历史，已执行）：下一次完整 solution 若再失败，使用增强 TRX 锁定存活类型并采集受控 testhost dump/gcdump；本轮已执行一次，不再重复采证。


## 7. Review Fix Round 1（FIX-001/FIX-002）

- 两项均 SHOULD_FIX，仅修正报告/证据/注释，不新增 Core/并发/压力/full-solution 运行。
- FIX-001：收窄“已排除/唯一剩余/永久强引用”；明确调查已交付、根因未定、GC 问题未解决；H1–H4 未全面排除，H5 不是唯一剩余假设。
- FIX-002：t00 标记为事后收口捕获；任务开始 HEAD/时间未记录；删除未证实的线程归因；区分手工声明与 TRX 观测；记录 loop10/Production5/6进程/12进程/5000 的偏差及 0-test/编译失败；修复 U+0007；首轮 final-target/final2 标为历史，当前正式候选只列 `gc-capture-20261023-01`。
- 修正前状态保存于 `artifacts/review-fix-round-1/startpoint-posthoc/`，明确为 post-hoc，不是任务起点。

## 8. 追加授权：一次有界 full-solution 失败采证

主线程于本次追加授权执行 Review 附档 `artifacts/review-round-1/next-capture-plan.md`，属于一次带新诊断 hook/候选/证据目标的实验，不是重复全量追绿。

### 8.1 目标与预算

- runId：`gc-capture-20261023-01`（实际运行前在新 artifact 中冻结，不可复用旧目录）。
- 工具预检/有限对照：独立短截止；对照只验证 hook/monitor 握手、强持有负对照、释放后成功、ack/超时和原失败不变绿，不批量 Core/压力。
- 正式批次：从正式 Release build 启动算起，总绝对截止 **15 分钟**；预先写入绝对 deadline，截止后不递增、不自动第二批。
- hook 等待 ack 最多 30 秒；collector 20 秒 watchdog；每 runId 首个失败最多一份 Full dump；正常测试不等待。
- 若工具链未就绪、dump 失败/目标消失/全部通过：写 `CAPTURE_INCOMPLETE`/`NOT_DETERMINED`/未复现并停止，不追加压力或生产猜测性修补。

### 8.2 测试 hook（默认关闭）

- 显式环境门控，默认关闭；位置为首 3 轮 GC 得出非空 alive 快照后、额外 GC 前、最终原断言前。
- 首快照失败判定保留；hook 不改变通过条件，不把正常测试加 sleep。
- 构造 `TrackedReference` 时保存 appId/role/完整类型字符串；失败路径不读 `WeakReference.Target`，不把 target/provider/module/manager 传入闭包、异常、日志或长寿命诊断表。
- `NoInlining` 短 helper 取得 bool/字符串快照后返回；不在测试线程读 target。
- 每 runId 用 `CreateNew` 一次性 claim；原子发布 `ready.json`（UTF-8，临时文件 flush 后 rename）：nonce/runId、candidate、完整测试名、`Environment.ProcessId`、进程 `StartTimeUtc`、TFM/runtime、UTC、首快照 role/appId/label、阶段。
- 同 nonce ack 最多等 30 秒；timeout/异常仍抛原失败；正常测试不等待。

### 8.3 监视器/collector 与正式运行

- 监视器在 solution 启动前就绪；确认 PID + StartTimeUtc 且属于本次进程树 testhost 后，才启动隐藏 collector；不得按名杀全部 dotnet。
- collector 使用现有 SDK 10.0.400 `Microsoft.Diagnostics.NETCore.Client.dll` 的 `WriteDump(Full)`；外部子进程执行，20 秒 watchdog；finally 写 ack；不安装新依赖。
- 正式命令：候选 Release solution build 成功并记录 DLL/PDB 后，默认完整 `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <本runId目录>`；无 filter/全局-f/隐式 runsettings，保留并行；独立子进程关闭外部/probe gates、清理连接/broker 变量，只加本次 GC 采证门控。
- 不使用 `Start-Process -Wait` 等待 MSBuild node-reuse 后代；按直接子进程 PID/exit 独立观察；只清理本任务创建的进程树。
- 新 artifacts 目录，不覆盖旧运行；正式运行一次后按失败/无 dump/全部通过分支停止。
- dump 分析只使用现有本机工具；若无法关联 appId/role/target 的真实强根，记录 `CAPTURE_INCOMPLETE` 及缺口，不宣称确认原因，不安装 VS 组件/SOS/包。

## 9. 本轮执行结果（采证后）

- 报告修正：FIX-001/FIX-002 完成静态修正；未为它们运行代码测试。
- 采证工具链有限对照：默认关闭 hook、monitor/collector 握手成功（control 12）；无 monitor 超时控制 30.909 秒；复用 Review 强持有/释放/首快照对照。
- 正式 full-solution（仅一次）：Release build exit 0（201 warnings/0 errors）；test exit 1；103 TRX、6 空；12,881/1/1,114；net6 6,715/1/593，net8 6,166/0/521。
- 目标 `RepeatedClosedApplications...` net6/net8 均 Passed，无 ready/dump：目标 `CAPTURE_INCOMPLETE` / `NOT_REPRODUCED`。
- 正式唯一失败为相关 `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection`（net6）；没有目标 dump/根链，`ROOT_CAUSE = NOT_DETERMINED`。
- 候选：`BingModuleManager` shutdown completion 在异步状态机 finally 之前发布，可能短暂保留 manager/provider；未确认、未修补，交 Sol/high 审查。
- 停止：未运行第二次 full-solution，未追加压力/无限 GC，未修改生产代码；旧 Stage B 证据不改写。

## 10. Review Round 2 修复结果

- FIX-001 保持 CLOSED，不重开。
- FIX-002：当前正式候选改为 `gc-capture-20261023-01`；首轮 final 标历史；正式 build/test 结果、目标未复现、net6 相关失败、source/binary 关联、Describe/49TRX/编译失败、工具迭代分类、H6 同步前提均已准确记录。
- FIX-003：runner/monitor/collector/timeout 脚本落实同一冻结 deadline 的正预算检查；过期写 `TIMEOUT_NOT_STARTED` 且 0 次新启动；活动 wait 使用正剩余；deadline 后仅有限清理本次创建的进程树；timeout-control 增加 45 秒外层 watchdog。
- 纯虚拟确认：预算越界后 0 次 monitor/test；timeout/cleanup 仅清理假 PID；`contractSatisfied=true`。
- 未运行真实 build/Core/solution/GC/并发/压力/dump；未修改生产代码；未修改 `review.md`/旧 Stage B 证据。
- 下一步：交主线程转 Sol/high 第三轮复核；不再自主采证。

## 11. Review Round 3 限定收尾结果

- FIX-002：报告三处事实冲突已统一；当前候选、来源限制、H6、停止边界保持。
- FIX-003：最后准入、真实 started/PID、try/finally 清理、实际剩余 wait 已落实；45 秒 timeout-control 保留。
- 纯虚拟确认：build/hash 越界 0 启动；目录准备跨截止仅 monitor/test 0；PID 发布 OSError 清理 test/monitor 两树并保留异常；`contractSatisfied=true`。
- 未运行真实 build/Core/solution/GC/并发/压力/dump；未改生产/hook/StageB/review；`ROOT_CAUSE=NOT_DETERMINED`，`GC_PROBLEM=未解决`。
- 停止：交主线程转 Sol/high 复核，不自动第四轮或继续采证。
