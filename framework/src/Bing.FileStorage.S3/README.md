# Bing.FileStorage.S3

S3 兼容存储 Provider，目标框架为 `net6.0`，固定使用 `AWSSDK.S3` **4.0.104.6**。

```csharp
services.AddBingFileStorage(options =>
{
    options.DefaultStore = "s3-v1";
    options.Containers.Add("attachments");
});
services.AddBingS3Storage("s3-v1", options =>
{
    options.ServiceUrl = "https://s3.example.invalid"; // AWS 可留空
    options.Region = "us-east-1";
    options.AccessKey = configuration["Storage:S3:AccessKey"];
    options.SecretKey = configuration["Storage:S3:SecretKey"];
    options.ForcePathStyle = true;
});
```

支持流式对象操作、Range、multipart、签名下载和单分片上传请求。**不支持原子 `CreateOnly`**：`SaveAsync` 会在读取输入前拒绝该模式。Bucket 必须由部署端预先创建并保持私有；逻辑容器对应 Bucket，不要通过公开 ACL 暴露对象。

DI 注册会创建并拥有共享 SDK 客户端；存储门面按范围绑定，不会单独释放客户端。自行构造 `S3FileStorageBackend` 时，传入的客户端由调用方拥有。未配置密钥时使用 AWS 默认凭据链。协议兼容不代表已通过云厂商互操作认证；当前实际验证目标见 MinIO README。

普通请求保留最多 16 项自定义元数据。`bing-upload-` 前缀由 Provider 保留，调用方不能用于新建对象或上传；Provider 的 token/mode 字段不占用这 16 项额度。S3 请求还会验证包含内部字段的元数据键和值合计不超过 2 KiB。持久化分片句柄时完整保存返回的元数据，通过 `FileWriteRequest.RestoreMultipart` 从受信记录重建请求；该恢复入口不得直接使用客户端提供的元数据。
