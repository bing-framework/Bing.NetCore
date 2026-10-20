# 功能缺口与后续工作清单

日期：2026-10-21。候选：`a99a16c4b55c407cc81d771822667044e2e9e9b1`。本文件按能力去重，不按 TODO、异常语句或未通过门禁数量计数。

## 数量口径

| 分类 | 数量 | 说明 |
|---|---:|---|
| 确定未实现功能 | **2** | G01 微信退款；G02 领域全局验证规则；公开入口无条件 NotImplementedException |
| 候选待确认功能 | **2** | C01 OAuth 统一工厂；C02 按租户自动选择文化；缺产品支持契约 |
| 冻结修复项 | **7** | F001–F007 → plan I01–I07；编译、验证覆盖和文档问题，不计新功能 |
| 兼容决策后的已确认缺陷 | **1** | F008 SQL 工厂作用域；运行时已复现，不能当待实现功能重复统计 |
| 外部/工具链/性能门禁 | **3** | F011–F013；缺验收证据不等于缺功能 |

上述 2 项是有明确证据的下界。仓库没有覆盖所有业务场景的已批准产品验收总表，且 AOP 依赖图尚未完整编译，因此不能断言“全仓只剩这两项”或给出整体完成百分比。扫描来源为 `artifacts/todo-source-scan.txt`，关键源码原文带行号保存在 `artifacts/source-evidence.txt`；是否构成缺口须结合调用链、文档与测试判断。

## 确定缺口

### G01 · P2 · BLOCKED_APPROVAL · 微信退款（F009）

- **定位**：`components/src/Bing.Biz.Payments/Wechatpay/Services/WechatpayRefundService.cs:30`，`RefundAsync` 无条件抛出 `NotImplementedException`。同模块参数 builder、退款 URL 与公开服务入口已经存在；它们不执行退款请求，也不解析结果。
- **根因/用户影响**：入口已公开而实现缺失，调用无法退款。不是抽象基类要求用户 override，也不是 Provider 明确不支持。
- **去重范围**：微信退款的请求构造、签名/证书、传输、响应处理为同一能力，不分别计数；支付宝等其他提供者不因这一处缺失被连带判缺。
- **需先决定**：支持哪一版微信协议；凭据与证书来源；幂等键和重试规则；受理/完成/失败的返回语义；通知验签与退款查询是否属于本次范围；公开接口是否能容纳这些语义。
- **建议路线 R-G01**：先产出协议和支持范围说明 → 冻结请求/响应契约 → 用模拟 HTTP/固定签名向量实现与测试 → 失败/超时/取消/重复调用测试 → 获授权后在隔离沙箱验收。只新增必要实现，复用现有依赖版本。
- **验收**：有效请求、业务失败、网络失败、取消、重复请求都有确定行为；敏感凭据不入日志；契约与文档一致；有沙箱结果或明确记录沙箱阻断。禁止调用真实资金退款、私自升级支付包或以返回成功替代实现。
- **冻结条件**：维护者确认上述协议和范围后建立独立任务，建议 `wechat-refund-contract-and-implementation`。未确认前不纳入 Luna 的7项修复。

### G02 · P2 · BLOCKED_APPROVAL · 领域全局验证规则（F010）

- **定位**：`framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/DomainObjectBase.cs:131`，`UseValidationRules` 无条件抛出 `NotImplementedException`。`UseStrategy`、`UseStrategyList` 有实现，只解决实例策略选择，不等于全局规则机制。
- **根因/用户影响**：公开的“启用全局规则”入口没有规则发现和应用实现，调用立即失败。不能用空方法替换异常后宣称功能完成。
- **去重范围**：规则注册/发现、继承/覆盖、执行顺序和生命周期合并为一个能力；不同实体泛型不是不同功能。
- **需先决定**：规则注册源（显式注册、DI 或其他方式）；实体与规则生命周期；基类/派生类泛型匹配；重复规则处理与优先级；同步/异步执行约束；缓存和配置变化是否支持。
- **建议路线 R-G02**：先定义兼容契约和规则源 → 确认是否需要新增公开 API → 实现默认规则提供/应用 → 各实现直接测试 → 对现有实例策略做回归 → 补使用文档。批准前不改 API baseline。
- **验收**：无规则、单规则、多规则、继承覆盖、重复注册、失败与异常路径、并发调用和配置有效期均有明确结果；与 UseStrategy/UseStrategyList 的组合不隐式覆盖调用方选择。
- **冻结条件**：维护者批准规则语义后建立 `domain-global-validation-rules`。不能由 Executor 自行发明全局静态注册表。

## 候选待确认

### C01 · P3 · BLOCKED_APPROVAL · OAuth 统一工厂

- **定位**：`components/src/Bing.Biz.OAuthLogin/IOAuthLoginFactory.cs:6` 为无成员接口；搜索未见对应实际工厂实现/服务注册。各平台 Provider 实现仍存在，模块 README 描述手工创建或注册使用路径。
- **判断**：空接口证明没有统一工厂能力，但不能证明这是已承诺必须交付的功能。现有平台登录能力不能整体判为缺失。
- **决策**：是否保留该预留抽象；若支持，选择器键/平台范围、配置/凭据来源、DI 生命周期、非法平台行为如何定义。
- **验收/后续**：批准后形成 `oauth-factory-support-contract`，用各受支持平台的选择成功/失败测试及无真实凭据的请求契约验证。若确定不支持，文档明确该接口为预留，不擅自删除 public API。

### C02 · P3 · BLOCKED_APPROVAL · 按租户自动选择文化

- **定位**：`framework/src/Bing.AspNetCore.MultiTenancy/Bing/AspNetCore/MultiTenancy/MultiTenancyMiddleware.cs:83` 的“设置国际化”TODO；租户解析/上下文切换和后续 middleware 调用已经实现。
- **判断**：缺 tenant→culture 选择策略，但未见确定的文化配置来源和优先级要求。不能将整个多租户或本地化模块判为未实现。
- **决策**：租户默认文化、用户偏好、请求头、路由和系统默认之间的优先级；Culture/UI Culture 区别；非法文化与无租户 fallback；与 ASP.NET Core localization middleware 的顺序。
- **验收/后续**：批准后建立 `tenant-culture-resolution`，覆盖优先级、fallback、跨请求/异步隔离、异常后恢复与 middleware 顺序；不得泄漏一个租户的文化给下一个请求。

## 已实现、抽象契约与明确限制

以下 ACCEPTED_LIMITATION 表示当前源码/文档/测试明确界定的支持边界，**不代表本次替维护者批准新的发布例外**。若产品需要扩大支持，另开设计任务。

### L01 · P2 · ACCEPTED_LIMITATION · FreeSQL 通用 IQueryable 并非原生异步

- **证据**：`framework/src/Bing.FreeSQL/Bing/FreeSQL/FreeSqlAsyncQueryableProvider.cs` 已实现 Execute：取消检查、同步 LINQ 执行、Task.FromResult/FromException；CanExecute 明确 false。`framework/tests/Bing.FreeSQL.Tests/Core/FreeSqlAsyncQueryableProviderTest.cs` 含 `CanExecute_ShouldRemainFalse`。
- **可见行为**：直接调用包装方法可能阻塞调用线程；取消令牌只在操作开始前检查，不是数据库执行中的取消；CanExecute=false 使通用选择器不会把它声明为原生异步 Provider。
- **Fail fast/配置/替代**：已取消令牌返回取消 Task；没有开关使此实现自动变原生异步。需要真异步 IO 时使用已支持的原生查询路径或另行设计适配器。
- **验证边界**：静态确认存在实现；当前 FreeSQL 套件被 F001 阻断，本次未宣称对应测试通过。修正文档不等于承诺异步性能。

### L02 · P2 · ACCEPTED_LIMITATION · 动态客户端支持的返回形状有限

- **证据**：`framework/src/Bing.DynamicApi.Client` 的方法契约校验与对应 Tests，当前只支持声明的 Task/Task<T> 异步返回形状；同步/ValueTask 等形状显式拒绝。两类动态客户端本次本地测试已执行，统计见 test-summary.md。
- **可见行为**：不支持的接口签名初始化/解析时失败，不会自动转换；请求 body 数量等同样有契约约束。
- **配置/替代**：用受支持签名定义接口，或在调用层适配。没有批准将所有 CLR 返回形状纳入支持，不把每个 NotSupportedException 单列功能。

### L03 · P2 · ACCEPTED_LIMITATION · SQL Provider 能力不完全相同

- **证据**：`Bing.Data.Sql` 能力/语法校验、五 Provider 实现与直接单测；完整 SQL 和 SQLite 本次有执行证据。
- **可见行为**：不支持的表达式、对象命名、跨数据库/Provider 组合按能力边界拒绝。支持某一 Provider 不意味着所有数据库特性都能统一抽象。
- **Fail fast/配置/替代**：沿用具体异常和 Provider 能力判断，选择受支持表达式/同库查询或明确原生 SQL 路径；不通过放宽校验“补全功能”。本次没跑外部数据库，不能把 SQL 生成通过当外库执行通过。

### L04 · P3 · ACCEPTED_LIMITATION · 需要业务实现的扩展点及测试占位

- **证据**：MVC 通用控制器的 protected virtual `ToItem` 要求派生类提供业务映射；某些 OAuth/支付枚举或不适用动作明确拒绝；admin `TestMessageEventHandler` 的异常用于测试失败路径。
- **可见行为**：未 override 必需映射会失败；测试异常只作用于测试夹具。抽象/虚方法本身、受限 Provider 分支、测试用 NotImplementedException 不合并为新的产品缺口。
- **配置/替代**：使用项目文档要求的派生实现和受支持动作；这不豁免已公开且无条件失败的 G01/G02。出现具体已配置业务链仍必然失败时，按新缺陷重新审查。

### L05 · P2 · ACCEPTED_LIMITATION · 可变查询对象与缓存扫描有所有权边界

- **证据**：SQL Query/Builder 为可变构建状态，Clone 有单独隔离路径和测试；缓存 SCAN/批量路径及已有边界说明不提供事务快照语义。
- **可见行为**：同一查询对象不应被多个线程并发改写；缓存扫描期间有写入时不能保证稳定快照。不能把无支持承诺的共享对象并发或快照一致性当已实现能力。
- **Fail fast/配置/替代**：这两类限制不保证始终 fail fast；使用独立 Query/Clone，缓存需要强一致快照时使用专门数据/快照设计。不能以参数配置把 SCAN 变事务快照。F008 则是在正常独立请求 scope 下也失败，不能借此边界豁免。

## 已经实现而不再计缺口的条目

| 条目 | 当前实现证据 | 当前验证边界 |
|---|---|---|
| 领域事件自动排空 | EFCore UnitOfWorkBase.PrepareDomainEvents；FreeSQL UnitOfWorkBase.RegisterPendingDomainEvents 与 StoreBase 事件源登记；DomainEventDispatchSession 有循环排空、轮数上限、成功后清理 | Ddd.Domain 单元套件本次通过；EFCore/FreeSQL 及外部 CAP 当前端到端尚未完整验证 |
| FreeSQL LINQ 执行包装 | Execute + 各操作调用，不再是空返回 stub | L01 的同步兼容限制；不等于原生异步 |
| 缓存批量/扫描相关能力 | CSRedis/FreeRedis/EasyCaching 实际实现及既有测试/历史任务 | 本地 Caching/EasyCaching 部分已跑；外部 Redis 正式预算没有当前结论 |
| SQL 映射/Provider/查询工厂 | 当前实际实现和本次大量直接测试/SQLite | 存在 F008；外部 Provider 执行门禁仍未通过 |

## 外部与性能门禁

| ID / 状态 / 优先级 | 当前缺口 | 解除条件 |
|---|---|---|
| F011 / BLOCKED_EXTERNAL / P1 | 外部 Provider、Redis Cluster/Hybrid、admin CAP、蓝绿与受保护 CI 没有本次证据 | 本地编译问题先解决；维护者提供明确非生产环境与运行窗口；执行既有门控命令，记录实际候选/配置/退出码与结果 |
| F012 / BLOCKED_EXTERNAL / P2 | CodeSmith net45 引用程序集缺失；MSB3644 | 提供匹配工具链并构建旧方案，或单独批准支持范围调整；不自行升级 TFM |
| F013 / BLOCKED_EXTERNAL / P1 | 当前正式性能/资源预算未验收；Dry smoke 不足 | 指定正式主机/隔离服务和已有预算，按真实热路径运行必要矩阵；缺正式预算时另列 BLOCKED_APPROVAL，不自行发明阈值 |

性能待办的依据：当前 SQL Dry smoke 为真实测量，但样本不足以估计稳定 P95；历史 Redis budget JSON 有多项未通过，仅能证明对应历史候选/环境的状态，不能算当前失败或当前提升。不得直接把历史“预算失败”变成无限优化清单。

## 推迟与不适用

- **D01 · P3 · DEFERRED**：无已复现问题的普通 TODO/命名/重复注释清理（如 Logging 的泛化优化标记、EntityBase 注记、导航属性已注释分支）。原因：不能据此证明行为缺陷，批量清理会扩大本次修复。当前没有独立发布阻断证据。建议未来 `focused-maintenance-todos` 按真实使用场景逐条确认；不算功能候选数量。
- **D02 · P3 · DEFERRED**：DocFX API 站点生成/部署与展示完善。已有生成入口，缺网站产物不等于缺运行时功能；本次修复 XML 结构足够。当前代码包发布是否依赖网站应由发布流程决定，本次不新增门禁。建议 `api-docs-publishing`，不自动发布。
- **N01 · NOT_APPLICABLE**：本次没有优化实现，没有合法 before/after 性能提升声明；新增未曾存在的能力也不得伪造历史 before。
- **Verified Boundaries**：本次未通过重资源实验建立新容量边界。历史 timeout 保持历史归属，不拿来推算当前吞吐或内存。

## 移交状态

Completed：本次审查、原始证据和可执行计划。

Open Actionable：F001–F007，冻结修复 0/7。

Blocked Approval：F008、G01/G02、C01/C02。Blocked External：F011–F013。Accepted Limitations：L01–L05。Deferred：D01/D02。Not Applicable：N01。

后续由父会话选择已确认修复交给 Luna、再由 Sol 独立验证；本会话到此停止。不得只因整体发布仍 BLOCKED 自动实施候选功能或循环重跑外部/性能验证。
