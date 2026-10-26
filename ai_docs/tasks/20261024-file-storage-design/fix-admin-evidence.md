# FIX-004 Admin 权限接入验证

## 结果

两套 Admin 宿主都通过服务端 `AdminFilePermissionChecker` 将文件权限映射到 Admin 的模块、操作和角色权限记录。资源目录由 `AdminFilePermissionCatalog` 固定为 `bing-file-management` 模块以及 `files.upload`、`files.publish`、`files.manage`、`files.host` 四项操作；授权按 Admin 登录令牌中的应用 ID 和角色 ID 查询，显式拒绝记录优先。未注册、禁用或重复的文件模块均拒绝授权。

`FileStorageModule` 在 `FileManagement:Enabled=true` 时接入权限检查器和资源初始化服务。初始化器是显式运维入口，不会在正常启动时自动建资源或授予角色权限。Admin 数据库结构就绪后，可在一次性维护命令或运维调用中执行：

```csharp
using var scope = app.Services.CreateScope();
await scope.ServiceProvider
    .GetRequiredService<IAdminFilePermissionResourceInitializer>()
    .InitializeAsync();
```

初始化只创建缺少的模块和操作资源，不写角色授权；完成后由管理员在现有权限管理界面将操作资源分配给角色。若 Admin 应用尚未初始化、资源 URI/操作编码冲突或重复，初始化会明确失败。

真实登录用例通过 Admin `/api/OAuth/signIn` 生成 JWT，再用 Bearer 令牌访问实际 Admin 文件控制器。覆盖未认证拒绝、无上传权限拒绝、无发布权限不能写公开容器、无宿主权限拒绝、禁用模块拒绝、管理列表拒绝、非文件所有者删除拒绝、管理者访问和删除、公开文件匿名读取及私有文件匿名拒绝。上传者可删除自己的未绑定文件；测试中的拒绝删除由另一个没有 `files.manage` 权限的用户触发。

Admin.Service 显式对齐 `System.IdentityModel.Tokens.Jwt` 到 `8.14.0`。两套 Admin 实际解析出的 IdentityModel 组件为 `8.14.0`，而 JWT handler 曾解析为 `6.10.0`；真实登录在 `JwtSecurityTokenHandler.WriteToken` 处发生程序集类型加载异常。将依赖放在两宿主共用的身份服务项目，避免测试项目单独覆盖版本。

定位期间，使用测试用 Checker 代理和请求预检探针的版本曾出现一次 `202`，移除代理及探针后，纯真实 JWT HTTP 用例稳定返回预期 `403`。该现象只出现在测试夹具版本，内部触发原因未确认，未据此认定生产权限漏洞。

## 验证

环境使用专用 MySQL 8.4 测试实例及每次运行随机创建、删除的独立数据库；连接通过 `BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION` 提供，不连接 Admin 业务数据库。两个测试项目的最终定向运行均为 5 通过、0 失败、0 跳过：

- EF Core：`dotnet test modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj --filter "FullyQualifiedName~AdminFileControllerHttpTests|FullyQualifiedName~AdminFilePermissionHttpTests" --no-restore --verbosity quiet --logger "console;verbosity=minimal" --logger "trx;LogFileName=admin-efcore-final.trx"`
- FreeSQL：`dotnet test modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj --filter "FullyQualifiedName~AdminFileControllerHttpTests|FullyQualifiedName~AdminFilePermissionHttpTests" --no-restore --verbosity quiet --logger "console;verbosity=minimal" --logger "trx;LogFileName=admin-freesql-final.trx"`

HTTP 文件闭环验证上传会话、分片、完成、查询、绑定/解绑、中文文件名、Range 下载、跨租户拒绝和删除。MySQL 附加断言验证文件版本 CAS、并发任务抢占、过期任务租约接管后旧 token 无法更新且有效 token 可更新；复制目标退休后不可发布、根记录 CAS 失败会回滚复制发布状态，以及成功发布后的目标不可退休。

- EF Core TRX：`modules/admin/tests/Bing.Admin.EFCore.Tests/TestResults/admin-efcore-final.trx`
- FreeSQL TRX：`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-freesql-final.trx`

上述两个最终 TRX 已原样复制到 `evidence/fix-round-1/`，由本轮 `final-manifest.json` 记录哈希。身份、角色和权限仓储使用测试种子/Moq；实际 JWT 生成与验证、文件控制器、SQLite/MySQL 文件仓储及内容 IO 均真实执行。未启动完整生产 Admin Startup，也未连接生产身份库。

构建仍输出仓库既有的 .NET 6 生命周期和部分依赖 TFM 支持警告；本次两宿主最终测试均成功。
