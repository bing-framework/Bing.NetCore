# HTTP 故障恢复样例

在仓库根目录运行：

```powershell
dotnet run --project samples/Bing.Samples.HttpResilience
dotnet run --project samples/Bing.Samples.HttpResilience -- --probe
dotnet run --project samples/Bing.Samples.HttpResilience -- --hedging
```

样例启动临时 loopback Kestrel 下游，自动验证两个客户端首次 503 后成功、逐次签名刷新、文件下载/上传不重放、成功响应体挂起时的调用方预算，然后退出。不依赖外部服务，不使用生产密钥。

SDK JSON 契约使用 DTO。文件使用 `HttpResponseMessage`/`IRemoteStreamContent`，由调用方读取并释放。运行时客户端的完整调用预算由适配器设置；SDK 使用 linked cancellation token。

`--probe` 使用可控成功 handler，记录同进程当前“未启用/启用恢复”的耗时与进程分配增量，并对未启用/启用恢复各执行 20 次容器创建/释放。生命周期探针将 HandlerLifetime 缩短为 1 秒，在固定 20 秒观察窗口内检查 transport 的实际 Dispose 计数及 provider/client/handler 弱引用回收；不把容器 Dispose 返回当作 handler 已释放。两组必须完成回收才输出 PASS。它不模拟网络、不提供生产性能阈值，也不宣称历史版本性能对比。

详见 [接入指南](../../docs/guides/http-resilience.md) 和 [迁移说明](../../docs/migrations/http-resilience-adoption.md)。

样例显式注册 Fences Provider。--hedging 启动两个本地同服务端点，验证 WebApiClient 与运行时客户端从慢主端点转到快速备用端点，逐分支刷新签名；描述下载仍使用主端点。
