# 文件存储容量验证

独立生成数据流并完整回读，验证分片、取消和清理；不预先分配整文件数组。100 GiB 测试会真实写入。Local 在 `--root` 所在盘预留 3 倍数据量加 10 GiB，用于对象和合并临时文件；MinIO / 七牛按 `Path.GetTempPath()` 所在盘检查当前实际并发所需的分片暂存空间。证据会记录暂存目录、卷、预算和可用空间。

磁盘预检查、运行中采样和结束报告均按实际目录查询空间。Windows 会跟随目录联接及卷挂载点；Linux/macOS 会解析符号链接并识别最深挂载点。目录不存在、不可访问或无法解析文件系统时直接失败，不按路径开头的盘符回退。工具会在预检查前创建 `--root` 目录，便于首次运行时查询其实际文件系统。

目录联接回归无需上传大文件。在 Windows 上，将 `BING_CAPACITY_TEST_OTHER_VOLUME_ROOT` 指向与系统临时目录不同卷的专用测试目录，然后运行 Qiniu 测试项目中的 `CapacityProbe` 测试；测试只创建随机空目录和联接，并在退出时清理。

```powershell
dotnet run --project samples/Bing.FileStorage.CapacityProbe -c Release -- --backend local --gib 1 --root H:/BingCapacity --evidence H:/reports/local-1gib.json
dotnet run --project samples/Bing.FileStorage.CapacityProbe -c Release -- --backend local --gib 100 --root H:/BingCapacity --evidence H:/reports/local-100gib.json
$env:BING_FILE_STORAGE_MINIO_ENDPOINT = 'http://127.0.0.1:19000'
$env:BING_FILE_STORAGE_MINIO_ACCESS_KEY = '<隔离测试账号>'
$env:BING_FILE_STORAGE_MINIO_SECRET_KEY = '<隔离测试密钥>'
dotnet run --project samples/Bing.FileStorage.CapacityProbe -c Release -- --backend minio --gib 100 --root H:/BingCapacity --evidence H:/reports/minio-100gib.json
```

按后端顺序运行。探针使用专用随机 Bucket / 目录，16 MiB 分片、并发 1，120 分钟受控截止；退出清理本次内容。结果记录客户端 WorkingSet、PrivateBytes、托管内存、磁盘可用空间、实际字节数与吞吐。客户端内存不等于 MinIO 服务进程或系统文件缓存占用。测试没有正式性能 SLA，不据此宣称性能提升。

七牛云是可选后端。先完成独立的 1 GiB 兼容性验证，再评估是否执行 100 GiB；后者会产生显著流量和存储费用。测试只使用预先创建的私有 S3 空间，清理本次对象，不创建或删除空间。必须显式传入 `--allow-qiniu true` 才会继续：

```powershell
$env:BING_FILE_STORAGE_QINIU_ENDPOINT = 'https://s3.<region>.qiniucs.com'
$env:BING_FILE_STORAGE_QINIU_REGION = '<region>'
$env:BING_FILE_STORAGE_QINIU_BUCKET = '<预先创建的实际 S3 空间名>'
$env:BING_FILE_STORAGE_QINIU_ACCESS_KEY = '<隔离测试账号>'
$env:BING_FILE_STORAGE_QINIU_SECRET_KEY = '<隔离测试密钥>'
dotnet run --project samples/Bing.FileStorage.CapacityProbe -c Release -- --backend qiniu --allow-qiniu true --gib 1 --root H:/BingCapacity --evidence H:/reports/qiniu-1gib.json
```

只有 1 GiB 完整上传、回读校验、取消和清理都通过后，才考虑把 `--gib 1` 改成 `--gib 100`。示例命令是操作入口，本仓库此次未连接七牛、未运行容量测试，也没有七牛容量验收报告。执行前应检查区域、费用、空间权限、网络超时和清理策略；进程被强制终止时需在七牛控制台核查未完成分片。
