<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261023-module-lifecycle-retention-fix
AI_REVIEWED_AT: 2026-10-23T18:50:02+08:00

# Admin InventoryProbe 构建恢复专项验收

Reviewer：GPT-6.1-Sol / high，使用 review-code。已读适用 AGENTS、GOAL_RULES、任务 plan/execution、独立 Review2、build-recovery-plan/diagnosis/CIA、两项目真实 diff、本机 SDK targets 和最小构建 raw；Luna worker独立核对命令、结果及历史边界。本报告只验收两个 Admin 测试项目的构建元数据恢复；原 review.md、GC 生产修复及旧工具三项结论不变。

**结论：PASS，OPEN_ACTIONABLE=0。** 修改符合 SDK 的单目标框架引用协议，保留构建依赖和复制行为。具备由主线程在新的构建候选恢复最终 checkpoint 的条件。本次未运行 build/test/MSBuild 评估或 GC/Probe。

```text
REVIEW_SCOPE: ADMIN_INVENTORY_PROBE_BUILD_RECOVERY
IMPLEMENTATION_STATUS: PASS
AFFECTED_MINIMAL_BUILD: PASS
OUTPUT_COPY_IDENTITY: PASS
OPEN_ACTIONABLE: 0
MUST_FIX: 0
SHOULD_FIX: 0
PREVIOUS_FINAL_CHECKPOINT_BUILD: FAIL_CS2012
PREVIOUS_FINAL_CHECKPOINT_TEST: NOT_STARTED
NEW_FULL_RELEASE_CHECKPOINT: PENDING_MAIN_THREAD
HISTORICAL_LOCK_OWNER: NOT_DETERMINED
HISTORICAL_CS2012_EXCLUSIVE_CAUSE: NOT_DETERMINED
GC_PRODUCTION_REVIEW: PASS_UNCHANGED
OLD_GC_TOOL_FINDINGS: CLOSED_UNCHANGED
NEXT_ACTION: STOP_HAND_BACK_TO_MAIN_THREAD
AUTO_CONTINUE: false
```

## 实际调用链与验收矩阵

两个 Tests 项目最终均为 net6.0；两个 InventoryProbe 均在自身项目声明单一 TargetFramework=net6.0。项目 diff 与 recovery-diff.patch 一致：只移除 ProjectReference 的 SetTargetFramework，以及 CopyInventoryProbeToTestOutput 内 Properties 中的 TargetFramework；Configuration、ReferenceOutputAssembly=false、BuildReference=true、Build 后复制及 solution 四项入口均保留。暂存区这两个项目无差异，不把其他 dirty 工作区内容算作本次修改。

| 原计划/验收项 | 判定 | 真实行为与证据 |
| --- | --- | --- |
| 去掉冗余全局 TFM、保留正确框架选择 | PASS | SDK 10.0.400 的 _GetProjectReferenceTargetFrameworkProperties 在有显式 SetTargetFramework 时跳过常规协商（:1831）；删除后调用 GetTargetFrameworks，单 TFM 返回 HasSingleTargetFramework=true（:2001），随后将 TargetFramework 加入 UndefineProperties（:1956–1961），让引用项目自身的 net6.0 生效。不是把 probe 改成测试项目的多目标构建。 |
| 保留单项目构建依赖、solution 默认并行图 | PASS | SDK ResolveProjectReferences 的实际 MSBuild 调用以 BuildReference=true 为准（:2153–2158）；ReferenceOutputAssembly=false 只控制返回输出是否作为程序集引用，不取消依赖构建。Configuration 显式保留。solution 根项仍在，最小 solution 同时包含两个 probe/两个 Tests；无串行化参数或禁用并行变更。 |
| GetTargetPath 与输出复制语义 | PASS | Configuration 继续按调用方传入；probe 自身 net6.0 决定路径。GetTargetPath 返回 TargetPathWithTargetPlatformMoniker，相关 targets 无 Compile/Build 依赖；原递归文件选择、空结果 Error 和 OutDir/InventoryProbe 目标映射未变。实际 Release 复制 DLL 与各自 probe 输出 hash 相同。 |
| 当前受影响构建与产物证据 | PASS | minimal-build/command.json 为四项目 slnx 的 dotnet build -c Release --no-restore，无 -m:1；result exit0、未超时、7.50463s。日志154 warnings/0 errors，MSBuild内部耗时7.33s，无 CS2012。当前四份 DLL hash 仍匹配记录。 |
| 诊断/报告边界及最终检查点移交 | PASS | diagnosis/recovery-result 明确只支持静态冲突风险与最小构建成功；原正式失败、未启动测试和锁归因未知均保留。完整 solution 未重跑，不作为本次实现 finding。 |

本机 SDK 关键 targets 摘录及文件 hash 见 [sdk-targets-excerpts.json](artifacts/build-recovery-review/sdk-targets-excerpts.json)。本轮输入身份见 [reviewed-inputs.json](artifacts/build-recovery-review/reviewed-inputs.json)。

## 全局属性与因果证据的准确范围

原始 project-evaluation.json 是**评估阶段**的 ProjectReference 元数据，删除 SetTargetFramework 的 post-fix 结果不等于实际目标执行时所有继承属性的完整记录。本次判断同时追踪 SDK 的框架协商与 ResolveProjectReferences，不能只凭两份 JSON 的属性字样或相同输出路径认定构建身份已完全一致。

常规 solution/单项目 Release build 没有全局 -f 时，测试项目自身声明的 TargetFramework 是局部评估属性，不会仅因它存在就自动变成子任务的全局属性。若调用方显式传入全局 TargetFramework（例如合法的 net6.0），自定义 GetTargetPath MSBuild 任务仍可能继承它：删除 Properties 中的显式赋值并不等于 RemoveProperties。标准 ProjectReference 路径则按 SDK 的单 TFM 协商取消此属性。这里额外的 GetTargetPath 实例只查询路径，不执行 probe 编译；不能把它与实际编译实例等同。当前两个 probe 的 TFM 与支持的测试 TFM 一致，Debug/Release 仍由 Configuration 选择，没有发现由此导致的构建/复制回归。

SDK 源码还明确解释不同全局属性集会形成不同项目实例，并可能并行争用同一资源（:1800–1804）。结合此前显式 TFM 与无该全局属性都写同一 obj/bin，足以支持移除冗余引用配置的合理性。**这不证明该风险就是历史 CS2012 的唯一原因，也不证明当时谁持锁。** 尤其原 AfterTargets 的 GetTargetPath 本身不构成另一次编译证据；报告中的图风险主要落在原 ProjectReference 的构建路径。

## 原始结果、历史失败与 Release

原始命令、结果、当前 DLL hash 复核及历史摘要见 [raw-audit.json](artifacts/build-recovery-review/raw-audit.json)。证据来自 build-lock-diagnosis/minimal-build；不把 minimal-build-attempt1-path-error 的失败混入成功：首次临时 slnx 相对路径错误导致 MSB3202、exit1、0.321024s，随后路径修正后的最小构建才是本次成功记录。该准备错误保留，不宣称整个准备过程只有一次无失败启动。

正式 final-candidate-checkpoint 仍是 Bing.All.slnx Release --no-restore 在16.409514s内 exit1，FreeSQL.InventoryProbe 的 obj/Release/net6.0 DLL 写入 CS2012；测试 NOT_STARTED，TRX=0。后来的 Restart Manager NO_LOCKERS 只是采样时无锁，进程快照没有提供故障时的文件句柄证据；不归因用户 Sample、某个 PID 或其他线程，也未停止进程。

| 维度 | 专项结论 |
| --- | --- |
| Implementation | PASS；本次两个项目配置修正完成，5/5专项验收项通过，无可执行 finding。 |
| Affected validation | 既有最小默认并行 Release build PASS，复制身份 PASS；本轮仅 L0 源码/SDK/日志/身份静态核对，新增 L1–L5 运行0。单项目行为按实际 SDK 依赖链静态验收，没有冒称另跑单项目构建。 |
| GC 修复与既有受控因果 | 原独立 Review2 PASS 保持；本专项不重新判断历史 GC 排他归因，不用构建成功宣称 GC 已解决。 |
| Full Release | 原 checkpoint build FAIL/test NOT_STARTED；恢复后的完整 build/test 尚无新结果。由主线程冻结新的构建候选并执行一次最终 checkpoint，记录新结果，旧失败保留。 |

## 停止边界

| 分组 | 当前项 |
| --- | --- |
| Completed | SDK 单 TFM 引用协议、构建依赖、配置/路径查询/复制、最小 raw 与历史边界验收。 |
| Open Actionable | 0；无 MUST_FIX/SHOULD_FIX，不派发下一轮修复。 |
| Blocked Approval | 无。 |
| Blocked External | 本专项无。 |
| Not Applicable | Admin 业务扩展、GC/旧 Probe 重跑、外库、压力、串行化构建、版本/SDK/TFM/依赖/API baseline/预算变更。 |
| Accepted Limitations | 未捕获历史锁持有者；最小成功不证明历史唯一根因，也不替代完整候选检查点。 |
| Verified Boundaries | 当前 SDK 的框架协商恢复；默认并行最小构建成功；输出复制身份匹配；旧失败未冲销。 |
| Deferred | 主线程安排新的最终 Release build/default solution test checkpoint。 |

No-Progress：CHANGED（构建元数据恢复及对应最小证据已验收）。Next Action：**STOP，交回主线程**。只写本专项报告及 artifacts/build-recovery-review；未改原 review.md、实现/测试/执行报告、旧 raw、Stage B 或父路线图，未执行 Git 写操作、通知、进程停止或额外构建。
