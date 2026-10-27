# Bing.FileStorage.Abstractions

文件存储公共契约，目标框架为 `netstandard2.0`。本包不依赖数据库、ASP.NET Core 或具体对象存储 Provider。

`IFileStorage` 提供流式保存、读取、属性查询和删除；`IMultipartFileStorage`、`ISignedFileRequestProvider`、`IFileRangeReader` 分别表示可选能力。调用前检查 `Capabilities`，不要假设每个后端都支持全部能力。

需要核验一个分片时，确认 `Multipart` 能力后调用 `IMultipartFileStorage.GetPartAsync(upload, number, cancellationToken)` 扩展。支持 `IMultipartFilePartReader` 的后端使用单片查询；旧 Provider 无需新增接口成员，会回退到完整清单。仅目标分片缺失返回 `null`，会话缺失、权限和网络错误仍抛出异常。工厂包装器实现该接口以提供兼容回退，不能只凭类型判断底层是否提供查询加速。

```csharp
var address = new FileAddress("attachments", "objects/0123456789abcdef");
var request = new FileWriteRequest(address, inputLength, FileWriteMode.Overwrite, "application/octet-stream");
await storage.SaveAsync(request, input, cancellationToken);
```

保存操作从输入流当前位置读取 `Length` 字节；输入流由调用方拥有。`ReadAsync` 只在异步回调执行期间提供读取流，Provider 会在回调结束后释放该流。存储位置应保存为 `FileLocation`，不要把短期签名 URL 当作永久地址。

`CreateOnly` 要求后端提供原子保证。Provider 不支持时，必须在读取输入前以 `UnsupportedCapability` 拒绝。租户范围由 `StorageScope.ForTenant` 显式绑定，本包不执行业务授权。
