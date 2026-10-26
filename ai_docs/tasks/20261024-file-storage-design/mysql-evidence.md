# Admin 文件管理 MySQL 集成证据

## 隔离环境

- 镜像：`mysql:8.4`，本机镜像 ID `sha256:b3b90af2a6552ae30c266fdb7d5dd55f3afb72404bb78d37fe8a23eb857fd3fb`。
- 容器：`bing-file-management-mysql-test`，仅绑定 `127.0.0.1:13326`。
- 测试数据：命名卷 `bing-file-management-mysql-test-data`；每个 Host 测试各自创建随机 `bing_file_tests_<GUID>` 数据库，退出时只删除该库。
- 凭据：每次运行由密码学随机数生成器生成临时 root 密码，不记录到证据或日志。
- 环境门控：未设置 `BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION` 时，两个 MySQL 用例均显示明确的跳过原因，不会连接数据库。

测试不加载 Admin Startup，也不连接默认业务数据库、Redis 或其他外部业务服务。容器通过本轮脚本的 `finally` 清理，数据卷一并删除；清理后确认容器、卷和 `127.0.0.1:13326` 均不存在监听，连接环境变量已清除。仓库中其他 MySQL 容器未触碰。

## 覆盖与结果

每套 Admin 宿主均使用 TestServer 加载真实 `Bing.Admin.Apis.Commons.FileController`，文件内容走真实 FileSystem Provider，持久化使用对应的 MySQL 实现。HTTP 流程验证匿名访问拒绝、上传会话、分片请求与上传、完成、查询、绑定和解绑、中文附件下载、Range 下载、跨租户拒绝及删除清理。另验证旧版本 CAS 更新失败，以及两个 worker 同时抢占同一待处理任务时恰有一个成功。

| 宿主 | 命令 | 结果 |
|---|---|---|
| EF Core | `dotnet test modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj --framework net6.0 --no-build --no-restore --filter FullyQualifiedName~RealAdminController_MySql --verbosity quiet` | 通过 1，失败 0，跳过 0 |
| FreeSQL | `dotnet test modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj --framework net6.0 --no-build --no-restore --filter FullyQualifiedName~RealAdminController_MySql --verbosity quiet` | 通过 1，失败 0，跳过 0 |

本轮因共享 fixture 变更已先分别构建两个测试项目，随后在上述隔离容器上运行真实 MySQL 用例。控制台出现部分仓库传递依赖对 .NET 6 支持生命周期的告警；两项测试均通过。
