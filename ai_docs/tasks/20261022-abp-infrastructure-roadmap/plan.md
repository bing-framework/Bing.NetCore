# ABP 开源基础设施对标路线图

- Task ID：`20261022-abp-infrastructure-roadmap`
- 版本日期：2026-10-22。
- 状态：用户已批准方向；本文件冻结分阶段边界。阶段 A 可按独立计划实施；B/C/D 开始前各自形成可执行子计划，不能直接把路线图当作源码改造清单。
- 阶段 A：[20261022-abp-phase-a-tracing-contracts/plan.md](../20261022-abp-phase-a-tracing-contracts/plan.md)。
- 执行约束：仓库 `AGENTS.md`、用户级规则与 `C:/Users/jianx/.codex/GOAL_RULES.md`。文本显式 UTF-8。保留当前大量未提交修改，禁止 add/commit/push/reset/restore/clean，禁止外部通知。禁止变更 SDK、依赖版本、TFM、API baseline 或性能预算。

## 1. 目标与不变量

在现有 Bing 框架上补齐服务方法与业务操作的基础设施契约：显式启用、默认兼容、真实调用链可验证、职责可组合。ABP 是开源能力与生命周期设计参考，不引入 ABP 包、不替换 AspectCore、不复制商业模块。

固定不变量：

1. 旧注册和旧默认行为继续可用，新自动能力必须 opt-in。
2. Scoped 工厂创建独立 Query；同一个 root 可共享冻结数据源对应的连接/本地事务，Query 不共享可变查询状态。
3. 普通嵌套参与当前 root，不释放 root 资源；独立操作建立新 DI scope 并重新解析服务与 EF 上下文，不能复用外层 EF 实例。
4. ORM 保存与物理事务提交职责分离；保存、提交、完成通知与释放分别验收，物理提交恰好一次。
5. 多数据源为有序的独立本地事务，允许先前数据源已提交而后续失败。不承诺分布式原子提交、不实现补偿事务。
6. 权限或输入验证拒绝时业务写入为 0；租户边界覆盖路由、查询、写入，不以环境上下文存在代替隔离证明。
7. 保留 source-backed ValueTask 单次消费、取消令牌与原始异常、嵌套代理、完成回调已有验收行为。
8. 每阶段独立设计、实施、验收；未实现、跳过、环境未验证、接受限制分别记录。

## 2. 现状与证据基线

### 2.1 已验收能力

- 多源 Uow 已有资源共享、参与式嵌套、requiresNew、rollback-only、取消清理、释放和完成回调；`framework/src/Bing.Uow/Bing/Uow/IUnitOfWorkScope.cs`、`IUnitOfWorkScopeManager.cs` 与 `framework/tests/Bing.Uow.Tests/UnitOfWorkScopeTest.cs` 提供真实契约与直接测试。
- Dapper 与显式映射 EF 可共享本地事务，Query 保持独立；多源 CAP 的业务与 Outbox 使用本库事务。已有 HTTP、BackgroundService、Hangfire Sample，真实双 MySQL / RabbitMQ 验收，不等同异构数据库全面验收。
- 权威专项状态：[多源 CAP delivery-status](../20261021-multisource-cap-diagnostics/delivery-status.md)、该任务 `review.md`、`execution.md` 与 `traceability.md`。保留其 Dashboard 未配置、历史 evidence-only SQLite 探针未执行、Hangfire 内存存储等边界。
- 最新默认全量：12,561 Passed / 0 Failed / 1,105 NotExecuted；net6.0 6,515/0/588，net8.0 6,046/0/517。来源为 [full-suite final-report](../20261022-full-suite-recovery/final-report.md)、`execution.md` 及 `artifacts/final-candidate-cache-retry-release-l4-20261022T101104Z/` 中 103 份 TRX 和清单。旧 `review.md` 的全量状态早于最后检查点，不覆盖最新结果。此处复用已核对事实，不重新审查原 181 项。
- 正式 clean-source Release evidence 门禁仍 BLOCKED；本地全量通过不等于该门禁完成。不要清理用户工作区以满足门禁。

### 2.2 差距登记

状态用于规划而非宣称新增实现完成。G02–G11 的详细符号范围应在各子计划中按实际行为收敛；已有类型或测试数不能作为能力完成依据。

| Gap | 优先级 | 当前依据与状态 | 目标与阶段 |
|---|---|---|---|
| G01 跟踪拦截 | P1 | 已确认缺陷：CapTrace 的 FromCap 参数为 null/类型不匹配时不调用 next；有效 trace 修改可变外层对象。Admin GetInstance 同类写法 | A：目标恰好一次，独立上下文与恢复 |
| G02 自动 Uow | P1 | 旧 EnableBingUnitOfWork 是成功后提交/回调；已有手工 scope，不等于自动拥有者生命周期 | B：opt-in Begin/完成/释放、嵌套参与 |
| G03 资源与保存 | P1 | Dapper/显式 EF 共享本地资源已实现；通用自动保存/提交分离仍需契约设计 | B：资源适配、通知、事务策略/超时，Dapper+EF 后 FreeSQL |
| G04 验证 | P1 | 有基础验证及可选参数验证；DomainObjectBase.UseValidationRules 真实抛 NotImplementedException | C：DTO 服务方法验证与领域全局规则分开建设 |
| G05 授权 | P1 | 已有权限/HTTP 基础设施，三入口统一服务方法授权未作为闭环验收 | C：权限定义、取值/缓存和统一拒绝行为 |
| G06 审计 | P2 | 已有审计组件与 HTTP 场景，不等于服务方法耗时/异常/参数闭环 | C：外层结果审计、脱敏、与 HTTP scope 合并 |
| G07 租户 | P1 | 已有租户上下文与部分场景，不等于全面数据隔离 | C：租户→数据源、查询/写入边界，EF+Dapper 后 FreeSQL |
| G08 事件 | P2 | 多事件入口、CAP Outbox/去重与事务阶段已有基础 | D：整合入口与诊断、operation/tenant/trace 传播 |
| G09 后台执行 | P2 | HTTP/Worker/Hangfire Sample 已有；公共业务执行模型待冻结 | B：统一 scope/Uow/取消/上下文恢复，适配现有调度器 |
| G10 设置 | P2 | 层级设置与缓存失效统一能力待子计划确认 | D：默认/全局/租户/用户，敏感值处理 |
| G11 功能开关 | P3 | 独立功能定义、租户值、检查能力待子计划确认 | D：功能检查与权限分离 |
| G12 装配文档 | P1 | 已确认 Uow README 把 Begin 写在没有此方法的旧 manager 上 | A 起贯穿各阶段：可编译示例、支持矩阵、迁移 |

整体完成度：现有基础设施已具备真实业务与集成证据，但新增路线图任务尚未实施。不同 gap 的粒度不同，不以“12 项占比”或测试总数推断能力百分比。

### 2.3 参考依据

2026-10-22 查阅 [ABP Uow 文档](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) 与 [ABP 10.6.1 UnitOfWorkInterceptor](https://github.com/abpframework/abp/blob/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkInterceptor.cs)。参考其调用范围、保存/完成阶段和选项职责；Bing 的现有接口、显式开关及本地多事务边界优先，不照搬 ABP 默认约定。后续阶段对其他能力另取固定版本官方源码，避免凭类名推断完成度。

## 3. Phase A：G01/G12 跟踪与契约文档

| Task | 目标、现状与修改范围 | 实施步骤、依赖 | 验证、风险与验收 |
|---|---|---|---|
| A01 | 冻结局部 before 与测试入口；现有工作区含既有修改 | 按独立 A 计划保存相关源码副本、命令/候选身份；先写失败回归 | 红证据必须是行为失败，不是编译/环境失败；禁止复位工作区 |
| A02 | 修复 CapTraceAttribute 的目标遗漏与外层污染 | 在真实代理测试下修复；保存引用→必要时新上下文→next 一次→finally 恢复 | sync/Task/ValueTask、异常/取消、嵌套/并发；公开 API 不变 |
| A03 | 核查并修复真实 Admin CAP 同类问题 | 用 CAP 8.0.1 ISubscribeInvoker 实际调用，红回归后最小替换上下文 | 目标内部 trace 可见、父对象不变、异常取消/连续并发恢复；不改消费控制策略 |
| A04 | 修正 Uow 契约与支持矩阵 | 旧 Commit 与新 Begin/Complete 分开示例，编译同源示例 | 文档与程序集契约一致，未实现能力显式标明 |
| A05 | 有界开销观察与阶段验收 | 仅 trace 热路径探针；L1→受影响 L2/L3→阶段 L4 | 原始证据、符号测试映射、独立 Review；不发明预算 |

详细文件、测试方法、命令、时间窗口、风险与停止条件以 [阶段 A 计划](../20261022-abp-phase-a-tracing-contracts/plan.md) 为准。A 不实施自动 Uow、授权、验证或跨 ORM 重构。

## 4. Phase B：G02/G03/G09 操作与事务生命周期

前置：A 验收；先新建 `20261022-abp-phase-b-operation-uow/plan.md` 冻结公共契约、所有权表、状态转换及回调顺序。该 ID 为后续建议，不由本次 Planner 创建。

| Task | 目标/现状 | 修改范围与步骤 | 依赖 | 验证/验收与风险 |
|---|---|---|---|---|
| B01 | 自动 Uow 拥有者；旧拦截只成功后提交 | 候选 Bing.Ddd.Application.AspectCore、Bing.Uow；定义显式启用方式，复用原标记，区分 owner/participant；先合同测试再实现 | A、B 契约冻结 | 旧注册行为不变；自动/手工/嵌套/异常取消均一次目标；参与者不提前提交释放。风险：旧 ValueTask 链回归 |
| B02 | 保存与物理提交分离；已有资源 root | 候选 Bing.Uow、EntityFrameworkCore、Dapper.Core；定义保存阶段、root 提交/通知、超时/事务策略及失败传播；独立 operation 建立 DI scope 重新解析 | B01 | Dapper+EF 同库一次物理提交；requiresNew 不复用外层 EF；rollback-only、部分提交、通知失败仍清理；SQLite 及受影响真实 provider。风险：双重提交、连接所有权 |
| B03 | 业务执行器统一 HTTP/Worker/Hangfire | 候选应用层/宿主适配、现有 Sample；冻结一个操作执行模型，传递取消与上下文并恢复，Hangfire 仅适配 | B01/B02 | 三入口成功/失败/取消一致，独立 DI scope 的服务真实重新解析，未实现调度器；风险：后台作用域泄漏 |
| B04 | FreeSQL 资源独立适配 | 候选 Bing.FreeSQL 与 Admin.FreeSQL；单独子计划与适配测试，复用冻结资源协议 | B02 Dapper+EF 验收 | FreeSQL 保存/提交/回滚/释放一次，不能以 EF 通过代替；风险：ORM 生命周期差异 |
| B05 | G12 迁移与能力矩阵 | 现有 Uow/ORM/应用层 README、Sample；补 opt-in 前后示例及 owner/participant/独立操作规则 | B01–B04 按已完成范围 | 所有文档主路径可编译；后续未完成适配标明，不宣称全 Provider 支持 |

新 API 的命名、参数、返回类型、默认选项与可见性仅在 B 子计划冻结。不得为了追求“统一”废弃旧 manager 或扩大共享 Query。超时值及资源限制遵循既有批准边界，不自行制定性能容量预算。

## 5. Phase C：G04/G05/G06/G07 业务服务横切契约

每个子任务先单独 plan。目标组合顺序：审计观察最外层结果；授权与输入验证先于任何业务写入；Uow 管理执行、保存、提交与释放。拒绝路径可被审计但写入计数为 0。HTTP、直接 DI、后台三入口均走服务方法契约，不能只依赖 MVC Filter。

| Task | 目标/现状 | 修改范围与步骤 | 依赖 | 验证/验收与风险 |
|---|---|---|---|---|
| C01 | DTO 服务方法验证 | 候选 Validation、Ddd.Application.AspectCore；复用验证组件，定义递归/空值/取消与显式启用，三入口对照 | B 操作协议 | 直接 DI/HTTP/后台错误一致；拒绝时目标与写入 0。风险：重复验证/破坏旧默认 |
| C02 | 领域全局规则；UseValidationRules 未实现 | 独立 `domain-validation-rules` 计划，候选 Ddd.Domain/Validation；冻结注册、泛型/继承、执行顺序、生命周期与重复注册规则，再实现 | C01 可独立，需共享验证契约 | 规则组合、生命周期、异常、继承/排序直接测试，不能以 DTO 验证替代领域规则 |
| C03 | 服务授权与权限值缓存 | 候选 Authorization.Abstractions/Permissions/应用层；统一权限定义、值来源、拒绝、缓存键与失效 | B，C 组合协议 | 三入口拒绝写入 0；用户/租户隔离、命中/未命中/失效；风险：后台绕过/跨租户缓存 |
| C04 | 服务方法审计 | 候选 Auditing、应用层、AspNetCore；记录耗时/异常/脱敏参数，合并 HTTP 已有 scope，统一后台上下文 | B、C01/C03 顺序 | 成功/拒绝/异常/取消审计一次，秘密不输出，审计故障策略明确；风险：重复记录与泄密 |
| C05 | 租户的数据访问边界 | 独立 `tenant-data-boundaries` 计划；候选 MultiTenancy、EF、Dapper/Sql、工厂；明确 host/admin 权限与切换恢复，先 EF+Dapper，FreeSQL 后续子任务 | B，C03 | 两租户真正并发、数据源路由、查询/写入拦截、独立数据库连接结果；缺 tenant、host/admin、非法切换均明确。风险：仅加过滤器而漏写入或连接复用 |
| C06 | G12 组合文档与能力矩阵 | 各包 README/迁移说明更新支持入口与开关 | 各子任务验收 | 每能力对应符号和直接测试，不把未验证三入口写为支持 |

SQL/Provider 路径变化必须服从根 AGENTS：默认实现独立测试类，SQL 完整字符串断言，映射缓存隔离/命中/未命中，Provider 成功失败分支，SQLite 本地集成，必要热路径 benchmark 说明。租户缓存和执行上下文优先级作为直接契约测试。

## 6. Phase D：G08/G10/G11 事件整合、设置与功能

先事件入口整合与诊断，再设置与功能。继续按子计划执行，不从此路线图推导未批准的新公开接口。

| Task | 目标/现状 | 修改范围与步骤 | 依赖 | 验证/验收与风险 |
|---|---|---|---|---|
| D01 | 事件入口、事务阶段与上下文传播 | 候选 Events/EventBus/适配器、Uow；盘点现有入口并做内部整合，保留旧接口，定义即时/提交后、operation/tenant/trace | B/C 已冻结上下文与事务协议 | 事务前后顺序、回滚不发提交后事件、失败通知、恢复、去重；复用 CAP Outbox 存储与消费去重，不复制协议 |
| D02 | 事件诊断闭环 | 复用既有诊断与 Sample，补跨入口关联和失败定位 | D01 | 真实事务消费链改变时 MySQL/RabbitMQ；诊断不泄密、不自建重试调度。风险：重复投递/重复存储 |
| D03 | 层级设置 | 独立 `hierarchical-settings` 计划；默认→全局→租户→用户优先级、读取、覆盖、缓存失效与敏感值 | C 租户/用户边界、D02 后启动 | 各级回退、并发失效、用户租户隔离、敏感值不可见；风险：缓存维度不足 |
| D04 | 功能定义与检查 | 独立 `tenant-features` 计划；功能定义/租户值/检查，权限保持独立 | C03、D03 取值模式 | 无功能/无权限分别拒绝，值缓存隔离和失效；功能启用不授予权限 |
| D05 | G12 支持矩阵与迁移 | 各接口、新旧入口/配置可编译示例 | D01–D04 | 兼容层有必要性记录，无无关 cleanup，不伪称与 ABP 完全一致 |

## 7. 跨阶段验收与证据协议

1. 每阶段计划列“已确认文件”和“候选文件”；未确认的边界先最小复现，不做全框架扫描。每任务提供目标、现状、步骤、依赖、验证、风险、验收。
2. 测试前输出 Change Impact Analysis：ChangedFiles/Projects/PublicContracts/RuntimePaths/Providers/TFMs/BuildPackaging/BenchmarkHarness/DocsOnly/AffectedDependents/RiskLevel。
3. 先失败回归和 L1，再受影响 L2/L3。每阶段 final candidate 执行 Release solution build 与默认 L4，覆盖既有 net6.0/net8.0；Admin 项目保持现有 net6.0，不增加 TFM。方案文件为 `Bing.All.slnx`。
4. 事务或消费控制链、Provider/IO 改变时才重跑对应真实 MySQL/RabbitMQ；SQL 执行路径改变必须 SQLite。其余外部证据仅在 relevant source 未变时标记 `REUSED_UNCHANGED_SCOPE`，不计作当前全量新增通过数。
5. 有界开销 probe 仅针对拦截/scope 热路径实际变化；先声明工作负载、短诊断与正式观察窗口、终止方式。没有批准预算不宣称性能门禁 PASS，不扩展为完整资源矩阵。
6. 每子任务交付 `execution.md`、迁移/支持说明、`traceability.md`（最终生产符号→行为→测试项目/完整方法名→TFM→结果→原始 artifact）、原始 TRX/stdout/stderr 与候选清单。独立 Reviewer 维护 `review.md`。
7. 测试/实现/性能证据/外部 gate/Release 分列。跳过、无环境、N/A、接受限制不得合并为 PASS。历史报告不覆盖较晚原始证据。
8. 独立 Review→Fix→Review 自动最多 3 轮；只有 OPEN_ACTIONABLE>0 继续。连续两轮根因、验收、候选和证据无变化触发 No Progress Guard；同类实验最多诊断一次+正式一次。不因 PARTIAL/NOT_VERIFIED/BLOCKED/TIMEOUT/RELEASE_STATUS!=PASS 重试。
9. 每轮输出 Completed/Open Actionable/Blocked Approval/Blocked External/Not Applicable/Accepted Limitations/Verified Boundaries/Deferred，并给实现项数量、Release gate 数量、下一动作或 STOP。

## 8. 不在本路线图当前实施范围

- 引入 ABP NuGet、替换 AspectCore、商业支付/身份/租户管理产品模块。
- 跨库原子事务、分布式补偿、复制 CAP Outbox 存储协议、自建作业调度器。
- SDK、依赖、TFM 升级，自动更新 API baseline，放宽性能/容量预算。
- 无证据的通用重构、历史 181 项重审、为了“全绿”修改门控或清理工作区。
- VFS、BLOB、Data Seed 等为 DEFERRED 候选，须未来独立需求与计划；不影响阶段 A 收口。

## 9. 计划交付状态

本次仅创建总计划与阶段 A 计划；业务源码、测试与配置未实施。当前可执行下一步：按阶段 A 先建立失败回归，再最小修复。B/C/D 为后续独立设计项，不在阶段 A 自动展开。
