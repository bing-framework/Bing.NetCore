<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261027-resilience-boundaries
AI_EXECUTION_FINISHED_AT: 2026-10-27T09:08:28Z

## Review 修复记录：Round 1（本轮权威状态）

任务 `20261027-resilience-boundaries`，模式 REVIEW_FIX，fixScope=recommended。本轮从 2026-10-27T08:16:13Z 开始，处理复核报告的 FIX-BND-R01～R05，均为 SHOULD_FIX。原始复核见 [review-before-fix.md](evidence/fix-round1/review-before-fix.md)。下方“历史实施记录”保留先前报告，其 805 项、78 轮和旧审查结论只代表当时状态，不能替代本轮证据。

五项修复均已实施并通过必要验证；新的独立审查已判定 R01～R05 全部 CLOSED、OPEN_ACTIONABLE=0，状态 PASS_WITH_ISSUES，见 [review.md](review.md)。新增公共 API 不暴露引擎类型，未升级全局依赖、提交或发布。

| FIX | 根因与修复 | 新验证 |
|---|---|---|
| R01 | 原 Ordered 只有固定优先级。新增平滑加权轮转，按服务隔离，TTL/LRU 和端点容量有界；HTTP 先做契约/撤销过滤，再固定本次候选快照。Ordered 保留显式注册。 | 每框架 12 个职责用例；3:1、等权、并发、成员/权重变化、回收、HttpClient 首发分布。独立 nupkg 消费再次断言 100 次首发选择为 75:25。 |
| R02 | MySQL Schema 只验列，未校验 FORCE INDEX 必需名称。现在必须有 `{prefix}_due(kind,next_at,id)`，不改变认领锁或索引提示。 | 每框架 4 项真实 MySQL：默认并认领成功、等价改名、缺失、错误列序。 |
| R03 | 统一诊断缺少扩展 producer。接入切换/排空、发现刷新/stale、分布式准入、Replay/Saga 已提交状态；新增 RecordLifecycle 的 Meter/Activity 异常隔离。 | 每框架 5 项职责用例；成功/失败/取消、状态标签、真实 SQLite 流程、监听器抛错不影响发布。 |
| R04 | 逻辑过期没有物理载荷/历史保留治理。增加可选 IOperationRetentionStore、不可变保留策略、事务批量清理和 Worker 调度。保留幂等身份，Saga 父子原子清理，历史分批续扫，未知/人工/在途记录受保护。 | 每框架 11 项职责用例，真实 SQLite/MySQL；保护窗口、并发认领、取消/异常回滚、批量恢复、审计/幂等保留、授权和禁止重新激活。独立包验证已清理数据仍可按键找到。 |
| R05 | 本机性能证据缺 HTTP/Redis/Journal 指定路径。新增两个可复现探针，固定工作负载、预热与三轮重复，分别保存双框架原始数据。 | 新增 120 轮：HTTP 48、Redis 24、Journal 48。记录 p99、连接、实际并发、明确的队列口径和资源快照；不是生产 SLA 认证。 |

### 修改与兼容边界

- 生产代码：Discovery 选择器与三个来源；HTTP Discovery 注册/过滤；Core diagnostics/registry；Distributed observer；Operations 保留契约/Relational 清理与索引校验；Replay/Saga 配置、注册、协调器和 Worker。完整文件清单见 [static-check.json](evidence/fix-round1/static-check.json)。
- 新测试类：WeightedDiscoveryTests、MySqlSchemaIndexTests、LifecycleDiagnosticsTests、OperationRetentionTests。旧 ClientExtensionsLoopbackTests 主备测试显式选择 Ordered，保留原有发送次数和逐次签名断言；未放宽恢复行为断言。
- Retention 默认 null。配置后清理不可恢复载荷；幂等摘要/状态永久保留，历史按显式期限清理。`retained:v1:` 标记复用已有 Data 列，无 DDL 迁移；启用后回滚目标必须保留标记读取兼容，关闭策略不恢复数据。
- 加权分布测试经过实际 HttpClient 管线和记录型 handler，不称为真实网络分布测试。既有两客户端 loopback 回归和 R05 网络采样分别证明其覆盖范围。
- `RecordLifecycle` 只添加固定事件/结果标签；原策略事件的稳定 pipeline/provider 标签保持。没有将操作 ID、URL 或配置代数放入新增标签。
- 默认 no-op 清理分支、Saga ManualIntervention 父状态排除由源码核对；没有将缺乏独立断言的分支写成已直接测试，见 test-map。

### 验证结果

最终六项目 × 双框架 **869/869，0 失败、0 跳过**，使用真实临时 Redis/MySQL 的对应门控；12 份 TRX 与命令见 [regression-summary.json](evidence/fix-round1/regression-summary.json)。新增 32 项/框架，共 64 项；定向复跑不重复计数。

| 项目 | net6 | net8 |
|---|---:|---:|
| Extensions | 207 | 207 |
| Core | 88 | 88 |
| HTTP | 71 | 76 |
| Runtime adapter | 34 | 34 |
| Runtime | 19 | 19 |
| WebApiClient SDK | 13 | 13 |

[独立包消费](evidence/fix-round1/package-consumption/results.md) 使用独立 NuGet cache、nupkg 和无 ProjectReference 消费项目，**34 包、53 命令、6 次运行、6 次发布全部通过**。检查 assets/nuspec/deps：BuiltIn 无 Polly/Fences/Microsoft Resilience；Fences 无 Polly/Microsoft Resilience；Polly 单独消费。当前漏洞查询 0 条；保留 net6 NETSDK1138 提示，未压制降级警告。

[新增性能结果](evidence/fix-round1/performance-results.md) 保存全部 120 轮及复现脚本。HTTP 使用真实 loopback；Redis 使用真实独占实例及 TCP 延迟/中断代理；Journal 使用 SQLite/MySQL，断言积压、重复、确认恢复与清理的业务计数。Redis 失联采样在一次 RST 和预热之后，是连接池已知失联的稳态开销，正式窗口新增 RST/连接为 0。Journal 恢复采样重建宿主并读取过期租约账本，没有在采样中杀进程。延迟/队列/连接工厂指标的不同口径已明确，不伪称驱动内部观测值。

### 历史失败、复验及环境

- 定向诊断测试首次暴露抛异常全局监听器污染后续捕获；测试现在先解除自身订阅再抛错，继续验证异常隔离。早期 Saga 清理终态白名单误用了 Succeeded，已改为 Completed/Compensated，并由 E2E 和真实存储职责测试覆盖。
- 首轮扩展回归的旧主备测试因默认加权首选变化失败；显式 Ordered 后保留原始断言，最终 869 项全绿。所有失败 TRX/log 保留在 fix-round1，不替换成通过结果。
- HTTP/Redis 探针修正 Timer.ActiveCount 类型、CSRedis 端点格式和代理释放竞态。两次无效探针因 port= 被忽略写入本机开发 Redis 六个随机探针状态键；已按完整键和 cfg 精确核对后删除，未改变其他数据/容器设置。记录见 [probe-routing-cleanup.json](evidence/fix-round1/http-redis-performance/probe-routing-cleanup.json)。这些失败数据不计入正式测量。
- 首次新包消费的测试数据使用了非哈希 scope/key，正式契约校验正确拒绝；修正消费者输入后重新打包消费，初次失败报告保留。
- 正式测试服务为任务独占 mysql:8.4、redis:7.4-alpine，只绑定 loopback；镜像与端口见 [environment.json](evidence/fix-round1/environment.json)。最终按精确任务标签清理，见 [cleanup.json](evidence/fix-round1/cleanup.json)。

### Change Impact Analysis 与收口

ChangedProjects=Resilience、Discovery、Http.Discovery、Distributed、Operations、Operations.Relational、Replay、Compensation；ChangedPublicContracts=新增选择器/生命周期方法/可选保留接口及选项；ExistingSignaturesChanged=0；TFMs=net6/net8；Risk=MEDIUM（准入/状态/数据清理）。先运行直接职责测试，再完成相关六项目回归、真实本地存储/网络验证和独立包消费。最终生产代码在该回归后未再变更，文档修正复用代码证据；未运行无关数据库门禁或重做旧 HA 矩阵。

MUST_FIX=0；SHOULD_FIX=5；执行完成=5；PARTIAL/BLOCKED/FAILED=0（当前修复范围）。生产 SLA/容量阈值仍为 BLOCKED_APPROVAL；目标部署认证及完整管理页面变更操作仍为 BLOCKED_EXTERNAL，均未标成已通过。

独立审查包含 [实现复审](evidence/fix-round1/review-implementation.md)、[性能口径复审](evidence/fix-round1/review-performance.md) 和最终 [review.md](review.md)。最终判断与 OPEN_ACTIONABLE 数以 reviewer 输出为准，执行者不改审查结论。无新增可执行发现时停止，不因生产门禁再启动修复轮次。

本轮未 git add/commit/push/PR、未发布 NuGet、未执行生产迁移；保留工作区其他任务修改。`node .agents/scripts/task-finish.mjs 20261027-resilience-boundaries --no-notify` 已成功：finalStatus=COMPLETED、reviewRound=1、notificationStatus=skipped，见 [task-finish.json](evidence/fix-round1/task-finish.json)。独立验收结束后停止，不再重复测试。

---

## 历史实施记录（下方原报告保留，已由 Round 1 新证据补充）


# 实施执行报告

## 执行结论

计划 BND-01～BND-06 已完成。本轮交付可选 ORM 事务桥接、生产 Journal 迁移与只读校验、三个注册中心协议源、管理 API/控制台、持久订单补偿样例、Redis HA 故障验证和可重复性能测量。默认接入行为、中性公共契约和两个客户端入口保持。

有效测试证据为 **805/805**，包含历史 723 项兼容基线与新增 82 项；这是按稳定 testId 和相关源码范围合并的结果，不是隐去失败后的单次全量 PASS。首轮一个旧核心时间敏感用例失败、定向复验通过，原始 TRX 保留。独立审查的 5 项发现全部 CLOSED，OPEN_ACTIONABLE=0。

## 任务信息

- Task：20261027-resilience-boundaries；上游：20261027-resilience-extensions。
- 执行开始：2026-10-27T06:33:12.046Z；环境：Windows / PowerShell / .NET SDK 10.0.400。
- 主代理负责架构、实现集成与验证；luna_worker 负责 Redis HA、窄范围独立审查和人工补偿管理测试。
- 用户已选择“全部分阶段补齐”；未增加生产部署、真实支付业务或全厂商版本认证范围。

## 计划执行情况

| 阶段 | 交付 | 验收 |
|---|---|---|
| BND-01 | SQLite/MySQL v1 版本化 SQL、嵌入资源、SchemaVersion/GetMigrationScript/ValidateSchemaAsync；EF Core/FreeSQL 可选包、Dapper 原事务复用 | 双框架职责类各 12/12；真实 MySQL 类型/索引/事务引擎；SQLite 三种 NOCASE 拒绝和有效 BINARY；ORM 提交/回滚 |
| BND-02 | Consul、Nacos v1、Kubernetes EndpointSlice；冻结配置、分页、ETag、可信 origin、认证、取消隔离、过期和撤销 | 双框架各 20 个职责用例；坏协议不沿用 stale、取消与传输所有权 |
| BND-03 | Operations.AspNetCore 管理包、可选控制台；持久订单库存/模拟支付/配送补偿 | 管理各 7 项，样例各 2 项；真实 Kestrel/SQLite 的授权、防伪、CAS、Unknown 确认及审计 |
| BND-04 | Sentinel/Cluster 任务独占探针和脚本 | 真实 Redis 双框架 14 阶段通过 |
| BND-05 | 三 Provider × 双框架的当前性能测量，发现/Replay/Saga 本地工作负载 | 78 轮正式测量，吞吐、p50/p95、分配与资源；无生产阈值断言 |
| BND-06 | 解决方案登记、指南、迁移与回滚、测试映射、独立包消费和审查 | 34 包 / 6 消费者运行发布；独立发现 5/5 CLOSED；UTF-8/静态检查通过 |

## 已完成事项与 API/数据/配置变化

- 三个新可选包均支持 net6/net8：`Bing.Resilience.Operations.EntityFrameworkCore`、`Bing.Resilience.Operations.FreeSql`、`Bing.Resilience.Operations.AspNetCore`。不在中性核心自动引入 ORM 或 ASP.NET Core。
- EF Core Relational 固定 6.0.36、FreeSql.DbContext 固定 3.5.311；Dapper 复用已有 ADO.NET 事务。桥接必须有宿主事务，不自行提交、回滚或关闭其连接。
- Journal 校验只读，不执行 DDL；校验列、类型/宽度、空值、主键、幂等唯一键、认领索引、MySQL InnoDB/utf8mb4_bin、SQLite BINARY。脚本版本历史由宿主迁移工具管理。
- 三个注册中心源不依赖厂商 SDK。永久协议/认证/信任错误清除旧快照；临时网络错误只在 MaxStale 内回退；EndpointSlice 完整分页后发布。
- 管理能力要求宿主身份、权限、访问范围、actor；写入前验证防伪令牌和版本。状态投影排除载荷、幂等键和租约；人工动作通过现有协调器及持久审计。
- 样例业务使用独立 SQLite 账本，模拟提交后丢响应，重建处理器后确认；逆序补偿恢复库存和撤销模拟支付。同键异体和补偿换键均拒绝，未知副作用不直接补偿。
- 既有 Redis 故障测试增加可配置容器名称，并在故障注入前检查精确任务标签；默认旧配置仍有效。

接入代码、注册和回滚见 [使用指南](../../../docs/guides/resilience-boundaries.md)；生产迁移见 [迁移说明](../../../docs/migrations/resilience-extensions/README.md)。

## 修改文件

生产范围：`RegistryServiceEndpointSources.cs`；`RelationalOperationStore` 与新增 `.Schema.cs`/嵌入脚本；三个新可选包；`SqliteOrderWorkflow`、`DurableDemo`、`ManagementDemo` 和样例入口。解决方案添加三个可选包。

测试范围：`RegistryAdapterTests`、`RelationalBoundaryTests`、`OperationsManagementTests`、`OrderWorkflowSampleTests`、`RedisOutageTests` 配置、测试项目引用。完整符号到测试方法映射见 [test-map.md](test-map.md)。

交付范围：本任务目录、`docs/guides/resilience-boundaries.md`、既有扩展指南/迁移接管说明和版本化 SQL。30 个相关生产/测试/文档文件的静态检查清单见 [static-check.json](evidence/static-check.json)。

## 测试结果与证据

| 验证 | 最终有效结果 | 原始证据 |
|---|---|---|
| Core | net6 88/88；net8 88/88（含定向复验） | [六项目首轮记录](evidence/final-tests/latest.json)、[预算复验](evidence/tests/core-budget-recheck.trx) |
| HTTP | net6 71/71；net8 76/76 | 同上；源码范围后续未变 |
| Runtime adapter / Runtime / SDK | 各框架分别 34/34、19/19、13/13 | 同上；源码范围后续未变 |
| Extensions | 各框架最终有效 175/175 | 完整回归各 174/174 后，校验职责类 12/12 覆盖原 11 项并新增 1 项；[明确证据清单](evidence/validation-summary.json) |
| 包消费 | 34 包；6 次运行、6 次发布；53 命令通过 | [最终消费报告](evidence/package-consumption/results.md)，正式 run `2026-10-27T07-31-19-079Z` |
| 示例 | net6/net8 编译并运行；Replay 写一次、Saga=Compensated | [net6](evidence/tests/sample-final-net6.0.log)、[net8](evidence/tests/sample-final-net8.0.log) |
| Redis HA | 14 阶段通过 | [拓扑/限制](evidence/redis-ha/README.md)；接受 run `20261027T065843Z-359afd0116894c9d900939cb694b26e4` |
| 性能 | 78 轮正式测量 | [结果](evidence/performance/results.md)、[复现说明](evidence/performance/README.md) |

新增 41 个用例/框架（含 Theory），共 82 项。汇总按 TRX 的稳定 testId 合并，避免长 Theory 参数的显示名称截短造成重名；[validation-cases.json](evidence/validation-cases.json) 逐项保留结果来源。没有将定向复跑重复计数。

首轮净结果是 795 项、794 通过、1 失败：旧 `FallbackActionIsCancelledByExecutionBudget(providerName: "BuiltIn")` 在 net8 的同步等待超时，发生时有其他构建工作；核心和测试源码未改，随后三 Provider 同一职责用例 3/3 通过。这里只记录观察与复验，不把并发负载作为已经证明的唯一根因。原失败位于首轮 TRX，未删除或放宽断言。

## Change Impact Analysis 与验证范围

最后生产变更只涉及 SQLite DDL 排序规则校验与样例补偿幂等键；ChangedProjects=Operations.Relational/样例，ChangedPublicContracts=无既有签名变化，ChangedTFMs=net6/net8，Risk=MEDIUM。先进行存储/样例 13/13 双框架定向测试，再扩展项目各 174/174；随后同一排序规则发现补齐单引号写法，仅重验职责类各 12/12、真实探针和最终包消费。

- L0：相关编译、UTF-8/空白、解决方案 diff check。
- L1/L2：四个职责测试类与扩展项目。
- L3：真实 SQLite/MySQL、Kestrel、Redis HA、独立 nupkg 消费、样例。
- L4：本任务阶段检查点已运行六项目双框架；后续未改变范围标记 REUSED_UNCHANGED_SCOPE。
- L5：固定工作负载测量已完成；排序规则和文档变化不影响性能探针路径，未重复测量。

## Build / 依赖 / 格式

最终包消费使用独立 NuGet cache、明确本地源，无 ProjectReference。检查 assets、nupkg nuspec、发布 deps：BuiltIn 不含 Polly/Fences/Microsoft Resilience；Fences 不含 Polly/Microsoft Resilience；可选 Polly 单独消费。当前 NuGet 漏洞查询 0 条，不代表未来数据库不会更新。没有通过压制 NU1605 解决依赖冲突，也未修改全局依赖基线。

net6 样例构建保留 SDK 的 NETSDK1138 支持周期警告。新范围 UTF-8/空白 30 文件检查通过；`git diff --check -- Bing.All.slnx` 通过。全工作区存在其他任务修改及既有空白问题，不把全局结果伪称为干净。

## 计划偏差与基线问题

注册中心保持在显式使用的 Discovery 包中实现 HTTP 协议，不新增三个厂商 SDK 包；与本任务计划一致。MySQL ORM 桥接组合未运行：ORM 所有权使用真实 SQLite 驱动验证，MySQL 使用关系型存储/迁移及原多 Worker 测试验证。

浏览器自动化已验证管理登录与查询；旧页面原生确认框导致 CUA 的焦点控制超时，重复读取无法恢复，停止同类尝试。控制台最终使用“确认并执行”按钮、原因与版本字段，写操作由真实 Kestrel 的 CSRF/CAS/重复审计测试验证；未声称最终页面完整点击链已自动通过。

性能探针正式测量前的 Debug 草稿和一次 Saga 终态断言写错的运行不纳入正式证据；包消费初次暴露管理依赖注册/Minimal API 参数绑定问题，修复后重新打包消费，旧失败记录保留。

## 独立审查

[独立审查汇总](review.md) 引用两份独立报告。关闭的问题包括坏协议沿用 stale、控制台路径遮蔽查询、人工补偿重复审计测试缺口、SQLite NOCASE 破坏身份比较、补偿换键结果不一致。按根因进行定向修复，最后没有 OPEN_ACTIONABLE。

## 剩余边界与生产门禁

| 分类 | 状态及影响 |
|---|---|
| Completed | BND-01～06；本机实施与必要验收 6/6；独立发现 5/5 CLOSED |
| Open Actionable | 0；不进入下一轮自动修复 |
| Blocked Approval | 生产 SLA/容量/回归阈值尚未给定；当前只交付实测数据，不能自动批准阈值 |
| Blocked External | 目标注册中心版本/TLS/RBAC、真实生产部署和完整浏览器变更操作验收不由本机协议/接口测试替代 |
| Not Applicable | 新能力无历史等价 before；不伪造 before/after 性能提升 |
| Accepted Limitations | Nacos v1 兼容结构；单宿主 Docker；Cluster 三主无副本晋升；样例支付为本地账本；宿主负责凭据与业务补偿规则 |
| Verified Boundaries | Sentinel 晋升后 Open/epoch 保留；全槽 Cluster Lua 调用、定向断链与恢复；显式 FailClosed/FailOpen 行为 |
| Deferred | 当前计划内无推迟的可执行功能；厂商全部版本和真实业务规则不在本轮范围 |

IMPLEMENTATION_STATUS=PASS；TEST_STATUS=PASS_WITH_RECHECK；PERFORMANCE_EVIDENCE_STATUS=MEASURED；本任务本机验收=PASS；生产 RELEASE_STATUS=BLOCKED（上表部署/阈值条件）。已记录吞吐、延迟、进程分配与资源快照；常驻 driver/JIT 缓存差值不直接判为管线泄漏。旧 Generation/分区资源路径未改变，沿用已有证据。

## 环境清理与 Git

本机 MySQL/Redis 测试环境见 [environment.json](evidence/containers/environment.json)；已核验任务标签后删除两个临时容器及其匿名卷，[cleanup.json](evidence/containers/cleanup.json) 证明无本任务容器残留。HA 脚本在 finally 清理自己创建的容器/网络。演示 Web 宿主已停止。

未自动 git commit、git push、创建 PR 或发布 NuGet；未执行生产数据库迁移。保留其他任务工作区修改与旧任务证据。当前源码/报告仍为待用户审阅的未提交工作区内容。

## 收口

No-Progress Check=CHANGED（已修复并取得新证据）；Next Action=STOP。本任务交付完成；生产门禁单列，不因这些门禁反复修改已通过的实现。执行 `task-finish --no-notify` 完成状态登记，不向外部发送消息。
