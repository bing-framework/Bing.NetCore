# 宿主与部署

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

ASP.NET Core 适配提供宿主角色、HTTP 流量闸门、就绪标记、在途请求计数和停止排空。CAP 消费者可按宿主角色启停。仓库同时提供 Docker Compose + Nginx 与 Kubernetes 蓝绿发布模板和验收脚本。

## 核心实现

- 宿主角色：`framework/src/Bing.AspNetCore/Bing/AspNetCore/Hosting/`
- HTTP 流量闸门：`BingTrafficGateExtensions.cs`、`BingTrafficGateMiddleware.cs`
- CAP 消费排空：`framework/src/Bing.Events/Cap/BingCapConsumerHostDrain.cs`
- 部署模板：`deploy/bluegreen/`

## 用法

```csharp
services.AddBingTrafficGate(options =>
{
    options.DrainTimeout = TimeSpan.FromSeconds(30);
});

app.UseBingTrafficGate();
app.Services.MarkBingTrafficReady();
```

宿主角色、配置键和中间件顺序见[集群宿主配置](../guides/cluster-host-configuration.md)。

## 依赖与配置

- Web 与 CAP 消费者建议分开部署，避免 HTTP 切流隐式控制消息消费。
- 绿版只有在依赖预热和检查完成后才调用 `MarkBingTrafficReady()`。
- 停止时闸门先拒绝新请求，再等待在途请求完成；超时或取消会报告未排空，而不会假装成功。

## 待确认与注意事项

- **待确认**：`deploy/bluegreen` 是可执行模板，不等同于当前生产环境已完成动态切流和回滚验收。
- **待确认**：强杀、节点失联、数据库破坏性迁移和消息契约不兼容仍需部署环境演练。
- 蓝绿版本必须共享稳定消费者组和业务幂等键；不能用版本号创建新组来规避迁移。
- 数据库应采用先扩展、后迁移、最后删除的兼容步骤。

