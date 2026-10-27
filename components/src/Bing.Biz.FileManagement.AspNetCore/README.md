# Bing.Biz.FileManagement.AspNetCore

附件管理 Web 适配层，目标框架为 `net6.0`，依赖 `Bing.Biz.FileManagement`、`Bing.FileStorage.AspNetCore` 及 ASP.NET Core。

```csharp
services.AddBingFileManagementWeb(runBackgroundWorker: true);
```

宿主需要注册数据库仓储、存储 Provider、文件业务服务和身份验证，并提供派生自 `FileManagementControllerBase` 的控制器。基础路由为 `/api/files`，涵盖上传会话、状态查询、分片、完成/中止、列表/详情、下载/Range、短期下载链接、关联和删除。公开内容路由只允许读取已发布且状态为 `Ready` 的文件。

`GET /api/files/upload-sessions/{id}?includeParts=false` 用于轻量状态轮询，返回原有元数据和空 `parts`，跳过数据库及远端分片查询。省略参数或传入 `true` 保持完整清单行为；所有模式都要求原会话所有者和租户权限。内置上传页面等待初始化、完成及内容检查时使用轻量查询，真正续传前只读取一次完整清单并重新检查状态、核验已上传内容；暂停可取消正在进行的状态请求。

注册会添加错误到 HTTP 状态码的异常筛选器和 Data Protection。启用 worker 后，会在新的 DI scope 中执行持久任务和清理；若使用应用签名下载路由，多节点需要共享 Data Protection 密钥，并连接到相同的持久化数据库和共享存储。Web 层不会自动初始化数据库结构。

完整接口和授权行为见 [文件存储与附件管理](../../../docs/modules/file-storage.md)；部署、配置及恢复步骤见 [接入、部署与恢复](../../../docs/migrations/file-storage-adoption.md)。
