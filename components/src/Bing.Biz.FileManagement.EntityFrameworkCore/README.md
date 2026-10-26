# Bing.Biz.FileManagement.EntityFrameworkCore

EF Core 元数据仓储，目标框架为 `net6.0`，依赖 `Bing.Biz.FileManagement` 和 EF Core Relational 6.0.36。数据库 Provider 由宿主另行安装。

```csharp
services.AddBingFileManagementEntityFrameworkCore(options =>
    options.UseSqlServer(configuration.GetConnectionString("FileManagement")));
```

若使用 SQLite、MySQL 或其他数据库，请安装对应 EF Provider 并替换 `UseSqlServer` 配置。此方法注册 `IDbContextFactory<FileManagementDbContext>` 和 `IFileManagementStore`；每次仓储操作创建独立 DbContext 和短事务，不会把文件传输放进数据库事务。

此包不会自动创建或同步数据表。请在部署阶段通过 EF Migration 创建 `BingFileRecords`、会话、分片、关联、操作、导入和复制尝试表 `BingFileTransferAttempts`。上传会话表保存后端上传 ID 与 `BackendMetadataJson`。文件内容仍由 `IFileStorage` Provider 访问，不写入数据库。
