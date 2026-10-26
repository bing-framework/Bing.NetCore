# Admin 文件控制器 HTTP 集成证据

## 覆盖范围

共享用例 `AdminFileControllerHttpTests.RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete` 通过 TestServer 加载实际 `Bing.Admin.Apis.Commons.FileController`，并以隔离临时目录、真实 FileSystem Provider 和独立 SQLite 文件运行。EF Core 项目使用 `FileManagementDbContext` 建表；FreeSQL 项目使用 `FileManagementFreeSqlSchema.SyncStructure` 建表。测试不启动 Admin Startup、MySQL、Redis 或业务服务。

用例覆盖匿名请求拒绝、创建上传会话、初始化任务、会话查询、分片摘要登记与上传、完成任务、文件查询、业务绑定与解绑、中文文件名附件响应、单区间 Range 响应、跨租户拒绝、逻辑删除任务及物理对象清理。

## 执行结果

| 项目 | 命令 | 结果 |
|---|---|---|
| EF Core | `dotnet test modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj --framework net6.0 --no-restore --filter FullyQualifiedName~AdminFileControllerHttpTests --verbosity quiet` | 通过 1，失败 0，跳过 0 |
| FreeSQL | `dotnet test modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj --framework net6.0 --filter FullyQualifiedName~AdminFileControllerHttpTests --verbosity quiet` | 通过 1，失败 0，跳过 0 |

构建输出包含仓库当前 .NET 6 及其传递依赖支持生命周期告警；未影响测试执行结果。

MySQL 8.4 专用门控用例、真实执行结果及隔离容器清理记录见 [mysql-evidence.md](mysql-evidence.md)。
