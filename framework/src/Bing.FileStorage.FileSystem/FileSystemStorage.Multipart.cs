using System.Text;
using System.Text.Json;

namespace Bing.FileStorage.FileSystem;

internal sealed partial class FileSystemStorage
{
    /// <summary>
    /// 本地分片上传的持久化清单。
    /// </summary>
    private sealed class Manifest
    {
        /// <summary>
        /// 获取或设置逻辑文件容器名称。
        /// </summary>
        public string Container { get; set; } = "";
        /// <summary>
        /// 获取或设置对象键。
        /// </summary>
        public string Key { get; set; } = "";
        /// <summary>
        /// 获取或设置内容字节数。
        /// </summary>
        public long Length { get; set; }
        /// <summary>
        /// 获取或设置每个分片的字节数。
        /// </summary>
        public int PartSize { get; set; }
        /// <summary>
        /// 获取或设置写入策略。
        /// </summary>
        public FileWriteMode Mode { get; set; }
        /// <summary>
        /// 获取或设置内容的 MIME 类型。
        /// </summary>
        public string ContentType { get; set; } = "";
        /// <summary>
        /// 获取或设置随文件保存的自定义元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
        /// <summary>
        /// 获取或设置本地分片上传的持久化处理阶段。
        /// </summary>
        public string State { get; set; } = "uploading";
        /// <summary>
        /// 获取或设置已经完成的分片记录。
        /// </summary>
        public List<FilePart> Parts { get; set; } = new();
    }
    /// <summary>
    /// 验证分片上传标识并生成会话目录路径。
    /// </summary>
    /// <param name="id">分片上传标识。</param>
    /// <returns>经过根目录范围和链接检查的上传会话目录路径。</returns>
    /// <remarks>
    /// 上传标识不是 N 格式的 Guid 时抛出 UploadNotFound 错误。
    /// </remarks>
    private string SessionPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new FileStorageException(FileStorageError.UploadNotFound, "本地上传标识无效。");
        return LocalPaths.Under(_root, "uploads", id);
    }
    /// <summary>
    /// 打开分片上传会话的独占锁文件。
    /// </summary>
    /// <param name="directory">上传会话目录。</param>
    /// <param name="ct">用于取消锁等待的令牌。</param>
    /// <returns>持有独占锁的文件流。</returns>
    /// <remarks>
    /// 会话目录不存在时抛出 UploadNotFound 错误；遇到锁冲突时最多重试 600 次。
    /// </remarks>
    private static async Task<FileStream> LockAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, "session.lock");
        LocalPaths.CheckLinks(path);
        // 锁不跨网络 IO；操作系统在进程退出时释放它，可在重启后恢复。
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (DirectoryNotFoundException ex) { throw new FileStorageException(FileStorageError.UploadNotFound, "上传会话不存在。", ex); }
            catch (IOException) when (attempt < 600) { await Task.Delay(50, ct); }
        }
    }
    /// <summary>
    /// 读取并验证分片上传清单。
    /// </summary>
    /// <param name="directory">上传会话目录。</param>
    /// <param name="upload">要核对的分片上传请求。</param>
    /// <param name="ct">用于取消清单读取的令牌。</param>
    /// <returns>与上传请求匹配的持久化清单。</returns>
    /// <remarks>
    /// 清单不存在时抛出 UploadNotFound 错误；容器、对象键、长度、分片大小、模式、内容类型或元数据不匹配时抛出 Conflict 错误。
    /// </remarks>
    private static async Task<Manifest> LoadAsync(string directory, MultipartUpload upload, CancellationToken ct)
    {
        var path = Path.Combine(directory, "manifest.json");
        LocalPaths.CheckLinks(path);
        Manifest? manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(path, Encoding.UTF8, ct)); }
        catch (FileNotFoundException ex) { throw new FileStorageException(FileStorageError.UploadNotFound, "上传会话不存在。", ex); }
        if (manifest == null || manifest.Container != upload.Request.Address.Container || manifest.Key != upload.Request.Address.Key ||
            manifest.Length != upload.Request.Length || manifest.PartSize != upload.PartSize || manifest.Mode != upload.Request.Mode ||
            manifest.ContentType != upload.Request.ContentType || manifest.Metadata.Count != upload.Request.Metadata.Count ||
            manifest.Metadata.Any(p => !upload.Request.Metadata.TryGetValue(p.Key, out var value) || value != p.Value))
            throw new FileStorageException(FileStorageError.Conflict, "上传句柄与持久清单不一致。");
        return manifest;
    }
    /// <summary>
    /// 以耐久方式保存分片上传清单。
    /// </summary>
    /// <param name="directory">上传会话目录。</param>
    /// <param name="manifest">要保存的分片上传清单。</param>
    /// <param name="ct">用于取消写入的令牌。</param>
    /// <remarks>
    /// 先写入临时文件并刷新到持久存储，再替换 manifest.json；无论写入结果如何都会尝试删除临时文件。
    /// </remarks>
    private static async Task PersistAsync(string directory, Manifest manifest, CancellationToken ct)
    {
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(manifest), new UTF8Encoding(false), ct);
            using (var durable = new FileStream(temporary, FileMode.Open, FileAccess.Write, FileShare.None)) durable.Flush(true);
            File.Move(temporary, Path.Combine(directory, "manifest.json"), true);
        }
        finally { LocalPaths.DeleteIfPresent(temporary); }
    }
    /// <inheritdoc />
    public async Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = Guid.NewGuid().ToString("N");
        var handle = new MultipartUpload(id, request, partSize);
        var directory = SessionPath(id);
        Directory.CreateDirectory(directory);
        await using var gate = await LockAsync(directory, cancellationToken);
        await PersistAsync(directory, new Manifest { Container = request.Address.Container, Key = request.Address.Key,
            Length = request.Length, PartSize = partSize, Mode = request.Mode, ContentType = request.ContentType, Metadata = new(request.Metadata) }, cancellationToken);
        return handle;
    }
    /// <inheritdoc />
    public async Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default)
    {
        var length = upload.GetPartLength(partNumber);
        FileStorageStreams.ValidateSource(source, length);
        FileStorageStreams.ValidateSha256(sha256);
        var directory = SessionPath(upload.UploadId);
        LocalPaths.Capacity(directory, length);
        // 上传时不占会话锁；完成前必须重新检查清单状态，迟到的分片不能覆盖已冻结的分片。
        await using (var initialGate = await LockAsync(directory, cancellationToken))
        {
            if ((await LoadAsync(directory, upload, cancellationToken)).State != "uploading")
                throw new FileStorageException(FileStorageError.Conflict, "会话已冻结。");
        }
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".part.tmp");
        try
        {
            string actual;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous))
            {
                actual = await FileStorageStreams.CopyExactAsync(source, output, length, cancellationToken);
                await output.FlushAsync(cancellationToken); output.Flush(true);
            }
            if (sha256 != null && sha256 != actual) throw new FileStorageException(FileStorageError.InvalidChecksum, "分片摘要不匹配。");
            await using var gate = await LockAsync(directory, cancellationToken);
            var manifest = await LoadAsync(directory, upload, cancellationToken);
            if (manifest.State != "uploading") throw new FileStorageException(FileStorageError.Conflict, "会话已冻结。");
            // 用不可变代文件+原子清单切换，避免分片覆盖成功而清单仍指向旧摘要。
            var part = new FilePart { Number = partNumber, Length = length, ETag = Guid.NewGuid().ToString("N"), Sha256 = actual };
            File.Move(temporary, Path.Combine(directory, part.ETag + ".part"));
            var previous = manifest.Parts.FirstOrDefault(p => p.Number == partNumber);
            manifest.Parts.RemoveAll(p => p.Number == partNumber); manifest.Parts.Add(part);
            await PersistAsync(directory, manifest, cancellationToken);
            if (previous != null) LocalPaths.DeleteIfPresent(Path.Combine(directory, previous.ETag + ".part"));
            return part;
        }
        finally { LocalPaths.DeleteIfPresent(temporary); }
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        var directory = SessionPath(upload.UploadId);
        await using var gate = await LockAsync(directory, cancellationToken);
        var manifest = await LoadAsync(directory, upload, cancellationToken);
        if (manifest.State == "aborted") throw new FileStorageException(FileStorageError.UploadNotFound, "会话已中止。");
        return manifest.Parts.OrderBy(p => p.Number).ToArray();
    }
    /// <inheritdoc />
    public async Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default)
    {
        FileStorageStreams.ValidateParts(upload, parts);
        var directory = SessionPath(upload.UploadId);
        // 持有文件锁防止多个进程同时合并；数据库任务租约只用于调度，不能替代这一内容锁。
        await using var gate = await LockAsync(directory, cancellationToken);
        var manifest = await LoadAsync(directory, upload, cancellationToken);
        if (manifest.State == "aborted") throw new FileStorageException(FileStorageError.UploadNotFound, "会话已中止。");
        var ordered = manifest.Parts.OrderBy(p => p.Number).ToArray();
        if (parts.Count != ordered.Length || parts.Where((p, i) => p.Number != ordered[i].Number || p.Length != ordered[i].Length || p.ETag != ordered[i].ETag || p.Sha256 != ordered[i].Sha256).Any())
            throw new FileStorageException(FileStorageError.InvalidPart, "完成请求与已提交分片不一致。");
        var path = ObjectPath(upload.Request.Address);
        var existing = await GetPropertiesAsync(upload.Request.Address, cancellationToken);
        // manifest 已冻结时，对象中的 upload-id 可以确认上次原子提交成功（即使进程尚未保存完成状态）。
        if (manifest.State != "uploading" && existing != null && existing.Length == upload.Request.Length &&
            existing.Metadata.TryGetValue("local-upload-id", out var completedId) && completedId == upload.UploadId)
        {
            manifest.State = "completed";
            await PersistAsync(directory, manifest, cancellationToken);
            RemoveParts(directory);
            return new FileWriteResult(this.Location(upload.Request.Address), existing);
        }
        if (manifest.State == "completed") throw new FileStorageException(FileStorageError.Conflict, "已完成对象被外部修改或删除。");
        manifest.State = "completing";
        await PersistAsync(directory, manifest, cancellationToken);
        LocalPaths.EnsureParent(path); LocalPaths.Capacity(path, upload.Request.Length);
        var temporary = path + "." + upload.UploadId + ".merge.tmp";
        var header = new Header { Length = upload.Request.Length, ContentType = manifest.ContentType, Metadata = new(manifest.Metadata) };
        header.Metadata["local-upload-id"] = upload.UploadId;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous))
            {
                await WriteHeaderAsync(output, header, cancellationToken);
                foreach (var part in ordered)
                {
                    var partPath = LocalPaths.Under(directory, part.ETag + ".part");
                    await using var input = OpenRead(partPath);
                    var actual = await FileStorageStreams.CopyExactAsync(input, output, part.Length, cancellationToken);
                    if (actual != part.Sha256) throw new FileStorageException(FileStorageError.InvalidChecksum, "磁盘分片内容校验失败。");
                }
                await output.FlushAsync(cancellationToken); output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Commit(temporary, path, upload.Request.Mode);
            manifest.State = "completed";
            await PersistAsync(directory, manifest, cancellationToken);
            RemoveParts(directory);
            return new FileWriteResult(this.Location(upload.Request.Address), header.Properties());
        }
        finally { LocalPaths.DeleteIfPresent(temporary); }
    }
    /// <summary>
    /// 安全删除上传会话中的分片文件。
    /// </summary>
    /// <param name="directory">上传会话目录。</param>
    /// <remarks>
    /// 仅处理 *.part 文件，并在删除前检查文件链接。
    /// </remarks>
    private static void RemoveParts(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.part")) { LocalPaths.CheckLinks(path); LocalPaths.DeleteIfPresent(path); }
    }
    /// <inheritdoc />
    public async Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        var directory = SessionPath(upload.UploadId);
        if (!Directory.Exists(directory)) return;
        await using var gate = await LockAsync(directory, cancellationToken);
        var manifest = await LoadAsync(directory, upload, cancellationToken);
        if (manifest.State == "completed") return;
        manifest.State = "aborted";
        await PersistAsync(directory, manifest, cancellationToken);
        RemoveParts(directory);
    }
}
