# Astra GC 续查计划

## 起点与授权

真实续查起点记录于 artifacts/astra-investigation/startpoint.json（在本轮只读阅读后、首次改动/实验前捕获）。本轮由 Codex / GPT-6-Astra 负责总体判断，用户指定 xhigh。当前用户指令及 continuation-authorization.md 覆盖旧 OpenCode 专属调查/停止要求；不等待旧计划批准。

既有正式候选 gc-capture-20261023-01：build PASS，solution FAIL，12881/1/1114；失败是 net6 RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection。旧 Stage B 同总数但失败为 net8 RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive，分开使用。历史人工 dump 只证明工具。

## 范围与执行

1. 读取两个同步路径、DI 描述符/闭包/容器缓存、JIT 栈活性、AsyncLocal/ExecutionContext/全局状态。以具体可达引用解释对象所有权，不把未复现写成排除。
2. 根据源码选择最有依据且此前没有观测过的假设；实验前追加固定工作量、截止、观测目标、判据及与历史实验的差异。同类一次诊断+一次确认。
3. 优先独立诊断夹具与确定性信号；保留现有两测试和默认关闭 hook。若修改既有测试须另记影响，当前不修改。
4. 证明因果后交付失败回归和最小修复方案，由主线程实施生产修复/组织 Sol 验收；若缺证据精确记录最小缺口，不以局部绿覆盖旧 FAIL。

文件所有权：本计划、astra-diagnosis.md、必要新增诊断/回归夹具、artifacts/astra-investigation/。原 plan/execution/diagnosis/traceability/review、Stage B、历史 raw 与 OpenCode 所有工具不写。Luna 仅做原始 TRX 字段与时间机械提取。

## CIA（本轮初始）

- ChangedFiles：本计划、新诊断报告、独立 artifact；后续可能新增专用诊断夹具。
- ChangedProjects：当前无；若新增夹具只涉及 Bing.Core.Tests 或 artifact 内独立探针。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：生产路径不改；观测 DI 解析/模块同步生命周期和对象回收。
- ChangedProviders：无数据库 Provider。
- ChangedTFMs：不改，精准观测现有 net6/net8。
- ChangedBuildPackaging / ChangedBenchmarkHarness：无。
- ChangedDocsOnly：准备阶段是，实验阶段追加记录。
- AffectedDependents：无生产依赖变更。
- RiskLevel：LOW（隔离诊断；全局/线程池观察如需要只在独立进程）。

## 证据与停止

每次实际命令保存 UTF-8 stdout/stderr、退出码、开始结束时间和预设截止。复用既有全量，只运行新观测必要的精准 build/L1。禁止大量压力/全量、改断言、关闭并行、延长睡眠或预算、追加 GC 追绿。无新观测/达到同类限次即停止该假设；不因 NOT_DETERMINED 自动修生产。

当前：ROOT_CAUSE=NOT_DETERMINED；GC_PROBLEM=UNRESOLVED。Completed：规则/授权/旧报告阅读、起点捕获。Open Actionable：同步路径引用分析及新观测实验。Blocked Approval：无。Blocked External：无。Not Applicable：重复全量/压力与工具 FIX-003。Accepted Limitations：历史失败无真实根链。Verified Boundaries：尚无新增。Deferred：生产修复后的最终全量由主线程安排。

## 实验 A1：DI 第二次解析触发的后台编译所有权（运行前冻结）

新观测：两 TFM 实际使用 Microsoft.Extensions.DependencyInjection 6.0.0。官方 v6.0.0 源码 DynamicServiceProviderEngine 在第二次解析把闭包排入 UnsafeQueueUserWorkItem；IServiceScopeFactory 是 ResultCache.None 的 ConstantCallSite。InitializeCoreAsync 与 CleanupAsync 各一次扩展 CreateScope，共两次解析。候选引用链为线程池工作项/活动回调 → display-class → engine._serviceProvider，以及 callSite.Value → RootProvider。与旧实验的实质差异：不等概率复现，显式停在真实 DI 编译事件并证明字段引用相等。

隔离 console 探针只引用现有测试输出与安装的 .NET 引用程序集，由 SDK 10.0.400 csc 直接编译，无新 csproj/PackageReference/全局工具。调用现有 private 工厂，或现有 public 测试方法；不改原测试/断言/并行/GC次数。EventListener 只保存字符串/数值/事件，不持有 provider/module；在 IServiceScopeFactory 的 DynamicMethodBuilt 事件暂停真实编译工作项。闸门单次最多 10 秒，进程外截止 15 秒，程序 finally 释放闸门。使用普通线程池，不调整 Min/MaxThreads 或测试并行设置。

A1 诊断固定 4 个独立 net6 进程：retained 工厂一次；repeated 工厂一个 app；纯 DI 一次 CreateScope 负对照；纯 DI 两次 CreateScope 阳性。每进程工厂退栈后先用 NoInlining helper 输出实际闭包字段引用等同性（只返回字符串/布尔/弱引用），再执行原标准 3 轮 GC。不会在该进程追加 GC 追绿。预期：两次解析出现 callCount=2、实际 engine/callSite 指向本 provider、活跃线程栈含 DI 编译回调、disposed provider 仍 alive；一次解析无工作项且可回收。若无编译事件或字段不符合，报告无效/未证，不再无观测重试。

A1 确认预定：诊断支持机制后，各 net6/net8 执行原 retained public 测试及原 repeated public 测试各一次（预期原断言失败，退出码 1）；再各执行一次纯 DI 缓存 scope factory 对照（解析一次、CreateScope 两次，预期无编译工作项且回收）。共 6 个独立进程，不重跑全量。不是把失败作 PASS：探针结果分别保存实际退出码与预期机制判据。

本轮编译截止45秒；诊断总绝对截止120秒，确认总绝对截止120秒，在每批启动前记录。任何超时停止该批，只有已创建直接进程可清理。A1结论最多两批；不因换TFM再加循环。

CIA 追加：只新增 artifact 内 Probe.cs/run_probe.py；直接编译现有依赖不改生产/测试项目。L0=独立编译；L1=上述固定观测；L2-L5=不运行。风险 LOW，EventListener 扩大原有后台工作窗口只发生于独立探针进程；这是受控调度因果验证，不冒充自然失败 dump。

## A1 收口

诊断4个进程符合预期；确认原两测试×两TFM均原断言失败（exit1，repeated各84项），两TFM纯DI缓存factory对照回收成功（exit0）。无超时。实际Core/Test DLL与正式gc-capture候选hash相同。已交付 astra-diagnosis.md 与未应用candidate-fix.NOT-APPLIED.patch；生产/原测试未修改。A1两批额度已用尽，停止继续试跑。当前已证DI后台编译足以造成失败；历史排他根因仍未确定，GC整改未验收。后续实现/最终全量由主线程组织。
