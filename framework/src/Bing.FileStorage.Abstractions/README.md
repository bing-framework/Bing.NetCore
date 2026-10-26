# Bing.FileStorage.Abstractions

文件存储公共契约，目标框架为 `netstandard2.0`。本包不依赖数据库、ASP.NET Core 或具体对象存储 Provider。

`IFileStorage` 提供流式保存、读取、属性查询和删除；`IMultipartFileStorage`、`ISignedFileRequestProvider`、`IFileRangeReader` 分别表示可选能力。调用前检查 `Capabilities`，不要假设每个后端都支持全部能力。

```csharp
var address = new FileAddress("attachments", "objects/0123456789abcdef");
var request = new FileWriteRequest(address, inputLength, FileWriteMode.Overwrite, "application/octet-stream");
await storage.SaveAsync(request, input, cancellationToken);
```

保存操作从输入流当前位置读取 `Length` 字节；输入流由调用方拥有。`ReadAsync` 只在异步回调执行期间提供读取流，Provider 会在回调结束后释放该流。存储位置应保存为 `FileLocation`，不要把短期签名 URL 当作永久地址。

`CreateOnly` 要求后端提供原子保证。Provider 不支持时，必须在读取输入前以 `UnsupportedCapability` 拒绝。租户范围由 `StorageScope.ForTenant` 显式绑定，本包不执行业务授权。
