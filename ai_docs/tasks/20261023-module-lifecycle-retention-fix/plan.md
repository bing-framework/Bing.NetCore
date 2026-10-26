# 模块生命周期 DI 后台编译引用保留修正

- Task ID：20261023-module-lifecycle-retention-fix。
- 来源：用户已授权“继续修正功能”，由 Codex GPT-6-Astra/xhigh 调查 GC；Astra 已交付可重复因果证据和未应用候选。
- 设计依据：[Astra 调查](../20261023-module-lifecycle-gc/astra-diagnosis.md)第5节及其未应用补丁。本计划将该方案落实为独立实现/验收记录，不重新调查根因。
- 实施：OpenCode / deepseek-v4.1-flash，只执行已确定方案；独立验收：GPT-6.1-Sol/high。无需重复请求计划批准。

## 目标与边界

消除框架生命周期自身重复解析 IServiceScopeFactory 的触发点，并使两项既有弱引用回归在已证明的受控编译窗口内通过。保留初始化与关闭各自独立的 DI scope、异常与取消传播、释放次数及 legacy 分支行为。

已经确认的是 DI6 后台编译闭包可以在 Dispose 后持有容器，并足以使两个原断言失败。没有证明永久资源泄漏，也没有证明两份历史自然失败都只有这一个原因；不得把补丁宣传为修复 DI 的通用泄漏或保证任意应用 Dispose 后立即可回收。

## 实施

1. 保存真实工作区起点与本轮相关源码/现有 Core 和 Test 二进制身份；需要保留的旧二进制在重建前保存。本轮 artifacts 单独存放，复用 Astra 的红证据，不再重复旧候选诊断。
2. 在 BingModuleManager 按应用惰性缓存 IServiceScopeFactory，初始化和显式关闭通过该 factory 分别创建 scope。保持原解析时机；依据现有状态机核对并发初始化/关闭，不为推测增加新锁或改关闭协议。provider Dispose 回调继续不创建 scope，legacy 不改变。
3. 在 ModuleLifecycleTest 的 Create helper 中，将预先创建的同一个 Recorder 通过 singleton factory 注册，保持实例身份和原生命周期断言。Astra 只从源码指出 Recorder 同类触发点，实施者必须用直接覆盖/受控确认核对，不能声称此前已经实测。
4. 补充职责级回归：factory 只解析一次、两阶段 scope 不同且均正确释放；正常初始化/显式关闭、初始化失败、取消后清理、provider Dispose 路径；现有重入/并发/异步/legacy 行为复用或运行相关用例。覆盖自定义 IServiceProvider/工厂的契约，确认缓存的是同一应用的工厂而非 scope。
5. 保存执行报告、符号到测试映射和使用边界说明。既有 GC 调查、诊断工具报告及独立审查文件保持不变。

## 验证与验收

- 先做 CIA，精准编译及运行新增/受影响回归，覆盖 net6/net8 Release。
- 从 Astra 的探针复制出本轮候选验证版本，原 artifact 不改。有限监听并暂停 IServiceScopeFactory 与 Recorder 的编译事件，两个原测试在两 TFM 各确认一次。保留原断言与 GC 次数，输出事件/工厂解析计数；候选预期不触发这两个后台编译项且原断言通过。不得直接修改探针把失败当通过。
- 采用 Astra 已有闸门10秒、进程15秒、批次120秒等有界实验规则；先冻结本轮候选及工作量，不扩大为压力矩阵。不因未复现追跑。
- 运行完整 Bing.Core.Tests 的 net6/net8 Release，并按实际依赖影响选择消费者回归。无 SQL/Provider 创建链变化时不无因追加数据库测试。
- 无公共接口、依赖和构建图变更；不重跑无关性能矩阵。这里是低频生命周期解析变更，不宣称性能提升。
- Sol/high 独立验收代码、原测试保持、受控证据和自定义容器行为。通过后由主线程协调一次最终 Release 构建/默认 solution 检查点；实施阶段先不启动全量。最后检查点的新结果单独保存，不改写历史 FAIL。

## 文件与协作

主要修改：framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs、framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs，及必要的生命周期直接回归文件。

本任务的 plan.md 为实施输入；实现者写 execution.md、traceability.md、必要迁移/限制说明和 artifacts/。review.md 由独立 Reviewer 写。不得改 Astra 调查、旧 GC raw、FIX-003 工具、旧 review.md 或 Stage B 报告；父路线图由主线程维护。

保持 SDK10.0.400、net6/net8、现有依赖、API baseline 和性能预算。保留用户工作区修改；不 git add/commit/push/reset/restore/clean，不发送外部通知，task-finish 如使用必须 --no-notify。

## 停止与收口

只围绕本方案及直接暴露的问题修正，遵守 GOAL_RULES 的影响分析、证据复用与无进展停止。新候选可做对应回归，不能借本任务重跑已耗尽的旧假设。发现其它真实强根时保留失败并交主线程/Astra作定点判断，不由OpenCode扩展根因调查或通过额外GC、sleep、跳过断言、关闭并行来变绿。

交付状态分别报告：候选实现、精准/受影响测试、受控因果验证、最终全量及历史归因；工具 FIX-003 的通过与否不得充当生产修复证据。
