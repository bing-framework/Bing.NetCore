# Bing.FileStorage.AspNetCore

ASP.NET Core 表单上传与 HTTP 下载适配层，依赖 `Bing.FileStorage` 和 ASP.NET Core。此层不负责身份验证、租户授权或附件数据库。

`SaveFormFileAsync` 适用于大小受限的小型表单文件，并会释放它自行打开的输入流；大文件应使用 `Bing.Biz.FileManagement` 的持久分片会话。

```csharp
return new StoredFileResult(storage, address, fileName, contentType);
```

`StoredFileResult` 将内容以流式方式写入 HTTP 响应，提供附件文件名、Content-Type、`nosniff` 和 `private, no-store`。只有后端声明支持 Range 时才读取单个 byte range；不支持 Range 的后端会返回完整内容。存储读取流由 Provider 在消费回调结束后释放，客户端断开连接会通过 `RequestAborted` 取消读取。
