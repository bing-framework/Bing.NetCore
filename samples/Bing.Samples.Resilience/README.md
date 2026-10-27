# 独立容错样例

只依赖 Bing.Resilience.Fences，不依赖 Bing.Core 或 HTTP 模块。
运行：

```powershell
dotnet run --project samples/Bing.Samples.Resilience --framework net8.0
dotnet run --project samples/Bing.Samples.Resilience --framework net6.0
```

样例执行重试、DTO 降级、手动熔断恢复、并发/TokenBucket 配置以及 Hedging。
切换引擎时将 csproj 的 Provider 引用改为 Bing.Resilience.Polly，并将 AddFencesProvider 改为 AddPollyProvider；业务调用不变。
独立 NuGet 消费的两个版本位于 ai_docs/tasks/20261027-resilience-abstractions/consumers。
