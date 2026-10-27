<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261027-resilience-abstractions
AI_REVIEWED_AT: 2026-10-27T01:58:01+08:00

# 独立代码审查

## 结论

当前实现覆盖了通用管线、Fences/Polly Provider、两个 HTTP 客户端适配及计划中的恢复与 Hedging 行为。独立审查发现的地址池 TLS 降级风险已修复，清理失败降级边界也已补测；最终候选、全部 TRX、消费包、样例和测试映射均已交叉核对，没有开放的可操作审查项。

## 验收矩阵

| 阶段 | 状态 | 证据 |
|---|---|---|
| RA-01 依赖与原生 API | PASS | [dependency-probe.md](evidence/dependency-probe/dependency-probe.md) 记录 Fences/Polly 双框架还原、构建和签名验证。 |
| RA-02 中性核心 | PASS | 公开契约和 DI 注册/校验路径已检查；核心契约测试双框架通过。 |
| RA-03 两 Provider | PASS | 最终候选中 `Bing.Resilience.Tests` net6/net8 各 57/57；契约 Theory 同时验证 Fences 与 Polly。 |
| RA-04 HTTP 迁移 | PASS | 最终候选中 HTTP 恢复测试 net6 71/71、net8 76/76；运行时恢复测试各 34/34，两个旧客户端兼容项目各 TFM 分别 19/19、13/13。 |
| RA-05 HTTP Hedging | PASS | 分支复制、并发与速率限制、取消清理、全失败主响应、Retry-After、真实 loopback 和地址池 scheme 边界均有双框架测试。 |
| RA-06 交付验证 | PASS | [最终候选](evidence/final-candidate.json)列出 10 份 TRX，共 393/393、零失败/跳过；8 组独立消费、5 个样例命令、13 份文档链接检查均通过，详见下文。 |

## 已关闭问题

### FIX-001：已关闭

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX；已修复并复核关闭
- 问题：Hedging 地址池允许混用 HTTPS 与 HTTP。配置校验只分别验证每个端点属于 HTTP(S)，而请求克隆会把白名单请求头发送至所选端点。HTTPS 主端点配 HTTP 备用端点时，显式转发的自定义凭据头可能以明文发送。
- 原始证据：[BingHttpHedgingOptions.cs](../../../framework/src/Bing.Http.Resilience/BingHttpHedgingOptions.cs#L45) 曾只校验每个 origin 的 scheme；[BingHttpResilienceHandler.cs](../../../framework/src/Bing.Http.Resilience/BingHttpResilienceHandler.cs#L79) 收集转发白名单，[BingHttpResilienceHandler.cs](../../../framework/src/Bing.Http.Resilience/BingHttpResilienceHandler.cs#L93) 按所选 origin 创建分支请求。
- 影响：配置错误或环境漂移可能把显式允许转发的租户/业务凭据头降级到未加密连接。
- 修复证据：[BingHttpHedgingOptions.cs](../../../framework/src/Bing.Http.Resilience/BingHttpHedgingOptions.cs#L54) 现在显式拒绝 HTTPS 主端点搭配 HTTP 备用端点，也拒绝 null Origin；[ResilienceBoundaryTests.cs](../../../framework/tests/Bing.Http.Resilience.Tests/ResilienceBoundaryTests.cs#L95) 覆盖 HTTPS→HTTP 拒绝，以及 HTTPS→HTTPS、HTTP→HTTP、HTTP→HTTPS 组合。
- 验证：独立运行 `dotnet test framework/tests/Bing.Http.Resilience.Tests/Bing.Http.Resilience.Tests.csproj --no-restore --filter FullyQualifiedName~HedgingRejectsTlsDowngradeAtRegistration --nologo -m:1 -nr:false -v minimal`，net8 4/4、net6 4/4 通过；新增 TRX 证据位于 `evidence/tls-boundary/`。

## 已验证的修正与测试

- 清理失败现在由 `ResilienceResultCleanupException` 中性包装，并被恢复谓词排除；外层宽泛 Fallback 不会把清理错误变成成功。`ResilienceCleanupTests.CleanupFailureCannotBeConvertedIntoFallbackSuccess` 与原重试清理测试均覆盖 Fences/Polly。
- 已将 [ResilienceProviderContractTests.cs](../../../framework/tests/Bing.Resilience.Tests/ResilienceProviderContractTests.cs#L556) 的清理失败断言更新为中性异常，并验证原始异常保留为 `InnerException`、下一次尝试未执行、结果只释放一次。
- 最终 TRX 的声明值与各自 `ResultSummary` 一致：核心 57+57、HTTP 71+76、运行时适配 34+34、旧 DynamicApi 19+19、旧 WebApiClient 13+13，共 393 项全部通过；没有失败或跳过。TRX 路径、TFM 和计数列于 [final-candidate.json](evidence/final-candidate.json)。
- 补充边界方法已在源码和 [test-map.md](test-map.md) 中核实：半开熔断单探测、CallerCancellation 清理迟到 Hedging 结果、Fallback 总预算、端点并发转备用、全局 TokenBucket 拒绝、实际 configure 对象冻结及非法组合注册校验。
- FIX-001 的 `HedgingRejectsTlsDowngradeAtRegistration` 在 net6/net8 各 4/4 通过，TRX 保存在 `evidence/tls-boundary/`；清理异常由中性异常包装，且宽泛 Fallback 不会掩盖清理失败，最终核心测试全量包含相应双 Provider 用例。

## 最终交付证据

- [package-evidence.json](evidence/package-evidence.json) 指向 `2026-10-26T17-46-17-093Z`。该 run 的 54 个步骤均为 exit code 0，其中 19 个项目打包、8 组 Fences/Polly × Generic/HTTP × net6/net8 消费的 run/publish 共 16 条命令成功、4 组漏洞审计成功。
- 已直接检查免费 Fences 组合：`Bing.Resilience.Fences` nupkg、Fences Generic/HTTP `project.assets.json` 及四份 net6/net8 publish `Consumer.deps.json` 均不包含 Polly。
- [样例结果](evidence/samples/results.json) 中 5 个命令的 exit code 均为 0；[文档链接检查](evidence/doc-links.json) 检查 13 份文档且 `linkIssues` 为空。测试映射覆盖新增生产符号及本次补充边界方法。
- 最终生产范围 SHA-256 为 `0d3d47d4e19ab134e51f6c5a0db9ccb9dfb857ef780bff9a204b81a93c8e3a4b`。首次包消费因 Windows 深路径限制未成功，之后使用新的短 NuGet 缓存重新消费并通过；全局 diff 检查只报告既有无关空白问题，本任务范围检查通过，详见 `execution.md`。

## 审查边界

本次检查覆盖公共异常归一化、回调和结果释放、Hedging 许可/上下文/取消、HTTP 请求白名单与地址池安全、DI 首次解析校验及宿主释放。`execution.md` 正文和测试映射已完成，报告的机器状态仍为 `IN_PROGRESS`，由执行代理在本审查收口后转为终态；这不影响已核实的交付证据。包消费首次深路径失败及其短缓存重跑、diff 检查范围和资源检查数值均在 `execution.md` 留有记录。历史 233 项回归未计入本次通过数。
