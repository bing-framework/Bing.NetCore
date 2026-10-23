# F008：SQL 工厂改为 Scoped

Task：`20261021-f008-sql-factory-scope`。状态：**用户已批准并实施完成；已通过 Sol/high 第二轮独立验收，F008 CLOSED（计划 7/7，FIX-001/002/003 CLOSED，OPEN_ACTIONABLE=0）**。独立结论见 [review.md](review.md) 与 [final-report.md](final-report.md)。用户已明确批准四类核心 SQL 工厂及 EF Core SQL 查询适配工厂默认改为 Scoped，并要求 `PLEASE IMPLEMENT THIS PLAN`。本文件记录已批准范围，不构成新的审批门。

## 目标

修复 SQL 工厂捕获根 `IServiceProvider` 导致的两类真实作用域错误：`ValidateScopes=true` 时 Query Builder 首次解析 Scoped `IDataFilter` 会从 root provider 解析而抛异常；`ValidateScopes=false` 时请求 scope 的禁用过滤状态不会反映到 Query 渲染。实际接口名以源码为准，范围是 `ISqlQueryFactory`、`ISqlExecutorFactory`、`ISqlMultipleQueryExecutorFactory`、`ISqlTransactionScopeFactory` 和 `IEfCoreSqlQueryFactory`。`IDataFilter` 继续 Scoped；无状态元数据、注册表、Builder factory 和映射缓存继续 Singleton。

## 批准的实现契约

- 核心变更集中在 `framework/src/Bing.Dapper.Core/Bing/Data/Sql/DapperServiceCollectionExtensions.cs` 与 `framework/src/Bing.EntityFrameworkCore/Extensions/EfCoreSqlQueryServiceCollectionExtensions.cs`。核心四类工厂和 EF 适配工厂默认注册为 Scoped。
- 保留 `TryAdd*` 语义：调用者预注册的实现类型、实例、委托不覆盖；后注册遵循 Microsoft DI 的既有解析规则；重复 `AddSqlCore`、多 Provider 与 Provider 注册顺序保持幂等。不得强制替换用户自定义 Singleton。
- Factory 在被解析时绑定解析它的 DI scope。将 A scope 的工厂带到 B scope 调用时仍使用 A 的服务作用域，不做运行时重绑定。Query/Executor/Transaction Create 或 Begin 时确定并冻结数据库上下文；优先级保持 `SqlOptions` context → `IDatabaseContextAccessor.Current` → 元数据默认值，显式 `dbKey` 优先级、MappingProfile、租户和其他已有上下文组合不变，事务仍遵循主库语义。过滤器开关在 SQL Builder 渲染时动态读取。
- `SqlFactoryBase` 捕获作用域 `IServiceProvider`，用 `ActivatorUtilities` 创建 Provider runtime 并显式传入该 provider。`SqlQueryBase` 延迟创建 Builder services 时解析 `IDataFilter`；只创建 Query 不足以证明修复。Executor 实体 Mutation 路径也需验证。
- Multiple 工厂仅提供原有 batch/reader 能力，不新增任意原始 SQL 的自动过滤。事务 Begin/BeginAsync 执行真实开连接；事务 Query/Executor 保持同一 scope 与事务上下文。
- Clone 继续复制参数和子句，沿用共享 Services、所属 scope 的 `IDataFilter` 及已有 `_executor` 契约。不创建新 scope/连接，不宣称共享执行器可并发安全。
- 工厂直接创建的对象不由 DI 自动追踪释放。调用方负责 `using`/`await using`，且 scope 覆盖执行、枚举和释放全过程。区分直接 DI 解析 transient 对象与 Factory 产物的所有权；外部资源不得误释放，事务、reader 与租约必须正确释放。不扩展为 EF `StoreBase` 的资源所有权重构。
- 将迁移相关 fixture 和 SQLite E2E benchmark 的 root 工厂解析改为显式 scope，并调整必要消费者。已调查 Admin 仓储/查询服务为 Scoped，后台库存执行器和 Hangfire 已有 scope 机制；以测试与装配事实验证，不批量修改构造签名。Singleton/后台迁移说明要求每个业务操作通过 `IServiceScopeFactory` 创建 scope，禁止跨 scope 缓存工厂或 Query。自定义 Singleton 仍由注册者承担契约，不宣称所有错误 root consumer 会自动修复。

## 范围外

不实现退款、全局验证规则、OAuth 或租户文化；不扩展 Provider 支持；不改变公共方法或 Provider SPI 签名、包版本、API baseline、性能预算；不重构过滤器、SQL 格式化、缓存键或 Clone 算法；不修改旧任务 `ai_docs/tasks/20261021-merged-code-audit/` 的历史报告/日志；不运行外部生产库或真实支付。禁止 `git add`、commit、push、tag、reset、clean、PR。

## 执行步骤

### 1. 基线和失败回归先行

- 记录 HEAD、`git status --short`、起始 diff/stat、未跟踪文件；区分旧合并审查整体修复和本轮 F008。本轮开始时记录为 HEAD `5065c38ced05a95088e17d89d96c9a1610e37159`，工作区干净，无起始 diff 和未跟踪文件。
- 将旧任务 `artifacts/sql-scope-probe.log`、`artifacts/sql-scope-probe.json`、`artifacts/scope-probe/Program.cs` 原始证据复制至本任务 `artifacts/`，不修改旧文件。
- 将历史两个 scope 场景转成正式回归测试：启用 `ValidateScopes=true` 时 Query 首次 typed Builder 渲染可在子 scope 使用 Scoped `IDataFilter`；`ValidateScopes=false` 时子 scope Disable 可改变完整 SQL，恢复后完整 SQL 恢复。先运行并保存改生命周期前失败的 TRX/原始日志，确认两个缺陷仍可复现。

### 2. 注册与默认创建链

- 将五类默认工厂改为 Scoped，保留所有其他生命周期。
- 重写原 `KeepSingletonRegistrationsIdempotent` 测试为准确生命周期契约：五类工厂生命周期、注册数量、同 scope `Same`、不同 scope `NotSame`，无状态元数据跨 scope `Same`。
- 直接覆盖预注册 implementation type、委托、instance 保留；后注册覆盖/解析行为；重复 `AddSqlCore`、Provider 次序、多 Provider 幂等。
- 覆盖 `SqlFactoryBase` 使用解析 factory 的 scope 创建 Provider runtime；覆盖 SqlQuery、SqlExecutor、MultipleQueryExecutor 三个直接 DI transient runtime 入口；验证 `SqlOptions`/ambient/metadata context 优先级和 Create/Begin 后上下文快照固定。

### 3. 过滤、渲染与事务行为

- 在 `ValidateScopes=true/false` 下验证 Enable/Disable/Restore 的完整 SQL、嵌套 Disable→Enable、异常恢复、跨 await、多个独立 scope 并发（每分支单独 Query）、A factory 不重绑定 B。
- 覆盖 Query 创建前后和 Builder 创建前后切换过滤器，确保渲染时动态读状态；覆盖 Mutation 实体写入路径。
- 验证缓存首次未命中/再次命中、Singleton 映射缓存跨 scope 复用，以及有意义的租户/Profile/数据源隔离；命中后切换 filter SQL 正确；Enabled→Disabled→Enabled 多次渲染不重复谓词或参数。
- 验证 Clone 参数/子句独立而共享 scope filter 与既有 executor 契约；不改算法、不扩展线程安全保证。
- 覆盖四工厂无参和具名 Create、事务 Begin/BeginAsync、事务子 Query/Executor 的固定上下文、所有权与异常资源释放。Multiple 保持原始 batch 语义，不给任意 SQL 添加过滤。

### 4. Provider 直接契约

- MySQL、PostgreSQL、SQL Server、Oracle、SQLite 各自建立/扩展职责测试，覆盖成功与明确失败。所有 SQL 使用独立字面量 `Assert.Equal` 断言完整字符串，并断言参数集合；不能使用 `Contains` 或从被测 formatter 计算 expected。
- Oracle 没有 Multiple runtime 时保留并断言明确失败；Doris 复用 MySQL 分支但保持只读、无事务；自定义 Provider SPI 不变。若失败在 SQL 生成前，断言异常与零连接打开。
- 对接口契约、默认实现和受影响 Provider 分支直接覆盖。若本次没有改 EntityMappingResolver/metadata provider/表命名/格式化/跨库校验，不为这些不相关职责制造算法或测试改动。

### 5. SQLite 与消费者验收

- 运行本地 SQLite Dapper 集成 net6/net8 全套。覆盖正常与软删除数据：A 默认一行、禁用过滤两行、恢复一行；B scope 不受影响；Count/Page、Clone、跨 await、事务子 Query/Executor、实体写入受影响行、提交/回滚/异常、连接释放。原始 SQL 不自动增加过滤。
- 运行 EF Core 实际支持的 net6 测试，覆盖 Shared/Independent connection ownership 与 scope filter；运行 FreeSQL/Admin 必要装配和 scoped consumer 测试。
- 增加 Singleton 直接注入 scoped factory 的负例与每业务显式 scope 的正例；核对后台库存和 Hangfire 既有 scope。不批量变更已 Scoped 消费者的 constructor。
- 覆盖事务/连接 Owned vs External、失败/取消/提前终止流、reader 和 execution lease 清理。复用既有测试证据时，在 traceability 中准确标明 scope 和有效性。
- 将迁移相关 fixture 和 `SqliteDapperE2EBenchmarks` 的根工厂解析调整为显式 scope；运行修改后对应的 E2E smoke。纯 Builder/映射热路径和 harness 未变时复用精确历史证据并写明原因。

### 6. 有界性能 probe

- 在注册改动后运行无数据库生命周期 probe：net8 Release、`ValidateScopes=true`。场景为“创建 scope/解析四工厂/首次 typed Query 渲染/释放”和“同 scope 多次 Create”。每场景预热 100 次，正式 3 组、每组 1000 次，整个运行窗口 60 秒。
- 记录实际时间、分配、完成次数、异常数、连接打开数和退出码；不得自设性能通过阈值，不宣称稳态 P95 或性能提升。超时如实记载，不仅增加 timeout 复跑。
- 不改 `Bing.Data.Sql.Benchmarks` baseline/预算。由于本次不改映射缓存键、格式化热路径或 Builder clone 算法，说明这些基线场景不适用；工厂 scope probe 与 benchmark smoke 是独立证据。

### 7. 迁移说明与收口

- 在 `migration.md` 说明 Scoped 默认生命周期、Singleton 自定义工厂的责任、Singleton/后台任务通过 `IServiceScopeFactory` 每个业务操作建 scope、Factory/Query/Executor 释放边界、跨 scope 不缓存的行为及事务/reader 的使用期限。
- 维护最终生产符号 → 实际测试项目/类/方法 → 原始证据路径追溯表。每个 finding 使用 `GOAL_RULES.md` 状态；仅 `OPEN_ACTIONABLE > 0` 推动修复，最多 3 轮并遵守 No Progress / 同类实验次数限制。
- 执行记录须报告每个受影响项目与 TFM 的通过/失败/跳过/未运行数量、命令、TRX/日志、性能实测及限制。外部 CI、真实数据库、CodeSmith、正式性能资源独立标为 `BLOCKED_EXTERNAL`/未运行，不能伪造完成，也不得阻挡本地可完成项。
- 本任务完成只表示实施与本地所需验证完成；不修改旧 review 中 F008 总体状态。交父会话安排 GPT-6.1-Sol/high 独立验收，通过后由父会话更新总体遗留状态。

## 验收条件

1. 改生命周期前的两个正式 scope 回归测试均失败且保留证据，改后通过。
2. 五类默认工厂 Scoped，同 scope 同实例、异 scope 不同实例；无状态元数据仍 Singleton；所有 `TryAdd` 自定义注册/重复注册契约通过。
3. Query 创建链、Executor Mutation、过滤器动态渲染、数据库上下文快照、Clone、Provider 直接契约和资源所有权按本计划覆盖。
4. SQLite 集成 net6/net8、EF Core net6、必要 FreeSQL/Admin consumer/build、修改后的 SQLite E2E smoke 取得如实记录的结果；未执行项明确分类。
5. 完成规定的 60 秒内有界生命周期 probe（或记录受控失败/边界），不修改性能预算或 baseline。
6. `plan.md`、`execution.md`、`migration.md` 与原始 artifacts 齐备；代码 diff 无无关算法或 API 变更。当前执行结束报告为“实施完成，待独立验收”，不宣称总体 F008 已关闭。
