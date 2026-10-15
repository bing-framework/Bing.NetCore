<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-MODULARITY-DI-RESOURCE-20260929
AI_REVIEWED_AT: 2026-09-29T17:40:41.2702720+08:00

# 模块 DI 与资源生命周期复核

## 结论

对照 `plan.md`、最新 `execution.md`、真实源码、直接测试、资源工件和生产符号映射独立复核，当前计划内能力已经完成，未发现新的 MUST_FIX、SHOULD_FIX 或 OPTIONAL 问题。上一轮 FIX-018 与 TODO-002 均已闭环。

实现进度为 5/5，计划内直接验收为 5/5，当前可执行 TODO 为 0。锁定 SDK 8.0.424 未安装仍属于外部发布验证阻塞，不转换为代码缺陷，也不阻止本次代码审查通过。

## 计划验收矩阵

| 项目 | 状态 | 结论 |
| --- | --- | --- |
| T1 注册与所有权 | PASS | `IAsyncDisposable` 排除、多接口共享与限制、TryAdd/ReplaceExisting、开放泛型预检、`MultipleDependency` 状态迁移和所有权边界均有直接测试 |
| T2 循环与属性注入 | PASS | 构造、集合、Lazy、工厂、作用域、AspectCore 属性和混合循环均使用真实容器或受控子进程验证；构建期不可完整检测的动态路径已明确说明 |
| T3 异常、中断与清理 | PASS | 配置与初始化失败、取消、并发关闭、同步/异步释放、插件活动调用保留和退出后回收均有直接证据 |
| T4 内存与 GC | PASS（限定工作负载） | 20 次预热、100 次循环、释放计数、弱引用、托管堆、分配量和 GC 指标已有可审计工件；未外推为任意负载无泄漏 |
| T5 文档与追溯 | PASS | 使用指南、问题边界、执行记录及生产符号到测试方法映射与当前实现一致 |
| WebApi 当前候选 | PASS | SDK 8.0.425 构建成功，0 错误、1 个 net6.0 停止支持警告 |
| SDK 8.0.424 | NOT_VERIFIABLE | 当前环境未安装，实际验证使用 8.0.425；未修改 `global.json` 绕过 |

## 上轮问题闭环

- FIX-018：CLOSED。`Register_OpenGenericMultipleDependencyFirst_ShouldFailBeforeMutation` 分别覆盖 Singleton/Scoped，确认首接口为 `MultipleDependency` 时在生成开放泛型共享工厂前失败，并断言服务集合完全未写入。
- FIX-018：CLOSED。`Register_OpenGenericTryAdd_WithMultipleDependencyMiddle_ShouldRegisterRemainingDirectly` 分别覆盖 Singleton/Scoped，确认首接口被 TryAdd 跳过后，中间 Multiple 接口不建立共享入口，最后普通接口直接注册；测试断言没有工厂描述符且两个待注册接口均解析当前实现。
- TODO-002：CLOSED。新增方法已加入 `test-mapping.md`；Round 2 的定向与全量验证表述已统一；WebApi 已改为当前候选构建证据。

## 验证证据

- Round 3 Core `DependencyRegistrationBehaviorTest`：net8.0、net6.0 各 34/34 通过。新增测试只改变测试与文档，没有改变生产代码，因此复用 Round 2 的受影响项目全量证据。
- Round 2 Core：net8.0/net6.0 各 606/606 通过；AspectCore 各 45/45 通过；MVC 各 58 通过、1 跳过。
- 危险循环子进程：七种模式在 Debug/Release、net8/net6 下各 7/7 通过；相关实现和探针范围未变化。
- 资源探针：net8.0 5/5 通过，正常、失败、取消和插件更新均有释放次数及弱引用回收断言；相关资源路径未变化。
- 当前 WebApi 示例构建成功；`git diff --check` 返回 0，仅有工作区既有行尾转换提示。

## 已确认边界

- 未发现新的确定性生产缺陷、权限绕过、远程代码执行或可远程利用漏洞。
- Microsoft DI 对多个服务描述符返回同一可释放实例时可能重复释放；当前保留既有多接口共享契约，并提供单一资源所有者或非释放适配器方案。
- 开放泛型多接口实例共享不使用容器工厂转发，约定注册会提前拒绝；需要该能力时使用显式适配器注册。
- AspectCore 不提供字段注入，属性循环及动态工厂循环不能由构建期校验完整发现；危险路径已在子进程验证并写入使用边界。
- 插件存在不响应取消的活动调用时不会提前释放旧代资源；调用退出后才完成关闭和回收。
- 强杀进程、断电以及默认加载上下文程序集卸载不属于托管生命周期可保证范围。

## 状态与 TODO

- IMPLEMENTATION_STATUS: PASS。
- TEST_STATUS: PASS。
- RESOURCE_EVIDENCE_STATUS: PASS（限定当前探针工作负载）。
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE（没有批准的性能或内存阈值，不作性能达标声明）。
- DOCUMENTATION_STATUS: PASS。
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（SDK 8.0.424 未安装）。
- RELEASE_STATUS: BLOCKED。
- GOAL_STATUS: STOPPED_BLOCKED（本地可执行项全部完成，仅剩外部 SDK 门禁）。
- Completed：DI-001 至 DI-004、RES-001 至 RES-002、FIX-001 至 FIX-018、TODO-002。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：使用 SDK 8.0.424 重跑验收；工作流状态脚本因 `.agents/runtime/current-task.json` 权限/缺失未能自动登记，不影响代码候选。
- Deferred：无。
- No-Progress Check：所有本地可执行项已关闭，按规则停止自动 Fix 循环。
- Next Action：等待具备 SDK 8.0.424 的环境执行发布验证；当前代码无需继续修改。
