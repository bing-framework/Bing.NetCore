# Bing.DynamicApi.Client.Resilience

运行时动态 API 客户端的可选恢复适配包，面向 net6.0/net8.0。宿主需安装并注册独立 Provider；切换 Polly 时改为 AddPollyProvider，客户端调用不变。

```csharp
using Bing.Resilience;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;

services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
services.AddBingDynamicApiClient(options =>
    options.AddRemoteService("catalog", remote =>
    {
        remote.BaseUrl = "https://catalog.example";
        remote.MaxRetries = 0;
    }));
services.AddBingDynamicApiClientResilience("catalog", options =>
{
    options.Http.CanRetryRequest = request => request.RequestUri?.AbsolutePath == "/api/catalog";
});
```

自动选择 HttpPipeline 模式，设置完整调用预算 10 秒、独立描述下载预算 5 秒。旧 MaxRetries 非零将校验失败。描述下载内建重放许可，业务读取必须另行许可；流、上传、写操作不自动重放。

适配查找不区分 remoteName 大小写，发送使用登记的实际 HttpClient 名称。旧请求处理器每次逻辑请求只运行一次；需要逐次刷新的签名应通过返回 builder 的 AddHttpMessageHandler 注册到恢复管线内侧。

[完整指南](../../../docs/guides/http-resilience.md) · [迁移与回滚](../../../docs/migrations/http-resilience-adoption.md)

HTTP Hedging 通过 options.Http 配置并要求业务显式许可；描述下载始终在主地址使用顺序重试。详见[中性容错与地址池指南](../../../docs/guides/resilience.md)。
