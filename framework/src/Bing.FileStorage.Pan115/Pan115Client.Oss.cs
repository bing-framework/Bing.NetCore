using System.Security.Cryptography;
using System.Text;
using AlibabaCloud.OSS.V2;
using AlibabaCloud.OSS.V2.Credentials;
using AlibabaCloud.OSS.V2.Models;
using Bing.FileStorage.RemoteDrive;

namespace Bing.FileStorage.Pan115;

public sealed partial class Pan115Client
{
    /// <summary>
    /// 获取并验证 Pan115 上传使用的临时 OSS 客户端。
    /// </summary>
    /// <param name="context">提供远程存储连接和凭据的上下文。</param>
    /// <param name="ct">用于取消 API 调用的令牌。</param>
    /// <returns>使用已验证临时凭据创建的 OSS 客户端。</returns>
    /// <remarks>
    /// 端点必须是受支持的阿里云 OSS HTTPS 地址，且响应必须包含临时访问密钥和安全令牌。
    /// </remarks>
    private async Task<Client> Oss(RemoteDriveContext context, CancellationToken ct)
    {
        var token = (await Call(context, "/open/upload/get_token", true, Form(), ct)).Child("data");
        var endpoint = token.Text("endpoint");
        if (endpoint.StartsWith("http://", StringComparison.Ordinal)) endpoint = "https://" + endpoint[7..];
        var uri = RemoteDriveTransport.ValidateUrl(endpoint);
        if (!uri.Host.EndsWith(".aliyuncs.com", StringComparison.Ordinal) || !uri.Host.StartsWith("oss-", StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.Configuration, "115 返回了不受支持的临时 OSS 地址。");
        var region = uri.Host[4..uri.Host.IndexOf('.', StringComparison.Ordinal)];
        if (region.EndsWith("-internal", StringComparison.Ordinal)) region = region[..^9];
        if (string.IsNullOrEmpty(token.Text("AccessKeyId")) || string.IsNullOrEmpty(token.Text("SecurityToken")))
            throw new FileStorageException(FileStorageError.AccessDenied, "115 临时上传凭据不完整。");
        var configuration = new Configuration { Endpoint = endpoint, Region = region, SignatureVersion = "v4", DisableUploadCrc64Check = false,
            CredentialsProvider = new StaticCredentialsProvider(token.Text("AccessKeyId"), token.Text("AccessKeySecret"), token.Text("SecurityToken")) };
        return _ossClientFactory?.Invoke(configuration) ?? new Client(configuration);
    }
    /// <summary>
    /// 从异常及其内部异常中查找 OSS 服务异常。
    /// </summary>
    /// <param name="error">要检查的异常。</param>
    /// <returns>找到的首个 OSS 服务异常；未找到时为 null。</returns>
    private static ServiceException? ServiceError(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is ServiceException service) return service; return null; }
    /// <summary>
    /// 使用临时 OSS 客户端执行一次服务操作。
    /// </summary>
    /// <param name="context">提供远程存储连接和凭据的上下文。</param>
    /// <param name="send">使用 OSS 客户端执行的异步操作。</param>
    /// <param name="ct">用于取消凭据获取和操作的令牌。</param>
    /// <typeparam name="T">OSS 操作结果的类型。</typeparam>
    /// <returns>send 委托返回的 OSS 操作结果；安全令牌过期时刷新凭据并重试一次。</returns>
    /// <remarks>
    /// 遇到临时安全令牌过期时刷新客户端并重试一次。
    /// </remarks>
    private async Task<T> OssCall<T>(RemoteDriveContext context, Func<Client, Task<T>> send, CancellationToken ct)
    {
        for (var retry = 0; ; retry++)
        {
            using var client = await Oss(context, ct);
            try { return await send(client); }
            catch (Exception ex) when (retry == 0 && ServiceError(ex)?.ErrorCode is "SecurityTokenExpired" or "InvalidSecurityToken.Expired") { }
        }
    }
    /// <summary>
    /// 将非空文本编码为 UTF-8 Base64。
    /// </summary>
    /// <param name="value">要编码的文本。</param>
    /// <returns>UTF-8 Base64 字符串；value 为 null 或空字符串时为 null。</returns>
    private static string? Encode(string? value) => string.IsNullOrEmpty(value) ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    /// <summary>
    /// 使用临时 OSS 凭据上传空对象或分片内容。
    /// </summary>
    /// <param name="context">提供远程存储连接和凭据的上下文。</param>
    /// <param name="content">要上传的远程文件内容。</param>
    /// <param name="checkpoint">记录 OSS 目标和分片状态的检查点。</param>
    /// <param name="persist">持久化上传检查点的异步委托。</param>
    /// <param name="ct">用于取消读取、上传和持久化的令牌。</param>
    /// <remarks>
    /// 分片上传会持久化上传标识和每个分片 ETag，并在重试时核对远端收据；NoSuchUpload 会重置检查点，其他非取消异常映射为结果未知。
    /// </remarks>
    private async Task UploadOss(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct)
    {
        try
        {
            if (content.Length == 0)
            {
                await OssCall(context, client => client.PutObjectAsync(new PutObjectRequest { Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey,
                    Body = Stream.Null, ContentLength = 0, ContentMd5 = Convert.ToBase64String(MD5.HashData(Array.Empty<byte>())),
                    Callback = Encode(checkpoint.Callback), CallbackVar = Encode(checkpoint.CallbackVariables) }, cancellationToken: ct), ct);
                return;
            }
            if (string.IsNullOrEmpty(checkpoint.UploadId))
            {
                var init = await OssCall(context, client => client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
                    { Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey }, cancellationToken: ct), ct);
                checkpoint.UploadId = init.UploadId;
                if (string.IsNullOrEmpty(checkpoint.UploadId)) throw new FileStorageException(FileStorageError.OutcomeUnknown, "临时 OSS 未返回上传标识。");
                await persist(checkpoint, ct);
            }
            var count = checked((int)((content.Length - 1) / checkpoint.PartSize + 1));
            if (count > 10000) throw new FileStorageException(FileStorageError.CapacityExceeded, "115 远端分片数超限。");
            for (var number = 1; number <= count; number++)
            {
                var offset = (long)(number - 1) * checkpoint.PartSize; var length = Math.Min(checkpoint.PartSize, content.Length - offset);
                if (checkpoint.Parts.TryGetValue(number, out var saved))
                {
                    var page = await OssCall(context, client => client.ListPartsAsync(new ListPartsRequest { Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey,
                        UploadId = checkpoint.UploadId, PartNumberMarker = number - 1, MaxParts = 1 }, cancellationToken: ct), ct);
                    var actual = page.Parts?.SingleOrDefault();
                    if (actual?.PartNumber == number && actual.Size == length && actual.ETag == saved) continue;
                }
                // 一个临时分片供 SDK 重试和 CRC64/MD5 计算；永不缓冲整个文件。
                var path = Path.Combine(Path.GetTempPath(), "bing-115-" + Guid.NewGuid().ToString("N") + ".tmp");
                await using var staged = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await content.ReadRangeAsync(offset, length, (stream, token) => FileStorageStreams.CopyExactAsync(stream, staged, length, token), ct);
                staged.Position = 0; using var md5 = MD5.Create(); var digest = Convert.ToBase64String(await md5.ComputeHashAsync(staged, ct));
                var uploaded = await OssCall(context, client =>
                {
                    staged.Position = 0;
                    return client.UploadPartAsync(new UploadPartRequest { Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey, UploadId = checkpoint.UploadId,
                        PartNumber = number, Body = staged, ContentLength = length, ContentMd5 = digest }, cancellationToken: ct);
                }, ct);
                if (string.IsNullOrEmpty(uploaded.ETag) || string.IsNullOrEmpty(uploaded.HashCrc64)) throw new FileStorageException(FileStorageError.InvalidChecksum, "115 上传回执不完整。");
                checkpoint.Parts[number] = uploaded.ETag; await persist(checkpoint, ct);
            }
            await OssCall(context, client => client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey, UploadId = checkpoint.UploadId,
                Callback = Encode(checkpoint.Callback), CallbackVar = Encode(checkpoint.CallbackVariables),
                CompleteMultipartUpload = new CompleteMultipartUpload { Parts = checkpoint.Parts.OrderBy(x => x.Key)
                    .Select(x => new UploadPart { PartNumber = x.Key, ETag = x.Value }).ToList() }
            }, cancellationToken: ct), ct);
        }
        catch (Exception ex) when (ServiceError(ex)?.ErrorCode == "NoSuchUpload")
        {
            // 先由调用方检查网盘登记；不存在时下次从中转重传，绝不把 NoSuchUpload 当成功。
            checkpoint.UploadId = null; checkpoint.Parts.Clear(); await persist(checkpoint, ct);
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "115 上传句柄已失效，等待网盘对账。");
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not FileStorageException)
        { throw new FileStorageException(FileStorageError.OutcomeUnknown, "115 临时 OSS 上传结果未知，保留检查点。"); }
    }
    /// <summary>
    /// 中止检查点记录的临时 OSS 分片上传。
    /// </summary>
    /// <param name="context">提供远程存储连接和凭据的上下文。</param>
    /// <param name="checkpoint">包含 OSS 存储桶、对象键和上传标识的检查点。</param>
    /// <param name="ct">用于取消凭据获取和中止请求的令牌。</param>
    /// <remarks>
    /// NoSuchUpload 视为已中止；访问凭据错误、传输错误及其他失败分别映射为访问拒绝、服务不可用或结果未知。
    /// </remarks>
    private async Task AbortOss(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct)
    {
        try { await OssCall(context, client => client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            { Bucket = checkpoint.Bucket, Key = checkpoint.ObjectKey, UploadId = checkpoint.UploadId }, cancellationToken: ct), ct); }
        catch (Exception ex) when (ServiceError(ex)?.ErrorCode == "NoSuchUpload") { }
        catch (FileStorageException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var service = ServiceError(ex);
            var code = service?.ErrorCode;
            if (code is "AccessDenied" or "InvalidAccessKeyId" or "SignatureDoesNotMatch" or "SecurityTokenExpired" or "InvalidSecurityToken")
                throw new FileStorageException(FileStorageError.AccessDenied, "115 临时 OSS 清理被拒绝。", ex, service?.RequestId);
            if (ex is HttpRequestException or IOException or TimeoutException)
                throw new FileStorageException(FileStorageError.Unavailable, "115 临时 OSS 清理暂时不可用。", ex, service?.RequestId);
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "115 临时 OSS 清理结果未知。", ex, service?.RequestId);
        }
    }
}
