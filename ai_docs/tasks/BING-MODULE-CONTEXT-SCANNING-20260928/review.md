<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_REVIEWED_AT: 2026-09-28T13:00:09.858011+00:00

# 计划完成度复核与分类 TODO

## 结论

当前计划 CTX-01、CTX-02、SCAN-01、SCAN-02、SCAN-03、DOC-01 共 6 个实施任务均已有实现及对应证据，未发现仍未实现的计划内必需能力。本轮复核关闭 FIX-006、FIX-007；历史 FIX-001 至 FIX-005 维持 CLOSED。原生命周期 T1–T5 的基类钩子、异步注册、全模块分阶段初始化、扫描时机和热更新接入未发现新增缺口。

剩余为 2 项 OPTIONAL 易用性建议、4 组明确延期的能力、1 项外部安全审计证据。PASS_WITH_ISSUES 不等于具备 ABP 全部生态能力，也不等于第三方依赖安全审计已通过。

- IMPLEMENTATION_STATUS: PASS
- TEST_STATUS: PASS（复用相同源码范围的上一轮测试证据；MVC 各 1 项既有跳过）
- RESOURCE_EVIDENCE_STATUS: PASS（限本计划弱引用、事件退订及释放次数场景）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（NuGet 漏洞数据源）
- RELEASE_STATUS: BLOCKED（依赖安全审计证据仍缺失，不是新增实现缺陷）
- OPEN_ACTIONABLE: 0

## 逐项验收矩阵

| 计划项 | 结论 | 实际调用链与证据 |
| --- | --- | --- |
| CTX-01 配置/环境访问 | PASS | ServiceCollectionInstanceReader 采用最后一次显式实例；配置/环境扩展保留 HostBuilderContext 回退，拒绝注册期执行工厂；ModuleContextAccessTest 覆盖优先级、缺失、非 Root 配置及替换 |
| CTX-02 生命周期便捷访问 | PASS | BingModuleContextExtensions 从传入上下文的当前 provider 解析，不建额外作用域或缓存；ModuleContexts_ResolveScopedServicesFromCurrentProvider、ModuleLifecycle_UsesCurrentContextAcrossAllStages 覆盖实际生命周期 |
| SCAN-01 声明与快照 | PASS | Prepare 在配置前创建 BingModuleAssemblyFinder，合并最终模块声明和应用附加程序集，按实际程序集去重、校验跨代、设置描述符快照；ModuleScanningTest 覆盖未选择模块、继承、只读、冻结及自定义 loader |
| SCAN-02 DI/选项/事件 | PASS | DependencyTypeFinder 只对约定 DI 候选筛选；BingLoader 消费完整类型目录；CompleteDependencyScan 直接清理框架持有的原查找器；两种模式、晚选项、事件次数、追加查找器和弱引用均有测试 |
| SCAN-03 插件与回收 | PASS | BingPluginLoader 在临时解析范围内完成模块元数据准备和已部署静态私有依赖预加载，finally 退订；HotPluginHost 等待异步配置及三阶段初始化后切换；私有/附加程序集、失败候选和旧代回收有测试 |
| DOC-01 文档与映射 | PASS | Core README、迁移指南、插件指南、ADR 和 test-mapping.md 已提供使用、兼容语义及边界；本轮未改变文档契约 |
| 原 T1–T5 生命周期 | PASS（兼容复核） | BingModuleHooks 识别有效重写及接口桥接；ConfigureModulesAsync 和 InitializeCoreAsync 按全局阶段执行，固定扫描模式；热更新 BuildGenerationAsync 在 UseBingAsync 成功后才发布新代 |

“PASS”针对上述已批准范围，不涵盖任意动态加载、本机依赖或不可信插件沙箱。

## 已关闭问题

### FIX-006：CLOSED

ReleaseAsync 在只读集合中跳过描述符移除；可变集合仍尝试移除，枚举/移除异常加入清理列表后继续清空模块实例、插件容器和所有权记录。InitializeCoreAsync 捕获意外清理异常并附加到原始异常，随后进入 Failed。

直接证据：真实 WebApplication 的显式关闭、Host Stop、初始化失败且关闭成功/失败共 4 个案例；自定义可变集合 RemoveAt 抛错测试验证原始异常对象、清理聚合、内部实例清空、Failed 不可重试及释放一次。未通过解除宿主只读保护或额外根容器规避问题。

### FIX-007：CLOSED

PreloadPrivateDependencies 遍历已部署候选中的静态程序集引用，包括方法体引用和传递引用；当前代局部保存已访问集合，沿用现有程序集身份检查，不扩大服务扫描。模块元数据预检加载的附加程序集也在事件退订前补齐依赖。Resolving 在成功/失败两种路径均 finally 退订。

PluginPrivateDependencyTest 使用不被测试宿主直接引用的 Entry/Helper/Leaf 项目，覆盖默认和可回收上下文的配置、初始化、服务调用；验证扫描范围不扩大、成功旧代及失败候选可回收、失败后旧代继续服务。既有缺失依赖、身份冲突、解析事件退订测试也包含在上一轮完整回归中。

未部署且未使用的可选引用仍按原语义留到实际使用路径报错。字符串 Assembly.Load、运行中新出现 DLL 和本机 DLL 不在静态预加载支持范围，文档已明确，不将其再列为 FIX-007 未完成部分。

修复前证据保存在 [review-before-fix006007-verification.md](review-before-fix006007-verification.md)。

## 验证范围

本轮只读复核源码、Git 差异、计划、执行记录、测试源码与 TRX，不改业务代码、不重新运行全量测试。相关实现自上一轮验证后未在本轮修改，采用 REUSED_UNCHANGED_SCOPE；这不是对后续未知改动的验证。

| SDK 8.0.424 的既有证据 | 核对结果 |
| --- | --- |
| Core net8.0 / net6.0 | 各 497 通过，0 失败 |
| MVC net8.0 / net6.0 | 各 57 通过、1 既有跳过，0 失败 |
| WebApi | 生成成功，0 错误、1 个 net6 支持结束警告 |
| 新增案例 | Core 4 个、MVC 4 个，双框架全量 TRX 中均存在且通过 |

实际运行时间为 2026-09-28 20:39–20:41（UTC+8）。证据位于 TestResults/modularity-audit-20260928：core-fix-final-net8.0/net6.0.trx、mvc-fix-final-net8.0/net6.0.trx、webapi-fix-final.log。MVC 跳过项是 RemoteStreamContentTestControllerTest.UploadRawAsync，与本轮新增资源回归无关。测试映射见 [test-mapping.md](test-mapping.md)。

本轮变更只有审查报告及旧报告归档，L0 核对格式与证据；无需重新编译或运行性能/容量实验。没有在本轮进行网络 CVE 查询或全仓渗透测试，不能给出“没有漏洞”的结论。

## 分类 TODO

### Completed

- [x] 当前计划 6/6 个实施任务。
- [x] 原 T1–T5 生命周期兼容复核。
- [x] FIX-001 至 FIX-007 已关闭，重点完成 FIX-006/007 的修复后验收。
- [x] 使用、部署、资源边界及生产符号到测试方法映射。

### Open Actionable

无新的 MUST_FIX / SHOULD_FIX。当前不需要为了功能完整度继续自动修复。

### Optional / Deferred：优先考虑的 2 项易用性增强

- [ ] UX-001：热更新切换结果诊断。ReloadAsync 先发布新代，再等待旧代退出和关闭；旧代关闭抛错时调用者收到异常但新代已经生效。建议提供结构化阶段与代标识，例如是否已切换、失败发生于构建/初始化/旧代关闭。验收：同版本切换、候选失败、旧代关闭失败均能准确辨别，并确认当前服务可用性。
- [ ] UX-002：旧代排空可观测性。提供当前/退役代标识、活动调用数和等待状态，帮助定位迟迟不结束的 RunAsync。验收：受控长调用期间可观测等待原因，退出后自动清理；不因超时强制释放仍使用中的实例。

建议顺序 UX-001 → UX-002；复用同一代标识，避免两套状态定义。这两项属于后续增强，不是已批准计划的遗漏。

### Blocked External：1 项安全证据

- [ ] 恢复 NuGet 漏洞源访问后审计 Core、PluginRuntime 和 Web 消费者的直接/传递依赖，保存数据源时间及结果。此前 TLS/凭证失败，不能以构建/单元测试替代。

### Deferred：4 组后续能力

- [ ] 自定义生命周期阶段和阶段贡献者；目前固定 Pre/Main/Post。
- [ ] 插件在线分发与多版本依赖求解；目前验证已部署版本及区间，不自动下载或选版本。
- [ ] 不可信插件进程隔离；当前只支持完全可信代码，ALC 不是权限沙箱。
- [ ] 动态 Web 管道替换；当前整组服务代切换不替换已装配的 Web 中间件管道。

Blocked Approval：无。Not Applicable：SQL、容量/吞吐基准、发布动作。Accepted Limitations：默认上下文不可卸载、整组热更新、协作排空、旧静态入口单宿主语义、静态预加载边界。Verified Boundaries：未新增容量结论。

No-Progress Check: CHANGED（修复后验收关闭 2 项）；Next Action: STOP；Goal Status: STOPPED_BLOCKED（仅外部审计仍缺证据）。本轮没有修改计划、执行报告、业务代码或测试，也未提交、推送或发送外部消息。
