# 七牛 S3 兼容存储部署

七牛 Kodo 通过 `Bing.FileStorage.Qiniu` 接入现有 S3 存储实现。每个存储实例使用独立 `StoreName`；对象地址的逻辑容器通过 `ContainerBuckets` 映射到已创建的七牛 S3 空间名。Endpoint、Region 和空间名必须来自目标账号及区域的七牛 S3 配置，不能把普通空间访问域名当作 S3 Endpoint。

## 配置

从[示例配置](qiniu.example.json)复制所需字段到 `FileManagement:Stores:<store-name>`。`Provider` 必须为 `Qiniu`，Endpoint 必须是 HTTPS 的 `s3.<Region>.qiniucs.com` 域名，Region 必须与该 Endpoint 所属区域一致；`ContainerBuckets` 中每个逻辑容器都要显式填写实际 S3 空间名。应用账号需要对映射空间执行对象读写、删除及 S3 分片操作。不要将 Access Key 或 Secret Key 提交到仓库、镜像或日志，使用部署平台的密钥注入功能覆盖配置。

七牛默认使用 `UploadMode: ServerProxy`：浏览器分片经过应用服务器，服务端可在持久化回执后完成上传。代理请求使用 SigV4 非 chunk 载荷签名，不发送 SDK 扩展 checksum 头；目标环境验收包含传输中载荷被篡改时拒绝该分片。该模式仍会产生应用带宽和对象存储费用。`SignedParts` 会让浏览器直接向七牛上传；只有在目标账号和区域验证预签名请求头、分片上传、分片列表、完成校验、超时恢复及浏览器 CORS 后才可启用。未通过真实目标环境验证前保持默认代理模式。

更换 Endpoint、Region、空间映射或上传模式时注册新的 `StoreName`。不要原地修改已有实例配置，否则持久化文件和未完成会话可能仍引用旧位置或旧模式。默认存储切换只影响新文件；旧文件和迁移任务继续按各自记录的 StoreName 解析。

## 代理上传与续传

每个正在转发的分片会在应用进程临时目录暂存，默认分片大小为 16 MiB；部署时将 `TEMP`/`TMP` 指向有容量的持久服务磁盘，并根据允许的上传并发预留空间。单次普通 `SaveAsync` 使用的暂存分片可能达到 128 MiB。不会将 100 GiB 文件整体加载进内存；应用也不会缓存整个远端文件。正常完成、取消或失败会释放分片临时文件。

服务端只有在实际上传成功并保存 SHA-256、长度、ETag 回执后才承认分片完成。刷新或重启后，应用把数据库回执与七牛当前分片列表进行比对；远端已存在而数据库没有回执、ETag 改变或长度不符的分片都要求重新上传。同摘要重新登记不会清除已有回执。完成冻结、中止或过期后，迟到上传不能更新回执。

默认会话空闲期限为 24 小时、最长为 7 天，分片签名为 15 分钟，下载链接为 5 分钟。应用期限不能延长七牛侧 UploadId 的实际保留期限；上线验收必须确认目标区域的会话期限和跨日续传。遇到后端会话已消失时，只有唯一对象位置、长度及会话元数据一致才能确认已完成，否则保留失败状态并重新发起上传，不能直接认定成功。

初始化得到 UploadId 后，如果数据库发布失败，任务会重新读取文件记录。已提交的会话保留；未提交的上传先写入独立 Abort 补偿任务，再尝试中止。中止失败仍可在重启后重试，达到重试上限时通过现有任务恢复入口处理，原数据库异常继续记入初始化任务。补偿使用独立的有界令牌，客户端取消不会取消这次恢复机会。并发执行者胜出的 UploadId 不会被旧执行者的补偿删除。

如果数据库持续不可用，无法确认是否提交，也无法保存补偿任务，应用不会盲目中止上传；错误日志保留 FileId、StoreName 和已知 UploadId，数据库恢复后须人工核对这些记录与后端。后端创建成功但没有返回 UploadId，或者进程在获取标识后、持久化前直接崩溃，仍存在跨系统无法原子提交的窗口。部署方须核实并配置目标空间未完成分片的保留/回收策略，定期对账无业务引用的上传；不能将应用的 7 天期限当作后端自动清理保证。

## 浏览器直传与公开访问

ServerProxy 模式只访问应用现有的分片接口，不依赖七牛的浏览器 CORS。SignedParts 模式应为实际前端 Origin 配置允许的 `PUT`，并允许签名请求返回的 `x-amz-checksum-sha256` 及上传 Content-Type；需要浏览器读取远端回执时暴露 `ETag` 和 `x-amz-checksum-sha256`。若前端直接读取签名下载链接，还需按实际使用允许 `GET`/`HEAD`。浏览器自己设置 Content-Length，不能在 JavaScript 中强行设置该受限头。发布前需通过真实浏览器预检与上传验证，离线 HttpClient 测试不能证明 CORS 通过。

空间保持私有。公开文件仍经应用的公开 Ready 文件入口提供内容；私有文件继续由文件和业务授权控制。已签发的 S3 下载链接在有效期内是持有者凭证，解除业务权限不会即时撤销它；需要逐次授权时使用应用鉴权下载入口。

## Admin 与运维工具

两套 Admin 共用 `FileManagement:Stores` 配置。EF Core 与 FreeSql 的文件管理表仍需通过相应部署脚本或受控 schema 流程准备；添加云存储不会替代数据库 schema 部署。运维工具示例见[Bing.FileManagement.Tool 配置](../../../tools/Bing.FileManagement.Tool/appsettings.example.json)。

迁移时先为目标七牛实例配置新的 StoreName，使用 Tool 的 `migrate --file-id ... --target-store <store-name>` 预览，确认后显式传入 `--apply` 排队。迁移会按目标 StoreName 写入新对象并执行校验；不要在任务确认完成、保留期到期前删除源存储。七牛空间需在控制台预先创建，工具不会创建或删除云端空间。

## 验收与运行

先在隔离空间验证小文件上传、分片重试、完成、下载、Range、元数据、删除和服务重启恢复。使用 SignedParts 时额外验证七牛实际支持的签名请求头与 CORS；代理模式验证客户端取消和应用超时后的未完成分片清理。对每个对象同时核对应用记录的长度与 SHA-256，以及七牛端对象状态。保存目标区域、SDK 版本、配置模式、请求/响应摘要和清理结果，避免记录凭据。

容量探针通过 `--backend qiniu` 提供验证入口，且必须显式指定 `--allow-qiniu true`。七牛分片暂存使用 `Path.GetTempPath()` 所在卷；容量检查按 16 MiB 分片乘以探针实际并发数计算，不把 `--root` 当作云端对象暂存盘。预检查、监控和报告统一按实际目录查询，支持 Windows 目录联接/卷挂载点及 Linux/macOS 链接与挂载点；无法解析时明确失败。结果会记录暂存目录、卷和实际 S3 Provider / Qiniu 门面程序集身份。先跑 1 GiB，核验完整回读、取消和对象清理后，再决定是否运行 100 GiB。容量测试会产生实际流量和存储费用；本地构建或模拟服务通过不代表七牛云端兼容或容量验收通过。进程意外退出后，应在七牛侧检查测试对象和未完成分片。

Provider 云端测试通过以下环境变量配置一个隔离的预先创建空间；测试只操作随机对象键，不创建空间、不修改 CORS 或空间权限：

```powershell
$env:BING_FILE_STORAGE_QINIU_ENDPOINT = 'https://s3.cn-east-1.qiniucs.com'
$env:BING_FILE_STORAGE_QINIU_REGION = 'cn-east-1'
$env:BING_FILE_STORAGE_QINIU_BUCKET = '<实际 S3 空间名>'
$env:BING_FILE_STORAGE_QINIU_ACCESS_KEY = '<隔离测试 Access Key>'
$env:BING_FILE_STORAGE_QINIU_SECRET_KEY = '<隔离测试 Secret Key>'
dotnet test framework/tests/Bing.FileStorage.Qiniu.Tests/Bing.FileStorage.Qiniu.Tests.csproj
```

基础变量齐全时会运行代理上传、Range、签名下载、完成重放和 SigV4 载荷篡改拒绝测试。单独设置 `BING_FILE_STORAGE_QINIU_SIGNED_TESTS=1` 才会启用 `SignedParts` 直传测试；启用前应先完成目标空间的签名头和 CORS 配置验证。

示例 Endpoint 和密钥均为占位值。真实空间、区域、签名头、分页边界、SHA-256/ETag 语义及 1/100 GiB 容量仍需在计划部署的七牛账号和区域完成验收。
