# 文件管理浏览器示例

使用真实 SQLite 和 Local / MinIO。仅监听 127.0.0.1，页面内固定 demo-upload-token 是演示身份，不能部署为生产认证入口。

```powershell
$env:FileDemo__DataRoot = 'H:/BingDemo/Files'
dotnet run --project samples/Bing.FileManagement.Demo -c Release
```

打开 http://127.0.0.1:5188。选择文件后支持 4 路分片、暂停、刷新后重选同文件恢复、服务重启恢复、签名刷新、下载及删除。默认每片 16 MiB、最大 100 GiB；浏览器只为当前分片计算摘要。服务端完成合并期间查询状态。

示例列表显示最近 50 个当前用户可读的 Ready 文件，中止/失败/删除及无读取权限的记录不占名额。接口仍返回数组；其他调用方可使用 `skip/take` 对可见结果翻页，示例不提供完整文件管理 UI。模块的搜索、会话发现、任务管理、迁移批次、额度和内容检查能力见[增强能力说明](../../docs/modules/file-storage-enhancements.md)；示例不代替生产 Admin 的授权配置与 schema 部署。

切换 FreeSql 使用新的独立数据目录：设置 FileDemo__Orm=FreeSql，默认 EFCore；两 ORM 不共用已有 SQLite 文件。

切换 MinIO：

```powershell
$env:FileDemo__Provider = 'minio'
$env:FileDemo__DataRoot = 'H:/BingDemo/Minio'
$env:FileDemo__Minio__Endpoint = 'http://127.0.0.1:19000'
$env:FileDemo__Minio__AccessKey = '<隔离测试账号>'
$env:FileDemo__Minio__SecretKey = '<隔离测试密钥>'
dotnet run --project samples/Bing.FileManagement.Demo -c Release
```

示例只允许回环 MinIO，启动时创建私有 attachments/public Bucket。固定 MinIO 版本使用服务级 CORS，详见[部署说明](../../docs/migrations/file-storage-adoption.md)。DataRoot 保存对象、SQLite 和 Data Protection 密钥，重新启动不要删除此目录。业务授权器只为 demo/1 放行，示例身份有上传和公开发布权限。

切换七牛 S3 兼容存储时，先在私有空间创建用于 `attachments` 和 `public` 的实际 S3 空间，并配置精确的 HTTPS Endpoint、对应 Region 与凭据：

```powershell
$env:FileDemo__Provider = 'qiniu'
$env:FileDemo__Qiniu__Endpoint = 'https://s3.<region>.qiniucs.com'
$env:FileDemo__Qiniu__Region = '<region>'
$env:FileDemo__Qiniu__AccessKey = '<七牛 S3 Access Key>'
$env:FileDemo__Qiniu__SecretKey = '<七牛 S3 Secret Key>'
$env:FileDemo__Qiniu__ContainerBuckets__attachments = '<attachments 实际 S3 空间名>'
$env:FileDemo__Qiniu__ContainerBuckets__public = '<public 实际 S3 空间名>'
dotnet run --project samples/Bing.FileManagement.Demo -c Release
```

七牛空间需预先创建；示例不会创建或删除云端空间。默认使用服务端代理模式，SignedParts 仅在目标账号和区域完成兼容验证后再显式启用。该路径尚未进行真实云端验证；生产接入、CORS 与故障处理见[七牛部署说明](../../docs/operations/file-management/qiniu.md)。

容量验证使用[CapacityProbe](../Bing.FileStorage.CapacityProbe/README.md)，不会在浏览器里分配 100 GiB 数组。数据库增强迁移参见[迁移脚本与回填说明](../../docs/migrations/file-management-enhancements/README.md)，监控、健康检查与告警处理参见[文件管理运维说明](../../docs/operations/file-management/README.md)。

## 管理页面

访问 `/files` 使用共享管理页面；回环开发令牌 `demo-manage-token` 可以查询任务、重放事件和归档历史。业务关联填写 `demo` / `1`。原页面和管理页复用 Web 模块嵌入的上传脚本。已有 EF 示例库需要先执行 [治理增量 SQL](../../docs/migrations/file-management-governance/README.md)，或指定新的 FileDemo:DataRoot。
