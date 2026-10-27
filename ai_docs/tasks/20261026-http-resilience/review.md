<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261026-http-resilience
AI_REVIEWED_AT: 2026-10-26T15:46:08.602Z

# HTTP 故障恢复功能完整性复审

## 结论

**PASS。** 按本任务已锁定的范围，两个动态 API 客户端的恢复功能已接入真实调用链，本轮未发现仍需处理的功能缺口。原 FIX-001～FIX-003 的关闭结论继续有效，FIX-004 的剩余历史文案已修正并核验。

本轮重新审查共享管线、运行时注册/绑定、请求资格、完整预算、描述缓存、响应读取及所有权；新增审查探针验证此前未单独覆盖的“错误描述体挂起”“所有等待者取消后缓存恢复”等组合。net6/net8 共 6 个场景/框架组合均通过。没有把既有测试总数或文件存在本身当作功能完整的证明。

- IMPLEMENTATION_STATUS: PASS
- TEST_STATUS: PASS（复用 233 项回归；另有本轮 6 个专项场景/框架组合）
- PERFORMANCE_EVIDENCE_STATUS: 有界开销参考；不包含生产阈值
- RESOURCE_EVIDENCE_STATUS: PASS_WITHIN_PROBE
- EXTERNAL_GATE_STATUS: NOT_RUN（外部 CI、生产试点及压力验证）
- RELEASE_STATUS: NOT_PUBLISHED
- OPEN_ACTIONABLE: 0
- NEXT_ACTION: STOP

## 审查范围与基线

Task：20261026-http-resilience；第 3 次独立审查，响应用户“实现的功能是否还有欠缺”的请求。依据完整计划、用户最终锁定要求、execution.md、当前生产源码与 Git Diff、test-map.md、已有验证产物，以及本轮新增探针。当前基线仍为 `87b68e7dadbffb2f1de58d1ea7013e678bc83888`；HTTP 生产代码自修复第 1 轮后未变，最新修复仅涉及文档；暂存区为空。

主代理负责功能覆盖、HTTP/运行时调用链、预算、响应与共享任务边界。luna_worker 独立核对注册顺序、大小写、绑定冲突、请求许可及配置冻结；具体判断经主代理结合锁定方案和已接受边界核验。无关 SQL、文件管理等工作区变更未纳入。

上一份报告保留于 [第 2 次独立审查](artifacts/review-round-2.md)，首次发现问题的报告见 [初次审查](artifacts/review-before-round-1-fix.md)。本轮只写当前 review.md 与审查产物，没有修改生产源码、既有测试、plan.md 或 execution.md。

## 功能验收矩阵

| 计划阶段 | 结论 | 真实实现与验证 |
| --- | --- | --- |
| HTTP-01 依赖与环境 | PASS | 两个新包支持 net6/net8；固定 Http.Resilience 8.10.0、直接 Polly 8.4.2；WebApiClientCore 2.1.6 及全局 Microsoft 基线保持。既有打包/独立消费有效，本轮双框架构建零警告、零错误。 |
| HTTP-02 共享恢复 | PASS | 按总发送超时→并发→重试→熔断→单次超时组合；默认无业务许可不重试，限制 GET/HEAD 和无请求体；MaxRetryAttempts=0 省略重试。408/429/选定 5xx、网络/永久错误判定集中处理，Retry-After 不截短后提前发送。 |
| HTTP-03 隔离、资源与诊断 | PASS | client/profile 标识消除边界碰撞，handler 轮换保留状态；原生事件区分两层超时；响应丢弃由 Polly 释放，取消/失败后许可可回收；两组宿主生命周期探针提供实际 Dispose 和弱引用观察。 |
| HTTP-04 WebApiClient | DEVIATED_OK | 原 builder 接入，无第二套代理；宿主传 linked token 覆盖内容读取；内侧签名逐次刷新，上传/下载按白名单排除；SDK 错误包装和流所有权保留。新 SDK 测试集中于恢复集成项目，原项目回归有效。 |
| HTTP-05 运行时中性扩展 | PASS | Legacy/null/null 默认兼容；新模式仅向管线发送一次；元数据区分描述/调用、重放资格和流；完整预算跨描述等待与响应读取，描述拥有独立预算；失败缓存移除和调用方流所有权保持。 |
| HTTP-06 运行时适配 | PASS | 返回实际 named builder；remote 匹配不区分大小写并保留实际 HttpClient 名称；旧 MaxRetries 冲突、缺失/重复绑定可拒绝；描述 GET 内建许可，业务调用还须宿主许可；注册时不创建临时 ServiceProvider。 |
| HTTP-07 集成交付 | PASS | 样例、签名/文件场景、迁移和回滚、解决方案登记、两个框架的独立 NuGet 消费、执行报告及符号映射已交付；FIX-004 的七阶段基线标签和三处历史语义已核验。 |

共享扩展与运行时适配没有发现 Stub、TODO 或未实现的生产入口。对源码中实际使用的选项、请求/响应处理、绑定查找和预算传递进行了核对，没有仅凭声明存在判定完成。

## 新增组合边界验证

审查探针使用真实运行时代理和可选适配注册，可控 HttpMessageHandler/阻塞流注入故障，明确记录下载次数、业务次数、异常和释放行为。代码：[Program.cs](artifacts/review-round-3/Program.cs)；结果：[results.json](artifacts/review-round-3/results.json)。

| 场景 | 需要成立的行为 | net6 / net8 结果 |
| --- | --- | --- |
| 描述返回 HTTP 400 后错误体挂起 | 错误体读取使用独立 DefinitionTimeout；映射 TimeoutException，保留取消内层；响应释放；下一次重新获取描述 | 均通过：异常 TimeoutException；最终响应 Dispose=1，流已释放；第二次成功，描述共 2 次、业务 1 次 |
| 两位等待者均取消，共享描述体仍挂起 | 两个等待者各自收到取消；共享下载不被这些取消立即终止；其独立预算耗尽后失败缓存移除 | 均通过：等待者取消保留；独立下载超时并释放；后续调用成功，描述共 2 次、业务 1 次 |
| 调用方在读取描述错误体期间取消 | 调用方取消不能被误判为其操作超时；不能取消独立共享下载；共享下载自身到期后可以重新获取 | 均通过：调用方收到取消；下载继续到自身预算，随后恢复成功，描述共 2 次、业务 1 次 |

这些结果补充了原来主要针对描述成功体、单个等待者取消及业务错误体的覆盖。描述错误体从 response.RequestMessage.Options 获取原下载预算令牌；返回普通结果/错误时由响应 using 释放，缓存外层 catch 移除失败项，因此预算和重取并未停留在配置层。

探针首次诊断错误地要求嵌套 Stream 的 Dispose 调用严格为一次，未区分 HttpContent 读取/释放与最终 HttpResponseMessage 的所有权。该断言不是计划要求的“重试丢弃响应只释放一次”。最终探针直接计数 HttpResponseMessage Dispose=1，并检查流确已释放；原始探针及两次失败日志仍保留。没有修改生产代码、删除失败的既有测试或跳过资源检查。本轮探针构建与最终两次运行全部成功。

## 配置和集成边界的复核

- **注册顺序与配置冻结：** 客户端 Configure 与适配器 PostConfigure 按既有选项机制组合；适配器不静默覆盖非零旧重试以掩盖冲突。HTTP 参数在同步注册阶段复制为标量快照，OperationTimeout/DefinitionTimeout 也单独捕获。宿主持有配置对象后修改值，不会重写已注册管线。
- **唯一重试所有者：** HttpPipeline 将旧循环重试次数限制为 0，旧 RequestHandler 在每个逻辑请求中执行一次；重试许可再次检查最终方法、请求体和流标志。业务异常和解析错误不进入 HTTP 重试。
- **签名顺序：** 逐次签名 handler 必须通过恢复注册返回的 builder 继续追加。先注册在同名 client 上的 handler 位于恢复层外侧，只执行一次。这是既有、已明确记录的接入规则，不是支持任意处理器顺序的承诺。
- **名称语义：** 真实 HttpClient 名由绑定记录固定；代理名称大小写变体仍选中同一绑定。metadata.RemoteName 保留代理调用名（例如 PROBE），不是额外规范化为选项字典键 Probe；这是初次审查已经接受、现有测试明确断言的边界。宿主读取元数据时应沿用 remote 的不区分大小写语义。内部选择管线及重试许可未因此脱节，本轮没有新证据将其改列为功能缺陷。
- **文件边界：** 运行时流禁止重放，完整调用预算覆盖取得流；后续读取及释放由调用方负责。共享层无法仅从 HttpRequestMessage 推断 SDK 返回类型，宿主须在业务白名单中排除下载，样例遵循此规则。

## 既有证据复用与原问题状态

| 项目 | net6 | net8 | 证据处理 |
| --- | ---: | ---: | --- |
| Bing.Http.Resilience.Tests | 51 | 56 | 复用修复第 1 轮最终记录 |
| Bing.DynamicApi.Client.Resilience.Tests | 31 | 31 | 同上 |
| Bing.DynamicApi.Client.Tests | 19 | 19 | 同上 |
| Bing.DynamicApi.WebApiClient.Tests | 13 | 13 | 同上 |
| 合计 | 114 | 119 | 233 项通过，无失败/跳过；本轮未重复完整回归 |

证据清单：[修复第 1 轮](artifacts/fix-round-1/verification-summary.json)。15 个本地包打包、双框架独立消费、样例和有效依赖审计仍复用该轮记录；最后适配包审计的原 restore 失败和单项 --no-restore 补验已明确分开，不把原脚本整体失败伪称为整体成功。

- FIX-001：CLOSED；无歧义身份和双框架反例验证继续有效，详见 [第 2 次审查探针](artifacts/review-round-2/results.json)。
- FIX-002：CLOSED；两类原生超时事件、int 事件监听、运行时预算/取消活动及敏感标签检查继续有效。
- FIX-003：CLOSED；[资源探针](artifacts/fix-round-1/resource-probe.json) 在 1 秒 handler lifetime、20 秒观察预算下，约 11.24 秒确认开/关两组各 20 个 transport 被释放，被观察弱引用归零。该结论不扩展为真实 socket、生产默认寿命或生产内存阈值。
- FIX-004：CLOSED；第 15 节七阶段已明确标注实施前基线，三处历史缺口改为历史时态，当前状态与历史说明可区分；[文档修复检查](artifacts/fix-round-2/docs-check.json)。

## 有意不提供的能力与使用限制

下列项属于锁定范围外或明确的使用边界，本轮不将其列为“功能未完成”：

| 项目 | 当前边界 |
| --- | --- |
| 写请求、上传、流自动重试 | 首版不支持；未加入强制重放开关 |
| Hedging、跨节点熔断、地址切换、业务补偿 | 不在本任务范围 |
| 配置热更新 | 通过正常重启应用配置和回滚 |
| WebApiClient 完整调用预算 | 由调用方 linked token 提供，发送管线本身不覆盖响应体 |
| 取得流之后的超时和并发 | 由文件调用方管理；HTTP 许可不等于整段下载并发上限 |
| SDK RawReturn 字符串取消释放 | 保留 WebApiClientCore 2.1.6 行为；需明确控制读取/释放时使用 HttpResponseMessage 返回 |
| 忽略令牌的自定义处理器 | 不承诺强制中断；没有 Task.WhenAny 后遗留无人管理请求的新增实现 |
| 生产容量与正式发布 | 本地验证不替代外部 CI、生产试点/压力验证或发布流程 |

## 本轮影响分析与验证命令

本轮没有生产变更。ChangedFiles 仅为审查报告/探针；ChangedProjects/PublicContracts/RuntimePaths/Providers/TFMs/BuildPackaging/BenchmarkHarness 均无生产变化。审查范围涉及共享 HTTP 与运行时客户端，风险评估为 MEDIUM，按用户要求补查新的取消/预算组合。

- L0：当前依赖项目和新审查探针双框架构建、任务相关 git diff --check。
- L1：本轮 3 个新场景 × 2 个框架。
- L2/L3：REUSED_UNCHANGED_SCOPE，复用 233 项回归、包消费及样例证据。
- L4/L5：未执行；没有新代码变化或资源疑点要求重复整仓/资源测试。

~~~powershell
dotnet build ai_docs/tasks/20261026-http-resilience/artifacts/review-round-3/ReviewCoverage.csproj -m:1 -nr:false --nologo -v minimal
dotnet build ai_docs/tasks/20261026-http-resilience/artifacts/review-round-3/ReviewCoverage.csproj --no-restore -m:1 -nr:false --nologo -v minimal
dotnet run --project ai_docs/tasks/20261026-http-resilience/artifacts/review-round-3/ReviewCoverage.csproj -f net6.0 --no-build --no-restore
dotnet run --project ai_docs/tasks/20261026-http-resilience/artifacts/review-round-3/ReviewCoverage.csproj -f net8.0 --no-build --no-restore
~~~

首次构建用于诊断探针；第二次构建仅将所有权观测校正为最终响应，原始源码和日志保存于同一目录。最终构建零警告、零错误，两次最终运行 exitCode=0；具体 JSON 观测见本轮 results.json。

## 分类与收口

- Completed / CLOSED：HTTP-01～HTTP-07；FIX-001～FIX-004；本轮新增组合边界核验。
- Open Actionable：无。
- Blocked Approval：无本地功能项。
- Blocked External：外部 CI、生产试点/压力验证未运行，不作为代码缺陷。
- Not Applicable：本任务外 SQL/数据库验证、伪造历史等价性能基线、仅因重复审查而重跑所有测试。
- Accepted Limitations：上述 SDK、合作式取消、流读取、配置/状态及签名处理器顺序边界。
- Verified Boundaries：错误描述体独立预算、全部等待者离开后的缓存恢复、响应释放；既有隔离、重试次数与许可回收证据继续有效。
- Deferred：无计划内功能延期。
- No-Progress Check：CHANGED_EVIDENCE；新边界验证通过，无需开启修复。
- Next Action：STOP。

本轮未自动修改功能、提交、推送、发布或发送外部通知。
