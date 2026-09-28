<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_EXECUTION_FINISHED_AT: 2026-09-28T07:31:31Z

# 实施执行报告

> 历史 Round 1/2 内容保留为审计证据；当前实现结果以末尾最新的 Review Fix 记录和补充验证为准。`review.md` 由独立 Reviewer 维护，不在本轮改写。

## 执行结论

按用户批准的兼容式方案完成实现、直接回归测试、资源验证及迁移文档。旧入口保留立即 AddServices 与历史排序；新入口采用完整依赖图、分阶段配置、统一生命周期及启动期可信插件目录来源。未加入热插拔或不可信代码隔离。

- IMPLEMENTATION_STATUS: COMPLETED
- TEST_STATUS: PASS（必要范围；MVC 有 1 项既有跳过）
- RESOURCE_EVIDENCE_STATUS: PASS（对象回收、次数、后台任务与订阅停止）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE（未提出性能改善结论或容量门槛）
- EXTERNAL_GATE_STATUS: PASS（已使用仓库锁定的 .NET SDK `8.0.424` 完成最终验证）
- RELEASE_STATUS: PASS（验证门禁完成；未执行发布、提交或推送）
- OPEN_ACTIONABLE: 0

## 任务信息

- Task ID：BING-MODULARITY-HARDENING-20260927。
- 基线：2c36316e210ad2081520fe56e7413c748bad92dd；开始时仅任务计划为未跟踪文件。
- 输入：plan.md 与用户随后批准的详细实施方案；后者固定公开契约、同步注册、可信启动时组合及验证顺序。
- 主代理负责架构、实现集成、生命周期及最终验证；Luna 子任务完成依赖图/选项/测试及指定文档等独立部分。
- 已遵循项目 AGENTS、execute-plan 技能及 GOAL_RULES；不因环境问题自动进入无关修复循环。

## 计划执行情况

| 项目 | 状态 | 结果 |
| --- | --- | --- |
| T00 直接测试与兼容基线 | COMPLETED | 隔离故障模块夹具；冻结旧立即注册、批量顺序、Web 管道行为 |
| T01 依赖解析与类型校验 | COMPLETED | 迭代遍历、环/排除链诊断、自定义依赖提供程序、构造前校验 |
| T02 静态选项订阅 | COMPLETED | 集合所属弱键注册器；配置重载与公开外部事件行为保留 |
| T03 实例、状态与所有权 | COMPLETED | 仅选中模块实例化；失败不可重用；唯一释放者及初始化至多一次 |
| T04 静态兼容层与日志 | COMPLETED | 按所属宿主解绑；新入口不绑定；日志输出后清空 |
| T05 描述符、排序与扫描 | COMPLETED | 可替换加载器、只读目录、拓扑优先、选中程序集扫描 |
| T06 异步生命周期与 Host | COMPLETED | 可选异步钩子、短期 scope、异常/取消清理、逆序停止、真实 Host 停止 |
| T07 资源证据与交付 | COMPLETED | 弱引用与后台资源测试、双框架测试、示例构建、迁移/ADR/测试映射 |

## 已完成事项

两种入口共用依赖校验与资源所有权，生命周期状态由管理器维护，不依赖 Enabled 或基类调用。Web 管道在显式 UseBing/UseBingAsync 时装配，重复成功初始化不重复装配，重入和并发 Web 初始化明确失败。非 Web 异步并发调用共享同一次初始化。

初始化失败包括失败模块在内逆序停止，清理错误附加到原异常；作用域释放失败也不会覆盖原启动异常。异步容器释放遇到正在初始化的模块会请求取消并等待退出。单个关闭失败不阻断其余清理，最终汇总异常。

配置失败包括旧入口配置委托、类型发现、AddServices 和扩展注册失败；无法回滚用户钩子的外部副作用，失败集合必须丢弃。

## 部分/未完成事项

计划内核心事项均已完成。未执行堆转储、正式吞吐/工作集基准或容量矩阵；这些不是本次用户批准的资源结论条件。没有将单次 GC 或工作集变化表述为全应用无泄漏证明。

## 修改文件

- Bing.Core：Builders/BingBuilder、Modularity 的图/加载器/描述符/生命周期/注册记录、两类注册扩展与 provider 生命周期扩展。
- Bing.Core：BingLoader、Configuration 绑定、DependencyModule/ServiceLocator、InternalServiceCollectionExtensions、StartupLogger。
- Bing.AspNetCore：BingApplicationBuilderExtensions。
- 测试：Core 的 ModuleGraphTest、ModuleRegistrationTest、OptionsTypeRegistrationTest、ModuleLifecycleTest；MVC 的 WebModuleLifecycleTest。
- 文档：Core/AspNetCore README、架构 §8、ADR-0001、ADR-0011、使用文档、最佳实践、迁移说明及索引。
- 完整方法对应：[test-mapping.md](test-mapping.md)。证据清单：[evidence-manifest.json](artifacts/evidence-manifest.json)。

## API/数据/配置变化

新增 AddBingApplication<TStartupModule>(Action<BingApplicationOptions>)，返回原 IServiceCollection。新增 IBingModuleLoader、IBingModuleContainer、BingModuleDescriptor、IBingModuleManager、前/后配置接口、异步初始化与同步/异步关闭接口及上下文。异步管理器和钩子使用 Task/CancellationToken。新增 provider/Web UseBingAsync 与 provider ShutdownBing/ShutdownBingAsync。

保留 IBingModule 原形状；没有数据库、SQL、版本号、global.json 或生产 TFM 变更。显式增加已有版本的 Microsoft.Bcl.AsyncInterfaces 6.0.0 生产引用，以确保 netstandard2.0 导出的 IAsyncDisposable 在 net8 消费者能解析；Core 测试直接引用 Microsoft.Extensions.Configuration 6.0.0 进行真实配置重载测试。未升级现有依赖。

## 测试结果

使用仓库锁定 SDK 8.0.424；安装在用户临时目录，不修改 global.json。首次网络受限恢复失败后，使用获准联网方式恢复 SDK/既有依赖，环境阻碍已消除。

所有 dotnet 操作顺序执行，避免共享输出目录互相覆盖：

| 测试项目 | TFM | 通过 | 失败 | 跳过 | 最终证据 |
| --- | --- | ---: | ---: | ---: | --- |
| Bing.Core.Tests | net8.0 | 323 | 0 | 0 | [fix6b-core-full-net8.trx](../../../framework/tests/Bing.Core.Tests/TestResults/fix6b-core-full-net8.trx) |
| Bing.Core.Tests | net6.0 | 323 | 0 | 0 | [fix6b-core-full-net6.trx](../../../framework/tests/Bing.Core.Tests/TestResults/fix6b-core-full-net6.trx) |
| Bing.AspNetCore.Mvc.Tests | net8.0 | 47 | 0 | 1 | [fix6b-mvc-full-net8.trx](../../../framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix6b-mvc-full-net8.trx) |
| Bing.AspNetCore.Mvc.Tests | net6.0 | 47 | 0 | 1 | [fix6b-mvc-full-net6.trx](../../../framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix6b-mvc-full-net6.trx) |

MVC 跳过是既有 UploadRawAsync，原因 Http Status Code 415；没有跳过新增模块测试。新增和补充的直接模块测试覆盖依赖图、注册、生命周期、资源释放和 Web 边界；完整方法对应见 `test-mapping.md`。

复现命令（dotnet 指向临时安装的 8.0.424）：

```powershell
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net8.0 --no-restore
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net6.0 --no-restore
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net8.0 --no-restore
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net6.0 --no-restore
dotnet build samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
```

最终 Core 测试覆盖最后补充的旧配置异常处理与全部资源用例。Web 完整测试和示例构建证据复用：此后生产改动仅将旧 AddBing 的配置委托/发现纳入同一异常处理，不改变成功路径、Web 适配或模块管理器；已追加直接失败回归并在双框架通过。没有仅因文档变更重跑无关测试。

### 资源验证结果

- OptionsTypeRegistrationTest.AddOptionsType_ShouldNotKeepServiceCollectionAlive：丢弃后的服务集合与配置在固定 GC 轮次后弱引用均不再存活；内置实现没有静态事件强引用链。
- ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive：12 个完整启动/关闭应用，provider、模块及 recorder 共 36 个弱引用均可回收。
- AsyncShutdown_CancelsBackgroundLoopAndUnsubscribesExactlyOnce：实际异步循环通过握手启动，关闭返回时任务已完成；停止后事件不再回调，重复停止不重复执行。
- 正常、失败、取消、容器释放、关闭异常均以释放计数和状态断言验证；不使用计时等待或一次工作集差值判断泄漏。

## Build/Typecheck/Lint/Format

Core 与 AspNetCore 在双框架测试中完成构建。WebApi 示例构建成功：0 errors、3 个既有 NETSDK1138 警告，18.38 秒。构建输出见证据清单记录的工具会话。git diff --check 通过；UTF-8 读取检查通过。无独立适用的前端 Lint/Typecheck；本任务不涉及 Bing.Data.Sql，因此 SQL/SQLite/外部数据库门槛不适用。

## 计划偏差

1. SDK 原本缺失，现已恢复到临时目录并完成实际验证，不再标记环境阻塞。
2. 自定义加载器须在 AddBingApplication 前以实例注册，不提前构建临时容器；返回结果再次校验，防止破坏依赖完整性/排序。实现实例仍由应用所有者释放。
3. 历史 Round 1 曾将只实现 IAsyncDisposable、没有 IDisposable 的模块在构造前拒绝；Round 2 已移除该前置拒绝。当前此类模块必须通过异步初始化/关闭入口运行，并由异步关闭和容器异步释放完成资源清理；同时实现两种释放接口的模块在异步关闭优先 DisposeAsync。
4. DI 不追踪外部预建模块实例，manager 采用工厂注册由 DI 追踪；若只构建裸 provider 却从不解析 manager，必须显式 ShutdownBing，即使未初始化也一样。Generic Host 会解析停止适配器。文档明确要求，不承诺尚未激活容器的自动释放。
5. 构建阶段资源模型与新 API 不支持同一服务集合共享多个根 provider；多宿主必须使用独立集合。

## 基线问题

保留现有 XML 注释、可空注解、过时实体接口和 ASP0019 警告；未因本任务修改无关源码。早期测试中缺少 AsyncInterfaces 运行时引用、精确类型断言及夹具初始化问题已修复。早期失败/中间成功的 TRX 保留为历史，不作为最终结果；有效证据由 manifest 显式列出。

## 已知问题与 Finding 状态

| Finding | 状态 | 关闭依据 |
| --- | --- | --- |
| MOD-001 依赖环 | CLOSED | ModuleGraphTest 环与菱形图测试 |
| MOD-002 静态选项订阅 | CLOSED | OptionsTypeRegistrationTest 隔离/回收/重载 |
| MOD-003 实例与资源所有权 | CLOSED | 未选中构造、重复释放、后台任务及弱引用测试 |
| MOD-004 失败/重复初始化 | CLOSED | 配置失败、重复、重入、取消、失败清理测试 |
| MOD-005 静态跨宿主 | CLOSED | 新入口隔离、旧宿主停止不解绑新宿主 |
| MOD-006 顺序不一致 | CLOSED | 新图拓扑与 Web/provider 一致；旧规则直接回归 |
| MOD-007 依赖提供程序 | CLOSED | 自定义/继承提供程序、显式父依赖、类型校验 |
| MOD-008 扫描/排除边界 | CLOSED | 排除冲突链、选中程序集 finder、关闭扫描 |
| MOD-009 目录缓存与动态卸载 | DEFERRED | 用户明确排除热插拔/ALC 卸载，不是本次修复目标 |
| MOD-010 启动日志保留 | CLOSED | Output 清空及初始化异常清理 |

局部代码核对额外发现并修复了异步初始化期间容器释放、scope 释放异常覆盖根因、同步配置的异步资源释放缺口，以及旧配置委托失败状态遗漏。不存在明确待执行修复项，不继续启动自动修复循环。

## 风险与回归关注点

- 已验证的资源路径已闭环；不等同于任意第三方模块或整个应用没有泄漏。
- 用户模块必须正确响应取消、退订事件、停止自己的后台任务；框架不能撤销任意外部副作用。
- 短期生命周期 scope 内的 scoped 服务不能长期保存在模块或中间件中。
- 旧静态定位器仅单宿主兼容，程序集扫描不构成安全隔离；公开 RegisterType 的外部订阅仍由订阅者负责退订。
- 生产目标框架及依赖版本不升级。热插拔、模块版本依赖求解、不可信插件隔离后续独立设计。

## Reviewer 注意事项

重点核对 BingModuleManager 的状态/异常/取消路径、BingModuleRegistration 的唯一释放责任、legacy 排序分支和服务集合所属选项注册器。测试映射列出了生产符号及全部直接测试方法。迁移说明和 ADR-0011 记录了新增契约及资源限制。本报告为实施验证，不冒充独立全仓审查结论。

## Git 状态

改动保留在工作区；已保护开始时的计划文件。未自动 git commit；未自动 git push；未自动创建 PR。task-finish 使用 --no-notify，不向外部发送消息。

## Review 修复记录

### Round 1

- 模式：REVIEW_FIX；Fix Scope：recommended。
- 输入 Review：NEEDS_FIX；保留 [review.md](review.md) 原样作为独立证据。
- 用户授权：修复列举的缺失项；直接处理范围为 FIX-001～004（1 个 MUST_FIX、3 个 SHOULD_FIX）。
- 执行状态：COMPLETED；修复执行器未改写 Reviewer 结论。

### Change Impact Analysis

- ChangedFiles：BingBuilder、BingModuleRegistration、新增 ModuleReferenceComparer、ModuleDependencyGraph；对应 Core 测试和迁移/任务文档。
- ChangedProjects：Bing.Core、Bing.Core.Tests；Web 消费者回归覆盖 Bing.AspNetCore.Mvc.Tests。
- ChangedPublicContracts：没有新增/删除公开成员；图失败统一提供 Data["Bing.ModuleDependencyPath"]（Type[]）；旧构建器图失败后按既定契约失效。
- ChangedRuntimePaths：注册图校验→失败清理；模块资源身份去重→同步/异步释放；依赖错误路径诊断。
- ChangedProviders/TFMs/BuildPackaging/BenchmarkHarness：无。
- AffectedDependents：Bing.AspNetCore 和 WebApi 示例。
- RiskLevel：MEDIUM；执行精准测试后升级到两项目双 TFM 回归，不涉及 SQL/数据库门槛。

#### FIX-001

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 根因：默认 Equals/GetHashCode 参与模块资源身份判断。
- 修改：ModuleReferenceComparer 使用 ReferenceEquals/RuntimeHelpers.GetHashCode；Own、_released、释放列表 Distinct 统一使用该比较器。Builder 新增模块过滤改为类型集合，防止值相等导致既有模块重复 AddServices。
- 验证：ModuleResourceRegressionTest 的正常/重复关闭、异步关闭、配置失败三类相等实例测试；ModuleRegistrationTest.LegacyEqualModules_ShouldConfigureEachTypeOnlyOnce。每个实例释放次数直接断言为 1。
- 只读交叉核查：另一子代理检查资源集合、失败清理和旧排序，未发现明确生产逻辑遗漏。该窄范围核查不代替后续正式 Review。

#### FIX-002

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 根因：旧 Builder 的图验证位于 try/catch 外，未标记 Failed。
- 修改：AddModule/AddModules 图校验移入统一失败处理；验证仍早于该批实例配置。批量失败重复经过外层清理时，引用身份释放集合确保不重复释放。
- 验证：LegacyGraphFailure_ShouldPoisonBuilderAndReleaseExistingModules 三个用例（cycle/invalid/excluded），验证继续 Add、AddModules、AddBing、解析 manager、UseBing 全部拒绝，并确认失败前已注册模块只释放一次。

#### FIX-003

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 根因：类型验证不接收路径，环诊断只截取环本身。
- 修改：所有图验证错误附完整根到失败节点路径；环包含进入环前的前缀，空依赖包含 null 终点。反射读取属性/依赖提供程序抛错时附加路径后裸 throw，保留原异常实例和堆栈。框架生成的错误消息同样包含路径。
- 验证：强化 ModuleGraphTest 的精确 Type[] 路径与完整消息断言；覆盖两级非法依赖、无参构造、排除冲突、null 依赖/提供程序结果、原提供程序异常身份和根在环外的路径。
- 文档：modularity-runtime.md 记录稳定 Data 键、值类型及旧失败构建器不可继续复用。

#### FIX-004

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 真入口扫描：ModuleScanningTest 生成两个独立动态程序集，经真正 AddBingApplication 与 AdditionalModules 验证选中服务/Options 可用，未选中程序集服务及 Options 绑定描述符不存在；同程序集未入选 sibling 模块不在目录中，其服务仍按程序集共享扫描规则注册。
- 回收：Options 弱引用夹具改为真实 AddBingApplication→绑定配置→解析 IOptionsMonitor→显式 ShutdownBing→Dispose，验证 services/configuration/monitor 均可回收。原仅验证自定义 finder 的测试准确更名为 RegisterTypes_UsesCustomAssemblyFinder。
- 构造失败：成功构造的 root 后续依赖构造失败，断言原构造异常链及 root Dispose 次数。
- 关闭取消：预取消、中途握手取消均断言逆序尝试所有 hook、释放所有模块、汇总两个错误，无睡眠或工作集推断。
- [测试映射](test-mapping.md) 已补全所有 FIX 与方法映射。

### 验证结果与证据

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 定向注册/资源/Options/扫描 | 31 passed、1 fixture failure；随后修复动态接口的 Abstract 属性 | fix1-targeted-net8.trx（历史，非最终结果） |
| 精准图/扫描确认 | 8 passed | fix1-graph-scan-net8.trx |
| Core net8.0 全量 | 301 passed、0 failed | [TRX](artifacts/fix1-core-full-net8.trx) |
| Core net6.0 全量 | 301 passed、0 failed | [TRX](artifacts/fix1-core-full-net6.trx) |
| MVC net8.0 全量 | 47 passed、1 既有 skipped、0 failed | [TRX](artifacts/fix1-web-full-net8.trx) |
| MVC net6.0 全量 | 47 passed、1 既有 skipped、0 failed | [TRX](artifacts/fix1-web-full-net6.trx) |
| WebApi 示例构建 | 0 errors、1 既有 NETSDK1138 warning | [构建输出](artifacts/fix1-webapi-build.txt) |
| UTF-8 / git diff --check | PASS | 45 个工作区相关文本检查；行尾提示非内容错误 |

全部 dotnet 操作按 net8/net6 顺序完成，未并发写共享构建目录。测试 SDK 保持 8.0.424，使用既有临时安装与包缓存；未修改 global.json、生产 TFM、依赖版本或版本号。MVC 既有跳过仍为 UploadRawAsync，未跳过新增用例。

本轮增加 13 个实际测试用例（包括 Theory 三个输入），并增强既有路径/回收用例。Core 模块相关用例从 44 增至 57，Web 仍 6。首次编译遇到编辑中的重复 MethodImpl 属性，完成夹具后已消除；扫描夹具的动态接口 Abstract 属性错误亦已修复。以上是测试夹具问题，不掩盖或绕过生产断言。

最终证据清单：[fix1-evidence-manifest.json](artifacts/fix1-evidence-manifest.json)。原报告和审查使用的历史 artifacts 不覆盖，当前结果只引用 fix1 明确清单。未因文档编辑重复运行测试。

### 生命周期限制的处理

D-01/D-02 在 review 中为单列契约边界，不是 FIX-001～004 的未修代码项：

- D-01：未激活 manager 的裸 provider.Dispose 仍不能追踪预建模块。新增 ExplicitShutdownBeforeInitialization_ReleasesOwnedModulesOnce 证明文档要求的显式 ShutdownBing 路径在未初始化时也会释放一次；没有把“直接 Dispose 自动释放”描述为已实现。
- D-02（历史 Round 1 结论）：当时只实现 IAsyncDisposable 的模块仍在配置前被拒绝；Round 2 已移除该前置拒绝。当前此类模块必须使用异步初始化/关闭入口，同时实现两种释放接口的模块已验证在异步关闭时优先 DisposeAsync。

扩大这些契约需要单独设计容器构建/应用所有者入口或异步配置失败清理模型；本轮未擅自增加新公共入口。它们已保留在迁移指南与本报告中。

### Round 1 汇总

- MUST_FIX：1/1 已完成；SHOULD_FIX：3/3 已完成。
- PARTIAL/BLOCKED/FAILED：无当前 FIX。
- IMPLEMENTATION_STATUS：修复执行完成，待独立复审。
- TEST_STATUS：PASS；RESOURCE_EVIDENCE_STATUS：本轮要求的直接路径 PASS。
- PERFORMANCE_EVIDENCE_STATUS：NOT_APPLICABLE；EXTERNAL_GATE_STATUS：NOT_APPLICABLE；RELEASE_STATUS：NOT_ASSESSED。
- Completed：FIX-001～004 与相关测试/迁移/映射。
- Open Actionable：当前修复范围无剩余项；不据此覆盖原 review.md。
- Accepted Limitations：可信启动期模块组合与旧入口排序；D-01/D-02 保持已记录边界。
- Deferred：热插拔、ALC、外部插件隔离，仍不在本任务范围。
- No Progress Check：CHANGED（实际生产修复与新增通过证据）。
- 下一步：独立 Review；本轮不自动进入新一轮 Fix。
- 未 git commit、未 git push、未创建 PR。task-finish 使用 --no-notify，仅收口本地状态。

### Round 2：用户要求继续修复后续差异能力

- 触发：用户要求继续修复独立 Review 中记录的 D-01/D-02 差异。
- 范围：仅异步释放模块的构造、配置失败、异步关闭和容器释放；新增安全的根容器构建入口；同步更新迁移文档和测试映射。未修改 plan.md，未改写 review.md。

#### Change Impact Analysis

- ChangedFiles：`ModuleDependencyGraph`、`BingModuleLoader`、`BingModuleRegistration`、新增 `BingServiceCollectionExtensions`；Core 模块化测试、Core README、迁移指南、最佳实践、架构文档和 ADR。
- ChangedProjects：Bing.Core、Bing.Core.Tests；Bing.AspNetCore.Mvc.Tests 作为消费者回归。
- ChangedPublicContracts：新增 `IServiceCollection.BuildBingServiceProvider(ServiceProviderOptions = null)`；仅异步释放模块由拒绝改为要求异步生命周期。
- ChangedRuntimePaths：构造失败/配置失败同步等待仅异步资源的 `DisposeAsync`；根容器构建时可预解析 `IBingModuleManager`。
- ChangedProviders/TFMs/BuildPackaging/BenchmarkHarness：无。
- AffectedDependents：Bing.AspNetCore、MVC 测试和 WebApi 示例。
- RiskLevel：MEDIUM；先执行 Core 模块精准测试，再执行 Core/MVC 双 TFM 完整回归和 WebApi 构建。

#### D-02

- 执行状态：COMPLETED。
- 修复：移除 `ModuleDependencyGraph` 对仅 `IAsyncDisposable` 模块的前置拒绝；构造失败和同步注册失败均按逆序同步等待 `DisposeAsync`，清理错误附加到原始异常，不覆盖原始异常。
- 生命周期：仅异步释放模块必须使用 `UseBingAsync`、`ShutdownBingAsync` 和 `DisposeAsync`；同步关闭仍明确报错并要求异步入口。
- 测试：新增正常异步关闭、重复关闭/释放、配置失败和构造失败资源释放用例。

#### D-01

- 执行状态：MITIGATED / ACCEPTED_LIMITATION。
- 修复：新增 `BuildBingServiceProvider`，构建根容器后预解析模块管理器，使 `using`/`await using` 覆盖尚未初始化的模块；解析失败时释放临时 provider 且保留原始异常。
- 边界：原生 `BuildServiceProvider()` 在完全未解析管理器时仍无法由 Microsoft DI 触发框架释放回调。迁移文档和 ADR 要求使用新入口或显式 `ShutdownBing`；没有伪称原生入口已具备无条件自动释放。
- 测试：`BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ShouldAttachManagerForDisposeFallback` 与 `BuildBingServiceProvider_ShouldUseAsyncDisposeForUninitializedAsyncModule` 验证未初始化同步/异步模块由新入口的 provider 释放路径各释放一次。

#### Round 2 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 模块化精准 net8.0 | 61 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup2-core-modularity-net8.trx` |
| Core 模块化精准 net6.0 | 61 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup2-core-modularity-net6.trx` |
| Core 完整 net8.0 | 305 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup2-core-full-net8.trx` |
| Core 完整 net6.0 | 305 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup2-core-full-net6.trx` |
| MVC 完整 net8.0 | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/followup-web-full-net8.trx` |
| MVC 完整 net6.0 | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/followup-web-full-net6.trx` |
| WebApi 示例构建 | 0 errors、1 既有 NETSDK1138 warning | 本轮 build 输出 |
| `git diff --check` | PASS | 仅既有 LF/CRLF 转换提示 |

本轮未自动 git commit、未 git push、未创建 PR。下一步应由独立 Reviewer 重新审查 D-01/D-02 及新增公共入口。

本轮证据清单：`artifacts/followup2-evidence-manifest.json`。

### Round 3：复审缺口修复

- 触发：独立复审将 `FOLLOWUP2-FIX-001`（MUST_FIX）和 `FOLLOWUP2-FIX-002`（SHOULD_FIX）标为 `OPEN_ACTIONABLE`。
- 范围：仅修复根容器构建失败清理和同步异常路径的异步释放等待；保留原生 `BuildServiceProvider` 未激活管理器的已接受边界。

#### FOLLOWUP2-FIX-001

- 执行状态：COMPLETED。
- 修改：`BuildBingServiceProvider` 将原生 provider 构建纳入异常处理。`ValidateOnBuild` 或其他构建错误发生在 provider 返回前时，直接调用所属 `BingModuleRegistration.FailConfiguration`，释放已构造模块并保留原始构建异常。
- 测试：`BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseConstructedModules`；`BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseAsyncOnlyModulesSynchronously`。

#### FOLLOWUP2-FIX-002

- 执行状态：COMPLETED。
- 修改：新增内部 `BingModuleDisposal.DisposeSynchronously`，把仅实现 `IAsyncDisposable` 的同步失败清理调度到不携带调用方 `SynchronizationContext` 的线程池执行环境，再等待释放完成。构造失败和配置失败均使用该路径。
- 测试：`ModuleResourceRegressionTest.AsyncOnlyConfigurationFailure_DoesNotDeadlockCapturedSynchronizationContext` 使用吞回调的同步上下文和真正异步的 `DisposeAsync` 验证有界完成、原异常保留及释放一次。

#### Round 3 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 模块化精准 net8.0 | 64 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup3-core-modularity-net8.trx` |
| Core 模块化精准 net6.0 | 64 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup3-core-modularity-net6.trx` |
| Core 完整 net8.0 | 308 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup3-core-full-net8.trx` |
| Core 完整 net6.0 | 308 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/followup3-core-full-net6.trx` |
| MVC 完整 net8.0 | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/followup3-web-full-net8.trx` |
| MVC 完整 net6.0 | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/followup3-web-full-net6.trx` |
| WebApi 示例构建 | 0 errors、1 既有 NETSDK1138 warning | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |
| `git diff --check` | PASS | 仅既有 LF/CRLF 转换提示 |

SDK 使用仓库要求的 8.0.424 临时安装，未修改 `global.json`、生产目标框架或依赖版本。Round 3 证据清单：`artifacts/followup3-evidence-manifest.json`。

#### Round 3 独立复审结果

`review.md` Round 4 已将 `FOLLOWUP2-FIX-001` 和 `FOLLOWUP2-FIX-002` 标记为 `CLOSED`，当前 `MUST_FIX`/`SHOULD_FIX` 均为 0。复审状态为 `PASS_WITH_ISSUES`，仅保留原生 `BuildServiceProvider()` 未解析模块管理器时的 `ACCEPTED_LIMITATION`；无 Host 场景应使用 `BuildBingServiceProvider` 或显式调用关闭方法。

## Review 修复记录

### Round 4：静态兼容解绑与交付追溯收口

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/review.md`

#### FIX-001

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Core/Bing/DependencyInjection/ServiceLocator.cs`
  - `framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs`
- 根因：静态定位器解绑时清空 `_provider`，但 `ScopedProvider` 直接解引用字段；最后一个 legacy 宿主停止后，公开查询可能触发空引用。
- 修复：`IsProviderEnabled` 和 `ScopedProvider` 均通过 `_bindingGate` 读取 provider；provider 不存在时 `ScopedProvider` 返回 `null`，`InScoped()` 随之返回 `false`。保留按所属宿主解绑的 owner 检查。
- 验证：
  - `ModuleLifecycleTest.StoppingOlderLegacyHost_DoesNotUnbindNewerHost`：增加最后一个宿主关闭后的 `ScopedProvider`/`InScoped()` 回归断言。
  - Core 模块化精准 net8.0：18 passed、0 failed。
  - Core 模块化精准 net6.0：18 passed、0 failed。

#### FIX-002

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/execution.md`
  - `ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/test-mapping.md`
- 根因：历史轮次的异步模块限制没有明确标记为历史，测试映射保留了两个已改名或删除的方法名。
- 修复：将异步模块限制改为“历史 Round 1 结论”，明确当前已支持异步资源模块；更新映射为当前源码中的完整限定测试名。
- 验证：执行报告中的当前结论不再与 Round 2/3 冲突；测试映射名称扫描无缺失项。

### Round 4 汇总

- MUST_FIX：0
- SHOULD_FIX：2/2 已完成。
- PARTIAL：无。
- BLOCKED：无。
- FAILED：无。
- 回归验证：FIX-001 的 Core 模块化精准测试在 net8.0/net6.0 均通过；FIX-002 完成静态映射与文档一致性检查。
- 扩大回归：Core 完整测试 net8.0 通过 308/308，net6.0 通过 308/308；Web/MVC 生产范围未变化，沿用 Round 3 双框架证据。
- Accepted Limitations：原生 `BuildServiceProvider()` 未解析模块管理器时的资源所有权边界仍保留；使用 `BuildBingServiceProvider` 或显式关闭入口。
- 下一步：重新进行独立 Review；本轮不修改 `review.md`。

### Round 5：FIX-003 资源引用修复

- Review 状态：`NEEDS_FIX`；Fix Scope：`recommended`；Review 文件：`ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/review.md`。
- 本轮处理当前 Review 中唯一纳入范围的 `SHOULD_FIX`：`FIX-003`。

#### Change Impact Analysis

- ChangedFiles：`BingModuleRegistration.cs`、`BingModuleDescriptor.cs`、`BingBuilder.cs`、`ModuleResourceRegressionTest.cs`、本执行报告和测试映射。
- ChangedProjects：`Bing.Core`、`Bing.Core.Tests`。
- ChangedPublicContracts：未改变公开成员签名；描述符继续保留模块类型、程序集、依赖和执行序号，释放后不再持有内部模块实例。
- ChangedRuntimePaths：模块注册记录释放、服务集合中的模块实例描述符移除、旧入口 builder 模块引用清理、重复关闭和终态附加校验。
- ChangedProviders：Microsoft DI 服务集合直接实例注册的清理路径。
- ChangedTFMs：无；继续验证 `net8.0`、`net6.0`。
- ChangedBuildPackaging：无；未修改版本、目标框架或包依赖。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：执行报告和测试映射同步更新。
- AffectedDependents：Core 模块生命周期、旧入口和新入口的容器释放路径、Web 消费者；未改变 Web 生产实现。
- RiskLevel：`HIGH`，因为修改共享模块所有权和 DI 注册清理路径。

#### FIX-003

- 严重程度：`MEDIUM`。
- 处理要求：`SHOULD_FIX`。
- 执行状态：`COMPLETED`。
- 根因：`BingModuleRegistration` 在释放后继续保存根 provider、所有权列表、释放集合和包含实例的描述符；`IServiceCollection` 同时通过 `ServiceDescriptor.ImplementationInstance` 保存模块实例。
- 修复：
  - `BingModuleDescriptor` 将模块类型和程序集缓存为元数据，允许释放后清除内部实例引用。
  - `ReleaseAsync` 在锁内拍平、按引用身份去重并标记待释放实例，完成同步或异步释放后移除服务集合中直接引用这些模块的描述符。
  - 释放完成后清空 provider、所有权集合和释放集合，并设置终态标记；重复释放保持幂等，同一服务集合不能重新附加 provider。
  - 目录描述符保留只读类型元数据，避免以清空整个模块目录的方式修复引用问题。
- 修改文件：
  - `framework/src/Bing.Core/Bing/Core/Modularity/BingModuleDescriptor.cs`
  - `framework/src/Bing.Core/Bing/Core/Modularity/BingModuleRegistration.cs`
  - `framework/src/Bing.Core/Bing/Core/Builders/BingBuilder.cs`
  - `framework/tests/Bing.Core.Tests/Modularity/ModuleResourceRegressionTest.cs`
- 验证：
  - `ModuleResourceRegressionTest` net8.0：14 passed、0 failed。
  - `ModuleResourceRegressionTest` net6.0：14 passed、0 failed。
  - Core 模块化测试 net8.0：49 passed、0 failed。
  - Core 模块化测试 net6.0：49 passed、0 failed。
  - Core 全量测试 net8.0：312 passed、0 failed。
  - Core 全量测试 net6.0：312 passed、0 failed。
  - MVC 全量测试 net8.0：47 passed、0 failed、1 existing skipped。
  - MVC 全量测试 net6.0：47 passed、0 failed、1 existing skipped。
  - 新增新入口正常关闭、配置失败、`ValidateOnBuild` 失败及旧入口正常关闭弱引用回归均通过。

### Round 5 汇总

- MUST_FIX：0。
- SHOULD_FIX：`FIX-003` 已完成。
- PARTIAL：无。
- BLOCKED：无。
- FAILED：无。
- 回归验证：Core 模块化和 Core 全量测试双目标框架均通过；MVC 双目标框架全量测试通过；保留既有 `CS8632`、ASP.NET analyzer 和 XML 参数警告，无新增编译错误。
- `git diff --check`：通过，仅有工作区既有 LF/CRLF 转换提示。
- 下一步：重新进行独立 Review；本执行器不修改 `review.md`，不执行 git add、commit 或 push。

### Round 6：FIX-004/FIX-005

- 触发：独立复审发现两个 `OPEN_ACTIONABLE` 的 `SHOULD_FIX`：公开模块类型判断与实际加载契约不一致；启动日志失败分支缺少直接回归测试。
- 变更范围：统一 `BingModule.IsBingModule` 与当前 `BingModule` 加载约束；增加类型契约和启动日志异常清理测试；同步更新测试映射与本轮执行记录。

#### Change Impact Analysis

- ChangedFiles：`BingModule.cs`、`ModuleGraphTest.cs`、`ModuleLifecycleTest.cs`、`test-mapping.md`、`execution.md`。
- ChangedProjects：`Bing.Core`、`Bing.Core.Tests`。
- ChangedPublicContracts：仅收敛既有 `BingModule.IsBingModule` 的判断语义，不新增或删除公开成员；XML 文案与实际加载契约同步。
- ChangedRuntimePaths：模块类型预校验；初始化失败时启动日志输出失败、原始异常保留、日志缓存清理和模块资源回收。
- ChangedProviders：无；测试使用现有 Microsoft DI 和日志抽象。
- ChangedTFMs：无；继续验证 `net8.0`、`net6.0`。
- ChangedBuildPackaging：无；未修改版本、目标框架或包依赖。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：测试映射和执行报告同步更新。
- AffectedDependents：模块图入口和 Core 生命周期测试；Web 消费者仅复用既有 Core 契约，不改变 Web 生产代码。
- RiskLevel：`MEDIUM`；类型预校验属于公开辅助方法，日志异常路径属于初始化失败清理路径。

#### 验证计划

- L0：编译、`git diff --check`、测试名称映射和 UTF-8 检查。
- L1：模块类型契约、模块化测试和新增日志失败测试，按 `net8.0`、`net6.0` 顺序执行。
- L2/L3：Core 全量测试；由于共享 Core 公共契约发生变化，再复用或重新执行 MVC 消费者回归。

#### Round 6 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 模块化定向 `net8.0` | 70 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix4-modularity-net8.trx` |
| Core 模块化定向 `net6.0` | 70 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix4-modularity-net6.trx` |
| Core 全量 `net8.0` | 314 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix4-core-full-net8.trx` |
| Core 全量 `net6.0` | 314 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix4-core-full-net6.trx` |
| MVC 全量 `net8.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix4-mvc-full-net8.trx` |
| MVC 全量 `net6.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix4-mvc-full-net6.trx` |
| Core 文档构建 | 0 warnings、0 errors | `framework/src/Bing.Core/Bing.Core.csproj`，`netstandard2.0` |
| WebApi 示例构建 | 0 errors、1 既有 `NETSDK1138` warning | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |
| `git diff --check` | PASS | 仅既有 LF/CRLF 转换提示 |

#### Round 6 结果

- FIX-004：生产判断已与实际加载契约统一，补充接口实现、抽象类、开放泛型和无公共无参构造的直接测试。
- FIX-005：补充真实日志工厂失败测试，验证原始初始化异常、`Bing.ModuleLogError`、日志缓存清理、模块释放和失败后禁止重试。
- 本轮未修改版本、TFM、依赖、包配置或 Web 生产代码；未执行 git commit、git push 或发布操作。

### Round 7：兼容入口与模块图覆盖补强

- 触发：复核发现旧 `AddBing` 模块在注册阶段无法读取静态服务集合；同时新增入口自定义加载器结果、稳定排序、多根共享依赖和长依赖链缺少直接断言。
- 执行状态：`COMPLETED`。

#### Change Impact Analysis

- ChangedFiles：`ServiceLocator.cs`、`BingModuleRegistration.cs`、`ModuleGraphTest.cs`、`ModuleRegistrationTest.cs`、`test-mapping.md`、本执行报告。
- ChangedProjects：`Bing.Core`、`Bing.Core.Tests`。
- ChangedPublicContracts：未新增或删除公开成员；`ServiceLocator.GetServiceDescriptors` 的未绑定结果继续为空序列，旧入口仅增加注册阶段兼容绑定。
- ChangedRuntimePaths：旧入口模块注册前的服务集合可见性、注册失败解绑、宿主关闭解绑；新入口自定义加载器结果验证；依赖图稳定排序和显式栈长链遍历。
- ChangedProviders：无；继续使用现有 Microsoft DI 容器。
- ChangedTFMs：无；验证 `net8.0`、`net6.0`。
- ChangedBuildPackaging：无；未修改版本、目标框架或包依赖。
- ChangedBenchmarkHarness：无；本轮只覆盖模块启动正确性和资源引用边界。
- ChangedDocsOnly：测试映射和执行报告。
- AffectedDependents：旧模块注册兼容路径、新模块加载器扩展点、Core 生命周期；Web 生产实现未修改。
- RiskLevel：`MEDIUM`；涉及静态兼容入口和模块描述符校验，但所有权解绑按服务集合和 provider 所属关系判断。

#### 修复项

- FIX-006：增加旧入口注册阶段的服务集合绑定和按所属集合解绑，覆盖注册阶段读取静态服务描述符、注册失败清理和宿主关闭清理。
- FIX-007：补充自定义加载器实际重排结果和非法重复结果的直接测试，确保结果被采用且失败后注册器不可继续使用。
- FIX-008：补充就绪节点 Ordinal 全名排序、多根共享依赖去重及 128 节点长依赖链测试，验证依赖图顺序与显式栈遍历。

#### Round 7 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| ModuleGraph 定向 `net8.0` | 11 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix5-longchain-net8c.trx` |
| ModuleGraph 定向 `net6.0` | 11 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix5-longchain-net6.trx` |
| Core 全量 `net8.0` | 321 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix5-final-core-net8.trx` |
| Core 全量 `net6.0` | 321 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix5-final-core-net6.trx` |
| Core 模块注册与图定向回归 | 29 passed、0 failed（双目标框架） | `fix5-registration-net8c.trx`、`fix5-registration-net6.trx` |
| MVC 全量 `net8.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix5-mvc-full-net8.trx` |
| MVC 全量 `net6.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix5-mvc-full-net6.trx` |
| Core 文档构建 | 0 warnings、0 errors | `framework/src/Bing.Core/Bing.Core.csproj`，`netstandard2.0` |
| WebApi 示例构建 | 0 errors、1 既有 `NETSDK1138` warning | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |

- `git diff --check` 本轮最终检查通过，仅保留工作区既有 LF/CRLF 转换提示；未修改 `review.md`，等待后续独立复审确认 Finding 状态。

### Round 8：跨宿主静态绑定与测试隔离修复

- 触发：复核确认旧入口在宿主 A 已运行时注册宿主 B，会先替换服务集合但保留 A 的 provider；若 B 注册失败，静态定位器可能形成“B 集合 + A provider”的混合状态。另有旧 `AddBing` 测试未统一进入静态兼容集合，存在并行污染路径。
- 执行状态：`COMPLETED`。

#### Change Impact Analysis

- ChangedFiles：`ServiceLocator.cs`、`BingModuleRegistration.cs`、`ModuleRegistrationTest.cs`、`DependencyInjectionTest.cs`、`test-mapping.md`、本执行报告。
- ChangedProjects：`Bing.Core`、`Bing.Core.Tests`。
- ChangedPublicContracts：未新增或删除公开成员；仅收敛旧入口静态绑定在宿主切换和解绑时的内部状态一致性。
- ChangedRuntimePaths：旧 `AddBing` 注册期绑定、注册失败清理、旧宿主关闭、容器释放兜底；新入口模块图和生命周期逻辑未改变。
- ChangedProviders：无；仍使用现有 Microsoft DI。
- ChangedTFMs：无；继续验证 `net8.0`、`net6.0`。
- ChangedBuildPackaging：无；未修改版本、依赖和目标框架。
- ChangedBenchmarkHarness：无；本轮验证状态一致性和释放路径，不涉及 SQL 或性能热路径。
- AffectedDependents：旧入口 `ServiceLocator` 消费者、Core 模块化测试和 MVC Core 消费方；新入口不绑定静态定位器。
- RiskLevel：`MEDIUM`；修改共享静态兼容状态的宿主切换边界，并扩大测试串行范围。

#### 修复项

- FIX-009：`BindServices` 和 `SetServiceCollection` 在服务集合发生切换时于同一锁内清除旧 provider；注册记录保存精确配置引用，解绑时只清理当前配置，避免关闭旧宿主误清理新宿主。
- FIX-010：`ModuleRegistrationTest`、`DependencyInjectionTest` 加入 `Module static compatibility` 非并行集合；旧 DI 自动加载测试改用 `BuildBingServiceProvider`，确保断言失败时也能通过 provider 释放触发静态解绑。
- 新增跨宿主回归：A 启动后 B 注册失败，静态定位器不再解析 A；B 失败清理后 C 可以重新绑定，A 的直接 provider 仍由测试 finally 独立关闭；共享配置对象场景验证旧宿主关闭不会清理新宿主配置。

#### Round 8 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 模块化与旧 DI 定向 `net8.0` | 111 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix6c-modularity-net8.trx` |
| Core 模块化与旧 DI 定向 `net6.0` | 111 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix6c-modularity-net6.trx` |
| Core 全量 `net8.0` | 323 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix6b-core-full-net8.trx` |
| Core 全量 `net6.0` | 323 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/fix6b-core-full-net6.trx` |
| MVC 全量 `net8.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix6b-mvc-full-net8.trx` |
| MVC 全量 `net6.0` | 47 passed、1 既有 skipped、0 failed | `framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/fix6b-mvc-full-net6.trx` |
| Core 文档构建 | 0 warnings、0 errors | `framework/src/Bing.Core/Bing.Core.csproj`，`netstandard2.0` |
| WebApi 示例构建 | 0 errors、1 既有 `NETSDK1138` warning | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |
| `git diff --check` | PASS | 仅有工作区既有 LF/CRLF 转换提示 |

- 本轮未修改 `review.md`、版本、TFM、依赖或 Web 生产代码；未执行 git add、commit、push 或发布操作。

### Round 9：自定义加载器异常契约测试与最终证据同步

- 执行状态：`COMPLETED`。
- 变更范围：补充 `ModuleRegistrationTest` 对空模块目录、空模块描述符和失败后不可复用服务集合的直接测试；同步本报告的最终测试链接和统计。
- 生产代码、公开 API、目标框架、依赖版本和运行时行为未修改。

#### Round 9 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| `ModuleRegistrationTest` 精准测试 `net8.0` | 23 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/jianx_LAPTOP-2V5T48RH_2026-09-27_21_04_02.trx` |
| `ModuleRegistrationTest` 精准测试 `net6.0` | 23 passed、0 failed | `framework/tests/Bing.Core.Tests/TestResults/jianx_LAPTOP-2V5T48RH_2026-09-27_21_04_03.trx` |
| Core 当前测试程序集全量 `net8.0` | 325 passed、0 failed | `TestResults/fix9-core-full-net8.trx` |
| Core 当前测试程序集全量 `net6.0` | 325 passed、0 failed | `TestResults/fix9-core-full-net6.trx` |
| `git diff --check` | PASS | 仅有工作区既有 LF/CRLF 转换提示 |

本轮测试使用已安装的 .NET SDK `8.0.419` 通过 `MSBuild.dll`/`vstest.console.dll` 直接执行；仓库锁定的 `8.0.424` 当前不在本机，未修改 `global.json`。Round 8 的 `8.0.424` 双框架 MVC 和 WebApi 证据作为未受生产代码影响的范围证据复用。

### Round 10：启动期可信插件能力

- 执行状态：`COMPLETED`。
- 变更范围：新增启动期可信插件来源、UTF-8 清单解析、插件依赖排序、程序集候选表、默认加载上下文解析、独立插件描述符容器、插件文档及隔离夹具；补充插件资源释放、失败注册回收和配置委托失败不可重试测试。
- 公开契约：新增 `BingApplicationOptions.PluginSources`、`BingPluginSourceList`、`IBingPluginSource`、`BingPluginDescriptor`、`BingPluginDependencyDescriptor` 和 `IBingPluginContainer`；未修改 `IBingModuleLoader` 签名，旧入口和无插件的新入口保持兼容。

#### Change Impact Analysis

- ChangedProjects：`Bing.Core`、`Bing.Core.Tests`；MVC 和 WebApi 只作为既有消费者回归范围。
- ChangedRuntimePaths：应用注册前的插件发现/预检、插件程序集加载、启动模块并入统一模块图、选中程序集自动扫描、注册失败清理、宿主关闭时插件描述符清理。
- ChangedPublicContracts：仅增加启动期插件契约和 `BingApplicationOptions` 只读来源集合；无依赖升级、TFM 或版本号变更。
- ResourceRisk：插件程序集进入默认 `AssemblyLoadContext` 后保留至进程结束，符合本期可信启动插件边界；不承诺热卸载或程序集可回收。临时解析事件在成功和异常路径均通过 `finally` 退订，插件来源和失败上下文不由静态缓存保留。
- RiskLevel：`MEDIUM`；新增默认入口加载路径，但旧入口不读取插件来源，插件仍须在注册期完成清单和模块验证。

#### 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 插件相关 Core 测试 `net8.0` | 20 passed、0 failed | `TestResults/plugin-final8-net8.trx` |
| 插件相关 Core 测试 `net6.0` | 20 passed、0 failed | `TestResults/plugin-final8-net6.trx` |
| Core 全量 `net8.0` | 346 passed、0 failed | `TestResults/plugin-core-final8-net8.trx` |
| Core 全量 `net6.0` | 346 passed、0 failed | `TestResults/plugin-core-final8-net6.trx` |
| MVC 全量 `net8.0` | 47 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-final4-net8.trx` |
| MVC 全量 `net6.0` | 47 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-final4-net6.trx` |
| WebApi 示例 `net6.0` 构建 | 0 errors、既有 `NETSDK1138`/`NU1900` 警告 | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |
| Core 生产项目 `netstandard2.0` 构建 | 通过 | `Bing.Core.csproj` |
| `git diff --check` | 通过 | 仅有工作区既有 LF/CRLF 转换提示 |

插件相关 20 个用例覆盖单插件加载/扫描、初始化、依赖排序、菱形依赖、多个来源、清单和路径错误、精确版本依赖、插件入口模块内部依赖环、嵌套依赖环、嵌套缺失依赖、重复 ID、两个服务集合隔离、排除冲突、关闭后描述符清空及失败来源回收。不同完整程序集身份的冲突路径已由生产预检实现，但当前没有第二个隔离夹具程序集，未将其宣称为直接测试通过；默认加载上下文程序集卸载按计划明确排除。

#### 交付文档

- `docs/guides/modularity-plugins.md`：目录结构、清单字段、注册方式、诊断和可信/默认加载上下文限制。
- `docs/migrations/modularity-runtime.md`：启动期插件与模块生命周期、失败清理和迁移约束。
- `docs/architecture/adr/ADR-0011-module-runtime-ownership.md`：插件资源所有权、默认加载上下文和非热插拔边界。
- `test-mapping.md`：新增插件生产符号到直接测试方法映射。

### Round 11：插件验收缺口补齐

- 执行状态：`COMPLETED_WITH_EXTERNAL_GATE`。
- 变更范围：仅测试夹具、Core/MVC 测试和测试项目构建复制目标；生产代码、公开 API、版本和依赖未修改。
- Change Impact Analysis：影响 `Bing.Core.Tests` 的插件发现、生命周期、资源回收和夹具构建链；影响 `Bing.AspNetCore.Mvc.Tests` 的插件 Web 管道与 Generic Host 停止回归；风险等级为 `MEDIUM`。未触及 SQL、Provider、生产程序集加载实现或包图。
- 新增覆盖：插件异步初始化/同步入口拒绝、取消清理、异步关闭和释放一次；同名不同版本程序集冲突；清单必填字段与入口 DLL 缺失；默认解析回调成功/失败退订；失败注册对象图弱引用回收；菱形依赖去重与拓扑位置；两个服务集合的插件服务、配置和生命周期隔离；Web `UseBingAsync` 和 Generic Host 停止。
- 新增夹具：`Bing.Core.PluginFixtures.V2` 使用相同程序集名、不同程序集版本，并复制到独立测试输出目录，避免 V1/V2 互相覆盖。

#### Round 11 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 插件专项 Core `net8.0` | 38 passed、0 failed | `TestResults/plugin-final-round11c-net8.trx` |
| 插件专项 Core `net6.0` | 38 passed、0 failed | `TestResults/plugin-final-round11c-net6.trx` |
| Core 全量 `net8.0` | 364 passed、0 failed | `TestResults/plugin-core-final-round11c-net8.trx` |
| Core 全量 `net6.0` | 364 passed、0 failed | `TestResults/plugin-core-final-round11c-net6.trx` |
| MVC 插件 Web 专项 `net8.0` | 2 passed、0 failed | `TestResults/plugin-web-final-round11-net8.trx` |
| MVC 插件 Web 专项 `net6.0` | 2 passed、0 failed | `TestResults/plugin-web-final-round11-net6.trx` |
| MVC 全量 `net8.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-final-round11-net8.trx` |
| MVC 全量 `net6.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-final-round11-net6.trx` |
| Core 生产项目 `netstandard2.0` | 构建通过 | `framework/src/Bing.Core/Bing.Core.csproj` |
| `git diff --check` | 通过 | 仅有既有 LF/CRLF 转换提示 |

本轮使用本机安装的 .NET SDK `8.0.419` 通过直接 MSBuild/vstest 执行；仓库锁定 `8.0.424` 仍未安装，未修改 `global.json`。因此代码和测试结果已闭环，但锁定 SDK 的最终发布门禁仍为 `BLOCKED_EXTERNAL`。

#### Round 11 TODO 分类（当时）

- Completed：插件异步生命周期、取消清理、同名程序集身份冲突、清单字段/入口缺失、解析回调退订、对象图弱引用、菱形去重、Web/Host 插件回归。
- Open Actionable：无。旧 Round 9 的 `review.md` 结论属于独立 Review 报告治理，不构成本轮实现缺口。
- Blocked External：使用 SDK `8.0.424` 重新运行 Round 11 全部门禁。
- Accepted Limitations：默认 `AssemblyLoadContext` 不可卸载、插件代码完全可信、无运行时热插拔和进程隔离。
- Deferred：动态插件卸载、NuGet/SemVer 区间求解和完整 ABP 上层生态。

### Round 12：插件错误上下文补强

- 执行状态：`COMPLETED_WITH_EXTERNAL_GATE`。
- 变更范围：`BingPluginLoader` 的异常上下文和默认加载上下文程序集身份校验；Core 插件清单/依赖测试及插件文档。未修改公开 API、版本、TFM 或依赖。
- 修复内容：已解析插件 ID 的清单错误保留 `Bing.PluginId`；程序集错误附加 `Bing.PluginAssembly`；非法入口类型附加 `Bing.PluginType`；缺失、版本不匹配和环依赖附加 `Bing.PluginDependencyPath`；解析已加载程序集时校验完整程序集身份，避免仅按简单名称错误复用；新增已加载程序集版本冲突直接回归测试。

#### Round 12 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 插件专项 Core `net8.0` | 39 passed、0 failed | `TestResults/plugin-diagnostics-round12b-net8.trx` |
| 插件专项 Core `net6.0` | 39 passed、0 failed | `TestResults/plugin-diagnostics-round12b-net6.trx` |
| Core 全量 `net8.0` | 365 passed、0 failed | `TestResults/plugin-core-round12b-net8.trx` |
| Core 全量 `net6.0` | 365 passed、0 failed | `TestResults/plugin-core-round12b-net6.trx` |
| MVC 插件 Web 专项 `net8.0` | 2 passed、0 failed | `TestResults/plugin-web-round12-net8.trx` |
| MVC 插件 Web 专项 `net6.0` | 2 passed、0 failed | `TestResults/plugin-web-round12-net6.trx` |
| MVC 全量 `net8.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-round12-net8.trx` |
| MVC 全量 `net6.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/plugin-mvc-round12-net6.trx` |
| Core 测试项目构建 | 通过；仅既有 `NU1900`/`CS8632` 警告 | 直接 MSBuild `8.0.419` |
| MVC 测试项目构建 | 通过；仅既有 `NU1900`、XML/ASP 分析器警告 | 直接 MSBuild `8.0.419` |
| `git diff --check` | 通过；仅有既有 LF/CRLF 转换提示 | 工作区检查 |

#### Round 12 TODO 分类

- Completed：插件清单、程序集、启动类型和依赖链诊断上下文补齐；已加载程序集的完整身份冲突保护；双框架专项和 Core 全量回归。
- Open Actionable：无。
- Blocked External：使用仓库锁定 SDK `8.0.424` 重跑最终门禁；当前环境仍只有 `8.0.419`。
- Accepted Limitations：默认 `AssemblyLoadContext` 不可卸载、插件代码完全可信、无运行时热插拔和进程隔离。
- Deferred：动态插件卸载、NuGet/SemVer 区间求解和完整 ABP 上层生态。

### Round 13：锁定 SDK 最终门禁

- 执行状态：`COMPLETED`。
- 影响分析：仅验证既有实现；未修改生产代码、公共 API、项目依赖、TFM 或构建图。使用临时 CLI 家目录避免环境哨兵文件写入权限问题，未修改 `global.json`。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 全量 `net8.0` | 365 passed、0 failed | `TestResults/final-sdk8424-core-net8.trx` |
| Core 全量 `net6.0` | 365 passed、0 failed | `TestResults/final-sdk8424-core-net6.trx` |
| MVC 全量 `net8.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/final-sdk8424-mvc-net8.trx` |
| MVC 全量 `net6.0` | 49 passed、1 既有 skipped、0 failed | `TestResults/final-sdk8424-mvc-net6.trx` |
| WebApi 示例构建 | 通过，0 errors；仅既有 SDK/NU1900 警告 | `samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` |
| 锁定 SDK | `8.0.424` | 临时 SDK 安装目录，未修改 `global.json` |

#### Round 13 TODO 分类

- Completed：锁定 SDK 最终测试门禁、Core/MVC 双框架回归及 WebApi 示例构建。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：无。
- Not Applicable：正式吞吐/容量基准；本计划未定义性能阈值，资源结论已由弱引用、释放次数、后台任务和事件退订测试覆盖。
- Accepted Limitations：默认 `AssemblyLoadContext` 不可卸载、插件代码完全可信、无运行时热插拔和进程隔离。
- Deferred：动态插件卸载、NuGet/SemVer 区间求解和完整 ABP 上层生态。

### Round 14：可信插件运行中切换与协作式卸载

- 执行状态：`COMPLETED`。用户在后续任务中明确选择“插件卸载与热插拔”，因此本轮扩展了原计划中延期的动态插件能力。
- Change Impact Analysis：新增 `Bing.PluginRuntime`（`net6.0`/`net8.0`）作为动态宿主；`Bing.Core` 维持 `netstandard2.0`，仅为插件加载器增加内部独立加载上下文路径。旧 `AddBingApplication<T>()` 继续使用默认上下文。影响范围为插件程序集解析、模块生命周期、DI 容器和测试构建图；风险等级为 `HIGH`，按 Core/MVC 双框架全量回归验证。
- 实现：每次 `ReloadAsync` 创建候选插件图、独立容器与可回收加载上下文；候选初始化成功后原子切换新调用，等待旧代活动调用结束，逆序关闭模块、异步释放容器并请求卸载。候选失败时保留旧代；重入操作立即报错。`RunAsync` 为每次调用建立短期作用域；插件信息以不持有程序集引用的快照公开。
- 隔离修复：默认加载路径只检查默认上下文中的已加载程序集，避免动态上下文内的另一版本影响启动期静态注册。插件目录中包含 `Bing.Core` 或 DI 契约副本时，独立上下文仍复用宿主契约身份。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 全量 `net8.0` | 373 passed、0 failed | SDK `8.0.424`，`Bing.Core.Tests` |
| Core 全量 `net6.0` | 373 passed、0 failed | SDK `8.0.424`，`Bing.Core.Tests` |
| MVC 全量 `net8.0` | 49 passed、1 既有 skipped、0 failed | SDK `8.0.424`，`Bing.AspNetCore.Mvc.Tests` |
| MVC 全量 `net6.0` | 49 passed、1 既有 skipped、0 failed | SDK `8.0.424`，`Bing.AspNetCore.Mvc.Tests` |
| WebApi 示例构建 | 通过，0 errors | SDK `8.0.424`，仅既有 `NETSDK1138`/`NU1900` 警告 |
| 卸载资源专项 | 两套框架均通过 | `HotPluginHostTest` 的旧代及关闭后加载上下文弱引用验证 |
| `git diff --check` | 通过 | 仅有既有 LF/CRLF 转换提示 |

本轮生产符号与直接测试映射见 `test-mapping.md`。可回收上下文的实际卸载取决于插件和调用方是否释放全部引用；插件自身线程、事件订阅与静态引用需在关闭钩子中清理。切换只覆盖经 `BingHotPluginHost.RunAsync` 分派的服务调用，已装配的 ASP.NET Core 中间件不会动态替换。插件仍视为可信代码，不提供进程隔离；版本区间和 ABP 上层生态仍为独立后续事项。

#### Round 14 TODO 分类

- Completed：整组插件热切换、候选失败保留旧代、活动调用排空、协作式卸载、双框架资源与回归验证、使用文档和 ADR。
- Open Actionable：无。
- Blocked External：无。
- Accepted Limitations：动态 Web 管道重组、不可信插件隔离、插件保留外部引用时的强制卸载均不属于本轮可保证能力。
- Deferred：NuGet/SemVer 版本区间和 ABP 上层生态。

### Round 15：插件版本区间与预发布版本

- 执行状态：`COMPLETED`。继续处理 Round 14 延期的插件版本约束能力。
- Change Impact Analysis：修改 `Bing.Core` 插件清单解析、依赖版本校验和只读描述符，增加 `NuGet.Versioning` `5.4.0` 包；`Bing.PluginRuntime` 的快照增加完整版本文本。未改变生产 TFM、现有包版本、模块图或生命周期。影响静态注册和热更新两条插件入口，风险等级为 `MEDIUM/HIGH`。
- 兼容规则：原有普通三段或四段 `dependencies.version` 仍为精确匹配；原有 `System.Version` 属性保持三段或四段数值形态。新增 NuGet 区间语法、预发布和构建元数据；完整版本与依赖约束分别通过 `VersionText`、`VersionRequirement` 读取。区间只验证已发现的单一插件版本，不做在线发现或多版本求解。
- 失败行为：无效区间在模块构造前失败；版本不匹配继续报告完整插件依赖链；稳定版区间不隐式接受预发布版本。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 插件专项 `net8.0`、`net6.0` | 各 63 passed、0 failed | `PluginDependencyGraphTest`、`PluginManifestValidationTest`、`HotPluginHostTest` |
| Core 全量 `net8.0`、`net6.0` | 各 389 passed、0 failed | SDK `8.0.424` |
| MVC 全量 `net8.0`、`net6.0` | 各 49 passed、1 既有 skipped、0 failed | SDK `8.0.424` |
| WebApi 示例构建 | 通过，0 errors | SDK `8.0.424`，仅既有 SDK/包源警告 |
| 文档与追溯 | 已更新 | `docs/guides/modularity-plugins.md`、`docs/migrations/modularity-runtime.md`、`test-mapping.md` |

#### Round 15 TODO 分类

- Completed：精确版本兼容、NuGet 区间与边界、预发布准入、完整版本快照、非法区间诊断、双框架回归。
- Open Actionable：无。
- Blocked External：无。
- Accepted Limitations：同一插件 ID 在本地插件图中仍只能有一个版本；不进行 NuGet 下载或跨版本求解。
- Deferred：ABP 上层生态、不可信插件进程隔离。

### Round 16：模块完整生命周期与 ABP 风格钩子

- 执行状态：`COMPLETED`。本轮依据用户批准的生命周期计划完成 T1-T5；没有修改生产目标框架、现有依赖版本或包版本。
- Change Impact Analysis：影响 `Bing.Core` 的模块基类、配置入口、生命周期管理、扫描时机与注册状态，以及 `Bing.PluginRuntime` 候选代构建；Web 通过既有分派器接入。属于跨模块与宿主的高影响变更，因此执行 Core/MVC 双框架全量回归、插件候选失败测试和 WebApi 构建。
- T1/T3：新增基类同步/异步服务配置、Pre/Main/Post 初始化及关闭钩子；按真实虚方法重写、旧接口、旧 `AddServices/UseModule` 的顺序分派，三阶段分别遍历完整模块序列并共享短期作用域。同步入口预检异步实现；失败及取消后逆序关闭已开始模块，释放全部框架拥有的模块。
- T2/T4/T5：新增异步应用注册和按服务集合的配置占用；增加 `Compatible` 与 `BeforeConfigureServices` 扫描模式；热更新候选代等待异步配置及 Post 初始化成功后才切换。
- 资源结论：阶段测试验证模块关闭与释放次数、作用域返回前释放；热更新成功代及 Pre/Post 失败候选代均通过加载上下文弱引用回收验证。反射重写检测没有进程级类型缓存，不保留插件类型；不对默认加载上下文程序集做可卸载断言。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 新增生命周期定向测试 `net8.0` | 20 passed、0 failed | `ModulePhasedLifecycleTest` |
| 热更新 Pre/Post 候选失败 `net8.0`、`net6.0` | 各 2 passed、0 failed | 同时断言失败候选加载上下文弱引用回收 |
| Core 全量 `net8.0`、`net6.0` | 各 413 passed、0 failed | 锁定 SDK `8.0.424`；含阶段、扫描、插件资源及旧入口迁移提示回归 |
| MVC 全量 `net8.0`、`net6.0` | 各 50 passed、1 既有 skipped、0 failed | 含新 Web 三阶段上下文和管道测试 |
| WebApi 示例构建 | 通过，0 errors | 仅既有 SDK、包源及其他项目警告 |
| `git diff --check` | 通过 | 仅 LF/CRLF 转换提示 |

生产符号与直接测试映射见 `test-mapping.md`。本轮按计划更新 Core README、迁移指南及 ADR-0013。

#### Round 16 TODO 分类

- Completed：四组本期能力、热更新候选失败保护、直接回归与资源验证、文档及追溯映射。
- Open Actionable：无。
- Blocked External：无。包漏洞数据源不可达产生 `NU1900`，未影响已完成的编译与测试。
- Accepted Limitations：插件代码仍视为可信；默认加载上下文不能卸载；动态 Web 管道不随插件代重新装配。
- Deferred：自定义生命周期阶段、应用上下文便捷 API、模块附加程序集策略、插件在线分发与多版本求解、不可信插件进程隔离。

### Round 17：Review 修复记录

- Review 状态：`NEEDS_FIX`；Fix Scope：`recommended`；依据：`review.md` 的 FIX-013 至 FIX-017。
- 执行状态：`COMPLETED`（表示本轮 Executor 已处理五项 Finding；下一轮独立 Review 决定最终审查状态）。
- Change Impact Analysis：修改 Bing.Core 的基类桥接、内部虚钩子识别、模块关闭与异步注册取消路径，扩展 Core/MVC 测试及隔离插件夹具；未修改公开签名、生产目标框架、依赖、包版本或构建图。影响 Web 和动态插件宿主的生命周期，风险等级 HIGH，因此运行双框架完整回归及 WebApi 构建。

#### FIX-013

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 根因：`_started` 记录首次进入阶段的时间顺序，混合 Pre 钩子时与模块图顺序不同。
- 修复：初始化时冻结实际模块序列；清理时反转该序列，仅关闭已开始的模块，继续按引用身份去重。
- 验证：`ModuleLifecycleReviewFixTest.MixedPreHooks_ShutdownInReverseModuleOrder` 覆盖同步/异步成功及 Post 失败，断言完整事件序列与关闭次数。

#### FIX-014

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 根因：最派生类 `GetMethod` 返回同名隐藏方法，漏掉中间基类对 BingModule 虚方法槽的重写。
- 修复：按继承层级检查声明方法的基定义，忽略新隐藏槽；不建立静态类型缓存。
- 验证：`ModuleLifecycleReviewFixTest.HiddenAsyncConfiguration_StillRequiresAsyncRegistration`、`HiddenAsyncInitialization_RejectsSyncBeforeAnyPreHook`、`HiddenAsyncPreAndPostHooks_ExecuteInheritedVirtualSlots`、`HiddenAsyncShutdown_StillRequiresAsyncShutdown`。

#### FIX-015

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 根因：异步配置只在钩子前检查取消，最终钩子或公开扫描事件取消后仍可能提交注册。
- 修复：在每个钩子完成后、扫描边界及注册提交前检查取消，沿现有失败处理路径等待模块异步释放。
- 验证：`ModuleLifecycleReviewFixTest.ConfigurationCallbackCancelsAndReturns_RegistrationFails`、`RegistrationEventCancels_RegistrationFails`（兼容与提前扫描模式）、`CanceledRegistration_AwaitsAsyncModuleDisposal`，以及热插件取消候选集成测试。

#### FIX-016

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 根因：新钩子的基类默认实现没有续接部分旧接口；前后配置隐式接口实现可能映射回同一虚方法。
- 修复：显式调用 `base` 时桥接前后配置、旧异步初始化与同步/异步关闭接口；接口映射排除自身虚槽，并对旧接口再次调用基类加重入保护。
- 验证：`ModuleLifecycleReviewFixTest.ExplicitBaseCalls_BridgeOldInterfacesOnce`、`NewHooksWithoutBase_ReplaceOldInterfaces`、`ImplicitInterfaceMapping_BaseCallDoesNotReenterHook`、`SyncShutdownBaseCall_BridgesOldInterfaceOnce`。

#### FIX-017

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修复：补充 Web 三阶段新异步重写、Generic Host 等待新关闭重写、候选插件配置失败/取消与 Post 暂停期间保持旧代的测试；更新生产符号到测试方法映射。
- 验证：`WebModuleLifecycleTest.UseBingAsync_NewAsyncOverrides_ShouldShareApplicationBuilderAndComposePipeline`、`HostStop_ShouldAwaitAsyncShutdownOverride`，及 `HotPluginHostTest` 的三个新增候选集成测试；失败候选与被替换旧代均断言弱引用可回收。

#### Round 17 验证与 TODO

| 检查 | 结果 |
| --- | --- |
| 锁定 SDK | `8.0.424`；未修改 `global.json` |
| Core 定向测试 net8.0/net6.0 | `ModuleLifecycleReviewFixTest` 各 18 passed |
| Core 全量 net8.0/net6.0 | 各 434 passed、0 failed |
| MVC 全量 net8.0/net6.0 | 各 52 passed、1 既有 skipped、0 failed |
| WebApi 示例构建 | 通过，0 errors |
| 资源专项 | Core 全量包含热插件候选/旧代加载上下文弱引用测试；混合阶段关闭与配置取消释放次数断言通过 |

- Completed：FIX-013、FIX-014、FIX-015、FIX-016、FIX-017 的代码、直接测试、文档和映射。
- Open Actionable：当前 Executor 未发现未处理项；由下一轮独立 Review 确认。
- Blocked：无。存在 NuGet 漏洞数据源不可达的 `NU1900` 和既有警告，未影响编译与测试。
- Accepted Limitations / Deferred：沿用 Round 16 边界，不扩大本次修复范围。

### Round 18：Review 后续修复

- Review 基线：`review.md` 的 FIX-016、FIX-017、FIX-018；保留独立 Reviewer 的原始状态，不改写审查结论。
- Change Impact Analysis：修改 Core 生命周期分派及初始化结果发布、Core/Web 的 `UseBingAsync` 返回路径，扩展 Core/MVC 直接测试与热插件夹具；不改变公开签名、目标框架、依赖和包版本。因影响异步初始化及 Web/插件宿主，执行双框架全量回归和 WebApi 构建。
- FIX-016：旧接口后备分派经基类桥接进入，显式或隐式旧接口调用 `base` 时同阶段只执行一次；异常后桥接标志复原。
- FIX-018：模块初始化取消后将原始异常发布给管理器及 provider/Web 异步入口，保留原始消息、取消令牌、模块/阶段 Data 和清理汇总；并发非 Web 初始化共享结果。标准 `TaskCompletionSource.TrySetCanceled` 无法指定原始异常，故本轮曾将该路径记为 `Faulted` Task，并认为 `Task.IsCanceled` 与原异常身份不可兼得。Round 19 的直接探针和修复已推翻这一判断；此处仅保留历史过程记录。
- FIX-017：补齐同模块同步新重写、异步新重写及显式旧接口的优先级测试；热候选 Pre/Main/Post 异步配置分别覆盖失败、合作式取消、旧代继续服务和候选上下文回收。候选配置取消阶段由夹具直接记录；热宿主公开取消异常的阶段 Data 传播不在本轮验收断言内。

| 验证 | 结果 |
| --- | --- |
| 锁定 SDK | `8.0.424`；未修改 `global.json` |
| Core 全量 `net8.0` / `net6.0` | 各 449 passed、0 failed |
| MVC 全量 `net8.0` / `net6.0` | 各 53 passed、1 既有 skipped、0 failed |
| WebApi 示例构建 | 通过，0 errors |
| 热候选配置阶段定向 `net8.0` | 失败与取消各 3 passed |
| 资源证据 | 三阶段取消关闭/释放一次、候选上下文弱引用回收断言通过；不以工作集变化推断无泄漏 |
| `git diff --check` | 通过；仅既有行尾格式转换提示 |

生产符号到直接测试方法映射见 `test-mapping.md`。包漏洞数据源不可达产生 `NU1900`，未阻止本地构建和测试。FIX-016 与 FIX-017 已完成；FIX-018 在本轮尚未满足 `Task.IsCanceled`，该差异已由 Round 19 修复。其余延期能力沿用 Round 16 分类。

### Round 19：取消状态与热候选释放验收

- Review 基线：`review.md` 的 FIX-018 和 FIX-017 两个 `OPEN_ACTIONABLE`；本报告记录修复结果，不改写独立 Review 结论。
- Change Impact Analysis：调整 Core 模块管理器及 Core/Web 异步入口的取消结果发布；增强热插件候选夹具、Core/MVC 直接测试、迁移说明和测试映射。公开 API、生产目标框架、依赖和包版本保持不变。因涉及初始化状态和可回收插件，运行双框架完整回归及 WebApi 构建。
- FIX-018：管理器在执行用户钩子前发布共享任务，仍立即进入钩子；对原始结果做标准 `async/await` 桥接。原始 `OperationCanceledException`、令牌及 `Data` 保留，返回任务为 `Canceled`。Core/Web `UseBingAsync` 直接等待管理器任务，不再用 `TaskCompletionSource` 将取消转成故障。测试通过直接 `await` 捕获异常实例；`Should.ThrowAsync` 在取消任务上会重新生成异常，不能用于身份断言。
- FIX-017：六个 Pre/Main/Post 配置失败或取消用例均断言候选模块 `IAsyncDisposable` 恰好调用一次，关闭宿主后不重复释放；前配置失败用门控证明 `ReloadAsync` 等待异步释放完成，门控期间旧代仍可服务。保留候选加载上下文弱引用回收断言；GC 验证采用有界异步重试，等待异步清理调用栈退出，不以工作集变化推断无泄漏。

| 验证 | 结果 |
| --- | --- |
| 锁定 SDK | `8.0.424`；未修改 `global.json` |
| Core 全量 `net8.0` / `net6.0` | 各 449 passed、0 failed |
| MVC 全量 `net8.0` / `net6.0` | 各 53 passed、1 既有 skipped、0 failed |
| WebApi 示例构建 | 通过，0 errors |
| 热候选资源专项 | 六个配置失败/取消场景释放恰好一次；异步门控、旧代可用及 ALC 回收断言通过 |
| `git diff --check` | 通过；仅既有行尾格式转换提示 |

本轮 Executor 已处理 FIX-018 和 FIX-017，`OPEN_ACTIONABLE: 0`；下一轮独立 Review 决定最终审查状态。`NU1900` 为包漏洞数据源不可达，未阻止本地验证。延期能力沿用 Round 16 分类。

### Round 21：FIX-017 释放计数敏感性闭环

- Review 基线：`review.md` Round 20 的 FIX-017，指出释放回调在首次调用后被清空，导致 `disposalCount == 1` 无法识别重复调用。
- Change Impact Analysis：仅调整测试夹具的释放计数和直接测试，并更新测试映射与本执行报告；生产代码、公开契约、目标框架、依赖及构建图均未修改。风险为 LOW，复用生产代码未变化的既有证据，运行 Core 热插件宿主定向测试及 Core 全量测试双框架。
- 修复：夹具每次进入 `DisposeAsync` 都递增释放计数；新增双调用敏感性测试，连续调用两次并直接断言计数为 2。原有 Pre/Main/Post 配置失败与取消六个参数化场景继续断言框架恰好释放一次，同时保留异步释放门控、旧代可用及候选 ALC 回收断言。

| 验证 | 结果 |
| --- | --- |
| 锁定 SDK | `8.0.424`；未修改 `global.json` |
| `HotPluginHostTest` 定向 `net8.0` | 19 passed、0 failed |
| `HotPluginHostTest` 定向 `net6.0` | 19 passed、0 failed |
| Core 全量 `net8.0` / `net6.0` | 各 450 passed、0 failed（`--no-build --no-restore`；定向运行已构建当前测试程序集） |
| 释放计数敏感性 | `ConfigurationDisposalFixture_ShouldRecordEveryInvocation` 连续调用两次并观察到 2 次 |
| 六个候选失败/取消场景 | Pre/Main/Post 各失败与取消均断言框架释放 1 次，并保留旧代可用和 ALC 回收断言 |
| MVC / WebApi | 本轮未重跑；沿用上轮基线，生产代码与其影响范围未变化 |
| `git diff --check` | 通过，无错误 |

- Completed：FIX-017 的计数器敏感性与六个集成场景计数证据闭环；映射已更新。
- Open Actionable：无待 Executor 处理项；Round 20 Review 结论由独立 Reviewer 后续维护。
- Blocked Approval / Blocked External：无。
- Not Applicable：生产回归、MVC/WebApi 和性能验证；本轮仅改测试夹具/测试及两份文档，可复用生产代码未变化的证据。
- Accepted Limitations / Deferred：沿用 Round 16 分类。
- Next Action：STOP；交由独立 Reviewer 复核 FIX-017。
