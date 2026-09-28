<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_REVIEWED_AT: 2026-09-28T09:31:00Z

# 模块上下文与扫描扩展：修复后复审

## 结论

当前两组新增主能力均已落地，没有整组尚未实现的能力。原模块完整生命周期计划 T1–T5 沿用 Round 22 验收及上轮完整回归，未发现本轮修复引入新的生命周期能力缺口。

上轮 FIX-001 至 FIX-005 中，4 项 CLOSED。FIX-002 默认路径已修复，但扩展注册场景仍存在对象保留，已用独立探针确认。当前仍有 **1 项 SHOULD_FIX / OPEN_ACTIONABLE**，不能将 execution.md 的 OPEN_ACTIONABLE=0 作为最终验收结论。

- IMPLEMENTATION_STATUS: NEEDS_FIX
- TEST_STATUS: PARTIAL（既有回归通过；新确认边界尚无正式回归用例）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（默认路径、插件成功/失败候选回收已有证据；追加查找器后闭包保留已复现）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- RELEASE_STATUS: NOT_ASSESSED
- OPEN_ACTIONABLE: 1
- BLOCKED_APPROVAL / BLOCKED_EXTERNAL: 0
- GOAL_STATUS: STOPPED_REVIEW

本轮检查完整计划、执行报告、实际调用链、测试映射、工作区及暂存区差异和相关未跟踪文件；仅修改审查报告并在忽略目录 TestResults 中创建探针，未修改生产代码、正式测试、计划或执行报告。旧审查保存在 [review-before-rereview-20260928.md](review-before-rereview-20260928.md)。

## 计划验收矩阵

| 任务 | 状态 | 依据 |
| --- | --- | --- |
| CTX-01 配置和宿主环境 | PASS | 最后直接实例优先；无直接注册时回退 HostBuilderContext；工厂不提前执行，环境缺失无默认值 |
| CTX-02 生命周期上下文 | PASS | 从当前作用域读取配置及环境，无新增缓存或订阅；工厂、宿主隔离、reload 和真实生命周期测试存在 |
| SCAN-01 声明和快照 | PASS | 模块声明、继承、应用补充、自定义 loader、去重、只读描述符及配置时机冻结已接入；未选声明的独立范围有测试 |
| SCAN-02 DI、选项和事件 | PARTIAL | 两种模式及选项/事件语义已实现；原查找器被后续追加注册遮蔽时，筛选委托没有释放 |
| SCAN-03 插件和回收 | PASS（已验收路径） | 私有附加 DLL、本代身份、版本切换、类型发现失败诊断及旧代/两类失败候选回收均有直接证据 |
| DOC-01 文档和映射 | PASS（交付物） | README、迁移、ADR 和直接测试映射已存在；修复后须更新执行记录和映射 |

按当前计划为 5 PASS / 1 PARTIAL / 0 完全未实现，不换算为代码完成百分比。

## 上轮 Finding 核对

| Finding | 状态 | 依据 |
| --- | --- | --- |
| FIX-001 | CLOSED | ConfigureModulesAsync 消费复制后的 registrationMode；RegistrationModeMutation_ShouldNotRepeatOrSkipTypeEvents 覆盖同步/异步、Pre/Post、双向变更，断言事件一次、选项绑定及晚追加程序集不生效 |
| FIX-002 | OPEN_ACTIONABLE | 普通成功、失败、关闭自动 DI 已有弱引用测试；清理只查最后一个描述符，不能保证清理框架原实例 |
| FIX-003 | CLOSED | GetHostEnvironment 回退及直接注册优先已实现；ModuleContextAccessTest 覆盖最后上下文、无效直接注册、工厂不执行和缺失环境 |
| FIX-004 | CLOSED | BingModuleAssemblyFinder 附加扫描来源、阶段及 LoaderErrors，插件加载器补充上下文；真实缺失类型依赖用例断言原异常及诊断字段 |
| FIX-005 | CLOSED | HotPluginHostTest 验证附加 DLL v1/v2 身份和服务、旧代以及缺失 DLL/类型依赖两类失败候选回收；ModuleScanningTest 验证唯一未选声明、重复输入、自定义 loader 和只读集合 |

## FIX-002：追加查找器注册后，原筛选委托仍被保留

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题：CompleteDependencyScan 使用 LastOrDefault 取最后一个 IDependencyTypeFinder 描述符，再读取其 ImplementationInstance。模块在 PostConfigureServices 中调用 AddSingleton<IDependencyTypeFinder, DependencyTypeFinder>() 追加注册后，最后一项是类型注册，清理被跳过；原实例描述符仍留在服务集合中。
- 证据：framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingApplicationServiceCollectionExtensions.cs:169–171；原实例在同文件 Prepare 创建。framework/src/Bing.Core/Bing/DependencyInjection/DependencyTypeFinder.cs:18、37–41 保存并清理委托。
- 引用路径：存活的 IServiceCollection → 较早的实例描述符 → DependencyTypeFinder → ConventionalTypeFilter → 捕获对象。追加注册改变单服务解析结果，不会移除旧描述符。
- 复现：TestResults/context-scan-rereview/Program.cs 创建带实例筛选器的应用，在 Post 阶段可选地追加上述注册；退出独立栈帧后 GC，并保持集合存活。普通场景对象可回收，追加场景仍存活。探针未主动保存原查找器或筛选对象。
- 影响：计划 §3.3 要求释放注册期委托，但正常扩展注册使捕获对象图被延长保留。这不是远程可利用漏洞，也不是证明服务集合自身无法回收。
- 修复目标：清理绑定框架创建的原实例，不依赖完成时最后一个服务描述符。
- 修复建议：由注册流程局部对象或当前注册记录明确持有原查找器，同步/异步 finally 直接完成并清空所有权引用；不通过删除用户注册或禁止扩展掩盖问题。
- 验证要求：追加实例/类型查找器后，集合仍存活时原捕获对象可回收；成功、异常、异步取消共用确定清理路径；重新查询同时断言允许类型存在、被拒类型不存在；更新映射和执行记录。

## 验证及证据复用

影响分析：本轮无生产代码、公开契约、TFM、Provider 或构建图变更。审查范围为上轮 Core 修复、PluginRuntime 调用链及相关测试文档，风险集中在注册资源所有权。无 SQL/数据库或性能基准需求。

本轮使用 SDK 8.0.424 构建并运行独立 net8.0 探针，引用上轮当前 Core 测试输出：

    FilterRetainedWithPostOverride(False)=False;Finders=1
    FilterRetainedWithPostOverride(True)=True;Finders=2

两种场景期望均为 False；第二种未满足。git diff --check 通过。

正式回归复用同会话上一实施轮工具结果：Core net8.0/net6.0 各 487 通过；MVC 各 53 通过、1 跳过；WebApi 构建成功。本轮没有重复执行完整矩阵。这些用例未覆盖新确认的追加查找器场景，不能据此否定 Finding。

测试补强纳入同项修复，不另算缺失功能：现有筛选闭包弱引用用例仅同步入口，应补异步取消；筛选结果目前只排除 SiblingService，还应确认 DynamicService 存在，防止空结果假阳性。

## TODO

### 本期：1 个问题及其验收工作

- [ ] FIX-002：让清理绑定框架原查找器，避免追加注册后漏清筛选委托。
- [ ] 同项测试：覆盖追加注册、异步取消弱引用和筛选结果正向断言。
- [ ] 同项交付：更新 execution.md / test-mapping.md，按影响范围验证并复审。

以上三步属于同一根因及其交付，不是三个缺失能力。

### 后续：4 组 DEFERRED，不属于本期缺陷

- [ ] 自定义生命周期阶段/阶段贡献者：目前固定 Pre/Main/Post 和单一关闭阶段；需另行定义排序与异常契约。建议任务：生命周期阶段扩展点。
- [ ] 插件在线分发与多版本求解：本地版本区间校验已实现；远程下载、升级编排和候选版本求解另立任务。
- [ ] 不可信插件进程隔离：当前完全可信，ALC 不构成安全沙箱。建议任务：隔离插件宿主。
- [ ] 动态 Web 管道替换：现有 UseBing/UseBingAsync 装配管道，插件代切换不重建既有管道。建议任务：Web 插件路由与管道更新。

上述方向因本期计划明确排除而延期，不阻碍本期验收。完整 ABP 上层生态不纳入本次模块计划。

Accepted Limitations：默认上下文不可卸载；热更新为整组插件代；调用方自己保存旧代对象会阻止协作式卸载；旧静态兼容入口仅支持单宿主。

No-Progress Check: CHANGED（确认 FIX-002 剩余所有权边界并取得运行证据）。Next Action: STOP_REVIEW；本轮不自动修复。
