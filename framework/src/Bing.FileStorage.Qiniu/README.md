# Bing.FileStorage.Qiniu

通过七牛 S3 服务接入统一文件存储，默认使用服务端代理分片上传。

注册 `AddBingQiniuStorage(name, configure)`，显式配置 HTTPS Endpoint、S3 Region、服务端 AK/SK 和 `ContainerBuckets`。上传模式为 `S3UploadMode.ServerProxy`；`SignedParts` 仅在目标空间通过远端 SHA-256 及浏览器签名验证后启用。

基础操作、Range、短期下载签名、分片及恢复复用 `Bing.FileStorage.S3`。已有实例的区域、空间映射和上传模式不可随意更换；使用新 StoreName 后受控迁移。

完整配置和部署说明见 [七牛存储部署](../../../docs/operations/file-management/qiniu.md)。
