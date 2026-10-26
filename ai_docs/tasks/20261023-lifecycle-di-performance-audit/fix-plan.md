# 五项生命周期缺陷修复计划

任务：20261023-lifecycle-di-performance-audit；模式：REVIEW_FIX；范围：review.md 的 FIX-001—005。

用户最新指令“修复一下这些问题”明确授权实施，取代原 plan.md 仅审查阶段的只读限制。原审查和探针保留为 before 证据，执行者不得修改 review.md 自行关闭 finding。

## 修复契约

1. **FIX-002/003 事件 scope 所有权与异步清理。** 保留 IEventHandlerFactory、IEventHandlerDisposeWrapper 的既有成员及所有现有同步工厂行为。增加最小可选异步工厂契约 `IAsyncEventHandlerFactory`，提供 `ValueTask<IEventHandlerDisposeWrapper> GetHandlerAsync()`；IoC 工厂实现该契约。scope 包装器实现标准 IAsyncDisposable；异步分发优先异步获取、异步释放，非异步工厂/包装器继续使用原同步入口。不要给旧接口增加必需成员，或用同步等待代替异步分发的 await。创建失败须清理尚未交接的 scope；处理和清理同时失败时保留主异常及清理异常。同步兼容入口的清理策略需明确记录，不承诺只能异步释放的依赖可以用纯同步 Dispose 正常释放。处理器依赖先解析成功、处理器构造再失败的真实 DI 场景必须覆盖。
2. **FIX-004 异常通知。** 每次 NotifyAsync 创建异步 scope 并 await 清理，保留排序、订阅器异常隔离及日志。解析失败也须释放已创建依赖；清理异常不能抹掉仍需传播的解析主异常。
3. **FIX-001 Uow 启动。** 对整个 BeginRoot 的取消资源所有权交接加失败清理，包括 handler/observer 解析或启动观察器失败；成功后交给 root。失败保持外层 Current 及原主异常，不调用要求已入栈的 root.Dispose 兜底。不改事务、保存、回调顺序或 Query 状态模型。
4. **FIX-005 容器构建隔离。** EnsureBuildable 在底层容器构建前拒绝已附加或已释放的 runtime，保留首次构建失败的模块清理。不得通过忽略空模块掩盖提前释放。不增加并发 IServiceCollection 构建支持。

上述异步工厂是本次修复所需增量公共契约；现有接口、同步调用者和公共方法签名保持兼容。不修改批准的 API baseline、依赖版本、SDK、TFM、性能预算。

## 验证与证据

- 保存本次会修改文件的工作区起点及原 review 快照，保留全部既有未提交修改；不得用 HEAD 替代工作区备份。
- 先把已确认问题转为正式测试并记录失败，再修改生产实现。避免仅测试反射 private helper 或独立包装器而漏掉实际 LocalEventBus/NotifyAsync/BeginOperation/BuildBingServiceProvider 入口。
- net6/net8 Release 精准回归：真实 DI 构造失败、仅异步/仅同步/双接口依赖、旧工厂兼容、目标异常、解析异常、释放异常、exactly once；Uow caller/timeout 组合、启动失败、外层恢复、正常释放；首次/重复/独立集合构建与正常关闭对照。
- CTS/计时器资源断言应限定本次操作，避免依赖共享进程 Timer.ActiveCount 的绝对值。必要的私有运行时观测放有界独立子进程，不关闭全局测试并行、不循环重试等绿色。
- 精准通过后运行 Core/EventBus/Uow 完整测试、相关 AOP/source-backed ValueTask 回归和 SQLite 本地集成（net6/net8）。公共契约增量要验证旧工厂消费者兼容。外部数据库/CAP 门控关闭，不计跳过为通过。
- 最终候选 Release 构建；共享 Core/Uow 与 EventBus API 接入完成后，安排一次默认主方案本地测试作为 checkpoint。无新候选或缺陷不重复全量。
- 有界当前性能/资源 probe：net8 Release，串行、无外部服务，预热100次、3组每组1000次、整个窗口60秒。覆盖业务 scope 建立/释放、同 scope 工厂循环、Uow/事件构造失败清理。记录耗时、分配、GC次数与资源计数；资源正确释放是正确性断言，速度仅为观测，不创造新预算。先写 CIA 与固定窗口，再运行一次；历史 GC stress 不重跑。
- 原始命令、退出码、TRX、probe stdout/stderr 放 artifacts/fix-round-1/；失败不覆盖。交付 execution.md、符号到测试方法/原始结果映射及必要迁移说明。

## 分工与收口

- OpenCode / deepseek-v4.1-flash：独占本次生产、测试、使用说明修复及执行报告，依据 fix-review 技能注册状态；主线程不并行修改其文件。
- Codex / GPT-6.1-Sol high：实施后独立审查代码、兼容、回归强度与原始证据；不得把执行者自评等同验收。
- 如有新的 GC 根因调查需求，交回主线程安排 Astra/xhigh；实施者只修这里已有因果证据的5项，不重开已关闭 GC 调查。
- 最多3轮明确可执行的 Review/Fix；NOT_VERIFIED/性能无预算/外部未运行不启动额外修复。
- 按 UTF-8 读写；不 stage/commit/push/reset/restore/clean，不升级包、不改版本，不发送飞书等外部通知。task-finish 仅使用 `--no-notify`。
