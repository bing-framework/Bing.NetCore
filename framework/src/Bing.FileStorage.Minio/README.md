# Bing.FileStorage.Minio

MinIO 注册便捷入口，目标框架为 `net6.0`，依赖 `Bing.FileStorage.S3` 并复用其 `AWSSDK.S3` 4.0.104.6 实现。

```csharp
services.AddBingFileStorage(options =>
{
    options.DefaultStore = "minio-v1";
    options.Containers.Add("attachments");
});
services.AddBingMinioStorage("minio-v1", options =>
{
    options.Endpoint = "http://127.0.0.1:9000";
    options.AccessKey = configuration["Storage:Minio:AccessKey"];
    options.SecretKey = configuration["Storage:Minio:SecretKey"];
    options.Region = "us-east-1";
});
```

MinIO 必须显式配置 AccessKey 和 SecretKey，Bucket 需预先创建并保持私有。`public` 只是应用逻辑容器名称，不会设置 Bucket 公开读取。此 Provider 支持 S3 实现提供的 multipart、签名和 Range 能力，但不支持原子 `CreateOnly`。

真实 MinIO 测试固定使用 `RELEASE.2025-09-07T16-13-09Z`，镜像 digest 为 `sha256:14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e`。其他 S3 兼容服务未因此获得相同的互操作验证。
