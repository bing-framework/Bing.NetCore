<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-COMMON-ATTRIBUTES-DECOUPLE-20260930
AI_REVIEWED_AT: 2026-10-03T00:00:00+08:00

# 独立审查报告

## 历史审查结论（2026-09-30，当前结论见文末）

本轮已完成原审查列出的本地代码和测试补齐项。FIX-015 至 FIX-022 已关闭；FIX-017 的真实 SQLite 持久化测试已在本地依赖缓存中运行通过。

当前 OPEN_ACTIONABLE = 0。剩余工作属于外部环境验证或独立包发布决策，不应通过继续修改本仓库代码来规避。

## Findings

### FIX-015：异常提示注册污染和不可撤销

- 状态：CLOSED
- 修复：ExceptionPrompt.RegisterPrompt 返回可释放注册令牌，读取时使用锁保护快照；重复提示返回空令牌；测试在结束时退订。
- 验证：Bing.Core.Tests 的 net8.0/net6.0 全量均为 612/612。

### FIX-016：重复 EnableAop 丢失后续配置

- 状态：CLOSED
- 修复：基础服务只注册一次，每次调用的配置委托分别保留；后续调用仍可启用 Bing 参数验证。
- 验证：ParameterValidationRegistrationTest.EnableAop_WhenCalledAgain_ShouldApplyLaterConfiguration，AOP net8.0/net6.0 全量均为 102/102。

### FIX-017：Admin SQLite 没有证明真实持久化

- 状态：CLOSED_LOCAL
- 修复：新增 EFCore 和 FreeSQL 的真实 SQLite 写入、跨作用域回读、异常不持久化测试，并让连接/ORM 生命周期覆盖 Host。
- 验证：Admin EFCore 测试 6/6 通过，Admin FreeSQL 测试 6/6 通过；SQLite 结果已确认。分布式 MySQL/CAP 结果仍需专用环境验证。

### FIX-018：AOP 插件回收路径不足

- 状态：CLOSED
- 修复：使用实际插件夹具程序集、可收集 AssemblyLoadContext、真实 IgnoreAspect 代理调用和容器释放验证。
- 验证：IgnoreAspectPluginAssemblyResourceTest 已包含在 AOP 两目标框架 102/102 全量回归中。

### FIX-019：Sample 请求和停止缺少自动证据

- 状态：CLOSED
- 修复：新增 Bing.Samples.WebApi.Tests，请求 /sample/lazy 后断言业务 Lazy 值，再调用 StopAsync 并确认监听端口关闭。
- 验证：Sample 冒烟测试 1/1 通过。

### FIX-020：文档和映射失真

- 状态：CLOSED
- 修复：更新 AspectCore 迁移说明、工作单元执行器说明、Sample 说明和本任务执行记录；映射补充到新增测试方法和 NuGet 验证脚本。

### FIX-021：缺少真实 NuGet 消费者验证

- 状态：CLOSED
- 修复：新增 verify-nuget-consumer.ps1，打包契约和 AOP 包，在临时本地源中编译 netstandard2.0 契约消费者、编译 net6.0 AOP 消费者并运行类型转发检查。
- 验证：脚本通过。当前使用 SDK 8.0.425，网络审计源不可用只产生 NU1900 警告。

### FIX-022：工作单元标记位置与去重覆盖不足

- 状态：CLOSED
- 修复：增加接口方法、实现方法、接口类型/实现类型及重复标记的代理测试；同时修正实现方法标记的代理回调，避免 AspectCore 3 接口代理元数据转换异常。
- 验证：UoW 过滤测试 13/13 通过；AOP 两目标框架全量 102/102 通过。

## 计划执行映射

| 阶段 | 状态 | 审查结果 |
| --- | --- | --- |
| T1 公共特性抽离 | PASS_WITH_BOUNDARY | 契约类型已中立；Bing.Utils 1.5.0 的已发布 nuspec 仍声明 AspectCore.Extensions.Reflection 2.4.0 的传递下限。 |
| T2 AspectCore 执行器 | PASS | 参数验证、Ignore、属性注入、重复配置和真实插件回收均有直接验证。 |
| T3 工作单元执行器 | PASS_WITH_EXTERNAL | 代理行为、标记矩阵、去重及 Admin EFCore/FreeSQL SQLite 持久化已验证；MySQL/CAP 联动仍需专用环境。 |
| T4 依赖缩减与框架恢复 | PASS_WITH_BOUNDARY | Bing 工程无直接 Bing.Utils 源码依赖；要消除已发布包传递依赖需发布新的 Bing.Utils 包。 |
| T5 遗留项和文档 | PASS_WITH_EXTERNAL | Sample、NuGet 消费和执行记录已补齐；SDK 8.0.424、Admin 完整构建及 MySQL/CAP 仍需外部环境。 |

## 当前 TODO

- [ ] 在专用 MySQL/CAP 环境验证分布式事务联动；不得用 SQLite 或 Mock 代替。
- [ ] 安装并使用仓库锁定 SDK 8.0.424 重跑受影响测试；当前 8.0.425 结果只能作为实际环境证据。
- [ ] 若发布要求契约包完全没有 AspectCore 传递依赖，另行维护不依赖 AspectCore.Extensions.Reflection 的 Bing.Utils 版本或拆分反射包；这不是当前源码迁移可以解决的事项。

本轮已新增 `AdminMySqlCapIntegrationTest` 和
`eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1`。测试覆盖 EFCore
`AdminUnitOfWork` 与 CAP/RabbitMQ 的成功提交、业务回滚和回滚消息不消费；运行脚本提供专用测试库校验、端口预检、脱敏诊断和 TRX 验收。当前本机预检因未设置 MySQL/CAP 门控及连接环境而为 `Blocked/Preflight`，所以第一项 TODO 已具备可执行入口但仍等待专用外部环境，分布式事务结论保持 `BLOCKED_EXTERNAL`。

同时修复 FreeSQL Admin 工作单元在 CAP 出箱动作、保存或取消失败时未回滚底层事务的问题，并保留原始异常与回滚异常。

FreeSQL Admin 项目在临时离线包源下重新编译，现有测试 6/6 通过；这只覆盖本地 SQLite/AspectCore 行为，不能替代 FreeSQL 的真实 MySQL/CAP 联动。

EFCore CAP 夹具已改用每次运行唯一业务键，避免专用测试库中的历史数据使重复执行产生假失败；FreeSQL 的 CAP MySql 适配器已与 CAP 核心/RabbitMQ 统一到 8.0.1。两项只改善可重复性和依赖一致性，仍需专用 MySQL、RabbitMQ 环境完成真实联动验收。

仓库中的 `framework/src/Bing.Events.Cap.MySql` 是未被当前 Admin 工程引用的旧 CAP 3.1.2 源码镜像；本轮没有把它作为 8.0.1 验证路径，后续重新启用需单独迁移。

## 未考虑到的边界

- 默认 AssemblyLoadContext 中的可信插件程序集不能卸载，本轮只验证适配器缓存和可收集测试夹具的回收。
- 不响应取消的外部代码不能被托管框架强行终止；清理未完成时不报告为已释放。
- AspectCore 对同步/异步释放路径的选择遵循底层容器，Bing 不重复释放同一资源。
- ICommitAfter 在数据库已经提交后抛异常时，调用方仍会收到失败；重试和幂等策略需要业务层定义。
- Autofac 执行器、NativeAOT/Source Generator、非可信插件隔离和多版本插件求解不属于本任务。

## Bing.Utils 依赖核对

本轮核对的是已安装的官方 bing.utils.1.5.0.nupkg，其 nuspec 在 netstandard2.0、net6.0、net7.0 和 net8.0 组均声明：

AspectCore.Extensions.Reflection，版本下限 2.4.0。

这表示它不是 Bing 源码项目的直接 ProjectReference，但会作为 NuGet 传递依赖进入 Core 依赖图。NuGet 的下限约束在 AOP 3.0.0 图中可统一到 Reflection 3.0.0；因此不能把“无直接引用”表述成“无任何传递依赖”。

## 验证证据

- Bing.Core.Tests：net8.0 612/612，net6.0 612/612。
- Bing.Aop.AspectCore.Tests：net8.0 102/102，net6.0 102/102。
- Bing.AspNetCore.Mvc.Tests：net8.0/net6.0 各 58 通过、1 个既有跳过。
- UoW 标记位置专项：13/13。
- WebApi Sample 自动冒烟：1/1。
- NuGet 最小消费者脚本：netstandard2.0、net6.0 编译及类型转发运行均通过。
- Admin EFCore SQLite 持久化测试：6/6 通过。
- Admin FreeSQL SQLite 持久化测试：6/6 通过。

## 历史状态

- IMPLEMENTATION_STATUS = COMPLETE_LOCAL
- TEST_STATUS = PASS_LOCAL_PARTIAL_EXTERNAL
- RESOURCE_EVIDENCE_STATUS = PASS_WITH_LIMITATIONS
- EXTERNAL_GATE_STATUS = BLOCKED_EXTERNAL
- RELEASE_STATUS = BLOCKED_EXTERNAL
- GOAL_STATUS = ACTIVE_EXTERNAL

## 2026-10-03 当前对照审查

范围：公共特性解耦计划 T1–T5，重点审查最近新增的 Admin MySQL/CAP 事务路径、集成夹具和执行脚本。未修改生产代码或测试，未重新执行全量测试，也未重新探测外部服务。此前报告的 Core/AOP/MVC/Sample 通过数量仅为历史证据。

当前结论：基础特性中立化、类型转发、AspectCore 执行入口、工作单元适配包均已接入；不能继续沿用“仅剩外部环境”的结论。还存在 6 组本地可执行项及 2 组外部验证项。

| 计划范围 | 当前结果 | 缺口 |
| --- | --- | --- |
| T1 公共特性 | PASS_WITH_BOUNDARY | 源码类型已中立；包传递依赖仍需按实际源核对 |
| T2 AspectCore 执行器 | PASS（沿用历史证据） | 本轮未发现需重开已关闭项的证据 |
| T3 工作单元 | PARTIAL | 事务清理和真实 CAP 证据不完整 |
| T4 依赖迁移 | PARTIAL | 新增 CAP MySql 3.1.2 → 8.0.1 变更需要补齐影响闭包验证 |
| T5 测试及交付 | PARTIAL | 外部夹具、脚本和状态记录需修正 |

### FIX-023：回滚异常与取消清理不完整

- 状态：OPEN_ACTIONABLE；严重程度：HIGH；处理要求：MUST_FIX。
- 证据：FreeSQL `UnitOfWorks/MySql/AdminUnitOfWork.cs:61` 在同一个 try 中先调用 CAP Rollback，再调用 FreeSQL Rollback，前者抛出就跳过后者。EFCore 同名文件 `:61` 用业务取消令牌执行 RollbackAsync，回滚抛出会覆盖原始异常。
- 影响：故障/取消路径无法保证继续尝试其余清理，且根因可能丢失；现有 FreeSQL 6 项测试没有直接走 CAP 回滚失败分支。
- 修复目标与要求：分别尝试应执行的清理；保留原始异常和清理异常；明确提交完成后的失败不能报告为事务已回滚；清理令牌与业务取消语义分开。
- 验证：CAP 回滚抛出时仍尝试底层清理；取消期间执行清理；原始异常可定位；资源仅释放一次。先用可控故障验证，再用真实 Provider 验证事务结果。

### FIX-024：EFCore 业务表初始化存在顺序问题

- 状态：OPEN_ACTIONABLE；严重程度：HIGH；处理要求：MUST_FIX。
- 证据：`AdminMySqlCapIntegrationTest.cs:38/71` 先启动 CAP 宿主，`:165` 再调用 EnsureCreatedAsync。EFCore 的 EnsureCreated 不会在已有任意表的数据库中补建当前模型表；CAP 表先存在时，业务表可能未创建。
- 影响：干净测试环境或仅含 CAP 表的环境可能在真正验证事务前失败；跳过测试未覆盖此行为。
- 修复目标与要求：以确定且可重复的方式创建专用业务表，不依赖 CAP 与 EFCore 初始化竞争，不删除无关表。
- 验证：空测试库、仅有 CAP 表、业务表已存在三种场景均可运行。

### FIX-025：回滚消息断言与代理事务验收不足

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：`AdminMySqlCapIntegrationTest.cs:96` 只等待 5 秒断言未消费；没有检查 CAP published 存储是否存在该 payload。`:235` 在被拦截的目标方法内手工 SaveChangesAsync，因此成功持久化不能独立证明代理负责提交。
- 影响：消息延迟/重试可能产生假通过；代理失效也可能不影响该成功用例。
- 修复目标与要求：按唯一业务标识直接验证业务表和 CAP 出箱记录的原子结果；增加由代理完成提交的用例；短期未消费只能作为辅助断言。
- 验证：回滚后持久化出箱记录为零；成功后业务和出箱关联成立；代理提交次数、嵌套去重和 CommitAfter 顺序可观测。

### FIX-026：FreeSQL 真实 CAP 夹具与版本适配验证缺失

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：运行脚本 `:20` 只指向 EFCore；FreeSQL 测试只有 SQLite/Mock。`CapUnitOfWorkExtensions.cs:18` 仍反射非公开 Flush，找不到方法时静默跳过；MySql 包已变更到 8.0.1。
- 影响：FreeSQL 本地 6/6 无法证明 CAP 8 出箱、提交和回滚兼容。
- 修复目标与要求：补齐独立 FreeSQL MySQL/CAP 门控测试；核对当前 CAP 实际接口及 Flush 适配，不以仅编译通过判定兼容；覆盖成功、失败、取消、清理与提交后失败边界。
- 验证：本地夹具可编译/发现；专用环境中真实事务测试无跳过通过。写测试是本地项，真实运行单列为外部项。

### FIX-027：预检脚本存在错误放行和状态覆盖

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：脚本 `:203/237` 解析无效端口后设为 0，但 `:224/240` 和错误添加条件均要求端口大于 0，导致未添加端口错误；其余配置有效时可能报告 PreflightPassed。非 ValidateOnly 路径 `:272` 抛出后被 `:322` 覆盖成 Failed/Runner，丢失 Blocked/Preflight。
- 修复目标与要求：显式验证两种端口的 1–65535 范围；保留原始预检状态和诊断上下文；说明 --no-restore 的依赖还原前提及 PowerShell 7 要求。
- 验证：无需网络即可测试非数字、0、负数、超范围端口和缺少配置；ValidateOnly 与正式入口均准确保留阻断原因，不触发测试。

### FIX-028：补齐交付证据和准确状态

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 证据：execution.md 仍声明 OPEN_ACTIONABLE 为零；新增 FreeSQL 生产回滚路径没有直接测试映射；本轮 CAP 包变更后的完整 Admin 宿主验证未形成证据。
- 修复目标与要求：修复后更新生产符号到测试映射，记录 CAP 包版本偏离原“不升级 CAP”约束的原因和影响闭包验证；分别列出夹具未完成、代码缺陷、历史通过、实际跳过和外部阻断，不将 2 项跳过算作事务通过。
- 验证：映射对应真实方法和执行结果；新增 CAP 依赖闭包的宿主构建/实际依赖图可追溯。

### 外部项与边界

- EXT-001 / BLOCKED_EXTERNAL：EFCore 和 FreeSQL 分别在专用 MySQL/RabbitMQ 环境运行真实事务测试。现有诊断是 2026-09-30 的 Blocked/Preflight，本轮没有声称今天端点仍不可达。
- EXT-002 / BLOCKED_EXTERNAL：本轮 SDK 清单仍无 8.0.424；历史 8.0.425 测试不等于锁定版本验收。
- Bing.Utils：当前两份本机 1.5.0 缓存 nuspec 均列出 AspectCore.Extensions.Reflection 2.4.0；只能证明当前缓存/还原输入，不能据此断言用户所指包源、重发包或源码同样如此。发布/换源前应核对实际 nupkg 与还原图；本轮不要求用户发布新包。
- CAP 提供本地事务出箱与消息投递能力，验收描述应避免暗示跨库强一致或恰好一次消费；业务幂等与故障重试另列边界。

### 当前状态

- OPEN_ACTIONABLE = 6（FIX-023 至 FIX-028，按职责分组，不是缺少 6 个公共 API）
- IMPLEMENTATION_STATUS = NEEDS_FIX
- TEST_STATUS = PARTIAL；历史 EFCore 6 通过/2 跳过、FreeSQL 6 通过；本轮仅源码与本机证据核查，未重新运行测试。
- EXTERNAL_GATE_STATUS = BLOCKED_EXTERNAL
- RELEASE_STATUS = BLOCKED
- REVIEW_STATUS = NEEDS_FIX
