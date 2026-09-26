<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-DYNAMIC-API-20260925-001
AI_REVIEWED_AT: 2026-09-25T21:13:34.4849350+08:00

# 独立代码审查

## 结论与证据

`PASS`：FIX-001 至 FIX-008 均已闭环，当前未发现未解决的 MUST_FIX 或 SHOULD_FIX。Review Fix Round 3 的原样文本响应修复经过真实 TestServer 和 JSON 字符串自定义序列化器回归；本轮只更新独立审查报告，不修改业务代码或测试。

- 基线提交：`2c36316e210ad2081520fe56e7413c748bad92dd`；候选为当前未提交工作区，含新增未跟踪项目。对照完整计划、Review Fix Round 3 执行报告、工作区及暂存区差异、真实源码/测试/文档和根目录 `AGENTS.md`，无更具体子目录规则。普通 `git diff` 不包含未跟踪文件，已直接检查对应源码与测试。
- Change Impact：Round 3 只修改客户端成功响应处理和两处客户端测试；公共契约形状、MVC 服务端路由、TFM、项目图、SQL Provider 与 Benchmark 未变。`Task<string>` 的 TestServer 调用属于跨客户端/MVC 边界，风险 MEDIUM。
- 验证证据复用：Round 3 完成报告及同一工作区的运行结果记录，聚焦测试在 net6.0/net8.0 各 2 passed，客户端完整测试各 19 passed，MVC 完整测试各 70 passed、1 skipped，客户端生产项目构建通过。当前生产源码与测试文件未再修改；本次只修改审查报告。L0 源码和调用链检查、`git diff --check` 与暂存区差异检查通过；未重复执行同一候选的完整测试或全解决方案测试。
- FIX-008 直接证据：`BingDynamicApiClientProxyFactory.ExecuteAsync` 对目标 `string` 且非 JSON 的 `text/*` 调用 `ReadAsStringAsync(cancellationToken)`，其他结果仍走可替换 JSON 序列化器；非成功状态先走远程异常解析。真实 TestServer 的 `GetRawTextAsync` 返回 `plain:text-response`，客户端代理成功取得原文；`application/vnd.bing-test+json` 字符串响应由自定义序列化器反序列化且计数为 1。

## 计划验收矩阵

| 计划项 | 状态 | 依据 |
| --- | --- | --- |
| 显式发现、排除、DI/AOP 激活 | PASS | 接口型代理由真实 HTTP 集成测试验证，显式 MVC Controller 原路由保留。FIX-001、FIX-002 已关闭。 |
| 路由、绑定、上传下载、冲突检测 | PASS | 同源重复 Query/Form 名启动报错，不同来源同名允许；既有路由、上传下载及冲突测试通过。FIX-005 已关闭。 |
| API 描述与动态客户端 | PASS | 泛型参数类型身份与重载选择、`Task<string>` 原样文本响应均通过真实 TestServer 闭环；JSON 字符串仍由自定义序列化器处理。FIX-007、FIX-008 已关闭。 |
| 上下文、版本、扩展、文档及 E2E | PASS | 客户端代理通过真实 TestServer 获取定义并调用 MVC Action，原有上下文/版本/扩展测试保留。FIX-006 已关闭。 |

## 历史 Finding 复核

以下“问题”描述保留上一轮发现时的状态；当前结论以每项的 Finding 状态和“复核”证据为准。

### FIX-001：接口型 DI/AOP 代理缺少激活验证

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：MVC Controller 类型登记为应用服务实现类，而工厂返回服务接口实例；现有集成测试仅覆盖实现类子类代理。接口型 AOP/装饰代理能否在此 Controller/Action 组合下执行，当前证据不足，不能直接断言激活必然失败。
- 证据：`framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiRegistration.cs:29` 保存实现类；`BingDynamicApiServiceCollectionExtensions.cs:76-82` 将实现类映射到 `GetRequiredService(ServiceInterface)`；`framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/DynamicApiTestAppService.cs:87-94` 的代理继承实现类。
- 影响：计划要求的“沿用应用服务 DI/AOP 实例”在接口型代理上没有端到端证据，宿主采用此种代理时存在兼容性风险。
- 修复目标：证明动态端点能使用实际注入的接口型代理，并保留 MVC 绑定和契约元数据；若失败，再修复激活/调用链。
- 修复要求：补充接口型代理场景的 HTTP 集成测试；仅在复现失败后调整 Controller/Action 激活，不通过直接创建实现类绕开代理。
- 验证方式：注册不继承实现类的接口代理，HTTP 集成测试断言请求成功且拦截逻辑执行；原有子类代理测试继续通过。
- 复核：`BingDynamicApiActionDescriptorProvider` 将动态 Action 的 Controller 类型设为服务接口，原实现类型保存在 descriptor 属性供描述注册表使用；`DynamicController_UsesAnInterfaceOnlyServiceProxy` 与原子类代理测试在 net6.0/net8.0 通过。Round 1 的新增测试先复现 `InvalidCastException`，修复后通过。

### FIX-002：显式 Controller 实现服务契约时原端点被静默改写

- 严重程度：HIGH；处理要求：MUST_FIX；Finding 状态：CLOSED。
- 问题：扫描没有排除同时为显式 MVC Controller 的 `IAppService` 类；约定随后移除非契约 Action，并清空原 Controller 路由 selectors。
- 证据：`framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiRegistration.cs:153-157` 仅按服务类型筛选；`BingDynamicApiApplicationModelConvention.cs:36-43` 直接移除 Action/清空 selectors。现有冲突测试使用另一显式 Controller 类型，未覆盖同一类双角色。
- 影响：启用动态 API 后原显式 URL/Action 可悄然消失，且冲突检测看不到已删除的路由。
- 修复目标：显式 Controller 不被动态发现静默覆盖。
- 修复要求：对双角色类型明确排除或启动失败；若支持双入口，应保留原端点并检测最终路由冲突。
- 验证方式：使用 `[ApiController]`、`[Route]`、`ControllerBase` 且实现 `IAppService` 的同一类，断言原路由保留或启动时报清晰错误。
- 复核：注册扫描已排除默认 MVC 识别的显式 Controller 类型；`ExplicitControllerThatImplementsAppService_PreservesItsOriginalRoute` 在 net6.0/net8.0 通过。

### FIX-003：首次元数据请求取消污染共享缓存

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：第一个调用者的取消令牌被捕获进全局共享的 `Lazy<Task<...>>`；A 取消会取消 B 共用的抓取。A 单独取消时取消任务仍留在缓存，下一调用先失败一次才移除。
- 证据：`framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiDefinitionProvider.cs:31-36,146-155`。
- 影响：一个请求取消影响其他请求或其后第一次重试。
- 修复目标：共享抓取与单个等待者的取消独立；失败/取消任务不再污染缓存。
- 修复要求：保留同键单次抓取和缓存，但不以某个等待者的令牌控制所有等待者。
- 验证方式：并发 A/B 同键请求取消 A，B 仍成功；单独取消首调后新令牌下次直接重新抓取成功。
- 复核：共享抓取不再捕获首位等待者令牌，等待者独立取消，失败/取消抓取按原 `Lazy` 实例移除。`Should_cancel_one_definition_waiter_without_cancelling_shared_fetch`、`Should_remove_cancelled_definition_fetch_and_allow_retry` 在 net6.0/net8.0 通过。

### FIX-004：错误体读取不响应调用取消

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：非 2xx 响应体使用无令牌的 `ReadAsStringAsync()` 全量读取。远端先返回错误状态头、再延迟或不结束正文时，调用者取消不能及时中断错误体读取。
- 证据：`framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiHttpErrors.cs:11-13`；业务、流和元数据错误路径调用见 `BingDynamicApiClientProxyFactory.cs:100,134`、`BingDynamicApiDefinitionProvider.cs:50`；发送使用 `ResponseHeadersRead`。
- 影响：错误路径可能无限等待或缓冲大量正文，偏离计划中的取消支持。
- 修复目标：三条错误路径的解析受调用令牌控制，取消时释放响应。
- 修复要求：将令牌传递至错误体读取，保留 HTTP 状态及远程错误解析。
- 验证方式：模拟错误状态头已返回而正文读取阻塞，取消后及时结束并释放响应；普通错误解析继续通过。
- 复核：请求令牌经 `HttpRequestMessage.Options` 传至 `ReadAsStringAsync(token)`；阻塞错误流取消/释放测试与普通远程错误测试在 net6.0/net8.0 通过。

### FIX-005：重复绑定名称未按计划启动失败

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：两个方法参数同时声明 `[FromQuery(Name = "q")]` 时，两个值都从同一键绑定；注册仍成功，描述又生成两个同名查询项，调用方无法独立传值。
- 证据：`framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiRegistration.cs:85-114` 只检查多个 Body；`BingDynamicApiApplicationModelConvention.cs:134-173` 只检查路由占位符是否匹配参数，没有检查同来源最终键名重复。
- 影响：违反“无法唯一绑定的签名启动时报清晰错误”，可能静默传入相同的值。
- 修复目标：注册时识别同来源、同键名且不可区分的参数。
- 修复要求：根据最终契约/实现绑定元数据报出服务方法、来源和重复名称；不误判合法的不同来源同名参数。
- 验证方式：两个 `[FromQuery(Name = "q")]` 参数及重复 Form 名分别启动失败；合法不同来源同名参数继续成功。
- 复核：应用模型按最终 BindingSource 与 `BinderModelName` 检查同来源键名，Form/FormFile 合并分组；重复 Query/Form 与跨来源同名三项测试在 net6.0/net8.0 通过。

### FIX-006：缺少客户端直连真实测试宿主的端到端验收

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：服务端测试直接调用动态端点，客户端测试使用手写定义和 `StubHandler`；没有让新代理读取真实 API 描述并调用真实 MVC Action 的同一条链路。
- 证据：`framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/BingDynamicApiIntegrationTest.cs:62-189` 与 `framework/tests/Bing.DynamicApi.Client.Tests/BingDynamicApiClientTest.cs:18-45,303-356` 分别覆盖两半；计划明确要求测试宿主下的 CRUD、授权元数据、上下文、取消、流、异常及扩展能力端到端验证。
- 影响：ApiExplorer 描述和客户端参数/URL/文件约定之间的兼容性仍缺跨层证据。
- 修复目标：至少一组代理直连 MVC TestServer 的契约测试，覆盖真实元数据及主要调用链。
- 修复要求：通过 TestServer 的 HttpClient 供给具名 `IHttpClientFactory`，从真实定义发现后调用，不以手写描述替代；余下计划边界按风险补测。
- 验证方式：新增跨项目集成测试并在 net6.0/net8.0 跑受影响完整测试；显式 Controller 回归继续通过。
- 复核：`DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer` 用具名 `IHttpClientFactory` 的 TestServer handler，从真实元数据发现并调用 MVC POST，覆盖路径、查询、JSON Body 和响应；两个 TFM 均通过，显式 Controller 回归亦通过。

## 本轮 Finding 复核

### FIX-007：泛型输入参数导致客户端无法匹配方法签名

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：服务端描述把参数的 `Type.FullName` 原样写入 `TypeName`；泛型类型的 `FullName` 含嵌套实参的程序集限定名及逗号。客户端 `TypeNameMatches` 却在第一个逗号处截断，再与契约类型的 `FullName`、`Name`、`ToString()` 比较；例如 `List<int>` 三者均不匹配。调用在 `FindMethod` 阶段抛“找不到匹配签名”，请求未送达业务端点。
- 证据：`framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/BingDynamicApiDescriptionController.cs:110,136`；`framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiClientProxyFactory.cs:158-181,193-201`。只读诊断得到 ``List<int>.FullName = System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib, ...]]``，当前截断值为 ``System.Collections.Generic.List`1[[System.Int32``，与三种候选名称均不相等。现有客户端测试和真实 TestServer 测试只使用非泛型输入。
- 影响：共享应用服务接口中采用集合或泛型 DTO 作 Body 参数的方法，虽能在服务端形成动态端点和描述，但类型化代理无法调用，破坏“应用服务 → API 描述 → 客户端”主链路。
- 修复目标：泛型输入的类型身份能被正确比较，同时保留签名唯一性校验，不以忽略类型检查来掩盖方法重载歧义。
- 修复要求：使用结构化类型身份或能识别泛型嵌套边界的比较策略；确保完整 `FullName` 先精确匹配，仅在合理的兼容场景处理程序集限定名。
- 验证方式：增加 `List<int>` 和泛型 DTO 参数的客户端签名匹配测试，以及至少一个真实 TestServer 描述到代理调用的泛型输入用例；覆盖具有相同方法名、不同泛型参数类型的重载选择。随后按影响范围运行 net6.0/net8.0 客户端测试、相关 MVC 测试和生产项目构建。
- 复核：服务端 `GetTypeName` 现输出无程序集版本的 `Type.ToString()`；客户端先完整比较 `FullName`、`Name`、`ToString()`，再进入旧格式逗号兼容分支。同名 `ProcessAsync(List<int>)` / `ProcessAsync(List<string>)` 及泛型 DTO 输入均通过真实 TestServer 描述、路由和返回值验证；net6.0/net8.0 客户端各 17 passed，MVC 各 70 passed、1 skipped，两生产项目构建通过。完整类型身份仍参与唯一性匹配，未放宽为仅按方法名选择。

### FIX-008：`Task<string>` 原样响应无法由类型化客户端解析

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；Finding 状态：CLOSED。
- 问题：动态 MVC Action 对 `Task<string>` 返回未加 JSON 引号的文本；客户端对所有非流 `Task<T>` 成功响应无条件调用 JSON 反序列化器，因而将合法文本首字符当成 JSON token 解析并抛异常。
- 证据：`framework/tests/Bing.AspNetCore.Mvc.Tests/Bing/AspNetCore/Mvc/DynamicApi/DynamicApiTestAppService.cs:11,40` 声明并返回字符串；`BingDynamicApiIntegrationTest.cs:63-69` 断言实际 HTTP 正文为原始 `item:...`；`framework/src/Bing.DynamicApi.Client/Bing/BingDynamicApiClientProxyFactory.cs:136-140` 无内容类型或字符串分支；`BingDynamicApiClientAbstractions.cs:130-133` 直接 `JsonSerializer.DeserializeAsync`。Round 2 中相同生产候选的 TestServer 诊断在 `ProcessAsync(List<int>)` 返回 `integers:2,4` 后复现 `JsonException`，后续用例改为 DTO 才通过。
- 影响：共享服务接口中的普通 `Task<string>` 方法虽可正常发布并由 HTTP 直接访问，却不能通过新增的类型化客户端调用；服务端现有多个字符串端点均属于该形态，破坏计划中的应用服务到客户端链路。
- 修复目标：类型化客户端可读取服务端的原样文本字符串，同时不破坏 JSON 字符串、自定义 JSON 序列化器、取消和错误路径。
- 修复要求：按目标返回类型及响应媒体类型处理文本与 JSON；不得对所有非 JSON 响应盲目转成字符串，也不得改变显式 Controller 或全局结果包装语义。
- 验证方式：用真实 TestServer 的 `Task<string>` 动态方法证明代理返回原始字符串；补充 JSON 编码字符串和自定义序列化器的回归测试，再按影响范围运行 net6.0/net8.0 客户端测试、相关 MVC 测试与生产项目构建。
- 复核：客户端现在仅对目标 `string`、非 JSON `text/*` 成功响应读取原样文本，并传递取消令牌；`application/json`、`text/json`、`+json` 仍交给 `IBingDynamicApiJsonSerializer`。`DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer` 和 `Should_use_custom_serializer_for_json_string_response` 在 net6.0/net8.0 各 2 passed；客户端完整测试各 19 passed，MVC 完整测试各 70 passed、1 skipped，客户端生产构建通过。未改变服务端及显式 Controller 结果包装逻辑。

## 状态与交接

- `IMPLEMENTATION_STATUS: PASS`；`TEST_STATUS: PASS`（受影响的客户端与 MVC 双 TFM 完整测试通过）；`PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE`；`RESOURCE_EVIDENCE_STATUS: NOT_APPLICABLE`；`EXTERNAL_GATE_STATUS: NOT_APPLICABLE`；`RELEASE_STATUS: PASS`（本地计划验收范围）；`GOAL_STATUS: COMPLETED`。
- Task：`BING-DYNAMIC-API-20260925-001`；Review Round：4；Implementation：8/8 已闭环；Release Gates：5/5；Changed Scope：仅 `review.md`。No-Progress Check：CHANGED（FIX-008 生产实现和测试已改变并通过验收）。
- Validation Run：L0 为当前源码/测试调用链及差异检查；L1/L2/L3 复用 Round 3 同一候选的 net6.0/net8.0 聚焦、客户端完整、MVC 完整测试与客户端生产项目构建；L4 未执行全解决方案测试；L5 不适用。
- Completed：FIX-001 至 FIX-008。Open Actionable：无。Blocked Approval：无。Blocked External：无。Not Applicable：性能与资源门禁。Accepted Limitations：无。Verified Boundaries：无。Deferred：无。
- Next Action：STOP；按当前计划完成独立验收。未执行 `git add`、提交、推送或 PR。
