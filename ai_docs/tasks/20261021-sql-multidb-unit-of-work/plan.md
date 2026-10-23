# SQL 多数据库业务工作单元实施计划

交付状态（2026-10-21）：实施与 Sol/high 独立验收 **PASS**，T01–T08 验收 8/8；最终结果见 [交付报告](final-report.md) 和 [独立审查](review.md)。下文保留规划阶段的原始状态与设计依据。

Task：`20261021-sql-multidb-unit-of-work`
计划日期：2026-10-21
状态：`PLAN_READY_REVISED`；用户已授权本功能必要增量接口及计划后的实现。本轮仅修订本计划，完成后返回父会话，由父会话安排 Luna/max 实施、Sol/high 独立验收；未实施新能力，也没有新能力通过证据。

## 1. 执行约束与依据

- 已读取根 `AGENTS.md`、`C:/Users/jianx/.codex/GOAL_RULES.md`、`.agents/skills/create-plan/SKILL.md`，以及 F008 的 [plan](../20261021-f008-sql-factory-scope/plan.md)、[execution](../20261021-f008-sql-factory-scope/execution.md)、[migration](../20261021-f008-sql-factory-scope/migration.md)。本阶段不发送外部通知：这是父代理为本委派设置的范围约束，不将它表述为用户的通用通知偏好；不运行通知脚本。
- 本次分析基于 HEAD `5065c38ced05a95088e17d89d96c9a1610e37159` **加当前未提交的 F008 工作区**。本地 master 为 `561336642ca7cc65a91eb6b7e54a785a07722626`，仅用 `git show/git grep` 阅读，不切换、不恢复 master 文件。
- 起始工作区已有两份 merged-code-audit 报告修改、F008 任务目录、Dapper/EF 注册、AssemblyInfo、Provider/Core/SQLite/EF 测试及 fixture/benchmark 修改；这些均不是本计划的产物。实施开始重新记录完整 `git status --short` 和相关 diff，保留全部既有修改，不覆盖旧任务证据。
- 所有文本读写使用显式 UTF-8；PowerShell 脚本设置 UTF-8 控制台编码。禁止自动 git add/commit/push/tag/reset/clean/PR，禁止改包版本、既有已批准 API baseline、性能预算。不要将当前工作区写回历史 baseline。
- 初版已生成，本轮只修订此文件，保留其余文件。未来实施交付另有 execution/review/migration/traceability，不提前伪造这些报告。

## 2. 目标、范围与完成度

同一次业务操作可以通过多个独立的注入 Query、仓储 Query、Executor 访问多个具名数据源，Provider 可不同。相同业务 Uow 内，同一冻结有效数据源、Provider、租户、连接配置与资源来源的默认 Dapper Query/Executor 共享连接和事务。**显式映射到 EF context 的源，还必须让现有 EF StoreBase.Shared Query 与应用层默认 Dapper 共享同一资源，应用层先用与仓储先用等价**。未配置 EF 桥接不能算通过该跨层验收。各库顺序提交，不提供跨库原子性；只读汇总可选择非事务 Uow。

保留 F008 的四类核心工厂和 EF 适配工厂 Scoped、`ISqlQuery/ISqlExecutor/ISqlMultipleQueryExecutor` Transient、`Create(dbKey)` 和创建时数据库上下文快照。`Query()/From()` 已创建独立查询描述，顺序查询无需反复创建根 Query。本期不增加 `ISqlQuery<TDatabase>`、跨库 JOIN、分布式事务、savepoint 嵌套事务或 ABP 依赖。

当前能力判断（基于调用链，而非 README 宣称）：

| 能力 | 当前状态 | 依据与缺口 |
| --- | --- | --- |
| 多数据源、多 Provider 路由 | 已有 | SqlDataSourceDescriptor/Resolver、SqlProviderRuntime 与工厂 Create(dbKey)；需新增业务汇总与 Uow 资源隔离证据 |
| 工厂 scope 与过滤隔离 | F008 已完成 | 工作区 Scoped 注册及 F008 独立审查结论；不能用 HEAD 单独代表现状 |
| 每个查询描述独立 Builder | 已有 | SqlQueryBase.Query/CreateIndependentSqlBuilder；From 创建独立描述；不应靠改 Query 为 Scoped 解决事务共享 |
| 同库跨层自动共享事务 | 未实现 | Query 缓存自身 connection/transaction；只有显式 transaction scope 子对象共享 |
| Current、参与式嵌套、requiresNew、资源登记 | 未实现 | 现有 Bing.Uow 管理器仅 HashSet 注册与逐个 Commit |
| 无 Uow 独立连接、外部连接借用 | 已有 | Owned/External 及 RuntimeBinding；必须保持回归 |
| EF Shared 与默认 Dapper 跨层共享 | 未实现、本期必须实现显式桥接 | 由数据源到 EF context 的明确映射选唯一拥有者；不是运行时猜测物理库相同 |
| FreeSQL SQL 路径与原生 CRUD | SQL 路径可接入；原生桥接不在本期 | protected Sql 仅工厂+metadata；现有常规 Find/Add/Update/Remove 使用 FreeSQL 原生路径，不能混称 |

本功能尚未实施，不能以已有多库工厂或事务作用域计算为“多库 Uow 已完成”。实施验收冻结为第 10 节 T01–T08 共 8 项；发布证据另行计数。

## 3. 真实路径与设计背景

所有路径相对仓库根目录。

| 路径/符号 | 已确认行为 | 本次决策 |
| --- | --- | --- |
| `framework/src/Bing.Uow/Bing/Uow/IUnitOfWorkManager.cs`、`UnitOfWorkManager.cs` | Scoped marker；Register、GetUnitOfWorks、Commit/Async；没有 Begin/Current；IUnitOfWork.Commit 返回影响行数 | 保留旧接口、旧实现与自定义实现契约；增量 companion API，不强制现有实现补成员 |
| `framework/src/Bing.Uow/README.md` | 示例已写 `_uowManager.Begin()`，真实接口无此方法 | 本任务迁移文档及该 README 修正为实际新 API，不能当作已有实现 |
| `framework/src/Bing.Dapper.Core/Bing/Data/Sql/DapperServiceCollectionExtensions.cs` | TryAddScoped 四工厂，TryAddTransient runtime；元数据与连接工厂 resolver Singleton | 生命周期不变；默认 SQL runtime 内部按需发现业务 Uow |
| `.../SqlFactoryBase.cs`、`SqlQueryFactory.cs`、`SqlExecutorFactory.cs`、`SqlMultipleQueryExecutorFactory.cs` | 具名/ambient/default 路由；CreateOptions 克隆模板并冻结 context、连接字符串；ActivatorUtilities 创建产物 | 不新增数据源注册表；不在 Create 时开连接或绑定某个 Uow |
| `framework/src/Bing.Data.Sql/Bing/Data/Sql/Contexts/DefaultSqlDatabaseContextResolver.cs` | SqlOptions context → accessor.Current → metadata default | 保持此优先级；显式 dbKey 仍优先；Uow 不重写冻结路由 |
| `.../Contexts/DefaultSqlDataSourceResolver.cs` | PrimaryDataSource 路由、Transaction 主读策略、Provider 一致性检查 | 使用已有解析结果，事务 Uow 不把已创建 replica Query 偷换成 primary |
| `.../SqlQueryBase.cs` | GetOrCreateConnection 自建 Owned；AcquireExecutionLease 只保护单实例；Dispose 不释放 External；Query() 建独立 Builder | 增加业务资源借用和资源级租约，保持根对象生命周期与描述模型 |
| `.../SqlQueryBase.Transaction.cs` | 外部 transaction resolver 延迟读取；PrimaryReadStrategy.Transaction 可产生每次查询内部事务 | Uow 借用事务优先；复用 EF CurrentTransaction 延迟机制；禁止终结方法提交/回滚共享事务 |
| `.../SqlQueryBase.QueryPlan*.cs`、`SqlExecutorBase.cs`、`SqlMultipleQueryExecutorBase.cs` | 普通/分页/流式/写入/多结果集各有执行与清理入口；stream 某些异步路径仍取同步 GetQueryTransaction | 必须覆盖所有路径，不能只改普通 ToList；异步路径统一异步资源准备 |
| `.../SqlTransactionScopeFactory.cs`、`SqlTransactionScopeLease.cs` | Begin 真正开连接；owner Query 持连接；子 Query External；scope lease 计数并阻止活动执行时释放 | 保留显式单库 scope 独立语义；新增内部排除标记防止其 owner 被 ambient Uow 捕获；不能误认为现有计数租约已经阻止跨 Query 并发 |
| `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeBinding.cs` | 五个受控 binding 入口，经 ISqlQueryRuntimeBindingController；无资源注册机制 | 保留签名；EF Shared 且精确匹配显式映射的连接引用可参与，其他显式绑定不自动转移所有权 |
| `framework/src/Bing.EntityFrameworkCore/Core/EfCoreSqlQueryFactory.cs` | Shared：EF connection External + 延迟 EF transaction；Independent：立即创建 Owned connection；两者均冻结 context/metadata | Shared 复用已有绑定并加入明确映射的业务资源；Independent 保持独立 |
| `.../Core/StoreBase.cs` | 缓存工厂创建的 Shared Query；Dispose 当前仅置标记 | 消费者证据覆盖此路径；本期局部补齐已创建 Query 的幂等释放，不关闭 EF connection、不顺便重构仓储 |
| `framework/src/Bing.FreeSQL/Bing/Data/Stores/QueryStoreBase.cs` | 工厂 Create + FreeSQL metadata；没有绑定原生事务；Dispose 幂等释放 Query | 默认 SQL Query 可加入新 SQL Uow；FreeSQL 原生 ORM 事务仍独立，不能宣称二者共同回滚 |
| `framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs` | Commit→SaveChanges；有 transaction actions 时 TransactionCommit 自建/提交事务；PrepareDomainEvents 拒绝已有外部事务；保存后立即 Complete 事件和 SaveChangesAfter | 仅对精确参与的 mapped context 增加“flush、延后物理提交/通知”分支；无桥接旧行为不变；详见 7.2 |
| `framework/src/Bing.FreeSQL/Bing/Uow/UnitOfWorkBase.cs` | Commit→SaveChanges；FreeSQL 原生事务及 DeferredCommitUnitOfWork；已有事务与领域事件有冲突校验 | 本期不改变原生提交；Sql 属性路径共享不等于原生 CRUD 共享 |
| `framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs` | 拦截器先 next，再旧 manager.CommitAsync，最后 ICommitAfter.CommitAfter | 无新 Uow 时保持原样；活动新 Uow 下 mapped EF 只 flush，CommitAfter 登记到根成功回调，不能提前通知 |
| `modules/admin/src/Bing.Admin.Data.EFCore/UnitOfWorks/MySql/AdminUnitOfWork.cs` | 覆盖 TransactionCommit/Async，用 CAP BeginTransaction、publisher.Transaction、CommitAsync 自管事务 | 不静默跳过 CAP 覆盖；具体限制与现有 API 替代见 7.3 |

本地 master 的 MySQL `DapperServiceCollectionExtensions.AddMySqlQuery<TInterface,TImplementation>` 明确 `TryAddTransient`；其他官方 Provider 保持同类历史注册。原 Transient 是独立 Query 实例设计，不是应修正为 Scoped 的错误；本功能以资源登记弥补事务共享。

ABP 参考仅限机制：官方 [UnitOfWorkManager](https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkManager.cs) 的 Current/嵌套参与/外层恢复，以及 [UnitOfWorkDbContextProvider](https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/Uow/EntityFrameworkCore/UnitOfWorkDbContextProvider.cs) 的按当前 Uow 登记数据库与事务资源（2026-10-21 查阅）。本方案独立实现，不复制其依赖或按连接字符串合并的键策略；Bing 的 DI scope、ORM 兼容和冻结上下文契约另行保持。

## 4. 冻结的公共 API 与兼容方案

### 4.1 Bing.Uow 的增量 API

新增以下类型，命名空间均为 `Bing.Uow`，源文件放在该项目 `Bing/Uow/`。这是本功能所需新接口，不修改旧 IUnitOfWork、IUnitOfWorkManager 的成员或语义。

```csharp
public interface IUnitOfWorkScopeManager
{
    IUnitOfWorkScope Current { get; }
    IUnitOfWorkScope Begin(UnitOfWorkScopeOptions options = null, bool requiresNew = false);
}

public sealed class UnitOfWorkScopeOptions
{
    public bool IsTransactional { get; set; } = true;
    public System.Data.IsolationLevel? IsolationLevel { get; set; }
}

public interface IUnitOfWorkScope : IDisposable, IAsyncDisposable
{
    Guid Id { get; }
    UnitOfWorkScopeOptions Options { get; }
    bool IsCompleted { get; }
    bool IsRollbackOnly { get; }
    bool IsDisposed { get; }
    TResource GetOrAddResource<TResource>(object key, Func<TResource> factory)
        where TResource : class, IUnitOfWorkResource;
    IDisposable AcquireExecutionLease(object resourceKey);
    void OnResourceCommitted(object resourceKey, Func<Task> callback);
    void OnCompleted(Func<Task> callback);
    void SetRollbackOnly();
    void Complete();
    Task CompleteAsync(CancellationToken cancellationToken = default);
    void Rollback();
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkResource : IDisposable, IAsyncDisposable
{
    void Commit();
    Task CommitAsync(CancellationToken cancellationToken = default);
    void Rollback();
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public static class UnitOfWorkScopeServiceCollectionExtensions
{
    public static IServiceCollection AddUnitOfWorkScopes(this IServiceCollection services);
}
```

为 Dapper 与可选 EF 适配器增加以下**连接协作契约**，同样放在 `Bing.Uow`，只依赖 System.Data，不依赖 EF/Dapper/SqlDataSource 类型。不新增通用身份接口。两个接口的目的仅是让调用方发现显式配置的连接拥有者：

```csharp
public interface IUnitOfWorkConnectionProvider
{
    // 未映射的源返回 null；匹配后配置冲突必须报错，不能静默落回独立资源。
    IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope,
        string dataSourceKey, string providerKey, string tenantId, string connectionString);
}

public interface IUnitOfWorkConnectionResource : IUnitOfWorkResource
{
    object ResourceKey { get; }
    System.Data.IDbConnection Connection { get; }
    System.Data.IDbTransaction Transaction { get; }
    void EnsureReady();
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}
```

在现有 `Bing.EntityFrameworkCore` 程序集、`Bing.EntityFrameworkCore` 命名空间的扩展类增加一个注册入口：

```csharp
public static IServiceCollection AddEfCoreSqlUnitOfWork<TContext>(
    this IServiceCollection services, string dataSourceKey)
    where TContext : Bing.Datas.EntityFramework.Core.UnitOfWorkBase;
```

`TContext` 由同一业务 DI scope 的 `GetRequiredService<TContext>()` 解析，必须与仓储所注入的 context 是同一实例；注册不会自己 new/Dispose context。源使用现有 AddSqlDataSource 定义；此 API 仅保存源键→context 类型映射，不再定义连接字符串或第二套数据源。有效端点键映射明确，不将 replica 映射自动套到 primary。相同映射重复注册幂等；同源映射两个 context、同 context 类型映射两个源均明确配置错误。支持多个不同源分别映射不同 context；需要多租户不同连接时使用不同业务 DI scope，不能让同一个 scoped context 冒充多个租户资源。

该入口幂等调用 AddUnitOfWorkScopes/AddEfCoreSqlQueryFactory 并注册连接适配器；不代替 AddDbContext、Provider 或 AddSqlDataSource 注册，不覆盖已有自定义服务。示例显式列出前两项是为了展示完整依赖。

- `Current` 在没有 frame 时返回 null；仍安装或被异步分支继承的终态 frame 返回其终态 root，以便后续执行明确失败，不能跳过它回落到独立连接或外层 Uow。nullable 注解按项目现有设置处理。`Options` 返回副本，Begin 克隆选项，后续改调用者选项不影响活动 Uow。无效 IsolationLevel（含 Chaos/Unspecified）在 Begin 拒绝；null 使用 Provider 默认隔离级别，不强塞 ReadCommitted 到 SQLite。非事务 Uow 不允许非 null IsolationLevel。
- `Begin` 只建立逻辑边界，无 I/O，因此不新增 BeginAsync。首次真正执行才同步/异步准备连接与事务。资源工厂只构造未打开的资源，不在注册锁内做 I/O。
- 资源 key 非 null；同键返回同资源且 factory 只调用一次；同键不同资源类型明确 InvalidOperationException；factory 失败不留半登记项。key 是不透明 equality key，不允许管理器把 key.ToString 写日志。资源按首次登记顺序完成，逆序清理。
- OnResourceCommitted 针对已登记资源，只在该库物理提交确认成功后按登记顺序执行；即使随后别的库提交失败，该库的回调仍应执行一次。OnCompleted 仅在所有资源提交成功后执行。回调失败不会回滚已提交库或重试提交；汇总通知异常并保留已提交结果。同步 Complete 同步等待这些 Task，异步 Complete await；Dispose 不补发成功回调。参与句柄把回调登记到 root。
- `AcquireExecutionLease` 要求资源已登记且 scope Active。同一 key 只允许一个活动执行，第二个立即 InvalidOperationException；不同 key 可各自持租约。释放幂等。Complete/Rollback/Dispose 与租约状态在同一锁下转换，不得有“先检查空闲，再被新执行插入”的竞态。
- 默认 `UnitOfWorkScopeManager`（internal sealed）Scoped，`UnitOfWorkScope`、参与句柄、frame 均 internal；通过扩展方法 TryAddScoped 注册，不增加依赖扫描 marker 造成重复注册。预注册类型/委托/实例不覆盖，重复注册幂等，后注册遵循 DI 原规则。
- 旧 manager 不注册新 scope 为旧 IUnitOfWork，不把旧 Commit 当作 Complete，不自动导入旧 HashSet。旧自定义 manager 的接口与注册保持兼容。显式桥接 context 在活动新 Uow 中被旧 Commit 调用时按 7.2 执行 flush；无桥接/无新 Uow 的语义不变。旧 Attribute 不自动 Begin 新 Uow，调用者仍显式 Begin/Complete。
- GetResource 必须不做 I/O，返回的资源已经通过 scope.GetOrAddResource 登记，ResourceKey 是该登记键；EnsureReady 才打开连接/开始事务。多个已注册 connection provider 对同源返回非 null 是配置冲突，不能采用“最先使用者获胜”。EF adapter 用 TryAddEnumerable 注册一个 Scoped provider，映射集合为静态配置；尊重既有工厂自定义注册。

### 4.2 项目依赖与 API 门禁

- `Bing.Dapper.Core.csproj` 增加对已有 `Bing.Uow` 的 ProjectReference；`Bing.Uow` 不反向引用 Dapper/EF/FreeSQL，`Bing.Data.Sql` 不引入 ORM/Uow 依赖。Dapper 仍可单独注册运行，不要求 EF 或 ABP。
- `Bing.Uow` 使用 IAsyncDisposable 所需的 Bcl 依赖优先复用现有传递引用；如果编译确需直接声明，只使用仓库现有 `Microsoft.Bcl.AsyncInterfaces` 6.0.0，不改变任何已存在 PackageReference 或版本属性。
- SQL/Dapper 公共接口、Provider SPI、RuntimeBinding 签名、SqlOptions/DatabaseContext/SqlDataSourceDescriptor 公共形状均不变；跨程序集连接接口限于上面两个，具体实现 internal。EF 不需要反向依赖 Dapper.Core。`common.props` 对 SQL 项目的 RS0016/17/18/26 是 error，不得通过 NoWarn、pragma、降低 analyzer、重写 PublicAPI 文件绕过。
- 新公共面限于 Bing.Uow 的上述接口、EF 的一个映射注册方法及必要的既有 SaveChanges 虚方法 override；删除初版身份扩展接口。用独立 API consumer 测试和任务 `artifacts/public-api-additions.md` 记录新增面，保持既有所有 PublicAPI.Shipped/Unshipped 文件字节不变。必要实现不要求再次确认；超出本计划的 API 再记录偏差，不自动改批准 baseline。

## 5. Scope、嵌套与失败语义

### 5.1 Current 和嵌套

- manager 每个 DI scope 拥有独立 AsyncLocal frame 链；不能使用全进程静态 Current 把不同业务 DI scope 关联起来。Begin 同步安装不可变父指针 frame，跨 await 可见；退出严格 LIFO 恢复。不要原地修改一个被多个 ExecutionContext 继承的共享 holder 来实现 pop，避免子分支清空父分支。
- DisposeAsync 必须采用同步入口加异步清理核心：先在调用者 ExecutionContext 中验证 LIFO/无活动租约、冻结终态并恢复父 frame，再返回异步清理的 ValueTask。不能在 async 方法 await 之后才设置 AsyncLocal，否则调用者可能仍看到旧 Current。清理失败也已恢复父 frame；前置校验失败则不改变 frame。测试同时覆盖真实 await、清理异常、继承了旧 frame 的延迟子任务，禁止后者无声退化为 standalone。
- 无 Current 或 requiresNew=true：创建新 root Id、独立资源字典；requiresNew 暂挂外层，退出恢复原外层。Begin 不另建 DI scope，不重绑定已有 factory；后台和并行分支自己建立业务 DI scope。
- Current 存在且 requiresNew=false：返回参与句柄，Current 仍指同一 root，Id/资源一致。options 为 null 继承；显式传入选项必须与 root 一致，否则立即拒绝，不能悄悄升级只读 Uow 为事务。
- 参与句柄 Complete 只表示本层成功，不提交。参与句柄未 Complete 就 Dispose、显式 Rollback 或发生 SQL 执行错误，root 标记 rollback-only。root Complete 必须等参与句柄都退出；未退出/活动 reader 的 Complete 立即拒绝且保持可收尾状态。
- 内层 requiresNew 的异常/回滚只影响自己，外层可继续；参与式嵌套失败使外层不能成功提交。不用 savepoint，不承诺内层参与层单独回滚后外层仍可提交。
- 流入 Task.Run 的 Current 不意味着线程安全或自动新资源。同库并行必须每分支新 DI scope + Begin（或在同 scope 明确 requiresNew 且全新工厂产物）；不得将同一个注入 Query 传给并行分支。优先在示例使用独立 DI scope，隔离过滤器与其他 scoped 服务。

### 5.2 状态与提交

- root 状态：Active → Completing → Completed，或 Active → RollingBack → RolledBack；失败进入 Faulted，最后 Disposed。任何终态不得获取新资源或再执行 SQL。rollback-only 保持 Active 但 Complete 走回滚并抛 InvalidOperationException。
- Complete 无活动执行时冻结登记，按登记顺序逐库提交；已提交的库不能撤销。第二库失败/取消时，第一库仍已提交，失败库标记结果未知或失败，后续未提交库尽力回滚；不会重试已成功提交库，不伪称全局回滚。
- 每库提交成功后先记录 Committed，再执行其成功回调；回调异常单独收集，继续后续资源提交。物理提交全部成功时设置 IsCompleted=true，再运行全局成功回调；即使通知失败并抛出汇总异常，仍保持 Completed，后续 Complete 不重复通知或提交。物理提交失败则 Faulted，仅保留已提交库的回调，不运行全局回调。
- 原始异常保持主异常；多次 rollback/dispose 失败汇聚为 AggregateException，继续尝试所有剩余资源。可在 Exception.Data 放脱敏 Uow Id、资源序号及 Committed/FailedOrUnknown/RolledBack/NotCommitted 状态，不放连接字符串、安全身份值或凭据。迁移说明明确业务补偿属于调用方。
- 每个资源拥有自己的提交/回滚/释放状态，确保资源内部失败清理与 root 最终清理不重复执行 Provider Commit、Rollback 或 Dispose；未建立事务的非事务资源 Commit/Rollback 为无操作，连接仍由 Dispose 释放。
- Complete 成功后重复 Complete 为幂等无操作；Rollback/Dispose 幂等，不回滚已完成库；Complete 在 RolledBack/Faulted/Disposed 后拒绝。完成后 Current frame 不自动跳回外层，直到 Dispose，避免同一 using 块末尾 SQL 意外逃到外层。
- SQL 执行、映射读取、reader 读取或取消失败，若已关联 root，标记 rollback-only。业务代码 catch 后仍不能无声提交部分写入；纯取消预检若已知 Current，也要标记，避免前面的写入被提交。描述构建/参数验证且未启动执行的失败无需毒化事务；两者用测试区分。
- 清理 rollback/dispose 使用 CancellationToken.None；业务取消令牌只用于打开、开始、命令与提交。不得用已取消 token 跳过清理。无需额外自动重试策略。
- Complete/Rollback/Dispose 发现仍有活动执行或未退出参与句柄时 fail fast，保持状态可重试清理；不并发关闭正在读取的连接。调用方须在 Uow 退出前完成枚举并释放 reader。错误使用导致遗留 reader 的边界明确记录，不能靠强制关闭活动连接掩盖。正常 await foreach break、异常及取消必须自动释放租约。

## 6. Dapper 资源、键与 Query 绑定决策

### 6.1 内部职责

新增文件候选位于 `framework/src/Bing.Dapper.Core/Bing/Data/Sql/UnitOfWork/`，命名冻结如下；这些不是当前已存在的文件：

| internal 类型 | 职责与主要方法 |
| --- | --- |
| `SqlUnitOfWorkResourceKey` | 不可变 equality key；按本节全部字段比较；ToString 只显示脱敏资源类别，不显示秘密 |
| `SqlUnitOfWorkResourceKeyFactory` | `Create(...)` 从冻结 context/Provider/完整连接配置及既有 resolver 引用构造 key；没有认证方式白名单或身份扩展 |
| `SqlUnitOfWorkResourceProvider` | 先查询显式 IUnitOfWorkConnectionProvider；已映射则借用其唯一资源，无映射再 GetOrAdd Dapper 自有资源；校验 root/Query 亲和性，创建无 I/O |
| `SqlUnitOfWorkResource` | IUnitOfWorkResource；唯一拥有 connection/transaction；`EnsureReady()`、`EnsureReadyAsync(CancellationToken)`、Commit/Rollback/Dispose；Provider 能力检查与 SqlTransactionAsyncAdapter 复用 |
| `SqlQueryBase.UnitOfWork.cs` | 根 Query 亲和状态、冻结路由、显式模式标记、准备/借用/解绑；允许精确映射的 EF Shared 外部连接参与同一资源租约 |

默认服务注册到现有 AddSqlCore：key factory 无状态 Singleton，resource provider Scoped（构造不强制要求 IUnitOfWorkScopeManager）；用 TryAdd，不覆盖自定义。只有应用注册业务 scope manager 且 Begin 后才出现共享资源。资源 dictionary 只属于活动 root，不进入 Singleton 缓存。

### 6.2 资源键

普通默认路径与自定义 resolver 路径使用相同规则，不增加身份 Provider 实现要求。在同一业务 DI scope/Uow 内，key 包含：

1. 规范化 Provider Key（沿用 registry 的比较规则）和 DatabaseType；不能只用枚举区分自定义 Provider。
2. 冻结的有效 DataSource.Key/DbKey，不能仅凭 Server/Database 相同合并不同配置数据源。
3. TenantId，null 与非空不同，沿用租户大小写语义，不擅自 lower-case。
4. **解析后的有效读写端点**（有效 DataSource.Key、连接配置、PrimaryReadStrategy、只读/事务能力）；不能把主从端点、只读/写入来源仅因地址相同合并。同一有效主库上 Default/Primary 的表面偏好差异不必拆出第二事务；路由是否允许仍按 6.3 验证。
5. 创建时解析后的**完整连接配置值**（包括认证与会话参数）按 Ordinal 比较，保存在 internal key 私有字段中，不输出到日志/异常。允许等价连接字符串因排序不同而不复用，优先安全隔离；不把去密码后的物理身份用于相等判断。禁止全局保存这些 key。
6. 负责创建连接的**同一既有 ISqlDbConnectionFactoryResolver 实例引用**；不同来源实例不合并。不新增身份、凭据代际或认证方式接口。显式 EF 映射分支以登记的 context 实例/映射记录作为唯一来源，配置已经明确选择由 EF 提供连接，而非再调用 Dapper resolver。

MappingProfile 和 IDataFilter 不决定连接相等性，仍按 Query/Builder scope 隔离，不能将 Query/Builder/参数放入资源缓存。相同资源身份、不同 MappingProfile 可共享连接，但必须产生各自完整 SQL 和参数。

- 集成认证、自定义 resolver、token 配置本身**不是拒绝原因**。同 scope、同冻结 tuple、同 resolver 的标准工厂产物正常参与共享；现有 resolver 对此 tuple 负责选择连接。若应用需要在一个 scope 内表达不同账号/会话，应使用现有不同数据源配置、不同 resolver 实例或独立业务 scope，不由框架推断动态身份。
- 不能证明可复用的配置（例如自定义运行时仍可执行，但没有稳定具名源或完整配置快照）保守使用 Query 专属资源键：允许执行，仍由当前 Uow 管理该连接/事务并共同完成或回滚，**只是不承诺与另一个 Query 是同一物理事务**。不同 resolver 实例已有上述第 6 维隔离，同一实例内配置完整时仍正常复用。不得为此退回无 Uow 的自动提交。该来源要获得跨层同事务保证，须提供既有具名源+冻结配置+同 resolver，或明确 EF 映射；不要求接入身份平台。
- 显式传入外部连接/事务沿用已有所有权；只有第 7 节配置的 EF Shared 是本期自动登记外部连接拥有者的入口。没有足够信息的任意外部连接不能被悄悄抢走所有权，也不能把其独立模式说成已参加回滚。
- 保留原有“根本缺少可执行连接配置”的错误；本方案不把既有无效配置变成可执行，也不因认证方式未知而阻断本来能执行的查询。不同未知来源隔离、每个 Uow 自有事务都回滚、没有连接双重释放，是必须的直接测试。

### 6.3 路由与事务

- **不改变创建时路由快照**。Begin 不修改 IDatabaseContextAccessor。Query 在事务 Uow 前创建且目标本来可写、无需 PrimaryDataSource 重定向时，可以首次执行加入 Uow。
- 事务 Uow 内首次触碰每个数据源立即准备其本地事务，而不是等第一次写入，确保前后读写使用同一事务。事务不受 Query 释放或单次终结方法完成影响。
- 若冻结快照仍指向需 PrimaryDataSource 重定向的读端点、只读源，或 Provider/descriptor 不支持事务，事务 Uow 在开连接前拒绝。调用方需先通过已有 Primary 数据库 scope 建立正确快照，或 `Create(primaryDbKey)`；不能执行时换库。PrimaryReadStrategy.Transaction 可在事务 Uow 中复用整段 Uow 事务。
- 非事务 Uow 支持可写数据源与 Doris 等只读/无事务源混合汇总；每个 key 共享打开连接，没有事务，不承诺 rollback 能撤销已执行写入。非事务 Uow 遇到 PrimaryReadStrategy.Transaction 时拒绝（要求改用事务 Uow 或无 Uow 既有主读路径），避免单次主读事务破坏资源所有者约定。
- 同库不同 Query/Executor 第一次并发准备由资源级租约挡住；持有租约者完成 EnsureReady 后才将成功资源发布为 Ready。打开/Begin 失败销毁半成品、标记 root rollback-only，后续 Complete 不能成功；不保留坏连接供重试。

### 6.4 Query 亲和状态表

数据库上下文在 Create 时冻结；Uow 亲和性在**首次执行入口**固定（流式从首次 MoveNext 开始），纯 Query()/From()/SQL 渲染不获取连接或绑定 root。状态与资源引用存于根 Query，Clone/描述沿用原 executor，不获得新亲和性。

| 情况 | 明确行为 |
| --- | --- |
| Uow 前创建、仅构建/渲染、Uow 中首次执行 | 加入当时 Current，仍用原 context；按端点/能力规则先验证 |
| 创建于 Uow A，但直到 Uow B 才首次执行 | 加入 B；创建时只冻结数据源，未承诺 A；文档必须注明 |
| 无 Uow 已执行，再进入任意 Uow | 拒绝该根 Query 自动迁移；新建工厂产物或在业务 Begin 前不执行注入 Query |
| 已在 A 执行，在 A 内顺序重复 Query()/From() | 允许，同连接/事务；独立 Builder/参数 |
| 已在 A 执行，进入参与式嵌套 | 允许，Current root Id 不变 |
| 已在 A 执行，进入 requiresNew B | 旧 Query 拒绝在 B 执行；B 使用全新 Query/Executor，退出 B 后 A 原 Query 可继续 |
| A 完成/回滚/释放后继续用 A Query | 拒绝；不能退化到无 Uow 独立连接，也不能自动加入 C |
| Query 已 Dispose | 不可再构建或执行；不因共享连接仍在而复活 |
| Query factory A 被带到 DI scope B 调用 | 沿用 F008：仍绑定 A scope 服务与 manager，不重绑定 B |

这种首次执行固定规则支持应用服务和仓储在 Begin 前构造注入；也能明确阻止连接中途迁移。新业务操作应使用新 DI scope，避免 DI 已解析的旧根 Query 被跨业务复用。

### 6.5 资源所有权与显式模式

| 来源 | Query 对象释放者 | 连接/事务释放者 | 是否自动加入新 Uow |
| --- | --- | --- | --- |
| DI 直接解析的默认 transient | DI scope | 有 Uow：root；无 Uow：Query | 是（未显式绑定且首次执行） |
| 默认 factory.Create | 调用方 using/await using | 同上 | 是 |
| 显式 ISqlTransactionScopeFactory 子对象 | 按既有 scope/调用方契约 | 显式 transaction scope | 否；整个显式 scope 保持独立 |
| RuntimeBinding.BindExternalConnection 或 SqlOptions.Connection | 原对象创建者 | 外部拥有者 | 一般否；下述已映射 EF Shared 是精确例外 |
| RuntimeBinding.BindOwnedConnection | 调用方/DI（按来源） | Query | 否；显式 Owned 不能被自动转移 |
| 已映射 EF Shared + 同源默认 Dapper | 各自 DI/调用方；Store 缓存时 Store | connection 仍属 EF context；本地 transaction 只由 root 登记的 EF resource 完成；context 最终由 DI 释放 | 是，两种访问顺序一致 |
| 未映射 EF Shared | 工厂调用方；Store 缓存时 Store | EF DbContext/EF transaction | 否；明确未启用桥接，不计为跨层验收通过 |
| EF Independent | 工厂调用方 | Query | 否，保留独立含义 |
| FreeSQL QueryStoreBase 的默认 SQL Query | QueryStoreBase | 新 SQL Uow 或无 Uow Query | 是；FreeSQL 原生 CRUD 不因此共享 |

- 默认参与 Query 持借用引用，内部 connection/transaction ownership 标记 External，并记录 Uow 亲和性。Query.Dispose 只释放自身状态/引用，不能 Close/Dispose 共享资源。Dapper 资源自己拥有连接；EF 资源只管理本次打开/事务，不 Dispose DI context/EF connection。不要让资源 owner 持有 Query。
- 已绑定 ambient 资源后，RuntimeBinding 的 connection/transaction/context 绑定必须拒绝覆盖，即便物理地址相同；不能改变 owner。既有无 Uow 绑定校验继续保留。
- 显式 TransactionScopeFactory 保持独立，创建 owner 后、GetExecutionConnection 前设置 internal 显式模式；子对象沿用 SetTransactionContext。不得利用临时修改 ambient Current 来绕过，以免 async 泄漏。
- 活动新 Uow 中，已映射 EF Shared 与默认 Dapper 属于同一资源；EF Independent、未映射 Shared、显式 transaction scope 保持独立。模式选择必须在迁移示例明确，不能将 Independent 改名为 Shared 或用物理库猜测补齐映射。

### 6.6 执行入口与租约

必须在 root 实例租约基础上叠加资源租约，不替换原实例防重入检查。获取顺序固定为根实例状态 → 当前 Uow/key 资源租约 → EnsureReady；失败按逆序释放。锁内不做 Provider I/O。

改造覆盖：普通同步/异步 scalar/list/entity、raw Sql/SqlInterpolated、typed Query/From、分页 Count+Page（一次外层租约，内部不重复获取）、Procedure、ExecuteDirect/ExecuteDirectAsync、实体 Mutation、批量 Mutation、Multiple/reader、stream 同步/异步和提前终止。异步首次资源打开必须走 OpenAsync/BeginWithModeAsync；stream 不能为了接入而新增同步打开。Provider 能力拒绝先于实际 I/O。

reader/Multiple 的租约随返回结果存活，直到读完或 Dispose；同 key 的另一 Query 此时被拒绝，即使它是另一个 transient。资源仍只开一次、Query 无权自动关连接。两个不同 key 可以独立执行，但调用者的 Query/Builder/DI scoped 依赖仍须遵守自身非线程安全约束。

## 7. ORM 与后台消费者迁移

### 7.1 EF 显式映射、唯一拥有者与两种顺序

候选新文件位于 `Bing.EntityFrameworkCore`：internal `EfCoreSqlUnitOfWorkConnectionProvider`、`EfCoreSqlUnitOfWorkResource`、映射记录；注册入口放现有 Extensions。每个默认实现有一个独立职责测试类，不再增加另一套 QueryFactory。

1. `AddEfCoreSqlUnitOfWork<OrderDb>("orders")` 冻结的是**源键到同 scope context 的映射**。首次执行时用已冻结 SQL tuple 查映射，选择 EF provider；随后才允许无映射的源创建 Dapper Owned 资源。不是先 Dapper 开连接、等仓储出现再替换，也不按物理地址自动发现 context。
2. 应用层先执行：默认 Dapper resource provider 先查映射 → 解析同 scope OrderDb → 获取其 Database.GetDbConnection → 登记唯一 EF resource → EnsureReady 打开该连接并以 EF Database.BeginTransaction/Async 建立 CurrentTransaction → 应用 Query 借用；后来 StoreBase.Shared 取得同一个 OrderDb 和 connection/current transaction。
3. 仓储先执行：EfCoreSqlQueryFactory 保留现有 Shared 外部连接+延迟事务绑定，另校验该 Shared context 是否就是映射选定实例；执行时进入同一个 EF resource。后来应用层默认 Dapper 的映射查找返回该资源，不另建连接/事务。构造注入在 Begin 前发生、Shared Query 提前创建但未执行，也必须支持。
4. EF provider 资源键由 root、映射记录、context 实例、有效 Provider/源/租户/冻结配置构成；同配置重复调用返回同资源/key。不同 Query 的 MappingProfile/filter 仍独立。映射中的 context 是连接唯一拥有者，root 是该次本地事务唯一完成者，所有 SQL 对象只是借用者。
5. 保留现有 Provider/物理库校验防止错误路由；**映射本身明确授权使用 context 的连接配置与认证**，不凭物理库相等合并独立账号。查询冻结配置必须与选中的该源快照一致；运行中配置变化不得将原 context 重新包装成第二资源。EF 打开连接后可能隐藏密码，因此不能重新读取已打开 ConnectionString 当新的安全身份。
6. EnsureReady 仅在不存在事务时由 EF 创建本地事务。已有但并非当前 resource 创建的 CurrentTransaction、System.Transactions ambient transaction、同 context 已属于另一活动 root，均是特定所有权冲突：拒绝该映射参与，不创建第二事务，也不抢占已有事务。此限制与普通 custom resolver/认证可用性无关。
7. connection 不 Dispose；本 resource 如果原来关闭才通过 EF OpenConnection/Async 打开，退出时对应 CloseConnection/Async，原来已打开则保持原状态。只 Dispose 本 resource 创建的 IDbContextTransaction，确保 CurrentTransaction 解绑；context 最终仍由 DI Dispose。重复 Query/Store/root/DI 清理不得二次提交或关闭外部连接。
8. requiresNew 使用原同 scope context 会冲突，不能伪装独立事务。EF 独立嵌套/并行应创建独立 DI scope 并重新解析 context、仓储和 Query；纯 Dapper 仍支持原同 scope requiresNew+新 Query。租户变更同理，不跨租户复用已选定 context。
9. Independent 仍立即绑定 Query Owned 连接，明确绕过映射；未配置映射的 Shared 保留旧路径。这两类不能算作新功能“已跨层共享”。custom QueryFactory 若返回不支持框架 runtime binding 的对象，沿用既有不支持行为，不承诺自动适配第三方实现。

### 7.2 已映射 EF 的 SaveChanges、旧 Commit 和回调

实际源码需要处理三个接点：`UnitOfWorkBase.cs:427/599` 的 SaveChanges 两条路径，`:483/657` 的 TransactionCommit 自建/提交事务，`:784` 的 PrepareDomainEvents 外部事务拒绝及 `:1120` 的 SaveChangesAfter。仅绑定连接无法满足这些契约。

- **参与条件**：当前是事务业务 Uow、context 精确匹配映射且已由该 root 登记（或本次 SaveChanges 按映射首次登记）。否则仍走旧实现。不修改旧 IUnitOfWorkManager 接口、HashSet 或自定义 manager；它调用 context.Commit/Async 时，自然进入该参与分支。
- **flush 不等于 commit**：已映射 context 的 Commit/SaveChanges 在此分支仍实际保存并返回真实受影响行数，但不得调用 connection.BeginTransaction、TransactionCommit 或物理 Commit/Close/Dispose；它使用 root 创建并保持为 CurrentTransaction 的事务。调用者可显式 SaveChanges 让后续 SQL 读到本事务内变更，最终仅 root.Complete 提交。SQL-only 使用无须 SaveChanges。
- **不隐式扫描旧 manager**：新 Complete 仅提交已经执行到资源事务的命令，不自动 SaveChanges，也不发现未参与的 DbContext。EF tracked changes 和已登记事务动作必须显式 SaveChanges/Commit（或原拦截器触发）完成 flush；遗漏 flush 的跟踪变更不会被保存。不得用 HasChanges 作为提交拒绝条件，因为 SaveChanges(false) 成功后它仍可能为 true；不为本功能新增跟踪状态快照平台。示例和测试明确区分“SQL 已执行”与“实体只被跟踪”，不宣称 Complete 会保存后者。
- 统一路由 SaveChanges()、SaveChanges(bool)、SaveChangesAsync(CancellationToken)、SaveChangesAsync(bool,CancellationToken)，避免 bool 重载绕过桥接；内部调用 `base.SaveChanges(bool)` / `base.SaveChangesAsync(bool, ct)`，不能在 base 的虚调用间递归。保留 acceptAllChangesOnSuccess 语义与 Commit 的并发异常转换，不恢复 rollback 后的 EF tracking 状态；失败业务 scope 的 context 随 scope 丢弃。
- 上述统一只针对 mapped 事务参与分支。无参与时无参/仅 token 重载保持现有框架流程，新增 bool override 直接走 EF base 对应 bool 重载，保留它此前的行为；用内部执行标记防止原 SaveChangesCore 经 base 虚调用再次进入参与流程。无 Uow 回归必须同时断言这些重载的原通知/动作次数。
- 顺序保持：登记资源 → 当前资源租约 → EnsureReady → SaveChangesBefore/审计 → 准备领域事件 → transactionActionManager.CommitAsync(同一 DbTransaction) → base 保存 → RefreshGeneratedAuditEntityIds。**默认 TransactionActionManager.CommitAsync 只是执行/清空动作，并不物理提交**，可以复用；失败标记 root rollback-only，最终用不可取消令牌回滚。
- PrepareDomainEvents 仅将“本桥接管理的 CurrentTransaction”识别为可确认拥有者；真正外部事务的旧拒绝继续保留。同一个 context/resource 保存期间复用一个 DomainEventDispatchSession，重复 flush 不重复分发已见事件；DomainEventDispatchSession.Complete 延后到该资源 OnResourceCommitted，回滚不移除事件。SaveChangesAfter/PublishEventsAsync 为成功 flush 登记延后回调，物理提交前不调用；刷新生成的审计 ID 仍在 flush 后完成，避免丢失原有审计数据。
- 本期保持非重入资源租约：EF flush/事务动作执行时持有该资源租约；动作可使用其参数 IDbTransaction 执行命令，但不能同时/重入启动该资源的另一个框架 Query。领域处理器中的纯跟踪变更可被本轮保存；若处理器需要通过 Query 重入同连接，应把该读取移到 flush 前或使用已传入事务的受控命令。不能为绕过租约偷偷另开自动提交连接。原生 LINQ/用户直接拿 connection 的并发不由此框架拦截，仍要求顺序使用 DbContext。
- 非事务 Uow 中 EF SaveChanges 保留原生自有提交语义及通知时点，没有“共享业务事务”的承诺；SQL 连接共享仍可用于汇总。不得把这类立即持久化的数据写入事务回滚通过数。
- AspectCore 的真实顺序是 target → legacy manager.CommitAsync → ICommitAfter。活动新 root 下 manager 对 mapped EF 的调用只是 flush，故 ICommitAfter 要登记 root.OnCompleted，不能立即执行。无新 root 时顺序原样不变。若业务方法内部已成功 Complete/Dispose，context 上的完成记录让随后的旧 manager 重复 Commit 在无新变更/动作时返回 0、不重复通知；已回滚或出现新变更则明确拒绝，不能在事务外再次保存。
- 拦截器返回时若 Current 已 Completed，则 ICommitAfter 可立即执行一次（提交已经确认）；若仍 Active 则登记一次；Faulted/RolledBack 不执行。已经 Dispose 导致 Current=null 的路径依靠上述 context 完成记录防止额外保存，正常成功后回调保持原语义。不得向终态 root 追加回调。调用 SaveChanges(false) 后应按 EF 契约显式 AcceptAllChanges；若仍带未接受状态走旧 manager 的重复提交，明确拒绝而不是猜测哪些变更已保存。
- 回调在每库提交确认后执行；某库已提交但另一库失败，只运行已提交库的事件完成/SaveChangesAfter，不运行 root 级 ICommitAfter。通知异常与物理提交失败区分，已提交资源不重试/不回滚；复用 Bing.Data 现有 TransactionOutcome/PostCommitNotificationException 表达 EF 通知结果，Uow 通过“先记录提交成功、再运行 callback”的流程避免反向依赖 Bing.Data。

### 7.3 派生 EF/CAP 与 FreeSQL 的真实边界

- `AdminUnitOfWork.TransactionCommitAsync` 使用 CAP Database.BeginTransaction(publisher)、publisher.Transaction 与 CAP Commit，不能在桥接中无声绕过或套第二事务。**SQL-only 的 Admin Shared 查询可映射共享**；普通无事务动作的原生 flush 可复用上节基类路径。但 mapped 保存若存在 transaction actions/领域事件且 context 覆盖了 TransactionCommit/Async，本期在执行动作之前明确拒绝该保存并标记 rollback-only，不调用覆盖方法，也不假装 CAP 已参加。派生 SaveChanges 覆盖必须调用基类；完全自管事务的覆盖不在保证范围。
- 这是具体 CAP 所有权不兼容，不是“以后填一个 adapter”的占位实现。需要 CAP 的业务继续使用已存在的旧 EF/CAP Uow；将应用层及仓储 SQL 写入放入 `ITransactionActionManager.Register(async transaction => { ... })`，在动作内部通过 `IEfCoreSqlQueryFactory.Create(context, Shared)` 使用 CAP 此时已经建立的 CurrentTransaction，然后调用旧 context.CommitAsync。该路径不 Begin 新 root，CAP 仍是唯一提交者；它保留 CAP 语义，但不提供新 root 的多库协调。另一选择是本业务显式映射未自管事务的普通 UnitOfWorkBase context。二者都是完整现有调用方式，不将 CAP 迁移纳入本期。
- FreeSQL `QueryStoreBase.Sql` 是 factory.Create + metadata 的受保护 SQL 入口，可与应用层默认 Dapper 在同 root 共享资源；**当前基类 Find/查询及 StoreBase.Add/Update/Remove 实际走 FreeSQL Set，不是这个 SQL 属性**。因此新增消费者测试须用明确调用 Sql 的派生仓储，不将普通原生 CRUD 计为通过。
- FreeSQL 原生 SaveChanges/TransactionCommit 使用 FreeSQL UnitOfWork.GetOrBeginTransaction/Commit 和 DeferredCommitUnitOfWork；已有原生事务时领域事件登记还会拒绝。直接给 Dapper 绑定其连接不足以接入这些回调，本期不改此路径、不宣称自动桥接。业务需要新跨层保证时使用上述明确 SQL 仓储路径；原生 FreeSQL CRUD 保持旧独立 Uow，混用仍是两个事务拥有者。
- 新 EF StoreBase.Dispose 仅补齐已创建 `_sqlQuery` 的幂等释放，不在 Dispose 中初始化 Sql、不 Dispose context；FreeSQL 现有幂等清理保持。不修改 Admin CAP 实现、FreeSQL 原生 Uow 或全仓 ORM 事务机制。

CAP 旧路径的最小完整调用封装如下；调用者仍使用既有 CAP/数据源/仓储注册，在同一个业务 DI scope 传入 context、actions、factory。`applicationWrite` 接收本封装创建的 Query，`repositoryWrite` 调用现有仓储 Shared SQL 方法，二者只在动作执行期间真正发 SQL，不能先在动作外写库。本封装不创建新业务 root，验收沿用 Admin CAP 门控；未运行时明确记录未验证。

```csharp
static async Task RunLegacyCapSqlAsync(
    UnitOfWorkBase context, ITransactionActionManager actions,
    IEfCoreSqlQueryFactory factory, Func<ISqlQuery, Task> applicationWrite,
    Func<Task> repositoryWrite, CancellationToken ct)
{
    actions.Register(async transaction =>
    {
        ct.ThrowIfCancellationRequested();
        using var query = factory.Create(context, EfCoreSqlConnectionMode.Shared);
        await applicationWrite(query);
        await repositoryWrite(); // 同 context 仓储的 Shared Query 借用 CAP CurrentTransaction
    });
    await context.CommitAsync(ct); // CAP override 是唯一物理提交者
}
```

### 7.4 主要调用示例：显式映射现有 EF StoreBase

下面是本期新增 API 实现后的完整 SQLite consumer 示例（net6），应转入实际编译/执行测试；目前是目标契约而非已验证程序。引用现有 Dapper.Sqlite、EntityFrameworkCore 和 Microsoft EF Sqlite 测试依赖即可，勿为示例改包版本。既有项目已有的实体/仓储/Uow 可替换这里的最小类型。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Bing.EntityFrameworkCore;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var connectionString = "Data Source=orders-uow-demo.db";
var services = new ServiceCollection();
services.AddLogging();
services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
services.AddScoped<ITransactionActionManager, TransactionActionManager>();
services.AddSqliteProvider();
services.AddSqlDataSource("orders", DatabaseType.Sqlite, connectionString);
services.ConfigureSqlMetadata(o => o.DataSources.DefaultDataSourceKey = "orders");
services.AddDbContext<OrderDb>(o => o.UseSqlite(connectionString));
services.AddEfCoreSqlQueryFactory();
services.AddUnitOfWorkScopes();
services.AddEfCoreSqlUnitOfWork<OrderDb>("orders"); // 唯一 owner，非自动猜测
services.AddScoped<OrderStore>();
services.AddScoped<OrderOperation>();

await using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true });
await using (var init = provider.CreateAsyncScope())
    await init.ServiceProvider.GetRequiredService<OrderDb>().Database.EnsureCreatedAsync();

// 两个不同业务 scope，分别演示先应用层/先仓储；均不 Complete，因此回滚。
foreach (var repositoryFirst in new[] { false, true })
{
    await using var scope = provider.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OrderOperation>()
        .RunAsync(repositoryFirst, commit: false, CancellationToken.None);
}

public sealed class Order : IKey<int> { public int Id { get; set; } }
public sealed class OrderDb : UnitOfWorkBase
{
    public OrderDb(DbContextOptions<OrderDb> options, IServiceProvider services)
        : base(options, services) { }
    protected override void OnModelCreating(ModelBuilder model)
        => model.Entity<Order>().ToTable("Orders").HasKey(x => x.Id);
}
public sealed class OrderStore : StoreBase<Order, int>
{
    public OrderStore(OrderDb db) : base(db) { }
    public Task<int> InsertSqlAsync(int id, CancellationToken ct)
        => Sql.Sql("insert into Orders (Id) values (@id) returning Id", new { id })
            .ScalarAsync<int>(cancellationToken: ct); // 既有 Shared Query 路径
}
public sealed class OrderOperation
{
    private readonly ISqlQuery _query;
    private readonly OrderStore _store;
    private readonly IUnitOfWorkScopeManager _uows;
    public OrderOperation(ISqlQuery query, OrderStore store, IUnitOfWorkScopeManager uows)
        => (_query, _store, _uows) = (query, store, uows);
    public async Task RunAsync(bool repositoryFirst, bool commit, CancellationToken ct)
    {
        await using var uow = _uows.Begin();
        async Task<int> AppWrite() => await _query
            .Sql("insert into Orders (Id) values (@id) returning Id", new { id = 1 })
            .ScalarAsync<int>(cancellationToken: ct);
        if (repositoryFirst) { await _store.InsertSqlAsync(2, ct); await AppWrite(); }
        else { await AppWrite(); await _store.InsertSqlAsync(2, ct); }
        // 如有 EF tracked changes，在这里显式 db.SaveChangesAsync(ct) 只做 flush。
        if (commit) await uow.CompleteAsync(ct);
    }
}
```

consumer 测试另外用新连接查询 `Orders`，两种顺序的回滚结果都为 0、成功提交各为 2；断言两条路径 connection/transaction 同引用、Begin/Commit/Dispose 次数，不只检查程序能运行。再覆盖 context.Add → SaveChangesAsync → SQL 读取 → root 回滚，确保旧 EF 保存没有提前持久化。多库汇总继续用 `ISqlQueryFactory.Create("reporting")`，不同源各自资源，不跨库 JOIN。

### 7.5 BackgroundService/Hangfire 每业务 scope 示例

注册使用已有 Provider/数据源配置，再调用 `services.AddUnitOfWorkScopes()`。以下示例假定默认源和 `reporting` 已通过既有 AddSqlDataSource 配置；应用服务/仓储/后台 operation 都注册为 Scoped。

```csharp
// 应用层与仓储各自注入 ISqlQuery，两个对象不同。
public sealed class ReportOperation
{
    private readonly IUnitOfWorkScopeManager _uows;
    private readonly ISqlQuery _query; // DI 管理，业务代码不要 using 这个字段
    private readonly ISqlQueryFactory _queries;

    public ReportOperation(IUnitOfWorkScopeManager uows, ISqlQuery query,
        ISqlQueryFactory queries)
        => (_uows, _query, _queries) = (uows, query, queries);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        await using var uow = _uows.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
        var local = await _query.Sql("select 1").ScalarAsync<int>(cancellationToken: ct);
        using var reporting = _queries.Create("reporting"); // 工厂产物由调用方释放
        var remote = await reporting.Sql("select 1").ScalarAsync<int>(cancellationToken: ct);
        await uow.CompleteAsync(ct);
        return local + remote; // 示例 SQL 适用于 SQLite/MySQL/PostgreSQL/SQL Server
    }
}

public sealed class ReportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    public ReportWorker(IServiceScopeFactory scopes) => _scopes = scopes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var operation = scope.ServiceProvider.GetRequiredService<ReportOperation>();
                await operation.RunAsync(stoppingToken);
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

// 本仓库 Hangfire activator 已为每次 job 建 scope，job 可直接注入 scoped operation。
public sealed class ReportJob
{
    private readonly ReportOperation _operation;
    public ReportJob(ReportOperation operation) => _operation = operation;
    public async Task RunAsync(CancellationToken cancellationToken)
        => _ = await _operation.RunAsync(cancellationToken);
}
```

示例中的 `select 1` 不宣称适用于所有 Provider；Oracle 文档例子使用 `select 1 from dual`。实际迁移例子还必须给：事务 Uow 下应用写入+仓储写入后异常统一回滚；参与嵌套；requiresNew 下 factory 创建新 Query；Task.WhenAll 每分支独立 CreateAsyncScope；主库 scope 后再解析/创建 Query。所有 reader 必须在 Complete 前释放。

现有路径证据：`samples/Bing.Samples.Hangfire/Modules/HangfireModule.cs` 与 `modules/admin/src/Bing.Admin/Modules/HangfireModule.cs` 的 BeginScope/DisposeScope；`framework/src/Bing.Events/Ordering/OrderedEventConsumer.cs` 的 ConsumerWorker/ProcessStreamAsync 用 CreateAsyncScope。只把它们作为业务边界证据，不为本任务改事件消费逻辑。

## 8. Change Impact Analysis 与验证矩阵

每轮测试前先更新 CIA，不先跑全量。初始预计：

```text
ChangedProjects: Bing.Uow; Bing.Dapper.Core; Bing.EntityFrameworkCore（显式桥接、保存回调、Store 释放）;
                 Bing.Ddd.Application.AspectCore（完成后回调）;
                 对应测试及 Provider/FreeSQL/Admin consumers; Bing.Data.Sql.Benchmarks（有界场景）
ChangedPublicContracts: Bing.Uow 新增 companion scope/resource/connection API；EF 映射注册；旧 SQL API 不变
ChangedRuntimePaths: SQL 创建快照与配置键、首次执行资源租借、事务/reader/清理、DI；
                     EF Shared/SaveChanges/旧 Commit、领域事件及提交后回调
ChangedProviders: 五个官方 Provider + Doris 兼容分支 + 自定义 Provider
ChangedTFMs: 生产 netstandard2.0；EF net6.0；多数测试 net6.0/net8.0
ChangedBuildPackaging: Dapper.Core -> Bing.Uow ProjectReference；不改包版本/TFM
ChangedBenchmarkHarness: 新 Uow 资源 lookup/创建/租约与 SQLite E2E 有界场景
ChangedDocsOnly: 本轮是；实施轮不是
AffectedDependents: SQL Provider、EF Core、FreeSQL、AspectCore、Admin、Hangfire sample
RiskLevel: HIGH
```

### 8.1 新默认实现的直接测试与生产符号映射种子

以下类/方法为**待新增的验收目标**，不是已经执行的证据。执行者最终将实际方法名和 TRX/TestId 填入 `traceability.md`；不能只保留“已覆盖”的泛称。

| 生产符号/行为 | 测试项目 / 独立测试类 / 计划方法 |
| --- | --- |
| UnitOfWorkScopeManager.Begin/Current | Bing.Uow.Tests / UnitOfWorkScopeManagerTest / Begin_ShouldFlowAcrossAwaitAndRestoreOuter、RequiresNew_ShouldUseIndependentRoot、Participation_ShouldUseSameRoot、OutOfOrderDispose_ShouldReject、IndependentDiScopes_ShouldNotShareCurrent、ChildTaskPop_ShouldNotClearParent、DisposeAsync_ShouldRestoreCallerBeforeAsyncCleanup、InheritedEndedFrame_ShouldRejectStandaloneFallback |
| UnitOfWorkScope GetOrAdd/状态/Complete | Bing.Uow.Tests / UnitOfWorkScopeTest / GetOrAdd_ShouldHitMissAndIsolateRoots、GetOrAdd_WhenFactoryThrows_ShouldNotRegister、Complete_WhenParticipantIncomplete_ShouldRollback、Complete_WhenSecondResourceFails_ShouldReportPartialCommit、Cancellation_ShouldRollbackRemainingWithNone、CleanupFailure_ShouldAttemptEveryResource、RepeatedCompletionAndDispose_ShouldBeIdempotent |
| UnitOfWorkScope 成功回调 | 同一 UnitOfWorkScopeTest / PartialCommit_ShouldNotifyOnlyCommittedResources、GlobalCallback_ShouldWaitForAllCommits、NotificationFailure_ShouldKeepCompletedAndNotRetry、Rollback_ShouldNotNotify；不为同一默认实现重复建测试类 |
| UnitOfWorkScopeServiceCollectionExtensions | Bing.Uow.Tests / UnitOfWorkScopeRegistrationTest / Registration_ShouldPreserveTypeFactoryInstanceAndBeIdempotent、LegacyManagerCustomImplementation_ShouldRemainUsable；反射新 API 契约单独类 UnitOfWorkScopeApiContractTest |
| SqlUnitOfWorkResourceKeyFactory | Bing.Dapper.Core.Tests / SqlUnitOfWorkResourceKeyFactoryTest / SameSnapshot_ShouldMatch、ProviderTenantEndpointConfigurationResolver_ShouldIsolate、SamePhysicalDatabaseWithDifferentDbKey_ShouldNotMatch、MappingProfile_ShouldNotSplitResource、CustomResolverAndIntegratedAuthentication_ShouldShareWithoutExtraServices、UnknownConfiguration_ShouldUseQuerySpecificKey |
| SqlUnitOfWorkResourceProvider | Bing.Dapper.Core.Tests / SqlUnitOfWorkResourceProviderTest / SameRootAndKey_ShouldCreateOnce、DifferentRootOrKey_ShouldCreateSeparately、NoCurrent_ShouldKeepStandalone、PreRegisteredServices_ShouldBePreserved、MappedOwner_ShouldWinBeforeConnectionCreation、ConflictingOwners_ShouldReject、UnknownConfiguration_ShouldKeepSeparateResourcesInsideUow |
| SqlUnitOfWorkResource | Bing.Dapper.Core.Tests / SqlUnitOfWorkResourceTest / BeginFailure_ShouldDisposeHalfCreatedResources、AsyncFirstUse_ShouldUseAsyncOpenAndBegin、CommitFailure_ShouldRollbackAndPreserveErrors、BorrowerDispose_ShouldNotCloseResource、ProviderDefaultIsolation_ShouldBeRespected |
| SqlQueryBase 新亲和性与租约 | Bing.Dapper.Core.Tests / SqlUnitOfWorkBindingTest / PrecreatedQuery_ShouldJoinOnFirstExecution、PreviouslyStandaloneQuery_ShouldRejectJoining、RequiresNew_ShouldRejectOuterQueryAndRestoreAfterExit、CompletedUowQuery_ShouldRejectReuse、ExplicitBinding_ShouldRemainIndependent、RebindingBorrowedResource_ShouldReject；SqlUnitOfWorkExecutionLeaseTest 覆盖跨对象同 key 互斥、失败归还、分页单租约 |
| SqlFactoryBase/各工厂上下文创建链 | Bing.Dapper.Core.Tests / SqlFactoryUnitOfWorkContractTest / Create_ShouldFreezeContextWithoutOpeningConnection、OptionsAmbientDefaultPriority_ShouldRemain、FactoryScope_ShouldNotRebind、AllDefaultFactories_ShouldKeepF008Lifetimes |
| 单库显式 TransactionScopeFactory | Bing.Dapper.Core.Tests + SQLite Integration / SqlExplicitTransactionUnitOfWorkContractTest / ExplicitTransactionInsideAmbient_ShouldRemainIndependent、ExplicitScopeChildren_ShouldKeepOriginalOwnership |
| 每个官方 Provider/自定义 Provider 分支 | 各 Provider Tests 独立 SqlUnitOfWorkProviderContractTest；ProviderAndDbKey_ShouldChooseCorrectConnectionFactory、UnsupportedTransaction_ShouldFailBeforeOpen、NamedPrimaryEndpoint_ShouldRequireFrozenPrimaryContext；按真实能力覆盖成功和失败 |
| EfCoreSqlUnitOfWorkConnectionProvider | Bing.EntityFrameworkCore.Tests / EfCoreSqlUnitOfWorkConnectionProviderTest / Mapping_ShouldResolveSameScopedContext、UnmappedSource_ShouldReturnNull、ConflictingMappings_ShouldReject、Registration_ShouldBeIdempotentAndPreserveCustomServices、DifferentTenantOrSnapshot_ShouldNotReuseContext |
| EfCoreSqlUnitOfWorkResource | Bing.EntityFrameworkCore.Tests / EfCoreSqlUnitOfWorkResourceTest / EnsureReady_ShouldCreateOneEfCurrentTransaction、Dispose_ShouldCloseOnlyConnectionOpenedHere、CommitAndDispose_ShouldNotRepeat、ExistingForeignTransaction_ShouldReject、SameContextInRequiresNew_ShouldReject、PrepareFailure_ShouldReleaseOnlyOwnedResources |
| EF Shared 与默认 Dapper/Independent | Bing.EntityFrameworkCore.Tests / EfCoreSqlQueryUnitOfWorkContractTest / ApplicationFirstAndRepositoryFirst_ShouldShareAndRollback、PrecreatedSharedQuery_ShouldJoinMappedResource、Independent_ShouldIgnoreAmbientSqlUow、UnmappedShared_ShouldKeepLegacyOwnership、SharedReader_ShouldBlockSameResourceExecution |
| EF UnitOfWorkBase 保存/旧 Commit | Bing.EntityFrameworkCore.Tests / EfCoreMappedUnitOfWorkSaveChangesTest / LegacyCommit_ShouldFlushWithoutPhysicalCommit、AllSaveChangesOverloads_ShouldUseManagedTransaction、RepeatedFlush_ShouldDispatchEventsOnce、Rollback_ShouldSuppressPostCommitNotifications、SaveChangesFalse_ShouldPreserveAcceptAllChangesContract、TrackedOnlyChanges_ShouldRequireExplicitFlush、CustomTransactionOverrideWithActions_ShouldRejectBeforeDispatch |
| EF StoreBase 清理 | Bing.EntityFrameworkCore.Tests / StoreBaseSqlQueryOwnershipTest / Dispose_ShouldReleaseCreatedQueryOnlyOnce、DisposeWithoutSql_ShouldNotCreateQuery；同时验证不关闭共享连接 |
| AspectCore 新旧边界 | Bing.Aop.AspectCore.Tests / 现有 Uow interceptor 测试增加 ActiveRoot_ShouldDeferCommitAfter、CompletedRoot_ShouldNotSaveAgain、RolledBackRoot_ShouldRejectLateSave、NoRoot_ShouldPreserveLegacyOrder |
| FreeSQL QueryStoreBase | Bing.FreeSQL.Tests / QueryStoreSqlUnitOfWorkContractTest / SqlQuery_ShouldShareWithApplicationQuery、SqlQueryRollback_ShouldNotClaimNativeFreeSqlParticipation、StoreDispose_ShouldNotCloseSharedResource |

资源 key 是新局部缓存键，直接测试 hit/miss、同 key 命中、各维度分离、失败不缓存。现有实体映射缓存键/表命名/跨库 JOIN 校验不计划更改；若实际改到它们，按 AGENTS 增加接口契约、独立默认实现和每个受影响 Provider 职责测试，不允许综合测试替代。

### 8.2 SQLite 两库真实执行（net6.0 与 net8.0 均必跑）

新增 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqlMultiDatabaseUnitOfWorkContractTest.cs` 及必要 fixture。使用两个内容不同的唯一临时 SQLite 文件，建表/清理均受 fixture 管理，禁止共享单个 `:memory:` 连接冒充两库。

1. 应用服务与仓储构造注入不同 ISqlQuery，外加 Executor/工厂产物；同 key 实际 connection/transaction identity 相同（测试用计数工厂或诊断，不扩张公共 API）。在不同层写入后不 Complete/显式 Rollback，外部新连接验证两次写入均不存在；提前 Dispose 任一 factory Query 不关共享连接。
2. A/B 两库读出不同种子并在内存合并；分别写入后 Complete 各自成功，事务/连接不同，连接打开/释放次数准确。第二库 commit 故障用可控连接包装注入，真实第一库结果保留；对未知提交结果不虚构确定性。
3. 参与嵌套共享回滚；requiresNew 使用新对象/新资源，内层完成后恢复外层。SQLite 单文件写锁限制下，独立嵌套的资源身份用同库只读/先后非重叠事务证明；提交隔离用两库证明，不要求同时持有两个写锁的 SQLite 成功。
4. 业务异常、打开/Begin/命令/提交阶段取消与故障；rollback/dispose 继续执行，不使用已取消 token；主异常与清理异常保留。
5. 同 key 两个 Query + 活动 reader 的并发第二次执行被拒绝；reader dispose 后可重用。流式完整枚举、提前 break、读取异常、取消、Multiple partial read，reader 与 command/租约均清理。Uow 在 active reader 时 Complete/Dispose 的负例必须验证状态未被破坏，释放 reader 后能正常收尾。
6. 无 Uow Query 拥有独立连接，旧显式事务回归；Uow 前创建/提前执行/结束后复用/切换 Uow 全状态表。
7. filter A disable/restore、B 不受影响；同 Uow 不同 MappingProfile 的完整 SQL/参数不串；命中共享资源后重复渲染不追加重复谓词。仍验证 SqlOptions/ambient/metadata 默认优先级与数据库快照。
8. 两个业务 DI scope 并行各自执行、独立 Uow 和资源，异常只影响自身；不能把普通 new Query 当作同 Uow 中并行共享连接的解决办法。

身份兼容验证只针对配置和所有权：同一已有 custom resolver/集成认证字符串用可控连接验证无需附加服务；未知但可执行配置使用不同资源仍由 Uow 回滚，不能退回自动提交。真实 SQLite 用包装 resolver 验证同配置共享与不同配置隔离；不为此建立认证方式组合矩阵或连接外部集成认证服务器。

SQL 输出一律断言完整独立字面量与参数，不用 Contains，不从被测 formatter 计算 expected。行为集成须验证实际行集和回滚后的外部读取，不能只靠 connection reference 相同断言。

### 8.3 混合 Provider、外部门控与消费者

**EF 桥接为本地必需 gate（net6），不能用原 Shared 回归替代：**

1. 将 7.4 示例转为真实 SQLite consumer，应用先/仓储先分别覆盖 rollback 与 commit，外部新连接核对行数 0/2；还要覆盖原生 SaveChanges 先于 SQL。断言相同 connection/current transaction、一次物理提交/回滚、Query/Store 释放不关共享连接。
2. 旧 manager.Commit、四个 SaveChanges 重载实际 flush 后，另一独立连接在 Complete 前看不到写入；不 Complete/异常/取消后外部行集为空。SaveChanges(false) 保留 EF 状态语义；只跟踪未 SaveChanges 的实体不被 Complete 自动保存。
3. 动作拿到同一 IDbTransaction；重复 flush 的领域事件不重复分发；领域事件清理、SaveChangesAfter、ICommitAfter 均不提前发生，rollback 不通知。两资源部分提交仅通知已提交库，全局不通知；通知抛错不重复提交。沿用现有审计、事件、并发异常测试扩展，不重复复制其全部矩阵。
4. 用最小派生 context 模拟现有 Admin 的 TransactionCommit override，证明有 actions/领域事件时在派发前拒绝并回滚；执行既有 Admin CAP consumer/build，明确门控结果，不把“未跑外部 CAP”算成功。SQL-only 与无动作 flush 可参与，旧无 root override 路径保持。
5. reader 与 SaveChanges/另一 Query 同资源互斥；打开/Begin/取消失败和 initially-open connection 的所有权准确。原 context requiresNew 拒绝、新 DI scope 独立实例成功；未映射 Shared、Independent、无 Uow 都保持原行为。结合前述直接测试复用 fixture，不再扩展成全 ORM Uow 重写测试。

- 本地直接测试至少 MySQL/PostgreSQL/SQL Server/Oracle/SQLite 与 Doris 分支的路由、事务能力、Provider 拒绝和自定义 registry 不变；一个 Uow 同时登记两种 Provider 的可控连接，验证正确 runtime/factory、事务边界和独立清理。此证据明确为可控 Provider contract，不冒充真实异构数据库 E2E。
- 真实异构库 E2E 仅在既有 Provider 门控及专用测试连接可用时运行。否则 `BLOCKED_EXTERNAL`，本地任务不得无限重试或启动生产数据库；SQLite 两库真实证据不可省。
- 外部 gate 保持 `RUN_INTEGRATION_TESTS`（仅本地受控多 Provider）或 `RUN_MYSQL_INTEGRATION_TESTS`、`RUN_POSTGRESQL_INTEGRATION_TESTS`、`RUN_SQLSERVER_INTEGRATION_TESTS`、`RUN_ORACLE_INTEGRATION_TESTS`。沿用 `docs/testing/database-integration-tests.md` 与 `eng/ci/Invoke-ProviderIntegrationTests.ps1`，不新设全局放行开关。
- EF 实际只 target net6.0；FreeSQL Tests net6/net8。FreeSQL 用明确调用 protected Sql 的派生仓储验证跨层共享；另用现有原生 CRUD/事务测试保持边界，不冒充 native CRUD 同事务。后台示例在可编译 consumer fixture 验证一次 operation、一轮 worker scope、一次 job scope 的生命周期；无需启动 Hangfire server。

### 8.4 分层执行命令

以下命令基于真实 csproj 和仓库 TFM；新增 filter 指向本计划拟新增类。路径从仓库根执行，执行者为每个命令添加独立 UTF-8 stdout/stderr、退出码和 TRX 路径。项目构建共享 output 路径，默认顺序执行，避免已有 F008 遇到的构建锁竞争。

```powershell
# L0 + L1：先实现对应类，再执行精准测试；不能把零匹配当通过。
dotnet test framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj -c Release -f net8.0 --filter 'FullyQualifiedName~UnitOfWorkScope' -nologo -v minimal
dotnet test framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj -c Release -f net8.0 --filter 'FullyQualifiedName~SqlUnitOfWork|FullyQualifiedName~SqlFactoryUnitOfWork' -nologo -v minimal
dotnet test framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net8.0 --filter 'FullyQualifiedName~UnitOfWorkContractTest' -nologo -v minimal
dotnet test framework/tests/Bing.EntityFrameworkCore.Tests/Bing.EntityFrameworkCore.Tests.csproj -c Release -f net6.0 --filter 'FullyQualifiedName~EfCoreSqlUnitOfWork|FullyQualifiedName~EfCoreSqlQueryUnitOfWork|FullyQualifiedName~EfCoreMappedUnitOfWork|FullyQualifiedName~StoreBaseSqlQueryOwnership' -nologo -v minimal
dotnet test framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj -c Release -f net8.0 --filter 'FullyQualifiedName~UnitOfWork' -nologo -v minimal

# 受影响项目收口：同一矩阵分别指定 net6.0/net8.0；示例为 net6.0。
dotnet test framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.EntityFrameworkCore.Tests/Bing.EntityFrameworkCore.Tests.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.FreeSQL.Tests/Bing.FreeSQL.Tests.csproj -c Release -f net6.0 -nologo -v minimal
dotnet test framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj -c Release -f net6.0 -nologo -v minimal

dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj -c Release -nologo -v minimal
dotnet build framework/src/Bing.EntityFrameworkCore/Bing.EntityFrameworkCore.csproj -c Release -nologo -v minimal
dotnet build framework/src/Bing.FreeSQL/Bing.FreeSQL.csproj -c Release -nologo -v minimal
dotnet build samples/Bing.Samples.Hangfire/Bing.Samples.Hangfire.csproj -c Release -nologo -v minimal
git diff --check
```

Final Candidate 必须新增一次受影响完整矩阵，而非仅复用 F008：Uow、Core、Data.Sql、Data.Sql.CustomProvider、Dapper.MySql/PostgreSql/SqlServer/Oracle/Sqlite Tests、SQLite Integration、FreeSQL、Aop.AspectCore Tests 均 net6/net8；EF Tests net6；`modules/admin/tests/Bing.Admin.EFCore.Tests` 和 `Bing.Admin.FreeSQL.Tests` 按实际 net6 consumer 配置。各 Provider 的生产构建由这些测试及显式 affected build 覆盖；记录所有受影响 production 项目 build 结果，缺项补建，不默认跑全仓 85 项。外部 Provider 集成默认不启用。

SQLite 受控 Provider evidence contract 的 gate/结果目录沿用现有 harness：普通全套的 gated skip 单独记录；若本次连接/事务路径使旧证据失效，执行既有根级 `artifacts/test-results` 受控路径并引用产物，不能把 skip 当本地契约已通过。只有本任务新鲜证据或证明 relevant source scope 未变的历史证据可计入。

## 9. 性能与资源证据

- 受影响热路径是每次执行的 root/current/key 查找、租约、首次资源建立与释放。资源登记预计 O(1) lookup；Complete/Dispose O(R)，R 为本 Uow 触及资源数；内存 O(R+活动句柄)，不缓存 Query/Builder/结果。
- 在 `Bing.Data.Sql.Benchmarks` 添加独立 `SqlUnitOfWorkBenchmarks` 或等价有界 harness 场景，保留现有 baseline、预算和 F008 scoped fixture。场景必须调用真实默认运行链，不能只 benchmark Dictionary。
- net8 Release：资源 lookup/lease 场景预热 100 次，3 组各 1000 次；SQLite 两文件 E2E 预热 10 次，3 组各 100 次业务操作，分别无 Uow、同库多 Query 事务 Uow、两库非事务汇总。每个 probe 整体硬上限 60 秒，提前声明 EXPECTED_SUCCESS，超时受控结束并记录边界，不仅延长 timeout。
- 记录时间、分配（说明同步线程分配还是进程级、异步不能滥用线程计数）、连接创建/打开/关闭/释放次数、事务创建/提交/回滚次数、异常数、实际完成数、资源峰值和退出码；声明请求并发与实际并发，禁止虚构 P95/吞吐或将跳过计为通过。
- 新共享 Uow 无历史等价 API：before=NOT_APPLICABLE；无 Uow 路径如做 before/after，只冻结一次 F008 工作区范围 baseline，环境/输入/TFM/harness 一致，不用 master 古老 API 伪造对照。不为本任务重建全量历史基线。
- Mapping cache、格式化、Builder clone 算法未改时，基线场景不适用并说明；若执行者实际改到这些路径，补对应局部场景而不是启动 100K/500K/1M 矩阵。
- 这里只提供性能证据，不批准预算。正式阈值未授权时 `THRESHOLD_STATUS=BLOCKED_APPROVAL`，不因此循环修代码。

## 10. 分阶段任务（交 Luna/max）

### Phase A — 契约与业务工作单元

**T01：冻结候选与回归入口**

- 目标：保留 F008 工作区，建立可追溯起点。
- 现状/证据：第 1–3 节；现有 Uow 无 Current，多 Provider 工厂已存在。
- 修改范围：本任务 execution/artifacts、必要新测试；不改旧任务报告。
- 步骤：读取规则和计划；记录 HEAD/status/相关 diff、TFM/工具版本；按第 8 节 CIA；建立新契约测试和现状最小失败证据，编译失败与运行失败区分，不先跑全套。
- 依赖：无。
- 验证：UTF-8、路径/版本、测试匹配数；记录原有脏文件清单。
- 风险：把 HEAD 当候选会漏 F008；不得 reset 或重生成历史证据。
- 验收：起点明确，既有修改完整，新增失败由缺失功能而不是错误 fixture 导致。

**T02：实现 Bing.Uow companion API**

- 目标：可嵌套、可恢复、可登记资源的业务边界。
- 现状/证据：旧 manager 只有 HashSet；新 API 见第 4 节。
- 修改范围：Bing.Uow 新文件与独立测试；必要引用；旧 manager 不改变。
- 步骤：完成 DI/Current/frame、root/参与句柄、资源登记和非阻塞 key 租约；实现状态机、部分提交、每资源/全局成功回调、通知异常与提交结果分离、不可取消清理；增加中立连接协作接口，补齐 XML 中文契约。
- 依赖：T01。
- 验证：Uow 精准测试 net8，再 net6；旧 UnitOfWorkManagerTest；自定义旧 manager consumer；每个新默认实现独立测试类。
- 风险：AsyncLocal 子流污染、LIFO 误恢复、Complete/lease 竞态。
- 验收：第 4–5 节全部直接测试通过，旧 Register/Commit 无变化。

### Phase B — SQL 共享资源与全执行路径

**T03：实现资源键与拥有者**

- 目标：相同冻结配置与来源复用，其他配置隔离，默认和自定义 resolver 均可用。
- 现状/证据：现有 Query 自有连接；物理库地址不足以作为复用键。
- 修改范围：Dapper.Core ProjectReference、UnitOfWork internal 类型、AddSqlCore 内部注册、职责测试。
- 步骤：按第 6 节实现冻结配置键、provider、resource；先发现显式连接拥有者，再允许创建 Dapper 自有资源；复用已有连接 factory/事务 adapter；不明配置使用 Query 专属但仍属于 Uow 的资源，处理能力拒绝与异步准备失败。
- 依赖：T02。
- 验证：独立 key/provider/resource 测试，包括每维 hit/miss、预注册、custom resolver/集成认证兼容、配置不明时隔离且回滚、映射优先于建连接、异常和 Dispose 次数。
- 风险：凭据泄漏、错合并租户/端点、循环依赖或破坏 API gate。
- 验收：完整 key 契约成立，SQL 公共 baseline 无变化，构建通过且无 analyzer 绕过。

**T04：接入 Query/Executor/reader/显式事务**

- 目标：同库跨层共享且所有入口安全释放。
- 现状/证据：普通、Mutation、分页、stream、Multiple 各自执行入口与租约；显式 scope owner 直接 GetExecutionConnection。
- 修改范围：SqlQueryBase 新 partial、相关 QueryPlan/Transaction/Executor/Multiple 路径、SqlTransactionScopeFactory 内部标记、对应测试。
- 步骤：实现首次执行亲和状态；组合根/资源租约；同步与异步准备；借用所有权；错误标记 rollback-only；映射 EF Shared 是显式外部连接的精确参与例外，其他显式资源与 runtime rebinding 边界不变；分页避免重入、reader 生命周期挂接。
- 依赖：T03。
- 验证：精准 binding/lease/transaction/stream/multiple 测试；SQLite 同库回滚、无 Uow 回归、跨 Uow 表。
- 风险：只覆盖 ToList、async 中同步开连接、Query.Dispose 误关共享连接、Clone 绕过租约。
- 验收：所有入口均经过资源准备/释放，两个不同 Query 同 key 不能并发，顺序描述可复用。

### Phase C — 真实执行、消费者与性能

**T05：两库与 Provider 直接证据**

- 目标：两库真执行、混合 Provider 契约与限制可审计。
- 现状/证据：现有 SQLite fixture 和各 Provider Tests；外部测试有 gate。
- 修改范围：SQLite 新 contract fixture/类、各 Provider 独立职责测试、CustomProvider tests。
- 步骤：完成第 8.2 八组行为；本地混合 Provider 可控连接；完整 SQL/参数断言；连接配置/上下文/Profile/filter/缓存隔离；外部 gate 保留。
- 依赖：T04。
- 验证：先精准，再 SQLite 全套 net6/net8 和受影响 Provider 完整测试；实际行集与资源次数并证。
- 风险：SQLite 单库写锁被误当实现失败、mock 被冒充异构真库、测试零匹配。
- 验收：两库两 TFM 新鲜证据；未运行外部明确分类，不要求生产库。

**T06：EF 明确桥接与业务 scope 消费者**

- 目标：现有 EF Shared 仓储与默认 Dapper 在两个访问顺序下都共享，原生 EF flush 不提前提交，FreeSQL/后台边界清楚。
- 现状/证据：第 7 节真实路径；EF 旧 TransactionCommit 自管事务、保存后通知、Admin CAP override 必须区别处理。
- 修改范围：EF 映射注册/provider/resource、EfCoreSqlQueryFactory 映射校验、UnitOfWorkBase 参与保存分支、StoreBase 局部释放；AspectCore 延后 ICommitAfter；对应直接测试与 EF/FreeSQL/后台 consumer；不改 FreeSQL 原生 Uow 或 Admin CAP 实现。
- 步骤：先实现并直接验证映射/唯一 owner/双顺序；再覆盖四个 SaveChanges 重载、旧 Commit flush、动作与领域事件 session、资源成功回调；处理 CAP override 的明确拒绝和旧路径替代；补 Store 清理；最后验证 Independent/未映射/无 root、FreeSQL 显式 SQL 仓储和业务 scope 示例。
- 依赖：T04；T05 与本步骤只可分派边界清晰且不写同文件的测试子任务，整体设计由主执行者集成。
- 验证：第 8.3 EF 本地必需 gate 全部覆盖；EF net6、FreeSQL net6/net8、Aop、Admin 必要 consumer/build；测试不启动外部 Hangfire 服务。
- 风险：应用先用抢建 Dapper 连接、旧 SaveChanges 提前提交/回调、bool 重载绕过、CAP override 被静默跳过、重复保存或关闭、DI 对象被提前释放。
- 验收：真实行集证明跨层回滚/提交而非仅引用相同；旧回调在物理提交后；所有权矩阵逐项有证据；示例可编译且资源释放准确。

**T07：有界 probe 与最终受影响验证**

- 目标：了解新增热路径成本并确认最终候选。
- 现状/证据：新 lookup/租约/生命周期受影响，非映射算法变更。
- 修改范围：局部 benchmark/harness、artifacts、execution 的 CIA/验证表。
- 步骤：先正确性精准测试，再执行第 9 节有界 probe；冻结 final candidate，跑第 8.4 完整 affected matrix/build；记录 baseline/evidence 复用依据。
- 依赖：T05、T06。
- 验证：每项目/TFM passed/failed/skipped/not-run、TRX/退出码、probe 指标；无 API baseline/版本变化。
- 风险：无限重跑或把未批准预算转成实现失败。
- 验收：本地必需 gate 有可审计结果；外部/阈值独立列出；无全量重建浪费。

### Phase D — 文档与独立验收交接

**T08：交付与 Review → Fix 收口**

- 目标：实现可维护、迁移可操作、审查独立。
- 现状/证据：F008 已有报告结构；本计划是新功能，不回写旧结论。
- 修改范围：本任务 execution.md、migration.md、traceability.md、artifacts；必要 `Bing.Uow/README.md`、`docs/sql/sqlquery-usage.md`、`docs/sql/migration-guide.md`、`docs/testing/database-integration-tests.md`、事务指南局部更新。
- 步骤：同步精确新 API、显式 EF 源映射及两种顺序、原生 SaveChanges flush 责任、资源亲和/所有权、CAP 旧路径替代、FreeSQL SQL 与 native CRUD 边界、后台示例；维护实际生产符号→方法→证据；执行者自查后交父会话安排 Sol/high 独立 review.md；有 OPEN_ACTIONABLE 才交修复。
- 依赖：T07。
- 验证：文档链接/编码/diff、示例编译、traceability 无空覆盖；Reviewer 对照最终源代码和日志，不只读 execution 自评。
- 风险：执行者自行写 PASS review、历史证据被覆盖、把外部未运行伪装完成。
- 验收：交付完整且状态真实；最多三轮可执行问题修复，遵守第 11 节停止条件。

## 11. 报告、证据与自动停止

实施交付：

- `plan.md`：本文件，若实施有必要偏差，记录准确原因和最终选择，不追加冲突方案。
- `execution.md`：当前 candidate、T01–T08 状态、CIA、完整命令/结果摘要、简洁 round index、当前 findings。
- `review.md`：由父会话安排的 Sol/high 独立生成，Executor 不修改其裁决。
- `migration.md`：注册/API、顺序复用、首次执行绑定、requiresNew、主库路由、显式 EF 映射/SaveChanges 与回调、保守配置隔离、DI/Factory/ORM 所有权、CAP 与 FreeSQL 原生边界、部分提交、后台/Job 示例。
- `traceability.md`：最终生产文件/符号/关键行为 → 测试项目/TFM/类/实际方法 → TRX/日志/候选；新默认实现逐一映射，不能把整套测试数量代替职责证据。
- `artifacts/`：原始 UTF-8 日志、TRX、probe、必要 API 增量清单和候选 manifest；历史产物不可覆盖，不用宽泛 glob 混入旧失败/旧 candidate 结果。

每个 Finding 只能属于 OPEN_ACTIONABLE/CLOSED/BLOCKED_APPROVAL/BLOCKED_EXTERNAL/NOT_APPLICABLE/ACCEPTED_LIMITATION/VERIFIED_BOUNDARY/DEFERRED 之一，给出根因与验收标准。Review 至少分别报告 IMPLEMENTATION_STATUS、TEST_STATUS、PERFORMANCE_EVIDENCE_STATUS、RESOURCE_EVIDENCE_STATUS、EXTERNAL_GATE_STATUS、RELEASE_STATUS、GOAL_STATUS。

仅 OPEN_ACTIONABLE > 0 进入 Fix；本任务最多三轮可执行问题修复。连续两轮相同 Finding/根因/验收/候选相关范围且无新输入、环境或证据，立即 STOPPED_NO_PROGRESS。同一 Finding+Candidate+Workload+Environment+Hypothesis 最多一次诊断和一次正式确认；仅提高 timeout 不是进展。PARTIAL/NOT_VERIFIED/BLOCKED/TIMEOUT/RELEASE_STATUS!=PASS 不触发下一轮。

每轮按 GOAL_RULES 输出 Task/Round、Implementation x/8、Release Gates x/y、Changed Scope、L0–L5、Reused Evidence，以及 Completed/Open Actionable/Blocked Approval/Blocked External/Not Applicable/Accepted Limitations/Verified Boundaries/Deferred、No-Progress Check、Next Action、Goal Status。无 Open Actionable 时 Next Action=STOP；允许实施 PASS、外部发布门禁 BLOCKED。

本期已确定的边界：不保证跨库原子性；显式映射的默认 EF Shared/原生 flush 参与，但未映射 Shared、Independent、CAP 自管事务动作、FreeSQL native CRUD 不自动桥接；Complete 不代替显式 SaveChanges；不支持同一连接并发或事务动作重入框架 Query；不允许已执行 Query 跨 root 迁移；EF 同 context 的 requiresNew 需换业务 DI scope；SQLite 同库独立写事务受锁限制；不自动转移任意外部资源所有权。泛型数据库 Query、跨库 JOIN、分布式事务后续任务处理。这些不是待循环“修复”的缺陷。

## 12. 本轮计划完成检查

- 已读规则、F008 三文档、当前创建/执行/事务/资源释放路径、本地 master Transient 注册、ORM/后台消费者和测试构建配置。
- 已明确现状和缺口、最小兼容 API、资源键、所有权、嵌套状态、创建与执行时机、失败及部分提交边界。
- 已冻结 Phase/T01–T08、直接测试与两库 net6/net8、消费者/Provider 门控、受影响完整验证和有界 probe。
- 本轮仅修订本计划；不声称功能、测试或独立审查已经完成；按本阶段委派约束不发送外部通知，不进入实施。后续实现已有授权，无需再次确认常规设计决定。
