# Bing.FileStorage.FileSystem

本地文件系统 Provider，目标框架为 `net6.0`，依赖 `Bing.FileStorage`。适合单节点持久磁盘；多副本共享数据应使用共享对象存储。

```csharp
services.AddBingFileStorage(options =>
{
    options.DefaultStore = "local-v1";
    options.Containers.Add("attachments");
});
services.AddBingFileSystemStorage("local-v1", options =>
{
    options.RootPath = Path.GetFullPath("data/files");
    options.Namespace = "bing";
});
```

`RootPath` 必须是有备份、重启后仍保留的绝对目录，并限制为服务账号可访问。文件对象以内部 `objects/<container>/<key>.blob` 格式保存，断点续传状态位于 `uploads`；这些内容不是可直接托管的原始文件或静态目录。请通过 `IFileStorage` 读取。

支持原子 `CreateOnly`、分片上传、Range 和进程重启后的本地分片恢复；不提供签名 URL。分片合并需要额外磁盘空间，生产部署应监控剩余容量。
