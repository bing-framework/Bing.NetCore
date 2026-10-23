# 阶段 B 设计输入

本文件保存阶段 A 实施与审查期间核实的事实，供 Astra/high 编写独立的阶段 B 计划使用；不是冻结的 API 设计或实施完成声明。阶段 B 实施仍以独立计划和阶段 A 的验收结果为前提。

## 当前 Bing 调用链

- `framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs` 是 Scoped 服务，使用实例 `AsyncLocal` 保存 frame 链。普通 `Begin` 加入当前 root；`requiresNew` 建立新 root，但没有创建 Microsoft DI scope，也没有重新解析服务。
- `IUnitOfWorkScope` 已有资源登记、执行租约、逐资源提交回调、全局完成回调、rollback-only、完成与回滚。`UnitOfWorkScopeOptions` 当前只有 `IsTransactional` 与 `IsolationLevel`；超时属于待设计契约。
- `framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs` 的现有 opt-in 入口负责成功后调用旧管理器、处理完成回调；不会自动创建新的多源 root。既有 source-backed ValueTask 单次消费、取消和终端分派必须保留。
- `framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs` 构造时向旧管理器注册。`CommitAsync` 调用 `SaveChangesAsync`；只有显式映射资源路径参与当前多源 root。未映射路径可能自行创建并提交物理事务。因此不能把任意旧 `IUnitOfWork.CommitAsync` 当作纯保存阶段。
- `AddEfCoreSqlUnitOfWork<TContext>(dataSourceKey)` 显式建立 Context 与数据源映射。新自动保存需要处理已解析、已有跟踪变更但尚未执行 SQL 的映射 Context，同时避免为了完成操作而解析所有未使用的 Context。
- FreeSQL 现有事务/延迟提交能力不能等同于多源资源适配已完成；其适配按路线图独立规划和验收。

## 固定版本的 ABP 参考

2026-10-22 读取的官方 10.6.1 源码：

- [UnitOfWorkInterceptor](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkInterceptor.cs)：拦截调用建立 Uow 边界；预留边界路径与普通 Begin/Complete 路径分开。
- [UnitOfWorkManager](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkManager.cs)：新 Uow 有所属 DI scope，并在释放时恢复外层 ambient Uow 和释放 scope。
- [UnitOfWork](https://raw.githubusercontent.com/abpframework/abp/10.6.1/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWork.cs)：保存、事务提交、完成回调是不同阶段。

参考这些职责边界，不直接复制 ABP 的 Singleton/ambient 管理器和按方法名称推断事务的默认行为；Bing 的旧默认行为及已有多源本地事务边界优先。

## 子计划需要冻结的事项

1. 自动边界的 opt-in 注册、拥有者与参与者责任，及与旧标记/旧注册同时使用时的确定行为。
2. 独立业务操作创建新 DI scope 并重新解析服务；普通跨层调用共享 root，Query 的可变状态仍独立。
3. 映射 ORM 的保存必须先于物理提交；保存失败的 rollback-only、取消、异常、完成通知和释放语义；不得隐式提交未映射 ORM。
4. 已用/未用 Context 的登记时机、手工 SaveChanges 与自动保存协作、重复保存的领域事件和回调语义。
5. 可选事务策略与超时的作用范围，保持现有默认值；HTTP、Worker、Hangfire 共用的操作执行契约。
6. Dapper/EF 完成后再进入 FreeSQL 适配；事务路径变化按计划运行 SQLite 和对应外部证据，不以其他 ORM 的结果替代。

## 阶段 A 的组合经验与保留问题

AspectCore 的取消快路径需要从真实代理调用者以及真实外层拦截器同时验证。只在普通 async helper 中 await 一个取消 Task，无法证明代理不会吞掉取消或误走提交。阶段 A 的 FIX-001 已补入实际 CapTrace + Uow 的成功/取消对照，已由独立 Review 2 验收关闭；设计阶段 B 时应复用该夹具和原始证据。

该补测已发现一个夹具干扰：额外的最外层观察器若用普通 `async Task Invoke` 等待内层故障任务，遇到 `OperationCanceledException` 会将自己的返回任务变成 Canceled，再触发代理快路径。观察器原样传递任务后，net6/net8 组合聚焦用例各 10/10 通过，无需修改生产代码。后续新增审计、授权、验证等外层拦截器时，须验证整链的取消传播协议；不能照搬其他代理框架的 async 包装方式，也不能把测试观察器造成的变化误归责于被测组件。

阶段 A 最终全量曾出现 Core 模块 GC 回收断言在 net6/net8 各一次失败；随后单方法各一次通过，因果未定，不能据此认定永久泄漏或关闭失败。仓库 docs checker 的 `docs/sql/migration-guide.md` 中 `UseSqlite` 悬空符号也仍需跟进。两者均不应通过改写历史结果、放宽门禁或重复全量试跑来处理。
