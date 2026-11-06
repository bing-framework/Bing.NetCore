using System.Text.Json;
using Bing.FileStorage.RemoteDrive;

namespace Bing.FileStorage.AliyunDrive;

public sealed partial class AliyunDriveClient
{
    /// <summary>
    /// 分页核对远程分片列表并更新不匹配的检查点。
    /// </summary>
    /// <param name="context">提供连接凭据和作用域的上下文。</param>
    /// <param name="content">用于验证远程分片长度的内容。</param>
    /// <param name="checkpoint">包含预期上传分片收据的检查点。</param>
    /// <param name="persist">持久化检查点的异步委托。</param>
    /// <param name="ct">用于取消远程读取和检查点持久化的令牌。</param>
    /// <remarks>
    /// 分页标记必须唯一且不超过 10000 页；仅保留编号、长度和 ETag 均与检查点匹配的分片，不匹配项会从检查点移除并持久化。
    /// </remarks>
    private async Task ReconcilePartsAsync(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct)
    {
        var verified = new HashSet<int>();
        var seenParts = new HashSet<int>();
        var markers = new HashSet<string>();
        var marker = "";
        do
        {
            if (!markers.Add(marker) || markers.Count > 10000)
                throw new FileStorageException(FileStorageError.Unavailable, "网盘分片分页游标未推进。");
            var request = new Dictionary<string, object?> { ["drive_id"] = context.Connection.DriveId,
                ["file_id"] = checkpoint.FileId, ["upload_id"] = checkpoint.UploadId };
            // 首次查询省略游标，不能发送空字符串。
            if (marker.Length != 0) request["part_number_marker"] = marker;
            var page = await Call(context, "/adrive/v1.0/openFile/listUploadedParts", request, ct);
            if (page.TryGetProperty("upload_id", out var upload) && upload.GetString() != checkpoint.UploadId
                || page.TryGetProperty("file_id", out var file) && file.GetString() != checkpoint.FileId)
                throw new FileStorageException(FileStorageError.Conflict, "网盘分片清单不属于当前上传。");
            if (!page.TryGetProperty("uploaded_parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "网盘响应缺少分片清单。");
            foreach (var part in parts.EnumerateArray())
            {
                var number = part.Number("part_number");
                if (number < 1 || number > 10000 || !seenParts.Add((int)number))
                    throw new FileStorageException(FileStorageError.Unavailable, "网盘分片序号无效或重复。");
                var offset = (number - 1) * checkpoint.PartSize;
                var etag = part.Text("etag").Trim('"');
                if (offset < content.Length && part.Number("part_size") == Math.Min(checkpoint.PartSize, content.Length - offset)
                    && etag.Length != 0 && checkpoint.Parts.TryGetValue((int)number, out var receipt)
                    && receipt != "uploaded" && string.Equals(receipt.Trim('"'), etag, StringComparison.Ordinal))
                    verified.Add((int)number);
            }
            marker = page.Text("next_part_number_marker");
        } while (marker.Length != 0);

        var invalid = checkpoint.Parts.Keys.Where(number => !verified.Contains(number)).ToArray();
        if (invalid.Length == 0) return;
        foreach (var number in invalid) checkpoint.Parts.Remove(number);
        await persist(checkpoint, ct);
    }

    /// <summary>
    /// 中止远程分片上传并清除已保存的上传检查点。
    /// </summary>
    /// <param name="context">用于中止请求的远程驱动上下文。</param>
    /// <param name="checkpoint">要清除文件、上传标识及分片状态的检查点。</param>
    /// <param name="persist">重置检查点后持久化状态的异步委托。</param>
    /// <param name="ct">中止和持久化操作的取消令牌。</param>
    /// <remarks>
    /// 先尝试中止远程上传，随后清除检查点并保存；远程中止失败时不重置本地状态。
    /// </remarks>
    private async Task ResetUploadAsync(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct)
    {
        await AbortAsync(context, checkpoint, ct);
        checkpoint.FileId = null;
        checkpoint.UploadId = null;
        checkpoint.Parts.Clear();
        await persist(checkpoint, ct);
    }
}
