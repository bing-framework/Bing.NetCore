<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261024-file-storage-design
AI_REVIEWED_AT: 2026-10-25T08:20:07.407Z

# 文件存储与附件管理整体复核（Round 6）

## 结论

当前已经形成可接入应用的文件管理框架：**10 个模块和 9 类增强均有真实实现及调用入口**。本轮对照计划、当前源码和验证结果，**未确认新的 MUST_FIX / SHOULD_FIX，FIX-010 已关闭**。结论为 PASS_WITH_ISSUES，保留接入、可选增强及环境验收事项。

框架完成情况与实际业务上线准备分开计算。两套 Admin 在默认配置下尚未完成具体业务授权、消费者和运维部署；剩余为 **3 类接入工作、3 类可选增强、3 组环境验收**，不使用主观完成百分比。

IMPLEMENTATION_STATUS: PASS（本轮检查范围内 OPEN_ACTIONABLE = 0）
TEST_STATUS: EVIDENCE_VERIFIED（增强基线 398 项、最近修复 94 项及 2 次 CLI 冒烟；本轮核验原始证据，未重新运行）
PERFORMANCE_EVIDENCE_STATUS: 无新增吞吐或元数据规模结论
RESOURCE_EVIDENCE_STATUS: 历史 Local/MinIO 1 GiB、100 GiB Provider 证据仅在原范围复用
EXTERNAL_GATE_STATUS: 生产部署、监控、多实例与端到端大文件验收尚需实际环境
RELEASE_STATUS: 未进行生产上线验收
GOAL_STATUS: STOP（本轮只审查，不自动修复或扩展范围）

## 基础能力核对

| 能力 | 当前实际行为 | 结论 / 边界 |
|---|---|---|
| 模块和依赖 | Abstractions、Core、FileSystem、S3、Minio、Storage.AspNetCore、业务核心、两 ORM、业务 Web 共 10 个工程；Abstractions 经 framework.props 保持 netstandard2.0，Provider/业务支持 net6 | PASS；MinIO 通过 S3 实现，不宣称其他云已验证 |
| 可替换存储 | 命名实例、显式范围与持久位置；旧文件继续按记录位置读取，默认存储只影响新上传 | PASS |
| 基础 IO / HTTP | 流式存取、长度检查、幂等删除、属性与签名；单区间 Range、HEAD、中文附件名和 nosniff | PASS；不支持多区间响应 |
| Local 续传 | 持久清单、独立分片代文件、冻结完成、同卷提交、文件锁和恢复清理 | PASS；单节点持久目录，部署需保护 ACL，合并需额外磁盘 |
| MinIO 直传 | 服务端登记摘要、限定会话/分片签名、分页查片和服务端完成；丢失响应按会话令牌及对象属性对账 | PASS；已签请求到期前不可由应用即时撤回 |
| 附件与权限 | GUID、会话/关联/操作持久化；用户/租户校验、Ready 限制、私有/公开访问，有关联拒绝删除 | PASS；具体业务授权由宿主实现，默认拒绝 |
| 持久化与恢复 | 两 ORM 短事务/CAS/任务租约、长短任务槽和维护循环；内容 IO 不跨数据库事务 | PASS；既有竞争测试不等于多节点 SLA |
| 导入与迁移 | 明确旧数据归属/路径、预览/执行/稳定 GUID；独立目标复制、长度和 SHA-256、版本发布和延迟清理 | PASS；不自动改业务库或迁移真实旧数据 |
| 集成与交付 | 两 Admin 统一控制器、CLI、浏览器上传 Demo、升级脚本、部署/故障说明和测试映射 | PASS（框架接入）；完整后台管理前端不在原范围 |

已追踪主要调用链：FileStorageFactory → Provider；Controller → FileManagementService → Store；Scheduler → Processor → Provider/Migration。核心范围未发现 TODO / NotImplemented 占位。流包装器对 Seek/Write 抛 NotSupportedException 是只读非 Seek 契约，不是缺失功能。

## 九类增强验收

| 阶段 | 实际落点 | 结论 |
|---|---|---|
| ENH-01 契约/基线 | 增量模型、仓储、状态数值保持及升级资料 | PASS |
| ENH-02 业务/会话/搜索 | by-business、bindings、会话摘要、SQL 筛选及绑定用户/范围/条件的加密游标；保留旧列表数组契约 | PASS；规模验收另计 |
| ENH-03 任务管理 | 受限查询/脱敏详情/审计、版本和幂等恢复、唯一后继，API/CLI 入口 | PASS |
| ENH-04 批量迁移 | 持久发现/条目、安排/对账、暂停/恢复/重试、真实复制和进度 | PASS；FIX-010 已闭环 |
| ENH-05 资源准入 | 同事务预留，Ready/确认清理释放；共享 IO 槽与续租令牌 | PASS；是活跃上传额度，非已存空间总配额 |
| ENH-06 生命周期 | Ready/Deleted 同事务 Outbox、租约分发/退避、Demo 幂等消费 | PASS；实际业务消费者待接 |
| ENH-07 内容检查 | 策略快照、声明/头部检查、隔离及真实 ClamAV INSTREAM | PASS（已声明大小边界） |
| ENH-08 健康监控 | DB/存储属性/维护新鲜度探针、受限诊断、Meter 及采集/告警示例 | PASS（模块）；Admin 运维接入待完成 |
| ENH-09 集成证据 | 两 Admin、两 ORM、CLI/Demo、升级脚本和真实外部组件记录 | PASS（现有验收范围） |

## FIX-010 关闭记录

- 原要求：MEDIUM / SHOULD_FIX。状态：CLOSED。
- 原问题：暂停批次的迁移虽已执行，对账候选只选 Running，条目一直显示 Queued。
- 当前 [FileMigrationBatchService](../../../components/src/Bing.Biz.FileManagement/FileMigrationBatchService.cs:80) 先对账 Running/Paused，每轮最多 100 条，再单独安排 Running。完成判断只对 Running 生效，使用最新版本 CAS，不覆盖并发暂停。
- [EF 查询](../../../components/src/Bing.Biz.FileManagement.EntityFrameworkCore/EfFileManagementStore.Enhancements.cs:250) 和 [FreeSql 查询](../../../components/src/Bing.Biz.FileManagement.FreeSql/FreeSqlFileManagementStore.Enhancements.cs:255) 都在 SQL 限量前筛选 Queued、对应文件的终态操作及批次状态；空暂停批次和执行中操作不占满候选。
- 本轮重新生成 4 个生产文件相对修复前快照的 Diff，与上轮 production.diff 完全一致。原始 TRX 覆盖成功/永久失败、服务容器重建、并发暂停/恢复、不发现/入队新条目、真实内容、1005 条执行中记录、HTTP 查询及隔离。
- 两 ORM 的独立 CLI 原始结果均为 Paused、Queued=0、Succeeded=1，实际文件位于 local-v2。
- 证据：[修复结果](evidence/fix-paused-batches/final-validation.json)、[测试与兼容说明](evidence/fix-paused-batches/README.md)、[本轮核验](evidence/review-round-6/verification.json)。自定义仓储需补全 ListBatchItemsToReconcileAsync 并重新编译。

FIX-001～009 没有新的反证，保持 CLOSED。

## 三类接入工作

| ID / 优先级 | 当前情况 | 完成路径 |
|---|---|---|
| INT-01 / P1：领域授权与消费 | Admin 未注册具体 IFileAccessAuthorizer / IFileLifecycleEventHandler。默认授权拒绝，事件无人消费时保留；Demo 是示例实现 | 选择真实业务对象，实现 Read/Bind/Unbind 和租户所有权规则；消费者按事件 ID 幂等；用两用户/两租户完成附件闭环 |
| INT-02 / P1：启用与升级 | 模块默认关闭，需配置持久存储；schema 升级、权限资源初始化和角色授权均为显式部署步骤 | 明确目标库、配置和备份，执行对应升级；在受控入口初始化资源，再由管理员分配权限，验证普通用户及禁用角色 |
| INT-03 / P1：运维接入 | Admin 未映射健康 HTTP 路由，未接文件 Meter exporter；受限 diagnostics 已有，Demo 映射健康路由 | 映射受控就绪端点，接宿主 exporter/Collector/告警，实际演练数据库故障、探针缺失和 worker 停止 |

**纠正 Round 5 的数据库描述**：两 Admin 并不强制使用独立元数据库。FileManagement 连接串缺省时可以回退到 DefaultConnection。专用连接是可选部署方式；仍需明确目标库并执行 schema 部署。CLI 的配置要求不能直接套用到 Admin。

依据：[共用模块](../../../modules/admin/src/Bing.Admin/Modules/FileStorageModule.cs:25)、[EF 连接](../../../modules/admin/src/Bing.Admin/Modules/FileStorageModule.EntityFrameworkCore.cs:13)、[FreeSql 连接](../../../modules/admin/src/Bing.Admin.FreeSQL/Modules/FileStorageModule.FreeSql.cs:13)、[默认业务授权](../../../components/src/Bing.Biz.FileManagement/FileManagementOptions.cs:51)、[健康注册](../../../components/src/Bing.Biz.FileManagement.AspNetCore/FileManagementWebRegistration.cs:33)。

## 三类可选增强

以下为 DEFERRED / OPTIONAL，不计为原计划未实现或本轮必修缺陷。

1. **NEXT-01：专用管理界面。** 复用现有 API 做附件、上传、失败任务恢复、批次和审计页面。当前已有 API/CLI/上传 Demo，完整后台前端明确排除。
2. **NEXT-02：Outbox 运维。** 增加失败事件查询、审计重放及按消费者独立确认。当前所有处理器共享 Delivered，部分成功可能重投；批次和事件在增强循环中顺序执行，可按延迟要求拆开。
3. **NEXT-03：历史治理。** 对完成会话/分片、成功任务、已投递事件、审计及完成批次提供保留期、预览和归档。必须保留幂等映射、恢复链、仍有效任务引用，不能误删 Ready 内容。

## 三组环境验收与边界

1. **部署/恢复**：真实配置、schema 升级、权限、密钥持久化及备份恢复；Collector、面板、告警接真实指标流。
2. **多实例/规模**：多进程争抢、断网/强停、积压恢复、大量元数据下的查询/统计成本；现有测试不能直接变成集群 SLA。
3. **端到端大文件/客户端**：真实 Web、代理、浏览器链上的大文件续传和资源，补充支持的浏览器矩阵。原 Local/MinIO 各 100 GiB Provider 验证不等于整条业务链验证。

内容扫描默认 MaxStreamLength 为 25 MiB，配置上限为 int.MaxValue；扫描容器不可超过该上限。**100 GiB 文件上传支持不等于 100 GiB 病毒扫描支持**。需要后者时应另行实现能覆盖目标大小的引擎/协议，不能把分片分别扫描成功冒充完整文件扫描。

Local 单节点、MinIO 签名有效期、Outbox 至少一次、活跃上传额度和共享 IO 槽的准入语义继续作为声明边界。批次总 Bytes 不是已完成字节进度；当前也不承诺全局公平性、批次整体撤销或自动回滚。

## 证据与本轮范围

- 完整读取 plan/execution/review、适用规则，检查工作区及暂存区 Diff。HEAD 为 3919c1509907d661cfef3679dad79368c8b9cb39；暂存区为空。大量文件管理代码尚未跟踪，直接核对源码，未仅凭 Git Diff 验收。
- 主代理审查核心存储、权限、上传/完成/恢复、查询、迁移、任务/资源/事件/内容及 FIX-010；luna_worker 只读核对两 Admin、CLI、Demo 和运维/领域扩展点。
- 逐份解析增强基线 6 份 TRX：398 通过；最近修复 4 份 TRX：94 通过；均无失败/未执行。二者范围有重叠，**不能相加为 492 项当前全量测试**。本轮未重复运行这些测试或把历史结果标成新跑结果。
- 当前生产 Diff 与修复记录一致，核验两 CLI 结果及 10 个模块。记录见 [verification.json](evidence/review-round-6/verification.json)、[影响分析](evidence/review-round-6/impact.json)；上一轮审查归档见 [previous-review.md](evidence/review-round-6/previous-review.md)。
- 未发现需要新实验确认的可执行缺陷，按未变范围复用证据；未启动外部服务、重跑容量/全仓测试、访问业务库或迁移数据。
- 只更新 review 和审查证据；未修改 plan、execution、业务代码、测试、配置，未提交、推送或发送外部通知。

Completed：整体能力/入口核对、FIX-010 关闭、证据核验、数据库描述纠正。
Open Actionable：0（本轮确认的未解决必修缺陷）。
Blocked Approval：无本轮需要审批的动作。
Blocked External：实际部署、多节点、端到端验收。
Not Applicable：未变内容路径的容量/吞吐重测。
Accepted Limitations：上述 Local、直传、额度、扫描和事件边界。
Verified Boundaries：历史 Provider 100 GiB 正确性证据保留原适用范围。
Deferred：INT 接入按业务部署安排，NEXT-01～03 为可选后续。
No-Progress Check：CHANGED（确认 FIX-010 修复，纠正数据库要求）。
Next Action：STOP；下一阶段优先完成 INT-01/02/03 的实际接入和验收。
