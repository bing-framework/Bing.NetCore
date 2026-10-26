<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261024-fusioncache-integration
AI_REVIEWED_AT: 2026-10-24T07:59:18Z

# FusionCache 第三轮 Review：正确性、安全与性能

## 结论

前两轮 FIX-001～010 均已闭环；FIX-005 的同锁真实重获测试满足验收。本轮没有发现新的阻断性生产逻辑缺陷或已证实的性能故障。新增一项共享依赖维护建议（FIX-011，OPTIONAL），并记录已经量化的 L1 分配开销。

审查结论是 PASS_WITH_ISSUES，适用于本任务当前实现和已验证宿主；不是全仓库安全认证或生产容量认证。第一、二轮报告分别保存在 review-round1.md、review-round2.md；不修改 plan.md、execution.md 或业务代码。

| 状态维度 | 结论 |
| --- | --- |
| IMPLEMENTATION_STATUS | PASS，未发现新增必须修复的实现缺陷 |
| TEST_STATUS | PASS，原 10 项验收闭环；本轮复用相关测试证据 |
| RESOURCE_EVIDENCE_STATUS | PASS，本地回源预算、关闭和锁生命周期范围 |
| PERFORMANCE_EVIDENCE_STATUS | PASS，本地轻量证据；有额外分配，未建立生产 SLA/容量门槛 |
| SECURITY_AUDIT_STATUS | 核心项目存在继承的易受攻击包版本记录；本机实测宿主加载版本不在该公告影响范围，Redis 样例审计无已知漏洞报告 |
| EXTERNAL_GATE_STATUS | NOT_VERIFIED，远程 CI、生产容量未验证 |
| RELEASE_STATUS | NOT_EVALUATED |
| OPEN_ACTIONABLE | MUST_FIX=0，SHOULD_FIX=0，OPTIONAL=1；默认 recommended 修复范围为 0 |

## 本轮主要观察

### FIX-011：建议清理继承的 System.Text.Json 易受攻击包版本

- 状态：OPEN_ACTIONABLE（仅 OPTIONAL，不触发默认 recommended 修复）。
- 严重程度：LOW（本次已验证集成宿主的维护优先级）；处理要求：OPTIONAL。上游公告本身为 High，此处不降低公告评级。
- 问题：`dotnet list ... package --vulnerable --include-transitive --format json` 在核心集成项目中报告 System.Text.Json 6.0.0 / GHSA-8g4q-xg66-9fp4。
- 引入链：Bing.Caching.FusionCache → Bing.Caching → Bing.Core → Microsoft.Extensions.DependencyModel 6.0.0 → System.Text.Json 6.0.0。根声明在 `framework/src/Bing.Core/Bing.Core.csproj:31`；是既有共享依赖，不是新引入的 FusionCache serializer，也不是此前旧 EasyCaching 的 System.Drawing 告警。
- 公告依据：Microsoft 公告 CVE-2024-43485 涉及带 JsonExtensionData 模型的反序列化算法复杂度拒绝服务；6.0.0～6.0.9、8.0.0～8.0.4 NuGet 版本受影响，修补版本分别为 6.0.10、8.0.5。[官方发布的安全公告](https://github.com/advisories/GHSA-8g4q-xg66-9fp4)。
- 实际影响核对：本轮最小消费者实测 net8 加载共享框架的 System.Text.Json 8.0.30，net10 加载 10.0.11；这两个实际程序集不在该公告影响范围。完整 Redis 样例依赖审计没有报告已知漏洞。因此没有证实本次支持宿主存在该漏洞的可利用路径，不能仅凭项目资产中的 6.0.0 就宣称当前缓存服务可被攻击。
- 剩余风险：包资产仍携带该版本的审计记录；其他宿主、手工部署、旧运行时和独立加载上下文不能继承本机的结论。未做攻击载荷或生产部署渗透验证。
- 建议目标：在共享依赖维护范围清理该传递版本，使最终解析及部署均使用已修补资产；优先从 Bing.Core 的依赖链处理，不为消除告警随意给 FusionCache 核心增加 serializer 硬依赖或升级整仓 Microsoft 包。
- 验证方式：重新检查资产审计、最终消费者实际加载版本，并按共享依赖影响覆盖旧 net6/net8 与新 net8/net10 消费；保留本任务固定 FusionCache/Redis 选型及两项核心直接依赖。此建议不表示这些共享范围现在已完成修复。

### L1 热路径：存在可量化开销，但目前不构成性能故障

`FusionCacheEntryOptionsMapper.cs:31` 每次复制默认选项；`FusionCacheManager.cs:64` 起创建 CacheEntry/Task，GetOrCreate 另创建转发闭包，键与标签在调用入口编码。选项复制是当前防止共享配置污染的既定实现选择，不应为了降低分配而直接共享可变选项。

本轮隔离探针使用无 Redis、无遥测监听器的纯 L1，值为 int 42、短 ASCII 键、无标签；每条路径预热 10000 次，测量 20000 次，确认均同步完成且不执行工厂。分配使用 GC.GetAllocatedBytesForCurrentThread，计时不包含结果对象和序列化输出。

| 路径 | net8 均值 μs/次 | net10 均值 μs/次 | 两 TFM 分配 B/次 |
| --- | ---: | ---: | ---: |
| 门面 TryGet | 1.772 | 2.072 | 424 |
| 同实例原生 TryGet | 1.279 | 1.412 | 96 |
| 门面 GetOrCreate 命中 | 1.623 | 1.972 | 608 |
| 同实例原生 GetOrSet 命中 | 1.292 | 1.419 | 184 |

原生对照使用相同缓存与已归一化键，但不提供门面的输入验证、CacheEntry/Task 返回契约及配置隔离，因此该差值不是功能等价的 Provider 性能排名。单进程单轮结果受 JIT/调度影响，不能用来判断 net8/net10 谁更快或承诺吞吐量。

结论：高频 L1 工作负载可以继续关注 GC 分配，热点候选是选项复制、键处理和委托创建；现有数据不足以把它认定为泄漏或需要立即修复的性能退化。是否优化应以真实负载分配预算为依据，优化时保留取消、配置隔离、标签和同键合并语义。

### 冷启动排队：预算生效，不等同于低延迟保证

复用上一轮数据：64 个同键请求实际回源 1 次；64 个不同键实际回源 64 次，活跃/排队峰值 8/56，P95 约 124～127ms。L2 命中 P50 约 363～394μs。不同键批量冷启动已经产生排队，默认并发 8、队列 128、30 秒排队时限是保护数据源的起点，不是所有业务的响应时间 SLA。

现有测量不足以证明生产负载一定满足延迟目标。建议业务上线时按数据库连接池和回源耗时配置预算，而非直接放开并发；本轮不将未设定的 SLA 变成代码缺陷。

## 旧问题闭环与实现检查

| 范围 | 当前结论 |
| --- | --- |
| FIX-001 元数据存活窗口 | CLOSED，L1/L2、Fail-Safe、jitter、单次 TTL 和禁止跳过标记层读写的检查仍在 |
| FIX-002 启动订阅 | CLOSED，等待首次订阅并显式报告失败；不等同于后续持续健康 |
| FIX-003 关闭与所有权 | CLOSED，独立工厂 scope、应用排空、订阅/专属连接释放；未发现新增重复释放路径 |
| FIX-004 崩溃恢复 | VERIFIED_BOUNDARY，进程内恢复会丢操作；不承诺持久 outbox |
| FIX-005 原锁释放重获 | CLOSED，记录原非空句柄和实际 lockName；官方 Release 后直接对同一 lockName 取得新非空句柄，前后检查均早于自然过期；finally 释放新句柄 |
| FIX-006 基础边界 | CLOSED，null/默认值、预取消、租户键/标签隔离仍覆盖 |
| FIX-007 诊断测量 | CLOSED，真实仪表/Activity 与测量字段符合已声明范围 |
| FIX-008 回滚 | CLOSED，切流前清理/预热或新空间及双空间失效步骤完整 |
| FIX-009 频道隔离 | CLOSED，实际频道/事件及释放证据有效 |
| FIX-010 异常来源 | CLOSED，Redis/序列化异常断言与报告范围一致 |

额外追踪：实际工厂内准入及取消/超时 finally、AsyncLocal 递归检查恢复、键/标签转义与长度限制、默认/命名 DI 解析、外部组件所有权、每次 options 复制、软/硬失效、样例停止流程。未发现可确认的跨实例键碰撞、准入额度漏还或新引入的阻塞等待。

注意：同键等待者不占回源额度，但也不由 128 的工厂队列上限限制总请求数；应用入口仍需自己的请求容量策略。默认 L1 没有字节硬上限，SizeLimit 也不是字节计量，标签元数据和 Fail-Safe 驻留另占内存。以上是已声明容量边界，不新增为缺陷。

## FC-00～FC-08 验收

| 阶段 | 状态 | 范围 |
| --- | --- | --- |
| FC-00 | PASS | 固定依赖/TFM、包消费和旧新 Provider 共存；继承依赖审计建议单列 FIX-011 |
| FC-01 | PASS | 七操作公共契约及 CacheKey 薄扩展 |
| FC-02 | PASS | 命名注册、配置隔离、启动及生命周期 |
| FC-03 | PASS | 回源合并、准入、取消、异常恢复、递归 |
| FC-04 | PASS | 真实 L2、Backplane、可选锁；FIX-005 闭环 |
| FC-05 | PASS | Fail-Safe、超时、刷新与失效边界 |
| FC-06 | PASS | 本地诊断及轻量测量；生产容量不在通过范围 |
| FC-07 | PASS | 运行样例、灰度和回滚文档 |
| FC-08 | PASS | 必要本地测试和报告；外部发布门禁仍未验证 |

## 验证与复用

基准 HEAD：3919c1509907d661cfef3679dad79368c8b9cb39；范围包含未提交和未跟踪 FusionCache 文件。对照同会话已读取的完整 plan、execution 与历轮报告，以及当前源码、测试、文档、Git 状态/差异。其他任务的 Core/EventBus/Uow 等修改保留，不将其审查扩张为本集成的修复任务。

本轮 Change Impact：仅审查文档及 output 隔离探针，生产/测试源码、公共 API、TFM、包版本、Benchmark Harness 均未改。风险 LOW；不运行全解决方案、完整 Redis 矩阵或重型压力测试。

- Fresh：两个 TFM 的隔离 L1/程序集加载探针均成功；核心及 Redis 样例 NuGet 依赖审计、dotnet nuget why 引入链；UTF-8/报告协议/任务范围 diff 检查。
- REUSED_UNCHANGED_SCOPE：Review Fix 2 的锁相关真实 Redis 每 TFM 4/4 通过；Fix 1 单元每 TFM 57/57、Redis 每 TFM 15/15、最终指标各 1 项；Review 2 精准 net8 14/14；旧缓存/EasyCaching及 `.4` 包消费、性能 JSON。未把既有通过冒充本轮重新执行，旧锁用例的验收以 Fix 2 更强断言为准。
- luna_worker 独立只读检查性能 harness/数据口径及 FIX-005 代码/TRX，未报告生产性能缺陷，确认同锁重获验收；主代理完成核心审查、依赖影响追踪和新探针。
- 原始新证据：`output/fusioncache-review-round3/ReviewProbe.csproj`、Program.cs、probe-net8.0.log、probe-net10.0.log、audit-core.json、audit-sample.json。
- 复现：`dotnet run --project output/fusioncache-review-round3/ReviewProbe.csproj -c Release -f <net8.0|net10.0>`；`dotnet list <生产项目或样例项目> package --vulnerable --include-transitive --format json`。
- 本轮未创建容器，未修改依赖，未发送外部通知，未暂存/提交/推送/发布。

## Round 3 汇总

- Task：20261024-fusioncache-integration；Round：Review 3（用户明确要求再次查正确性/性能）。
- Implementation：原 10/10 验收闭环；新增 MUST_FIX/SHOULD_FIX=0，OPTIONAL=1。
- Release Gates：本地证据通过，远程 CI/生产容量未验证；不计算未经定义的发布完成比例。
- Changed Scope：review.md、review-round2.md；output 下隔离探针与证据。
- Validation Run：L0 静态/依赖/格式；L1 小型运行探针；L2/L3 复用；L4/L5 不执行。
- Completed：旧问题闭环、安全引入链与实际宿主验证、L1 开销量化。
- Open Actionable：仅 OPTIONAL FIX-011；recommended 范围无待修项。
- Blocked Approval：无。
- Blocked External：远程 CI、生产容量与真实部署审计；不触发自动 Fix。
- Not Applicable：SQL/SQLite、新 API 历史同构基线。
- Accepted Limitations：最终一致、迟到工厂竞争、进程内恢复、非业务互斥、L1 非字节硬限额。
- Verified Boundaries：同锁真实重获、准入/排队峰值、本机宿主程序集版本及本次 L1 分配量。
- Deferred：原计划明确排除能力不扩张；共享依赖建议可纳入后续维护工作。
- No-Progress Check：CHANGED，有新安全审计/运行时及分配证据，未重复制造旧 Finding。
- Next Action：STOP。本轮仅审查，不自动修复可选维护项或为性能数字重构。
- Goal Status：未启动自动 Goal/修复循环；Review 交付完成。
