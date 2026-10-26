# Bing.Biz.FileManagement.AspNetCore

附件管理 Web 适配层，目标框架为 `net6.0`，依赖 `Bing.Biz.FileManagement`、`Bing.FileStorage.AspNetCore` 及 ASP.NET Core。

```csharp
services.AddBingFileManagementWeb(runBackgroundWorker: true);
```

宿主需要注册数据库仓储、存储 Provider、文件业务服务和身份验证，并提供派生自 `FileManagementControllerBase` 的控制器。基础路由为 `/api/files`，涵盖上传会话、状态查询、分片、完成/中止、列表/详情、下载/Range、短期下载链接、关联和删除。公开内容路由只允许读取已发布且状态为 `Ready` 的文件。

注册会添加错误到 HTTP 状态码的异常筛选器和 Data Protection。启用 worker 后，会在新的 DI scope 中执行持久任务和清理；若使用应用签名下载路由，多节点需要共享 Data Protection 密钥，并连接到相同的持久化数据库和共享存储。Web 层不会自动初始化数据库结构。

完整接口和授权行为见 [文件存储与附件管理](../../../docs/modules/file-storage.md)；部署、配置及恢复步骤见 [接入、部署与恢复](../../../docs/migrations/file-storage-adoption.md)。
