# DI 与资源释放专项证据

审查者：主线程。只读生产代码；最小探针保存在 `artifacts/ownership-probe/`。以下均为当前实现的新审查发现，不表示上一轮模块生命周期修复引入了这些问题。

## 运行方式

- CIA 与固定窗口：[cia.md](artifacts/ownership-probe/cia.md)。
- 探针引用已构建的真实 Bing.Core/Bing.Uow/Bing.EventBus 与依赖，无生产项目重建，无数据库或外部服务。
- net6：.NET 6.0.36 / Microsoft DI 6.0.0；net8：.NET 8.0.30 / Microsoft DI 8.0.0。版本来自对应 EventBus.Tests 输出，未升级依赖；不把 net8 的该消费者环境与旧 Core GC 的 DI6 实验混为一谈。
- 两个 TFM 各编译一次、执行一次；编译上限60秒，执行上限15秒，实际执行各约0.5秒。探针退出0只表示采集成功。
- [net6 原始结果](artifacts/ownership-probe/run-net6.stdout.json)、[net8 原始结果](artifacts/ownership-probe/run-net8.stdout.json)、[实际 DLL 与原输出身份](artifacts/ownership-probe/assembly-identity.json)。

## FIX-001：Uow 启动失败遗留取消注册和计时器

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 位置：`framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs:77`。
- 触发：`BeginOperation` 指定 Timeout 和 caller token；随后某个 `IUnitOfWorkSaveChangesHandler` 在 DI 构造阶段抛出。
- 源码：先 `CreateRootCancellation`，再解析 marker/handlers；没有包围后续启动过程的失败释放分支。root 尚未交给调用者，正常 Dispose 无从执行。
- 实测：两TFM均返回原始构造异常，Current 为 null，但 caller 的 CTS 回调由0变1，回调状态为 Linked2CancellationTokenSource；活动计时器由0变1。探针显式释放这两个 CTS 后回调与计时器均恢复0。
- 影响：启动失败频繁发生时累积取消注册与计时器；绑定长寿命 caller token 时可能长期保留关联对象。不是 GC 算法错误，也没有以工作集走势推断泄漏。
- 建议：将新建取消资源的所有权交接覆盖整个 BeginRoot 启动过程；成功交给 root 后由 root 释放，失败则释放 linked/deadline。不能直接调用要求已入栈的普通 root.Dispose。
- 验收：handler 构造失败、相关解析失败、正常建立/释放分别覆盖；失败后保持原异常与外层 Current，取消注册和计时器回到基线。

## FIX-002：事件处理器构造失败时丢失新 scope

- 严重程度：HIGH；处理要求：MUST_FIX；状态：OPEN_ACTIONABLE。
- 位置：`framework/src/Bing.EventBus/Bing/EventBus/IocEventHandlerFactory.cs:35`。
- 触发：新 scope 内已经创建可释放依赖，随后处理器构造抛出，或解析对象无法转为 IEventHandler。
- 源码：CreateScope 后直接 GetRequiredService 并创建包装器，异常时尚无包装器负责 Dispose；没有 catch/finally 清理。
- 实测：先创建可释放计数依赖再抛出，两TFM的 scope.Dispose 与依赖 Dispose 均为0。探针补充清理同一真实 scope 后均为1。
- 影响：处理器启动失败时资源清理被遗漏；依赖持有连接、订阅或其他外部资源时可能耗尽资源。未将纯计数探针说成实际数据库连接泄漏压力结果。
- 建议：为创建到包装器所有权交接之间增加失败清理；保留构造主异常并附加清理异常。正常路径仍由包装器拥有 scope。与 FIX-003 一起设计异步创建/清理兼容方式。
- 验收：构造失败、转换失败、清理自身失败、正常处理各路径，检查 scope/依赖恰好释放一次以及主异常保留。

## FIX-003：异步事件分发链只支持同步 scope 释放

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 位置：`framework/src/Bing.EventBus/Bing/EventBus/IocEventHandlerFactory.cs:38`；`framework/src/Bing.EventBus/Bing/EventBus/EventBusBase.cs:204`。
- 触发：事件处理器注入仅实现 IAsyncDisposable 的 Scoped 依赖。
- 源码：包装器只携带 scope.Dispose；异步 TriggerHandlerAsync 也使用同步 using，因此没有异步释放通路。
- 实测：两TFM包装器释放均抛出 DI 的 `Use DisposeAsync to dispose the container` 异常；依赖 DisposeAsync 次数为0。
- 影响：业务处理完成后仍因清理方式失败而发布失败，异步资源未正确释放；重复消息或业务重试可能放大影响。实际投递重试没有在本探针执行，不将其计作已观察结果。
- 建议：保留既有同步契约兼容，增加可选异步包装器/创建入口，并让异步分发优先 await 异步清理；避免在捕获 SynchronizationContext 的路径同步阻塞 DisposeAsync。
- 验收：真实 LocalEventBus 分发覆盖仅异步、仅同步、双接口依赖，正常/业务异常/构造异常路径；原接口消费者保持兼容。

## FIX-004：异步异常通知结束时同步释放 scope

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 位置：`framework/src/Bing.Core/Bing/ExceptionHandling/ExceptionNotifier.cs:39`。
- 触发：异常订阅器依赖仅实现 IAsyncDisposable 的 Scoped 服务。
- 源码：NotifyAsync 用 `using var ...CreateScope()`；订阅器处理异常的 catch 不包含方法结束时的 scope.Dispose。
- 实测：两TFM订阅处理各执行1次，但 NotifyAsync 最终因同步 Dispose 抛出上述 DI 异常，异步释放次数0。
- 影响：异常通知流程自身产生额外异常且遗漏异步资源释放，容易影响原异常处理链。
- 建议：NotifyAsync 创建并 await 释放异步 scope；保持订阅排序、逐个异常隔离与日志行为，明确清理失败的传播方式。
- 验收：异步依赖正常释放、订阅处理异常后释放、解析失败后清理，以及清理异常的传播。

## 已检查且暂未发现新缺陷的主路径

- 默认 SQL 四类工厂及 EF 适配为 Scoped，Query/Executor 为独立 Transient；metadata/registry 为 Singleton。没有发现默认 Singleton 直接捕获 Scoped SQL 工厂的新链路。
- BingOperationExecutor 持有 IServiceScopeFactory，每次操作创建 DI scope，开始 root 后解析服务，先处理 root 再异步释放 DI scope，并恢复 Trace。上述 FIX-001 是 root 返回之前的额外异常路径，不能用业务服务解析失败回归替代。
- Uow 活动 reader/执行 lease 拒绝并发提交/释放；Dapper 自有连接及事务有逐项清理；EF 资源释放与 DbContext 所有权区分，registry 在 Context Dispose 时注销。
- CAP 每数据源隔离 runtime，正常停止会逐个 Stop/Dispose；本轮没有真实 MySQL/RabbitMQ 验证，不能由源码检查扩展为外部可靠性验收。

## 性能与使用边界

- 一个长寿命 DI scope 内反复解析可释放 Transient，即使调用方提前 Dispose，对象仍可被 DI 保留到 scope 结束。应按业务操作建立 scope；循环创建 Query 使用 Scoped factory + using/await using。这是 Microsoft DI 的所有权行为，不等于本轮发现的异常路径遗漏。
- SqlQueryBase 在执行后绑定 Uow root；释放 Query 清理连接/事务，但保留 root/resource 引用。root 内资源与回调也未在 Dispose 后清空。持有已释放 Query/root 会延长该图的可达时间；当前未证明正常短业务 scope 结束后仍有静态根，因此记录为避免长寿命持有的使用边界，不单列永久泄漏 finding。
- SQL 映射、Mutation Plan、Getter 缓存有容量选项，但默认无上限。若使用不断变化的 DbKey/Profile/路由，应配置容量并观察命中、旁路及分配；该默认兼容行为有源码文档，不把“可增长”直接当现成性能回归。
- 没有新的负载测量不能宣称 P95、吞吐量或 GC pause 全部达标；已有 probe 的适用性另见 performance-evidence.md。
