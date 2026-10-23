# 阶段 B：业务操作、自动工作单元与保存协议

- Task ID：20261022-abp-phase-b-operation-uow。
- 日期：2026-10-22；状态：APPROVED_FOR_IMPLEMENTATION。主线程已核对修订后的契约、验证范围及命令，按用户已批准路线图交 Luna/max 实施。
- 仓库及所有命令的 WorkingDirectory：H:/Bing_Framework/Bing.NetCore；工具调用必须显式设置 workdir，不依赖默认目录。
- 范围：已批准路线图 G02/G03/G09 及必要 G12 说明；先 Dapper/EF，FreeSQL 独立适配。
- Astra/high 规划轮已结束；后续实施由 Luna/max 使用 execute-plan 执行，独立验收由 Sol/high 完成。

## 1. 前置、依据与不变量

依据：[父路线图](../20261022-abp-infrastructure-roadmap/plan.md)、[阶段 B 输入](../20261022-abp-infrastructure-roadmap/phase-b-design-inputs.md)、[阶段 A final-report](../20261022-abp-phase-a-tracing-contracts/final-report.md)、[阶段 A Review 2](../20261022-abp-phase-a-tracing-contracts/review.md)，以及根 AGENTS.md、C:/Users/jianx/.codex/GOAL_RULES.md、.agents/skills/create-plan/SKILL.md。阶段 A FIX-001/FIX-002 均 CLOSED，OPEN_ACTIONABLE=0，满足 B 设计前置，不重新打开。

1. 保留 SDK、TFM、依赖/程序集/包版本、API baseline、性能/资源预算。当前 global.json SDK=10.0.400；默认生产 netstandard2.0；EF/Application.AspectCore 为 net6.0；测试只用各项目已有 TFM。Dapper 2.1.28、CAP 8.0.1、AspectCore 3.0.0、现有 EfCore6Version=6.0.* 声明不变，不新增 ABP 包。
2. 保留大量既有工作区修改；禁止 add/commit/push/tag/reset/restore/clean、PR、发布和外部通知。用户禁止通知优先于技能的 workflow-notify 步骤。
3. 阶段 A 历史 L4 的 12,625 Passed / 2 Failed / 1,108 NotExecuted 和 docs 的 UseSqlite 悬空符号 FAIL 原样保留，GC 因果未定。仅本次规划不运行门禁；实施中按 L1→受影响 L2/L3，阶段最终候选按父路线图要求执行一次 Release solution build、关闭外部门控的完整默认 net6/net8 L4 及现有文档检查（§11.3）。旧 FAIL 不豁免新共享 Uow/IO 改动的最终验证，新结果也不改写旧记录。若再次出现原 Core GC 两项失败，照实记录并按证据分类，不扩修 GC、不为追绿重复运行。允许 B05 最小修正 UseSqlite 悬空示例，以实际注册入口和编译证据验收，不扩为无关文档改写。
4. 新接口已属批准增量方向，常规签名由本计划冻结，不另请示。旧公共接口不增加必实现成员，旧消费方兼容。
5. 多源仍是有序独立本地事务；后库失败不能撤销前库提交。不建分布式事务、补偿、调度器、新事件平台或 C/D 能力。
6. 独立操作必须新 DI scope 并重新解析服务/Context；旧 Begin(requiresNew:true) 仍只是 root 原语。普通嵌套共享 root，Query 可变状态不共享。
7. 保留 source-backed ValueTask 单次消费、原始异常/取消令牌、嵌套代理、终端分派和完成回调协议；数据源身份、租户、读写路由和连接所有权校验不放宽。

## 2. 当前真实行为与完成度

以下路径相对仓库根；存在的类不等于 B 已实现。

| 范围 | 已确认文件与符号 | 当前行为/缺口 |
| --- | --- | --- |
| Uow | framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs、UnitOfWorkScope.cs；tests/Bing.Uow.Tests/UnitOfWorkScopeTest.cs（tests 位于 framework 下） | Scoped manager 使用实例 AsyncLocal frame；普通 Begin 参与，requiresNew 新 root 无新 DI scope。LIFO、租约、rollback-only、部分提交、非取消清理已有直接测试。缺自动保存、超时、业务执行器。 |
| AOP | framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs | EnableBingUnitOfWork 成功后旧 manager.CommitAsync，再安排 ICommitAfter；无 Begin。ValueTask 适配器不受 Aspect scope 抑制，缓存单次结果，保留非虚终端分派。 |
| 标记 | framework/src/Bing.Ddd.Application.Contracts/Bing/Application/Aspects/UnitOfWorkAttribute.cs、BingUnitOfWorkScope.cs | Order/Scope 为已有契约；None/Aspect/Nested 是拦截抑制策略，不是 requiresNew。 |
| EF 发现 | framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs 的构造/RegisterToManager；Extensions/EfCoreSqlQueryServiceCollectionExtensions.cs | 构造向旧 manager 登记；AddEfCoreSqlUnitOfWork 显式映射。已解析、只跟踪变更、未 SQL 的 Context 不一定已建 resource。 |
| EF 保存 | UnitOfWorkBase.CommitAsync、TryGetMappedSqlResource、SaveChangesWithMappedResourceAsync、RegisterMappedSaveCallbacks | 任意 legacy CommitAsync 不等于 flush；未映射可创建/提交物理事务，非事务映射当前回落旧保存。每次显式成功 flush 有一次保存后通知；事件会话提交后清理。 |
| EF 资源 | Core/EfCoreSqlUnitOfWorkConnectionProvider.cs | Commit 不调用 SaveChanges；测试 Integration/EfCoreSqliteUnitOfWorkContractTest.Complete_ShouldNotSaveTrackedOnlyChanges 明确旧手工 root 不自动保存。 |
| Dapper | framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.UnitOfWork.cs、SqlUnitOfWorkResourceAccessor.cs、DapperSqlUnitOfWorkResource.cs | 首次执行绑定 root；映射 EF 优先，否则 owned Dapper resource。预取消毒化、租约、亲和已有；无跟踪 flush。 |
| 事件 | framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/Events/DomainEventDispatchSession.cs；framework/src/Bing.Events/Cap/CapUnitOfWorkMessagePublisher.cs | 事件会话有32轮限制和去重；CAP已有Outbox资源共享，不复制协议。 |
| 三入口 | samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs、Infrastructure/Background/ScenarioBackgroundWorker.cs、Jobs/SqlUnitOfWorkJob.cs | 应用手工Begin，Worker每项建scope，Hangfire当前缓存注入服务并传None；缺公共执行器。 |
| FreeSQL | framework/src/Bing.FreeSQL/Bing/Uow/UnitOfWorkBase.cs、DeferredCommitUnitOfWork.cs | 延迟内部提交不是多源资源适配，B04尚未完成。 |

G02/G03/G09 均为已有基础、B 增量未实现；不按测试数量计算功能百分比。发布前缺 owner 生命周期、已解析 EF 发现/纯保存、独立 DI 操作、三入口取消与对应证据。保留旧 manager 是必要兼容，不做无关 cleanup。

### 固定 ABP 10.6.1 官方参考

2026-10-22 读取以下固定 tag 官方源码；设计采用职责边界，不直接复制默认策略。

| 参考 | 源码事实及 Bing 决定 |
| --- | --- |
| [UnitOfWorkInterceptor](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkInterceptor.cs) | reserved 与 Begin/Complete 分支分开；Bing 不复制 reserved middleware 或 Get 前缀事务推断，显式 opt-in。 |
| [UnitOfWorkManager](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkManager.cs) | 新 Uow 拥有 DI scope，释放恢复 outer；Bing executor新scope重解析服务，保留 scoped manager。 |
| [UnitOfWork](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWork.cs) | 保存、物理提交、通知分阶段；Bing保留有序本地提交和部分提交结果。 |
| [AbpUnitOfWorkOptions](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/AbpUnitOfWorkOptions.cs) | 有隔离和timeout；Bing事务默认true不变，timeout用可空TimeSpan与协作取消。 |
| [EfCoreDatabaseApi](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/Uow/EntityFrameworkCore/EfCoreDatabaseApi.cs) | 保存适配独立于事务API；Bing新增映射专用保存入口，不能包装任意legacy Commit。 |

## 3. 冻结增量公共签名

以下为实施目标，尚不存在；省略using和中文XML注释，实施时补全。实现类默认internal，不新增包/生产项目。

### 3.1 Bing.Uow 程序集，命名空间 Bing.Uow

    public interface IUnitOfWorkExecutionScope : IUnitOfWorkScope
    {
        CancellationToken CancellationToken { get; }
    }
    public interface IUnitOfWorkOperationManager : IUnitOfWorkScopeManager
    {
        IUnitOfWorkExecutionScope BeginOperation(
            UnitOfWorkScopeOptions options = null,
            CancellationToken cancellationToken = default);
    }
    public interface IUnitOfWorkSaveChangesHandler
    {
        void SaveChanges(IUnitOfWorkScope scope);
        Task SaveChangesAsync(IUnitOfWorkScope scope,
            CancellationToken cancellationToken = default);
    }
    public interface IBingOperationExecutor
    {
        Task ExecuteAsync<TService>(
            Func<TService, CancellationToken, Task> operation,
            UnitOfWorkScopeOptions options = null,
            CancellationToken cancellationToken = default) where TService : class;
        Task<TResult> ExecuteAsync<TService, TResult>(
            Func<TService, CancellationToken, Task<TResult>> operation,
            UnitOfWorkScopeOptions options = null,
            CancellationToken cancellationToken = default) where TService : class;
    }
    public static class BingOperationServiceCollectionExtensions
    {
        public static IServiceCollection AddBingOperations(this IServiceCollection services);
    }

在现有 sealed UnitOfWorkScopeOptions 仅新增：

    public TimeSpan? Timeout { get; set; }

- BeginOperation 在当前 DI scope 建owner/participant，无requiresNew参数；独立业务调用统一executor。旧Begin签名保留。
- 新旧manager接口解析为同一个scoped默认实例，Current仍返回root。自定义manager未实现增量契约时，新入口在目标前明确失败，不覆盖注册或退回legacy Commit。
- ExecutionScope.CancellationToken为caller+root deadline，participant再合成自身caller；没有额外源时复用原token。旧接口实现者无需实现新接口。
- AddBingOperations幂等，注册manager alias、singleton executor（仅持IServiceScopeFactory）及内部启用标记；不注册代理，不打开连接。此opt-in容器中的手工Begin root也启用保存handlers；单独AddUnitOfWorkScopes保持旧“不自动保存”。
- handlers为Scoped，TryAddEnumerable按类型去重、登记顺序调用，仅opt-in root调用。Dapper无空handler；handler不能拥有root/Context/DI scope或调用旧manager.Commit。
- Timeout=null，无新默认限时；非正值或超出目标运行时共同支持CTS定时范围时Begin拒绝。IsTransactional=true、IsolationLevel=null不变。非事务+隔离级别仍拒绝。选项复制冻结。
- executor返回Task，原ValueTask在委托中await一次；不新增歧义重载。结果必须已物化，不能把依赖scoped服务的reader/Query/惰性流带出operation。

### 3.2 Application.AspectCore，命名空间 Bing.DependencyInjection

现有 BingUnitOfWorkExtensions 新增：

    public static IServiceCollection EnableBingAutomaticUnitOfWork(
        this IServiceCollection services,
        Action<BingAutomaticUnitOfWorkOptions> configure = null);

新增：

    public sealed class BingAutomaticUnitOfWorkOptions
    {
        public UnitOfWorkScopeOptions DefaultOptions { get; set; }
            = new UnitOfWorkScopeOptions();
    }

新入口隐含AddBingOperations并复用原selector/callback/validation；一个方法只能选一种生命周期。配置按注册顺序应用、最后显式赋值生效，运行时用快照，重复注册不重复拦截。

UnitOfWorkAttribute本阶段不增属性、不改Order/Scope。继续接口方法→实现方法→接口类型→实现类型优先级。默认事务用宿主DefaultOptions；逐操作策略用executor/显式外层BeginOperation，不按HTTP方法或方法名称推断。

新自动模式每个被标记调用均有轻量participant失败保护，包括旧Aspect scope会抑制的内层，以处理内层失败被业务捕获；只有owner保存/提交。旧模式保留原抑制规则，Scope.None不代表独立DI操作。

### 3.3 EF 保存注册，命名空间 Bing.EntityFrameworkCore

在现有 EfCoreSqlQueryServiceCollectionExtensions 增量增加：

    public static IServiceCollection AddEfCoreOperationSaving(this IServiceCollection services);

仅注册 scoped registry/handler，不启用操作、不创建 Context。已有 AddEfCoreSqlUnitOfWork<TContext>(key) 内部复用此注册；未启用 AddBingOperations 时 handler 不运行，所以旧 mapping 行为不变。自动操作中使用 EF 的宿主必须配置此入口（显式 mapping 入口已隐含）；即使暂未映射任何 Context，也能在完成前发现并拒绝 unmapped dirty Context。完全未注册 EF 保存适配的宿主不声明自动 EF 保存能力，不通过反射扫描全部 DI 服务补救。为独立入口增加幂等、无映射 dirty、缺旧 manager 的直接注册测试。

### 3.4 兼容面

不修改旧六组接口IUnitOfWork/IUnitOfWorkManager/IUnitOfWorkScope/IUnitOfWorkScopeManager/IUnitOfWorkResource/IUnitOfWorkConnectionResource的现有签名；Dapper/SQL公共面不扩大。EF registry、handler及纯保存入口均internal。

Uow/EF当前没有PublicAPI.*清单，以Uow编译/反射契约与旧consumer验证。现有SQL/Dapper的Shipped/Unshipped及eng/baselines.props不变；不以更新baseline消除意外API回归。新接口方向无需再次审批。

## 4. 注册、所有权与独立操作

### 4.1 新旧入口共存矩阵

| 组合 | 冻结行为 |
| --- | --- |
| 仅EnableBingUnitOfWork | 原成功后旧manager.CommitAsync/ICommitAfter；无自动Begin，默认取消/ValueTask行为不变。 |
| 仅AddUnitOfWorkScopes | 原手工root不自动保存；旧Complete_ShouldNotSaveTrackedOnlyChanges继续通过。 |
| 仅AddBingOperations | executor/BeginOperation可用，无标记自动代理；手工root也启用映射保存handler。 |
| EnableBingAutomaticUnitOfWork | 隐含AddBingOperations，有标记才自动边界；无标记方法可用executor显式包围。 |
| 旧+新自动注册，任意顺序/重复 | 自动分支唯一生效；无第二次旧Commit或ICommitAfter。 |
| AddBingOperations+旧标记注册，当前有opt-in root | 原适配器识别新root，走participant不调用旧manager.Commit；root外仍旧模式。 |
| 手工root+自动方法 | 参加外层；内层成功只关闭participant，手工owner Complete才保存/提交。 |
| 未映射/未适配ORM | 不自动Commit。检测到未映射EF待保存时在其他资源物理提交前失败；FreeSQL不宣称支持。 |

映射事务内已有EF Commit可继续flush；任意legacy Commit不成为新保存协议。opt-in root内显式调用未映射EF SaveChanges/Commit若要写入，应在I/O前拒绝并rollback-only；旧模式仍保留原autocommit。显式独立外部连接/未适配ORM写入不受新root原子性保证。

### 4.2 owner/participant矩阵

| 角色 | DI scope/root所有权 | 成功 | 失败/释放 |
| --- | --- | --- | --- |
| 自动方法无Current | 使用调用者已有DI scope，创建owner root | target→save→commit→通知 | 回滚未提交资源/释放root，不释放调用者DI scope |
| 普通嵌套 | 同DI scope、同root的participant | 只Complete participant | 失败/未完成毒化root；只pop自己的frame |
| 手工外层 | 调用者拥有DI/root | 调用者Complete | 内层不夺取提交/释放权 |
| executor每次调用 | 自建async DI scope、新manager、新root | 委托一次，owner完成 | root清理→DI清理→返回；不使用外层实例 |
| 旧Begin(requiresNew:true) | 同DI scope，新root暂挂外层 | 保留原语 | 外层已claim/pending EF不准迁入；隔离需求用executor |
| Query/Context/resource | Query由调用方、Context由DI；resource拥有其事务/自己打开的连接 | Query不提交共享事务 | root不Dispose DI Context，不误关借用连接 |

executor固定顺序：校验参数/预取消→建新DI scope→解析manager→BeginOperation→从新scope解析TService→委托一次并等待→Complete→root.DisposeAsync→DI.DisposeAsync→返回。构造/解析失败同样清理。Begin必须先于服务解析，覆盖构造时解析EF的情况。委托只捕获值参数，不捕获外层repository/Context/服务；不提供接收已有服务实例的重载。

TService的有状态依赖应Scoped/Transient，测试开启ValidateScopes；singleton executor不缓存业务服务。子操作失败被外层捕获不自动毒化外层root，向外传播才触发外层失败；子提交可在外层回滚后保留。不同scoped manager隔离root，外层await后仍为原实例。

## 5. 映射保存、事务、通知与释放

### 5.1 已解析Context清单与纯保存

1. EF新增scoped ResolvedEfCoreUnitOfWorkRegistry。AddEfCoreOperationSaving及mapping扩展注册registry/handler；UnitOfWorkBase构造最后只登记自身引用/顺序，不读ChangeTracker、不调用派生虚方法、不解析其他Context。登记失败/已释放对象不得保存。
2. 不只枚举已建resource，不遍历mapping去GetRequiredService。root前或root内已解析的tracked-only Context都进入清单；旧manager.GetUnitOfWorks可供诊断/去重，但缺旧manager不能导致漏保存。
3. 保存开始取得已完成解析实例快照，按引用去重/登记顺序遍历。pending检查包括DetectChanges/HasChanges、未执行事务动作、未分发领域事件；“已分发、等物理提交后清除”的事件不算还需保存。不能直接用旧HasPendingMappedState的“事件集合非空”触发重复flush。
4. 有真正pending才调用internal SaveMappedChangesForOperation/Async：映射/物理库/Provider/租户/读写准入→resource/lease→纯保存。失败不回落legacy TransactionCommit。无新变更/事件/动作不打开连接、不增加SaveChangesAfter。
5. EF-first/Dapper-first都使用同一映射resource；全部保存结束后才快照资源提交列表。保存前快照会漏掉tracked-only保存新建的resource，禁止这样实现。
6. 手工SaveChanges可多次flush；每次成功显式flush仍各有一次保存后通知（现有RepeatedFlush测试期望2次），领域事件沿同root/context会话去重。自动完成只保存尚有新状态的Context，不能增加空flush/重复通知。
7. SaveChanges(false)的已flush未接受状态必须单独识别。新自动完成在再次SQL/物理提交前拒绝，要求业务显式AcceptAllChanges；不能重复INSERT/UPDATE、不能自动Accept。旧手工模式原测试保持。
8. 自动最终保存只处理开始快照中的每个Context一轮，随后重查所有已解析实例；若回调新解析并弄脏其他Context，或重新弄脏已处理Context，在物理提交前失败/rollback-only，提示业务显式组织跨Context保存。不能静默丢数据或引入无界drain。已有单Context事件会话32轮限制不变。
9. 未映射EF有pending：报告Context类型、不泄露连接串，整个完成失败；未映射干净实例跳过。未使用mapping对应构造计数必须0。root之外仍旧行为。

### 5.2 内部状态与并发

Active→Saving→Completing→Completed→Disposed；失败走RollingBack/RolledBack或Faulted→Disposed。Saving是新内部状态，不能直接提前进入当前Completing，否则GetOrAddResource/AcquireExecutionLease会拒绝flush。

- Complete先验证LIFO、无未退出participant、无活动reader/lease、无未完resource factory，再获取唯一完成权。
- Saving只允许本次保存执行流登记资源/回调、执行SQL和SetRollbackOnly；其他并发业务、重入Complete/Dispose/Begin participant明确拒绝。内部完成权跨await保持，不用全局bool让其他流获得权限。
- 保存执行流的IsActive语义要允许EF保存，避免误入“已终态”路径；同步调整EnsureActive/EnsureCanParticipate/租约与失败标记，不能只改枚举。
- 保存失败/取消重新取完整resource清单，使用None清理，任何物理commit均为0。成功Complete重复调用保持幂等，不重保存/重通知；失败无自动重试。
- rollback/fault的EF实例不复用于新operation；已完成后pending按原规则拒绝。同root并发连接/Context使用不支持；并行工作各用executor。

### 5.3 事务策略矩阵

| 情况 | 冻结行为 |
| --- | --- |
| 默认 | true/null/null：按需事务、Provider默认隔离、无timeout。 |
| participant无options | 继承root，不重读全局默认、不续期。 |
| participant有options | 事务/隔离/timeout与root不同则目标/I/O前拒绝，不升级/降级。 |
| 非事务Dapper | 保持现有行为；已写入不能回滚。 |
| 非事务mapped EF新模式 | 专用分支不回落legacy TransactionCommit；EF6保存期间临时关闭Database.AutoTransactionsEnabled并finally恢复；resource只管连接，物理BeginTransaction计数0。 |
| 非事务需要事务的动作/领域事件 | 相关动作/SQL之前拒绝，不暗中升级为事务。 |
| foreign/ambient事务、只读、不支持事务、身份不匹配、需重定向副本 | 沿用准入失败和rollback-only，SQL-first/EF-first均测试。 |

非事务EF成功保存后执行本次SaveChangesAfter；root全局OnCompleted只在整个操作成功时运行。非事务失败可能已有持久化行，不能报告全部回滚。旧non-opt-in EF保存保持旧路径。默认command timeout与性能/容量预算不改。

### 5.4 提交/通知/异常顺序

1. 全部映射save成功→资源首次登记顺序commit→该资源OnResourceCommitted→所有资源确认后置Completed→OnCompleted。
2. A已提交、B失败/未知：A通知已发生，不回滚A、不运行全局成功回调；回滚B及后续未提交资源。保留resource key/index/outcome，Unknown不能伪装RolledBack。
3. 通知失败继续尝试其他通知/资源，保留已提交结果；全部资源提交则IsCompleted仍true；不重提/补偿。EF通知属于对应资源提交后，不是跨库全部成功后。
4. 新自动ICommitAfter在成功参与后登记root.OnCompleted，按服务实现引用在root内去重；participant不提前执行。旧模式语义不改。
5. 业务/保存原异常对象/堆栈优先，OCE保留token；边界清理错误附加Exception.Data["Bing.Operation.CleanupExceptions"]，不能替换主异常。无主异常才抛清理异常/聚合。当前root.ThrowWithCleanup在多个错误时会AggregateException；新opt-in分支必须改为保留主异常并附加清理错误，不能指望外层自动解包任意业务AggregateException。旧non-opt-in分支的聚合协议保留；通知多个失败且无业务主异常时仍允许聚合。
6. 失败仍尝试每个资源释放一次，rollback/dispose不可取消；root pop在异步清理await前恢复caller，沿用DisposeAsync_ShouldRestoreCallerBeforeAsyncCleanup。最后释放新DI scope。
7. resource不Dispose由DI拥有的DbContext，连接仅由打开者关闭；旧同步Complete/Dispose保留真实同步路径，异步executor不sync-over-async。用计数/独立连接结果证明资源归还，不重跑Core GC断言。

## 6. 取消、超时与真实代理协议

- BeginOperation owner合成caller与从Begin开始计时的deadline；participant继承deadline、按需合成自己的caller，只释放自己CTS。Timeout克隆/校验覆盖manager和scope现有两处Clone/Validate。
- AOP收集方法CancellationToken参数（允许多个，去重），与operation token合成；没有参数仍保护保存/提交。为向业务传deadline可临时替换参数为有效token，finally恢复Parameters原值；无额外源复用原token。
- 普通合法代理调用保留target一次，包括返回预取消Task/ValueTask；目标原OCE原样传播。目标忽略取消返回时完成阶段取消、不提交。executor在解析服务前预取消快速拒绝，target=0；两入口契约明确区分。
- AspectCore管道使用TaskCompletionSource.TrySetException保留可观察Faulted OCE；不能外层普通async Task包装导致Canceled快路径吞异常。同步next抛错也统一捕获/清理。
- source-backed ValueTask/ValueTask<T>仅AsTask/消费一次，保留缓存ReturnValue、Call/Callvirt非虚终端、显式接口及类代理行为；禁止重复Result读取。
- timeout为协作取消OCE（有效deadline token），不转TimeoutException、不自动重试。目标抛出的原异常优先；新合成token按有效token验收。全部物理提交后停止业务deadline，不因迟到取消跳过通知/释放或谎称回滚。
- 连接准备、保存、异步SQL、提交使用有效token；Dapper/EF实际异步入口合成当前operation root token和显式token。linked CTS存活至reader/stream释放，不能创建CommandDefinition后立即释放。同步I/O只边界检查，不承诺强制中断。
- 无token参数/不响应取消的业务或Provider只能在返回后阻止继续提交，不能Task.WhenAny后遗弃仍占用连接的任务。无token的既有领域事件/事务动作API保持，前后检查；不扩大公开接口。

## 7. HTTP / Worker / Hangfire

只复用现有宿主/队列/ScenarioOperationStore，不自建调度、重试、存储平台。

Sample新增显式 SqlUow:OperationsEnabled 开关，默认 false。SqlUnitOfWorkSampleOptions 增量添加非位置 bool OperationsEnabled { get; init; }，保留原 record 构造签名。Startup仅在true时调用AddBingOperations/新自动注册；false宿主和原测试仍保持旧手工保存契约。新路由固定POST /api/operation-scenarios/{scenario}，开关关闭时明确不可用；Worker/Hangfire按该宿主开关选择旧服务或OperationScenarioService，不在同一容器承诺两套root保存默认值同时生效。opt-in宿主的旧手工root会自动保存，这是§4明确的迁移效果。

| 入口 | 适配 | 验收 |
| --- | --- | --- |
| HTTP | 新opt-in示例路由注入executor，新scope解析OperationScenarioService，RequestAborted传入；旧/api/scenarios及手工示例保留 | TestServer成功/保存失败/取消；提交后才成功返回，新scope身份 |
| Worker | 每条新模式队列项由executor建业务scope，stoppingToken贯穿，不重复手工建scope或缓存服务 | 连续项/并发独立项、取消与失败后下一项，资源/上下文归零 |
| Hangfire | 新模式job注入executor；新增带CancellationToken重载，排队表达式传占位token供现有Hangfire替换；旧两参方法委托新重载兼容已排队调用 | 真实Server关闭/取消和失败传播，不以直接new job替代；重试配置保持，不承诺业务恰好一次 |

executor用现有Core TraceIdContext四个标量复制独立对象，finally恢复父引用。SQL的singleton AsyncLocalDatabaseContextAccessor已有getter/setter快照；同一异步调用继承路由值，适配器通过既有数据库scope/try-finally恢复。Uow不反向引用SQL/Events。

队列不依赖ExecutionContext传播：仅携带trace、operationId和已确认数据源/租户标量；消费入口在executor之前建立上下文，之后才解析业务服务；finally恢复。不能传Context/Query/ClaimsPrincipal/IServiceProvider。路由值不等于授权，阶段C安全边界未实现。

新OperationScenarioService与旧手工SqlUnitOfWorkApplicationService分开，复用repository/fixture；在开关关闭的旧宿主保留ef-complete-without-save语义；开关开启的独立测试宿主验证tracked-only自动保存，不把旧断言搬到新模式。公共执行器只提供业务边界，不接管现有调度失败策略。

## 8. 明确文件边界

### 8.1 已确认文件

下列文件将按所属任务修改或作为直接回归；不意味着每个文件都必须产生diff。

| 文件 | 作用 |
| --- | --- |
| framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScope.cs、UnitOfWorkScopeManager.cs、UnitOfWorkScopeOptions.cs、UnitOfWorkScopeServiceCollectionExtensions.cs | 新保存状态/令牌/timeout/alias与opt-in |
| framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs | 唯一模式选择、owner/participant、注册共存、取消链 |
| framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs、EfCoreSqlUnitOfWorkConnectionProvider.cs；Extensions/EfCoreSqlQueryServiceCollectionExtensions.cs | 解析登记、纯保存、pending/未接受状态、非事务、准入/注册 |
| framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.UnitOfWork.cs、SqlExecutorBase.cs、SqlMultipleQueryExecutorBase.cs、SqlQueryBase.QueryPlan.Paging.cs、SqlQueryBase.QueryPlan.Streaming.cs | operation token到实际I/O/reader；不改SQL输出协议 |
| framework/tests/Bing.Uow.Tests/UnitOfWorkScopeTest.cs、UnitOfWorkScopeApiContractTest.cs、UnitOfWorkScopeRegistrationTest.cs、UnitOfWorkDocumentationExamplesTest.cs | 旧契约/新API/编译示例 |
| framework/tests/Bing.Aop.AspectCore.Tests/UnitOfWorkAdapterTest.cs、CapTraceUnitOfWorkCancellationTest.cs | 旧注册及真实代理全形态回归 |
| framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqlUnitOfWorkResourceTest.cs、EfCoreSqlUnitOfWorkConnectionProviderTest.cs；Integration/EfCoreSqliteUnitOfWorkContractTest.cs | 新旧模式/真实物理事务生命周期 |
| framework/tests/Bing.Dapper.Core.Tests/DapperSqlUnitOfWorkResourceTest.cs；framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteUnitOfWorkContractTest.cs | 资源及SQLite集成 |
| samples/Bing.Samples.SqlUnitOfWork/Startup.cs、Infrastructure/Database/SqlUnitOfWorkSampleOptions.cs、Infrastructure/Background/ScenarioBackgroundWorker.cs、ScenarioWorkQueue.cs（同Background目录）、Jobs/SqlUnitOfWorkJob.cs、Controllers/JobsController.cs | 增量注册/后台适配，保留旧默认 |
| samples/Bing.Samples.SqlUnitOfWork.Tests/SqlUnitOfWorkHostTests.cs | 三入口旧场景回归 |
| framework/src/Bing.Uow/README.md；docs/architecture/abp-infrastructure-support-matrix.md；samples/Bing.Samples.SqlUnitOfWork/README.md、SqlUnitOfWork.http；docs/sql/migration-guide.md | 已编译迁移示例/支持范围；仅最小修正已确认的UseSqlite悬空示例，按实际源码注册入口及编译证据验证 |

### 8.2 新增与候选文件

以下为计划目标而非已有事实：

- Uow/Bing/Uow新增IUnitOfWorkExecutionScope.cs、IUnitOfWorkOperationManager.cs、IUnitOfWorkSaveChangesHandler.cs、IBingOperationExecutor.cs、BingOperationExecutor.cs、BingOperationServiceCollectionExtensions.cs。内部marker/token helper可同文件，不为拆类制造任务。
- Application.AspectCore/Bing/DependencyInjection新增BingAutomaticUnitOfWorkOptions.cs；新拦截分支可放BingAutomaticUnitOfWorkInterceptor.cs，复用旧ValueTask适配器。
- EF/Core新增ResolvedEfCoreUnitOfWorkRegistry.cs、EfCoreOperationSaveChangesHandler.cs；不要与已有resource claim的EfCoreSqlUnitOfWorkContextRegistry混淆。
- Uow.Tests新增OperationScopeLifecycleTest.cs、BingOperationExecutorTest.cs、OperationApiContractTest.cs；AOP.Tests新增AutomaticUnitOfWorkAdapterTest.cs、AutomaticUnitOfWorkCancellationTest.cs、AutomaticUnitOfWorkProbeTest.cs。
- EF.Tests/Core新增EfCoreOperationSaveChangesHandlerTest.cs；Integration新增EfCoreOperationSqliteTest.cs；Dapper.Core.Tests新增OperationCancellationTest.cs。
- Sample/Application新增OperationScenarioService.cs；Controllers新增OperationScenariosController.cs；Sample.Tests新增OperationEntryPointTests.cs。
- Dapper其他异步partial、EF真实查询入口、连接准备方法只按取消调用图确有遗漏时纳入，不能改SQL公共签名。测试csproj仅缺少现有ProjectReference时补引用，不改包/TFM/生产依赖图。
- framework/tests/Bing.Data.Sql.Benchmarks/SqlUnitOfWorkProbe.cs仅按实际scope/lease热路径影响更新既有probe，不扩完整矩阵或预算。

## 9. Phase / Task实施清单

Dapper/EF主线冻结10项：B00、B01.1、B01.2、B02.1、B02.2、B02.3、B02.4、B03、B05、B06。B04是独立子工作流，不算本主线已完成项。

| Task | 目标/现状证据 | 修改范围与实施步骤 | 依赖 | 验证/验收 | 风险 |
| --- | --- | --- | --- | --- | --- |
| B00 | 固定当前候选；dirty工作区 | 记录相关文件、HEAD/状态、SDK/TFM和旧证据；先职责测试 | 本计划审阅 | 保留旧测试；接口不存在的编译失败不能冒充行为红证据；新行为失败用可执行夹具 | 把HEAD当实际源码 |
| B01.1 | API/DI/root；只有手工manager | §3与Uow文件；注册矩阵→alias/BeginOperation/token→Saving/handler | B00 | 新默认实现独立测试，旧模式不自动保存；participant/LIFO/lease/重入 | AsyncLocal/并发保存 |
| B01.2 | 自动owner/participant；旧post-commit | selector唯一分支、失败捕获、ICommitAfter去重，保留ValueTask终端 | B01.1 | 返回全形态、标记优先级、显式接口/类代理、捕获内层异常、注册顺序 | 抑制漏失败/双消费 |
| B02.1 | 已解析EF发现/纯保存 | registry、handler、pending/未接受状态、映射内保存，root前/中解析 | B01.1 | handler独立测试+SQLite tracked-only；未用Context构造0；完成commit1 | 漏保存/legacy误flush |
| B02.2 | 保存/提交/通知、非事务 | 完成save后快照资源，非事务专用分支，事件与迟到pending规则 | B02.1 | 双SQLite/同库两种顺序；保存失败commit0；非事务BeginTransaction0；旧模式 | 双提交/事件重复 |
| B02.3 | 取消/timeout全链 | CTS/参数/实际I/O/reader寿命、主异常优先 | B01.2、B02.2 | 业务/保存/第二资源取消，原OCE/token，通知/Dispose失败 | 超时后提交/过早清理 |
| B02.4 | 独立操作执行器 | 新scope内Begin后解析，finally次序与trace副本 | B02.2、B02.3 | 内外DI/Context/Query/root不同；子提交外回滚；构造失败清理 | 复用外层服务 |
| B03 | 三入口统一 | 新Sample服务/路由、Worker/Hangfire增量executor、标量上下文/取消 | B02.4 | 真实TestServer/Worker/Hangfire同矩阵，旧路径保持 | 假job代替Server/吞取消 |
| B05 | G12迁移及A递延的UseSqlite文档事项 | §8文档、中文XML、编译示例；最小修正docs/sql/migration-guide.md悬空示例，依据真实注册入口/编译样例；execution/traceability/api-delta | B01–B03 | 主例编译，FreeSQL明确未实施；§11.3现有文档检查，分别记录A旧FAIL与B新结果 | 支持面夸大或扩为无关文档改写 |
| B06 | 有界probe/阶段最终候选验收 | §11局部L1→影响L2/L3，§11.3一次Release solution build+默认完整net6/net8 L4及文档检查，§12 probe，final-report和独立review | B05 | 逐符号及最终候选证据、新旧结果分列；仅文档变化后复用代码门禁；原GC失败按证据分类且不扩修，完成规定门禁后OPEN_ACTIONABLE=0停止 | 为追绿重复全量/benchmark |

### B04：FreeSQL独立适配与前置

建议taskId：20261022-abp-phase-b-freesql-operation-adapter。先独立create-plan，再实施；本文件不直接授权更改FreeSQL/Admin.FreeSQL。前置：B02 Dapper/EF的保存/资源/取消/通知已实现并独立验收，B03执行器契约稳定。

已确认调查范围：framework/src/Bing.FreeSQL/Bing/Uow/UnitOfWorkBase.cs、DeferredCommitUnitOfWork.cs；framework/src/Bing.FreeSQL.MySql/Bing/Uow/UnitOfWork.cs；modules/admin/src/Bing.Admin.Data.FreeSQL/CapUnitOfWorkExtensions.cs、UnitOfWorks/MySql/AdminUnitOfWork.cs；framework/tests/Bing.FreeSQL.Tests/Core/UnitOfWorkCancellationTest.cs、Data/Stores/QueryStoreBaseUnitOfWorkTest.cs；modules/admin/tests/Bing.Admin.FreeSQL.Tests的Persistence/Cleanup/AspectCore/CapUnitOfWorkExtensions测试。

子计划须冻结显式映射注册/已用实例发现、内部Commit延迟、连接/transaction所有权、重复保存/事件、回滚/释放一次、Dapper互操作、CAP借用，先直接单测再真实Provider。SQLite路径按该包现有依赖和实际能力设计，缺Provider依赖不得私自加包伪造已支持；列清所缺本地证据和专用MySQL门控。涉及MySQL/Admin CAP时必须相应外库，EF结果不替代FreeSQL。

主线可交付“Dapper/显式EF验收，FreeSQL待B04”，不能宣称阶段B全ORM支持。状态DEFERRED_TO_INDEPENDENT_PLAN，不据此自动扩修。

## 10. 契约矩阵与最终符号追溯

新增测试名为实施目标，不预填PASS。最终traceability.md必须记实际完整命名空间/类/方法、项目、TFM、结果、原始TRX。阶段A正确全名为Bing.Aop.AspectCore.CapTraceUnitOfWorkCancellationTest，无.Tests命名空间。

| ID | 生产契约 | 项目/计划方法或现有回归 |
| --- | --- | --- |
| C01 | 新API/注册共存 | Uow OperationApiContractTest.LegacyInterfaces_ShouldRemainUnchanged；OperationScopeLifecycleTest.Registration_ShouldAliasManagersAndKeepLegacyDefault；AOP AutomaticUnitOfWorkAdapterTest.RegistrationOrder_ShouldSelectOneLifecycle |
| C02 | owner/participant失败 | Uow OperationScopeLifecycleTest.ParticipantFailure_WhenCaught_ShouldPreventOwnerCommit；AOP AutomaticUnitOfWorkAdapterTest.NestedCalls_ShouldOnlyCompleteOwner；旧Complete_WhenParticipantIncomplete_ShouldRollback |
| C03 | 单消费/终端/整链取消 | AOP AutomaticUnitOfWorkCancellationTest.Proxy_AllReturnKinds_ShouldCallTargetOnceAndPreserveFailure、Proxy_SourceBackedValueTask_ShouldConsumeOnce、Proxy_OuterObserver_ShouldNotRunSuccessAfterCancellation；复用阶段A真实组合夹具 |
| C04 | scope/重解析 | Uow BingOperationExecutorTest.Execute_ShouldResolveServiceAfterBeginInNewScope、InnerCommit_ShouldSurviveOuterRollback、ResolutionFailure_ShouldDisposeScope、ParallelOperations_ShouldRestoreIndependentContexts |
| C05 | tracked-only/未用实例 | EF EfCoreOperationSaveChangesHandlerTest.ShouldInspectResolvedInstancesWithoutResolvingUnusedMappings；EfCoreOperationSqliteTest.TrackedOnlyContext_ShouldSaveBeforeSinglePhysicalCommit；旧Complete_ShouldNotSaveTrackedOnlyChanges保留非opt-in |
| C06 | legacy/未映射 | EF EfCoreOperationSqliteTest.UnmappedDirtyContext_ShouldRejectBeforeOtherResourceCommit、LegacyRegistration_ShouldKeepExistingCommitBehavior、MappedOperationSave_ShouldNeverEnterLegacyTransactionCommit |
| C07 | 手工flush/事件/false | EF EfCoreOperationSqliteTest.ManualFlushThenComplete_ShouldNotAddEmptySaveOrNotification、NewChangesAfterManualFlush_ShouldSaveOnceMore、UnacceptedFlush_ShouldRejectBeforeDuplicateSql、LateDirtyContextDuringFinalSave_ShouldRollbackBeforeCommit；旧RepeatedFlush_ShouldDispatchDomainEventsOnceAndDelayCleanupUntilCommit |
| C08 | 同库/跨库/失败 | EF EfCoreOperationSqliteTest.EfAndDapperInEitherOrder_ShouldShareConnectionAndCommitOnce、SaveFailure_ShouldRollbackAllBeforeAnyCommit；Uow旧Complete_WhenSecondResourceFails_ShouldReportPartialCommit；Dapper.Sqlite新增Operation_MultiSource_ShouldPreservePartialCommitOutcome |
| C09 | 非事务/策略 | EF EfCoreOperationSqliteTest.NonTransactionalSave_ShouldCreateNoPhysicalTransaction、NonTransactionalPendingTransactionAction_ShouldRejectBeforeIo；Uow OperationScopeLifecycleTest.ParticipantOptions_ShouldNotOverrideRoot |
| C10 | 超时/取消 | Uow OperationScopeLifecycleTest.Timeout_ShouldCancelWithoutRenewingParticipantDeadline；Executor.PreCancellation_ShouldNotResolveTarget；Dapper.Core OperationCancellationTest.Reader_ShouldKeepLinkedTokenUntilDispose；EF保存/提交前信号控制取消 |
| C11 | 通知/释放/主异常 | Uow OperationScopeLifecycleTest.PrimaryCancellation_ShouldSurviveCleanupFailure；AOP AutomaticUnitOfWorkAdapterTest.CommitAfter_ShouldRunOncePerServiceAfterRootCommit；旧NotificationFailure_ShouldKeepCompletedAndNotRetry、DisposeAsync_ShouldRestoreCallerBeforeAsyncCleanup |
| C12 | 三入口 | Sample OperationEntryPointTests.HttpWorkerHangfire_ShouldShareOperationOutcomeContract、HangfireShutdown_ShouldPropagateCancellation、NextItem_ShouldNotInheritFailedItemContext；旧SqlUnitOfWorkHostTests三入口 |
| C13 | 资源/缓存/上下文 | Dapper.Sqlite既有SqlFactoryCacheScopeContractTest、SqlFactoryScopeContractTest；EF既有EfCoreSqlQueryFactoryScopeFilterContractTest；若改resource key，新增命中/未命中/root/租户隔离直接测试 |
| C14 | 编译/API | Uow OperationApiContractTest、UnitOfWorkDocumentationExamplesTest新例；AOP LegacyMarkerConsumer.csproj及LegacyConsumerAssemblyTest二进制兼容测试，UnitOfWorkAdapterTest真实代理；Sample net6 build；UseSqlite修正后的真实注册入口编译证据；SQL公共面受影响才跑SqlQueryApiContractTest/TransactionApiContractTest |

新默认实现必须独立测试类，Sample不能代替。SQL输出变化要完整字符串断言；映射/Provider规则变化按AGENTS补各受影响分支成功失败、缓存命中/未命中/隔离、重复渲染与执行上下文优先级。原设计不改SQL格式化、映射缓存或Builder克隆；保持不变则明确benchmark不适用原因，不为填表扩改。

## 11. 真实验证命令、外库触发与基线复用

下列是未来实施命令，不是本次规划运行记录。每条命令工具调用均显式workdir=H:/Bing_Framework/Bing.NetCore。PowerShell设置Console.InputEncoding、Console.OutputEncoding、$OutputEncoding为UTF-8；日志/Markdown文件写入显式UTF-8。记录完整命令、退出码、TFM、候选和环境类别；结果放本任务新的artifacts目录，不覆盖历史。

### 11.1 L0/L1/L2与本地L3

以下全部WorkingDirectory=H:/Bing_Framework/Bing.NetCore：

    dotnet build framework/src/Bing.Uow/Bing.Uow.csproj -c Release --no-restore --nologo
    dotnet build framework/src/Bing.Ddd.Application.AspectCore/Bing.Ddd.Application.AspectCore.csproj -c Release --no-restore --nologo
    dotnet build framework/src/Bing.EntityFrameworkCore/Bing.EntityFrameworkCore.csproj -c Release --no-restore --nologo
    dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj -c Release --no-restore --nologo
    dotnet build framework/tests/Bing.Aop.AspectCore.Tests/LegacyConsumer/LegacyMarkerConsumer.csproj -c Release --no-restore --nologo
    dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release --no-restore --nologo

LegacyMarkerConsumer.csproj才是真正consumer，已ProjectReference引用LegacyAspectCoreContract.csproj旧契约stub；仅build stub不能算consumer验证。保留AOP完整项目中的LegacyConsumerAssemblyTest二进制兼容测试及UnitOfWorkAdapterTest/取消组合的真实代理测试，不能以consumer编译替代运行契约。

assets缺失才对受影响项目按现有版本dotnet restore一次，不升级包/SDK。

    dotnet test framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l2/uow-net6
    dotnet test framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l2/aop-net6
    dotnet test framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l2/dapper-net6
    dotnet test framework/tests/Bing.EntityFrameworkCore.Tests/Bing.EntityFrameworkCore.Tests.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l2/ef-net6
    dotnet test framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l3/sqlite-net6
    dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0 --no-restore --nologo --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l3/hosts-net6

除EF外，上述测试同样以-f net8.0及独立net8结果目录运行；EF仅现有net6.0，Sample生产仍net6。L1使用同命令加精确filter，换独立结果目录，例如：

    --filter "FullyQualifiedName~UnitOfWorkScopeTest|FullyQualifiedName~OperationScopeLifecycleTest|FullyQualifiedName~OperationApiContractTest"
    --filter "FullyQualifiedName~UnitOfWorkAdapterTest|FullyQualifiedName~CapTraceUnitOfWorkCancellationTest|FullyQualifiedName~AutomaticUnitOfWork"
    --filter "FullyQualifiedName~EfCoreOperation|FullyQualifiedName~EfCoreSqliteUnitOfWorkContractTest"

新增filter对应B任务完成后才存在，必须核对发现数非0，不能用未含新测试的旧DLL --no-build验证。每次修改先L1，再受影响完整L2，跨项目/DI/Provider执行L3。若改Events/领域事件生产链，追加其完整项目/CAP借用契约，否则对未变范围复用证据。

SQLite是SQL/连接/执行路径变更的必跑本地L3，无外库门控。仓库已有额外单场景受控runner：

    pwsh -File eng/ci/Invoke-SqliteContractTests.ps1 -ResultsDirectory artifacts/test-results/phase-b-sqlite-net6 -RunName phase-b-sqlite-net6 -Framework net6.0 -Configuration Release

它仅筛选ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence，不能代替本计划新Uow/EF测试。dirty工作区禁止加-ReleaseEvidence或运行完整clean-source RC runner。

### 11.2 外部Provider/CAP触发矩阵

| 触发 | 所需证据 | 无环境/范围处理 |
| --- | --- | --- |
| 共享Dapper/EF连接、保存/提交、取消改变 | SQLite先通过；本主线至少专用MySQL的两种先访问顺序、tracked-only、rollback/部分提交/取消 | BLOCKED_EXTERNAL；Mock/SQLite不能替代MySQL |
| 实际修改PostgreSQL/SqlServer/Oracle专属路径 | 对应Provider单元与真实集成 | 不受影响不启动外库；Oracle不能捏造通用runner参数 |
| 新operation root/EF保存/通知进入CAP Outbox链 | Sample CapMySqlIntegrationTests及双MySQL/Rabbit真实三入口，含EF-first | 内存/单进程不替代broker消费；旧证据不覆盖新路径 |
| 仅代理元数据/文档且CAP运行链未变 | 未变子范围REUSED_UNCHANGED_SCOPE | 不重复broker实验 |

真实Provider runner（按影响选择，不全跑；workdir仍为仓库绝对路径）：

    pwsh -File eng/ci/Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net6.0 -Configuration Release
    pwsh -File eng/ci/Invoke-ProviderIntegrationTests.ps1 -Provider PostgreSql -Framework net6.0 -Configuration Release
    pwsh -File eng/ci/Invoke-ProviderIntegrationTests.ps1 -Provider SqlServer -Framework net6.0 -Configuration Release

相应RUN_MYSQL_INTEGRATION_TESTS/RUN_POSTGRESQL_INTEGRATION_TESTS/RUN_SQLSERVER_INTEGRATION_TESTS=true；专用ConnectionStrings__MySqlConnection/__PostgreSqlConnection/__SqlServerConnection及ALLOW_DATABASE_RESET_FOR_TESTS=true；库名符合_test/_tests/_integration/_integration_test后缀。禁止生产库；关闭全局RUN_INTEGRATION_TESTS、DefaultConnection和其他Provider gate/连接；日志不输出秘密。需要覆盖net8的受影响Dapper路径同参数切net8。

runsettings必须区分两种入口，不自动发现、不混用、不改写任何现有本地凭据：

- Provider runner 的受保护白名单格式是仓库内已有、文件名严格为 integration.runsettings.local 的UTF-8 XML，结构为RunSettings/RunConfiguration/EnvironmentVariables。使用文件时必须显式 -RunSettingsPath；上面未传该参数的命令只使用Provider专用环境变量，不会自动读本地文件。只导入该Provider gate、reset许可、对应连接变量（MySQL另允许脚本声明的cross-database两变量）；不能把普通runsettings视为此保护入口。受保护CI拒绝本地runsettings，使用Provider-scoped环境秘密。
- 调用形式：先把 $providerRunSettingsPath 设为实际已有且通过保护校验的文件路径，再执行下面命令；不假定仓库根已经存在该文件，不复制、改名或迁移现有凭据。

    pwsh -File eng/ci/Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net6.0 -Configuration Release -RunSettingsPath $providerRunSettingsPath

- 四个项目现有文件名为 integration.local.runsettings，位于 framework/tests/Bing.Dapper.MySql.Tests.Integration/、Bing.Dapper.PostgreSql.Tests.Integration/、Bing.Dapper.SqlServer.Tests.Integration/、Bing.Dapper.Sqlite.Tests.Integration/。它们只能按普通 dotnet test --settings 显式传入，不能传给Provider runner的-RunSettingsPath，也不能算受保护Provider门禁证据。普通测试同样须满足相应测试门控与专用非生产库条件。

    dotnet test framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj -c Release -f net6.0 --no-restore --settings framework/tests/Bing.Dapper.MySql.Tests.Integration/integration.local.runsettings --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l3/mysql-local-settings

其余三个项目使用各自同目录的integration.local.runsettings与独立结果目录；最终默认L4不传任何此类文件。以上仅冻结调用规则，本轮不读取凭据内容、不改写配置、不运行命令。

这些Provider runner主要验证现有Dapper路径，不能当新增EF操作的MySQL证据。EF操作MySQL测试放Sample.Tests新OperationEntryPointTests/专门集成类，复用现有CapMySqlEfBridge夹具和RUN_CAP_MYSQL_TESTS门控，不新增Provider包。明确断言自动保存行/同事务/提交计数，不只跑旧场景。

双库CAP现有入口：

    pwsh -File ai_docs/tasks/20261021-multisource-cap-diagnostics/scripts/start-infra.ps1
    dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0 --no-restore --filter FullyQualifiedName~CapMySqlIntegrationTests --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/l3/cap-net6
    dotnet run --project samples/Bing.Samples.SqlUnitOfWork.Consumer/Bing.Samples.SqlUnitOfWork.Consumer.csproj -c Release --no-restore
    dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release --no-restore -- --SqlUow:Storage=CapMySqlEfBridge

RUN_CAP_MYSQL_TESTS=1；连接/broker按现有Sample README和隔离compose注入。新增操作测试加入精确filter，不能以旧CapMySqlIntegrationTests独自关闭新契约。consumer/host分进程运行；Windows后台Start-Process必须Hidden及显式WorkingDirectory。确认容器为既有任务隔离实例，当前规划不启动基础设施。

真实入口POST /api/scenarios/{scenario}、/api/worker/scenarios/{scenario}、/api/jobs/scenarios/{scenario}，异步结果GET /api/operations/{operationId}；新操作路由为/api/operation-scenarios/{scenario}，B03实施后同步请求文件。上面的host命令在新模式验收时追加--SqlUow:OperationsEnabled=true（B03新增开关，当前尚未实施）；旧路径回归用默认false的另一个宿主。验证两库业务行/Outbox、独立Consumer结果及取消/失败，不只HTTP 200。新路径改变后旧t06-e2e不能算新候选验证。

只有Admin实际CAP事务/消费链受影响才追加：

    pwsh -File eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1 -Framework net6.0 -Configuration Release

另需RUN_MYSQL_CAP_INTEGRATION_TESTS=true、CAP_RABBITMQ_HOST/USERNAME/PASSWORD、MySQL gate/reset，先TCP preflight。该runner含EF和FreeSQL Admin，FreeSQL只算旧路径回归，不关闭B04。无影响不跑。

### 11.3 阶段最终候选：Release build、默认L4与文档检查

B的共享Uow/IO改动最终候选固定后，执行一次以下完整门禁；本次规划不执行。WorkingDirectory仍显式为H:/Bing_Framework/Bing.NetCore：

    dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1
    dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/final-candidate/L4/trx
    python eng/ci/check-docs.py --quiet

默认solution test不加filter或全局-f，按各项目原有TFM执行完整net6.0/net8.0覆盖；EF/Admin等只有net6.0的项目不新增TFM。先确认同一候选Release build成功，才可用--no-build测试，不复用旧候选二进制。按项目/TFM解析TRX，记录Passed/Failed/NotExecuted、退出码和覆盖清单；不能只看进程退出码或把部分TFM当完整L4。

L4在独立子进程隔离环境：外部RUN_*门控显式关闭（含全局/Provider/CAP gate），probe门控关闭，清理该子进程连接/broker变量及RUN_METADATA_PATH，不加载隐式runsettings，保留默认本地SQLite及xUnit并行设置。仅记录变量名，不输出秘密，不修改父会话或本地凭据文件。参考阶段A plan §7.5已有隔离流程，运行产物写B的新目录，不执行会覆写A历史产物的旧runner。

B05允许且仅最小修正docs/sql/migration-guide.md已确认的UseSqlite悬空示例：UseSqlite本身是外部EF SQLite扩展，不应误写成Bing自有注册API。对照framework/src/Bing.Dapper.Sqlite/Bing/Data/Sql/DapperServiceCollectionExtensions.cs的AddSqliteProvider、Dapper.Core同路径文件的AddSqlDataSource，以及EF扩展AddEfCoreSqlUnitOfWork和Uow的AddUnitOfWorkScopes；以samples/Bing.Samples.SqlUnitOfWork/Startup.cs现有SQLite/EF桥接装配为编译对应物。优先把不完整的独立示例改为准确链接该Sample并保留必要说明，不发明替代API、不放宽checker。修正后的文档主路径由§11.1的Sample Release build验证，记录B候选证据，A历史Sample构建仅作旧基线。check-docs仅检查文档一致性，不能以符号匹配替代示例编译。最终执行上面的既有checker，保存stdout/stderr/退出码，分别记录A旧FAIL与B新结果；若出现其他无关文档问题，记录/分类，不扩为全面改写。

候选只改文档/报告后复用代码build/L4证据，文档检查只按相关内容变化更新，不为追绿重复全量。若生产/测试/harness随后改变，按影响分析说明旧证据失效并对新的最终候选执行必要门禁，不能无依据复跑相同候选。原Core GC两项若再次失败，保留新运行FAIL和因果证据；无因果证明仍记NOT_DETERMINED并递延独立调查，不扩修GC、不用隔离重跑成功冲销完整L4失败。即使本次L4通过，也只代表B候选新结果，不改写A历史FAIL。

### 11.4 基线复用

- A Review 2的AOP各TFM148/0、EventBus各409/0/1、组合各10/0为未变scope依据；B改AOP后只算before，必须新L1/L2。
- A历史L4 12,625/2/1,108和docs FAIL保留；更早full-suite recovery的12,561/0/1,105不覆盖较晚FAIL，局部通过数不能拼成新全量统计。B仍须§11.3新最终候选验证，分别记录新旧结果；旧FAIL既不被抹掉，也不豁免B的门禁。
- [多源CAP delivery-status](../20261021-multisource-cap-diagnostics/delivery-status.md)、[execution](../20261021-multisource-cap-diagnostics/execution.md)及artifacts/t06-e2e为历史基线。双MySQL不等于异构库；Dashboard未配置、Hangfire内存存储、历史evidence-only探针未执行边界继续保留。
- before冻结一次：RelevantSourceScope、测试/harness、Runtime/TFM、EnvironmentClass、工作区候选；不拿HEAD代替实际dirty源码。四项相关范围不变才REUSED_UNCHANGED_SCOPE；新API无等价旧能力则BEFORE_STATUS=NOT_APPLICABLE，不伪造before。
- 当前仅计划，L0仅完整性/UTF-8/路径；L1–L5未运行。API baseline保留eng/baselines.props的既有值，不重建基线或clean-source证据。

## 12. 有界probe、开销与维护性

空operation只建必要scope/frame/CTS，无额外取消源时避免linked CTS。保存发现O(已解析Context数+其跟踪实体数)，提交O(资源+回调数)；不扫描全部DI注册，不持全局Context强引用。回调/CTS随root或DI生命周期清理。

B06只针对实际变化热路径：

| Probe | 工作负载/入口 | 边界 |
| --- | --- | --- |
| 既有SQL Uow | dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -- --uow-probe --output ai_docs/tasks/20261022-abp-phase-b-operation-uow/artifacts/probe/uow-probe.json | 现有内部60秒；单并发双SQLite；lease 100预热+3×1000，E2E三场景各10预热+3×100；不改矩阵/预算 |
| 新自动代理/独立scope | B06在AOP.Tests新增AutomaticUnitOfWorkProbeTest，只RUN_BING_OPERATION_PROBE=1启用；dotnet test framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj -c Release -f net8.0 --no-restore --filter FullyQualifiedName~AutomaticUnitOfWorkProbeTest --logger trx | 此门控/filter为待实现入口；诊断5秒、正式30秒各最多一次；单线程100预热+最多5×1000次空/嵌套/独立operation；无外库 |

所有命令显式相同workdir。外层watchdog分别75秒/45秒，终止本次启动PID及其子进程，不按进程名杀全机dotnet；记录实际次数、elapsed、可靠采集的allocated/GC、open/commit/dispose计数、requestedConcurrency/maxActualParallelism/maxQueuedRequests（单并发1/1/0）。受控超时按边界记录，不补造统计、不延长预算。

旧probe保持工作负载/环境/harness时才可比较；新自动API仅current-only OBSERVED，无等价before，不宣称提速或性能门禁PASS。旧trace热路径未变则复用。THRESHOLD_STATUS=BLOCKED_APPROVAL（未设预算）不触发继续实验或常规签名审批。无关映射缓存/Builder benchmark记不适用，不跑重型容量矩阵。

## 13. 收口与停止规则

每轮Change Impact Analysis列ChangedFiles/Projects/PublicContracts/RuntimePaths、AffectedProviders/TFMs/Dependents、BuildPackaging、BenchmarkHarness、DocsOnly、RiskLevel。B涉及公共Uow、代理、IO，采用L1→受影响完整L2→SQLite/宿主/API consumer/相关外库L3；实施中不反复跑全局门禁；阶段最终候选按§11.3一次Release solution build、外部门控关闭的默认完整net6/net8 L4和文档检查，L5仅上述有界probe。仅文档变化后复用代码证据，不为追绿重跑，不扩修原GC问题。

后续执行交付execution.md（当前候选/结论/简短轮次索引）、traceability.md（最终生产符号→关键行为→实际测试全名/项目→TFM→结果→artifact）、api-delta.md（增量与旧API保留证据，不改baseline）、迁移/支持文档、原始TRX/stdout/stderr/命令/退出码/候选清单、final-report.md；review.md由独立Reviewer维护。历史artifact不可变，不用glob混拼不同候选。

主线收口：10项本地任务逐项记录，C01–C14有证据，新旧入口、独立实例、保存/取消/通知、三入口真实路径验收；B06完成§11.3规定最终build/L4/docs检查并如实分类结果，不能因局部OPEN_ACTIONABLE=0省略尚未执行的必要门禁。外库无环境单列阻塞；阶段B全ORM仍待B04。Implementation、Test、Docs和Release分列；历史FAIL保留，新候选结果单列。若仅剩原GC未定失败/外部阻塞且无B范围可执行问题，停止，不为全绿扩修或重跑。

Review→Fix→Review最多3轮；仅OPEN_ACTIONABLE>0且有明确本地验收条件才继续。连续两轮根因/验收/候选/证据不变，STOPPED_NO_PROGRESS。同一候选/假设/负载/环境最多诊断一次+正式一次。PARTIAL/NOT_VERIFIED/BLOCKED/TIMEOUT/RELEASE_STATUS!=PASS不单独触发Fix；无新证据不重开A CLOSED项。

每轮分组Completed/Open Actionable/Blocked Approval/Blocked External/Not Applicable/Accepted Limitations/Verified Boundaries/Deferred，并输出Implementation关闭数、Release gate状态、验证层级、复用证据和Next Action。通知失败已提交、非事务部分持久化、同root并发拒绝、最终保存迟到跨Context变更拒绝等按直接测试记录边界，不作无限修复目标。

### 本次规划交付

- Completed：规则/输入/A结论/真实源码/固定ABP参考，签名、矩阵、任务、命令及停止规则已冻结；按主线程审阅修正最终build/L4/docs门禁、UseSqlite最小修正范围、真实legacy consumer及两类runsettings入口，其余架构保持。
- Open Actionable：规划无剩余；未来10项实施尚未启动，不触发Planner编码。
- Blocked Approval：既有性能预算未设；计划交主线程审阅，不为普通签名重复请示。
- Blocked External：既有clean-source发布门禁；未来外库证据按实际环境记录。
- Not Applicable：本次代码测试/性能测量，新API历史等价before。
- Accepted Limitations：有序本地事务、协作取消、保留旧入口、同root不支持并行共享连接。
- Verified Boundaries：本次无新增运行证据，GC失败不写成已确认资源边界。
- Deferred：B04独立FreeSQL、C/D、原Core GC因果调查、A的OPTIONAL命名注记；UseSqlite已纳入B05最小修正，不再列为B之外的递延项。
- Implementation：0/10（未实施）；规划交付1/1；Release沿用既有FAIL/BLOCKED。
- Next Action：STOP，交主线程审阅；不自动execute-plan、不暂存提交推送、不发送外部通知。
