<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261022-full-suite-recovery
AI_REVIEWED_AT: 2026-10-22T10:06:55.798643+00:00

IMPLEMENTATION_STATUS: PASS
CACHE_RETRY_STATUS: PASS
TEST_STATUS: PASS
TEST_STATUS_SCOPE: 当前缓存职责L1/L2与限定独立否定probe；其他未变范围复用
CURRENT_FINAL_FULL_SUITE_STATUS: NOT_VERIFIED
PREVIOUS_FINAL_CHECKPOINT_STATUS: FAIL_PRE_CACHE_TEST_FIX
PROVIDER_TEST_STATUS: PASS_REUSED_UNCHANGED_SCOPE
RELEASE_STATUS: BLOCKED
OPEN_ACTIONABLE: 0
REVIEW_ROUND: 用户另行授权的缓存单项Fix→Review，不重置原三轮循环
GOAL_STATUS: STOPPED_BLOCKED

本项限定独立复验 **PASS**。并发测试修正了“同时准备调用等于持有同一失败Lazy”的错误前提，仍要求真实故障传播、每个观察到指定失败的调用至多一次显式业务重试、所有调用最终共享映射、provider精确加载两次及后续命中不再加载。确定性迟到调用与原stale保护继续验证失效项移除边界。未发现需要修改生产缓存的证据。

原FIX-001..004保持CLOSED，OPEN_ACTIONABLE=0。当前候选满足进入新最终Release构建与默认L4检查点的实现前置；本轮没有运行L4，不宣称当前全量通过。上一候选的真实L4失败保留为历史，clean-source正式发布限制另列，不要求提交或放宽门禁。

## 1. 授权、输入与实际差异

已读取 [本项追加授权](cache-retry-continuation.md)、review-code技能、AGENTS、`C:/Users/jianx/.codex/GOAL_RULES.md`、完整plan、最新execution/final-report、当前测试及相关生产调用链、修复前快照、原net6失败TRX和四份本次L1/L2原始TRX。授权仅允许一次本项Fix→Review→最终检查点，不重开原181或四项AOP问题，不进入新的自动循环。

上一份完整AOP验收报告已原样归档为 [AOP限定复验报告](artifacts/reviewer-cache-retry/review-aop-continuation.md)，其FIX历史与独立证据继续有效。

相对本项保存的起点，实际改动只有 `EntityMappingCacheIsolationTest` 的并发测试、一个迟到调用测试、阻塞provider的超时/Dispose及期望消息常量。原单次失败测试只把消息字面量提取为相同常量；其他既有命中、未命中、实体/上下文隔离、容量及stale保护的测试方法体未改。见 [真实测试差异](artifacts/reviewer-cache-retry/test-change.diff)、[方法体与原始证据复核](artifacts/reviewer-cache-retry/validated-summary.json)。没有把相对HEAD的大量既有生产差异归成本轮变更。

生产 `DefaultEntityMappingResolver.cs` 与 `DefaultEntityMappingResolver.before.cs` 字节相同，SHA256=`BE4E43C6CD015CFCF4B93019D2C960B381BB9A333C285435E59EA2E0A3BD453E`。当前测试源码SHA256=`122F84838B2DC3499FA0DA3472E3452FBB0FE6453C8EDCE1C8D2332940D5F15C`。本轮Reviewer读取前后，相关源码、AOP源码、plan/execution/final-report及181 CSV哈希均未改变，见 [输入身份](artifacts/reviewer-cache-retry/review-input-identities.json)。

测试前CIA：ChangedFiles=上述缓存职责测试；ChangedProjects=Bing.Data.Sql.Tests；ChangedPublicContracts/RuntimePaths/Providers/BuildPackaging/BenchmarkHarness=无；ChangedTFMs=net6/net8；ChangedDocsOnly=否；AffectedDependents=本测试及SQL单元项目；RiskLevel=MEDIUM。仅做小型否定验证，正式L1/L2复用当前TRX；没有运行全量、Provider、数据库或性能测试，没有扩展缓存架构审计。

## 2. 原失败与生产契约判断

保存的原net6项目TRX为1317项、1316 Passed、1 Failed，精确失败为 `Resolve_WhenConcurrentMetadataFailureIsRetried_ShouldAllowAllWaitersToRetry` 的计数断言：Expected 8，Actual 7，原堆栈定位410行。保存副本与历史原TRX哈希相同，原始文件未覆盖；[解析结果](artifacts/reviewer-cache-retry-evidence/cache-retry-evidence-check.json) 保留完整消息/堆栈。此处仅核对该原始项目失败，不重新审计全部103份L4。

当前生产流程先从ConcurrentDictionary取得 `Lazy<EntityModelMetadata>`，ExecutionAndPublication共享一次加载。读取Lazy.Value失败后，按同一个key/value原子移除该失败Lazy并 `throw;` 传播故障。已经持有旧Lazy的调用可观察同一失败；失败项移除后才进入的调用可取得新Lazy并直接成功。旧等待者的移除操作不能删除后来的成功Lazy。

ready/start只约束进入resolver前的准备，FirstCallEntered只证明首个provider调用阻塞；均没有观察其他调用取得哪一个Lazy。因此“8个同时启动的调用必须全部失败”不是现有缓存合同。原7失败、1迟到成功与生产实现相容，本轮没有证据要求库内吞错或自动重试。

## 3. 新测试是否保留有效断言

并发测试每个调用先真实Resolve；只有 `InvalidOperationException` 且消息等于本helper唯一指定故障时进入catch。catch里只有一次额外Resolve，第二次调用不在重试catch覆盖范围，持续故障会直接使任务和测试失败。非指定类型/消息及TimeoutException不被当作预期失败吞掉。

每个成功返回记录映射和实际Attempts（1或2）；至少一个调用必须观察首故障，8个结果都须完整返回、表名正确、引用同一成功映射。provider严格等于2次，并在随后缓存命中后再次断言仍等于2。迟到调用可直接成功，不再假定它持有已移除的旧Lazy；这与保留正确身份、次数及命中断言一致。

正式测试按异常类型与特定消息识别故障，没有逐个保存异常对象；firstAttemptFailures/businessRetries也在同一个catch中递增，不能仅靠计数相等证明调用正确。审查结合实际第二次Resolve、Attempts结果、provider严格次数、确定性首失败传播，以及下节独立观察的原异常实例/返回次数关联作结论，没有把单一绿灯或两个相等计数当作充分证据。

新增迟到测试先等待首个Resolve的异常通过Assert.Throws传播完毕，再调用下一次Resolve。这时resolver的catch移除步骤已结束；随后成功、相同缓存映射及provider=2证明失败项可重新加载且命中不再访问provider。原stale测试仍直接验证“旧值删除失败且新值保留；删除当前值成功”，未被删除或放宽。

退出路径：start、FirstCallEntered及provider release等待各有10s期限；任务汇合有20s期限。finally总会放行start/provider并再次有限等待全部任务，之后using才释放同步对象；迟到用例也在finally释放并汇合首任务。没有固定sleep、无限重试、测试框架自动重试、全局串行化或只降低失败数量区间。

## 4. Reviewer独立有界否定验证

在 [私有probe目录及说明](artifacts/reviewer-cache-retry/README.md) 中复制当前完整测试源码，保留断言/业务重试/finally，只给私有helper加入故障模式和观察器，使用当前Release生产DLL。未改原业务测试或生产bin，没有构建替代缓存实现。这些是测试有效性的受控诊断，不冒充正式TRX。

| 每个TFM的独立场景 | net6 / net8结果 |
| --- | --- |
| 原受控首次失败 | 8个返回共享映射，provider=2；本次各观察8个原异常实例与8次成功的第二次调用，关联相符 |
| 非指定消息的InvalidOperationException | 测试失败、原异常传播，未进入指定故障重试catch |
| 同消息但不同类型ApplicationException | 测试失败、原异常传播，未进入指定故障重试catch |
| provider持续失败 | 第二次业务调用故障向外传播，无伪成功或第三次业务重试 |
| provider不传播首故障 | “至少一个受控失败”断言拒绝通过，不能悄悄退化成全成功测试 |
| 迟到调用及原边界 | 新迟到用例、stale移除保护、原失败移除、hit、实体隔离均通过 |

两TFM各6组判定均符合预期；负面场景的通过表示测试成功拒绝错误行为。所有并发模式结束时活动调用数为0，同步对象在任务汇合后释放。受控首失败中的异常ReferenceEquals原helper异常，第二次成功结果数与实际观察失败数相等；本次8/8是一次调度观察，不成为新的固定八失败要求。

原始输出：[net6](artifacts/reviewer-cache-retry/negative-probe-net6.0.json)、[net8](artifacts/reviewer-cache-retry/negative-probe-net8.0.json)。每个TFM只运行一次，进程60s截止，没有timeout或重跑求绿灯。实际加载生产DLL路径与哈希记录在JSON，两个TFM的probe生产DLL/PDB与当前Release输出字节一致；[验证脚本](artifacts/reviewer-cache-retry/validate-evidence.py) 再次核对全部输入未变。该小型证据不声称穷尽全部调度。

## 5. 正式TRX与候选关联

明确采用 `artifacts/cache-retry-followup-20261022T094923Z` 的四份L1/L2，逐条UnitTestResult与Counters重算，核对测试程序集、方法及TFM，没有混入原失败或其他run：

| 正式证据 | net6.0 | net8.0 |
| --- | --- | --- |
| L1 EntityMappingCacheIsolationTest | 25 Passed / 0 Failed / 0未执行 | 25 Passed / 0 Failed / 0未执行 |
| L2 Bing.Data.Sql.Tests | 1318 Passed / 0 Failed / 0未执行 | 1318 Passed / 0 Failed / 0未执行 |

四份TRX内，并发重试、新迟到调用、stale保护、原首次失败移除、后续hit、实体隔离六方法均唯一实际Passed。其余缓存命中/未命中、隔离与容量职责断言保持在完整L1/L2中，没有为本项弱化。

[正式证据说明](artifacts/reviewer-cache-retry-evidence/README.md) 及其可复跑解析脚本记录：TRX CodeBase对应当前测试DLL；测试/生产CodeView与PDB身份一致；PDB文档校验和匹配当前源码；源码及构建时间早于各自运行。TRX未封存逐run DLL哈希和依赖实际加载路径，故不将这些关联证据冒充run-sealed正式发布证明。独立probe另记录当前实际加载生产身份。

原181 CSV有181个连续唯一原始索引，新缓存方法不在其中；修改时间早于本项测试编辑，本轮读取前后hash相同。本项证据集没有更早的CSV哈希快照，因此不声称仅凭本项材料证明其全部历史内容逐字未变。缓存新失败单独登记，没有增加成第182项、替换原失败或重开原181。

## 6. 功能建议与Findings状态

保留库内“向调用方传播首次故障、按key/value精确移除失败Lazy”的建议准确；当前不需生产补丁。业务是否重试应由调用方按明确可恢复的provider/业务故障判断，并有次数与时间边界；这里的一次显式重试是本场景验证策略，不代表任意InvalidOperationException都适合重试。永久错误、非指定异常及重试再次失败应继续传播，不能由resolver静默吞错。不要依赖“同时启动”推断共享某个Lazy。

新增验收项 CACHE-RETRY: PASS；未发现新的OPEN_ACTIONABLE，不制造FIX-005。

| 既有Finding | 当前状态 | 保留依据 |
| --- | --- | --- |
| FIX-001 目标/提交取消丢失 | CLOSED | 前次AOP独立证据；本项未改对应源码 |
| FIX-002 参数预取消拒绝目标 | CLOSED | 扫描移除与原契约证据未失效 |
| FIX-003 嵌套source重复消费 | CLOSED | 前次限定复验通过，源码未变 |
| FIX-004 类代理重入 | CLOSED | 前次Call/Callvirt与真实链证据通过，源码未变 |

完整结构化FIX历史、严重程度、原证据与闭合要求见归档AOP报告。本轮不重复泛审或运行AOP。

## 7. 计划与发布状态、停止条件

| 计划/授权项 | 本轮状态 |
| --- | --- |
| 原T01清单、T02门控/Scope、T03Logging | PASS，未变范围复用 |
| T04 AOP四FIX及EventBus | PASS，复用；EventBus是起点已有测试，未归成本轮新增 |
| 真实MySQL/SQLServer及原Postgres混合、清理与原容器保护 | PASS，复用既有证据；没有重新操作外库 |
| 本新增缓存测试契约与L1/L2 | PASS |
| 当前候选Release+默认L4 | NOT_VERIFIED，父任务待刷新；上一候选的单项L4失败仍保留 |
| clean-source正式发布门禁 | BLOCKED_APPROVAL，继续遵守用户不提交约束，不转为代码Finding |

本轮只写review及必要review artifacts，没有改业务实现/测试/plan/execution/final-report，不暂存、提交、推送或外部通知。未运行全量、Provider、性能、数据库，也未要求外部CI/IDE等未授权验证。

Task: 20261022-full-suite-recovery
Round: 用户授权的缓存单项限定复验（已完成，不重置三轮）
Implementation: 本新增项PASS；既有4/4 FIX保持CLOSED
Release Gates: 当前Release/L4待父任务新检查点；clean-source未放行，整体任务未宣称完成
Validation Run: L0=差异/UTF-8/源范围/正式TRX/PDB/身份；L1=两TFM各6组有界probe；L2=当前正式1318/1318证据核对；L3=未变范围复用；L4=未运行；L5=NOT_APPLICABLE
Reused Evidence: 当前缓存25/1318正式TRX；原181、AOP、门控、Logging、EventBus、Provider/清理
Completed: 缓存测试契约校正、确定性迟到路径及否定验证验收通过
Open Actionable: none（0）
Blocked Approval: clean-source正式发布门禁
Blocked External: none新增；外部CI/IDE不列必修
Not Applicable: 本轮无生产/Provider/热路径变更，无需追加数据库/Benchmark
Accepted Limitations: none新增
Verified Boundaries: 指定负面模式被真实拒绝，任务汇合及资源释放；不宣称穷尽调度
Deferred: none新增；最终检查点为既定下一阶段
No-Progress Check: CHANGED（用户新授权；新增测试与证据明确修正错误并发前提）
Next Action: STOP（Reviewer交付；父任务按授权安排当前Release build+默认L4一次新检查点）
Goal Status: STOPPED_BLOCKED（实现无待修项，正式发布未放行）
