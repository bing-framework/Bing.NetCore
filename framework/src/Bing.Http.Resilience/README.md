# Bing.Http.Resilience

可选 HTTP 故障恢复包，面向 net6.0/net8.0。依赖中性 Bing.Resilience 与 Microsoft.Extensions.Http，不引用具体恢复引擎。宿主安装 Bing.Resilience.Fences 或 Bing.Resilience.Polly 并显式注册。

```csharp
using Bing.Resilience;
using Bing.Http.Resilience;

services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
services.AddHttpClient("catalog")
    .AddBingHttpResilience("read", options =>
    {
        options.CanRetryRequest = request => request.RequestUri?.AbsolutePath == "/catalog";
    });
```

默认不许可业务重试；白名单许可后也只重放无体 GET/HEAD，宿主需排除流读取。总发送超时 → 逻辑并发 → Retry 或 Hedging → TokenBucket → 熔断 → 单次超时 → 内侧认证/签名 handler。

状态按实际 HttpClient 名称和用途隔离，Hedging 另按端点 ID 隔离。注册时冻结配置；每 client 只允许一条恢复管线。中性 Bing.Resilience Meter/ActivitySource 记录逻辑结果及策略事件；Provider 负责丢弃响应释放一次，最终响应归调用方。

顺序模式默认总预算 8 秒、单次 2 秒、重试 2 次，200 毫秒指数抖动、本地上限 2 秒；32 并发、无队列；熔断窗口 30 秒/最低 20 次/50%/打开 15 秒。Retry-After 不截短，429 不计熔断失败。TokenBucket 默认关闭。

Hedging 需要独立 CanHedgeRequest 许可和固定 origin 池。所有分支独立复制请求，头使用白名单，可变 Options 必须登记复制器，签名由内侧 handler 重建；禁止 HTTPS 主端点降级到 HTTP 备用。transport 必须关闭自动重定向与 Cookie 共享。

[HTTP 指南](../../../docs/guides/http-resilience.md) · [通用容错与 Hedging](../../../docs/guides/resilience.md) · [样例](../../../samples/Bing.Samples.HttpResilience/README.md)
