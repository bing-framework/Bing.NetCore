# Bing.Biz.FileManagement

文件与附件业务层，目标框架为 `net6.0`。依赖命名存储契约、用户及租户抽象，不绑定 ORM 或 Web 框架。

```csharp
services.AddBingFileManagement(options =>
{
    options.PartSize = 16 * 1024 * 1024;
    options.Containers["attachments"].MaxFileLength = 100L * 1024 * 1024 * 1024;
});
```

宿主还必须注册 `IFileManagementStore`、文件存储 Provider，以及 `ICurrentUser` / `ICurrentTenant`。默认只注册私有 `attachments` 容器；每个容器的文件上限默认为 100 GiB，会话空闲期限为 24 小时、最长有效期为 7 天。配置值还受分片和 S3 multipart 限制。

需要公开容器时，必须在 `AddBingFileManagement` 配置回调中显式添加：

```csharp
services.AddBingFileManagement(options =>
{
    options.Containers["public"] = new() { Visibility = FileVisibility.Public };
});
```

公开容器的写入要求 `files.publish`。默认 `IFileAccessAuthorizer` 会拒绝所有业务关联读取；宿主必须实现授权。默认权限检查器读取 `permission` claim（例如 `files.upload`、`files.publish`、`files.manage`）。上传会话会持久化 `BackendMetadataJson`，用于恢复后端分片会话所需的元数据。存储位置和 `FileId` 会持久化，签名 URL 只用于短期传输。此包不执行 SQL 建表，也不注册 HTTP 端点。

## 分片查询与后台任务

`FileManagementService.ListAsync(skip, take, manage, ct)` 按通过读取授权的 Ready 文件分页，`skip` 不计算非 Ready 或业务拒绝的记录，保持原数组响应和默认每页 50 条。普通列表保留创建者范围，管理列表需显式权限。两 ORM 的 `ListReadyFilesAsync` 在数据库过滤 Ready 后稳定排序、分页，服务按最多 100 条候选逐批检查授权；原 `ListFilesAsync` 仍可查询全状态记录。自定义仓储需补齐新增方法；无需数据库迁移。分页数据或权限并发变化时仍按普通偏移分页处理，不承诺跨请求快照。

`IFileManagementStore.FindPartAsync(sessionId, number, ct)` 按 `(SessionId, Number)` 联合主键查询单个脱离跟踪的分片快照；无匹配记录时返回 `null`。完整清单仍用于上传恢复和完成校验。此查询不引入缓存或数据库迁移。

`GetSessionAsync(id, includeParts: false, ct)` 仅返回会话元数据和空 `Parts`，仍校验当前用户、租户和会话所有者，但不查询数据库或远端分片。适合初始化、合并、内容检查的状态轮询；空清单不代表没有已上传分片。恢复上传必须调用原 `GetSessionAsync(id, ct)` 或显式传入 `true`，取得经过远端回执校验的完整清单。

持续后台任务由 `FileOperationScheduler` 执行：`Complete`、`Migrate` 使用长队列，`Initialize`、`Abort`、`Delete`、`DeleteReplica` 使用短队列；每个执行槽和维护循环使用独立异步 DI 作用域。`ShortOperationConcurrency` 默认 2，`LongOperationConcurrency` 默认 1，范围均为 1～16；`OperationPollInterval` 默认 1 秒，范围为 100 毫秒～1 分钟；`CleanupInterval` 控制独立维护周期。限制按宿主实例计算，多实例总并行量需合并预算。Admin Web Worker 默认接入此调度器；非 Web 宿主可显式调用 `FileOperationScheduler.RunAsync`。单个调度器实例不能同时重复启动。配置和调用示例见 [接入、部署与恢复](../../../docs/migrations/file-storage-adoption.md)。

完整部署、权限、接口和恢复说明见 [文件存储与附件管理](../../../docs/modules/file-storage.md) 和 [接入、部署与恢复](../../../docs/migrations/file-storage-adoption.md)。
