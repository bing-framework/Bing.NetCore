using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Bing.FileStorage.FileSystem;

/// <summary>
/// 原子保存文件内容与属性的本地文件存储。
/// </summary>
/// <remarks>
/// 内容与属性放入同一个不可拆分的文件，再同卷原子提交；内部格式不能直接当作静态文件发布。
/// </remarks>
internal sealed partial class FileSystemStorage : IFileStorage, IMultipartFileStorage, IFileRangeReader
{
    /// <summary>
    /// 保存文件系统存储的根目录路径。
    /// </summary>
    private readonly string _root;
    /// <summary>
    /// 初始化一个 <see cref="FileSystemStorage" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="root">当前对象图、工作单元或文件系统的根对象或根路径。</param>
    /// <param name="space">用于隔离文件系统存储数据的空间名称。</param>
    public FileSystemStorage(string name, StorageScope scope, string root, string space)
    {
        Name = name; Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _root = LocalPaths.Under(root, space, scope.Key);
    }
    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public StorageScope Scope { get; }
    /// <inheritdoc />
    public FileStorageCapabilities Capabilities => FileStorageCapabilities.CreateOnly | FileStorageCapabilities.Multipart |
        FileStorageCapabilities.Range | FileStorageCapabilities.RequiresCompletionCleanup;
    /// <summary>
    /// 生成文件地址对应的本地对象路径。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <returns>经过根目录范围和链接检查的对象文件路径。</returns>
    private string ObjectPath(FileAddress address) => LocalPaths.Under(_root, "objects", address.Container, address.Key.Replace('/', Path.DirectorySeparatorChar) + ".blob");

    /// <summary>
    /// 本地文件的持久化属性头。
    /// </summary>
    private sealed class Header
    {
        /// <summary>
        /// 获取或设置持久化文档的格式版本。
        /// </summary>
        public string Format { get; set; } = "bing-storage-v1";
        /// <summary>
        /// 获取或设置内容字节数。
        /// </summary>
        public long Length { get; set; }
        /// <summary>
        /// 获取或设置内容的 MIME 类型。
        /// </summary>
        public string ContentType { get; set; } = "";
        /// <summary>
        /// 获取或设置后端返回的不透明内容标识。
        /// </summary>
        public string ETag { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 获取或设置记录创建时间。
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>
        /// 获取或设置随文件保存的自定义元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
        /// <summary>
        /// 根据当前文件元数据创建文件属性。
        /// </summary>
        /// <returns>包含长度、内容类型、ETag、创建时间和元数据的文件属性。</returns>
        public FileProperties Properties() => new() { Length = Length, ContentType = ContentType, ETag = ETag, LastModifiedUtc = CreatedAt, Metadata = Metadata };
    }
    /// <summary>
    /// 读取并验证文件头和对象长度。
    /// </summary>
    /// <param name="stream">要读取文件头的文件流。</param>
    /// <param name="ct">用于取消读取的令牌。</param>
    /// <returns>包含文件格式和对象长度的文件头。</returns>
    /// <remarks>
    /// 文件头长度、格式或剩余对象长度不匹配时抛出配置错误。
    /// </remarks>
    private static async Task<Header> ReadHeaderAsync(FileStream stream, CancellationToken ct)
    {
        var size = new byte[4];
        await ReadExactAsync(stream, size, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(size);
        if (length < 1 || length > 32768) throw new FileStorageException(FileStorageError.Configuration, "本地对象头损坏。");
        var json = new byte[length];
        await ReadExactAsync(stream, json, ct);
        var header = JsonSerializer.Deserialize<Header>(json);
        if (header == null || header.Format != "bing-storage-v1" || header.Length < 0 || stream.Length - stream.Position != header.Length)
            throw new FileStorageException(FileStorageError.Configuration, "本地对象长度或格式损坏。");
        return header;
    }
    /// <summary>
    /// 从流中读取指定数量的字节。
    /// </summary>
    /// <param name="stream">要读取的输入流。</param>
    /// <param name="bytes">接收数据的缓冲区。</param>
    /// <param name="ct">用于取消读取的令牌。</param>
    /// <remarks>
    /// 在缓冲区填满前遇到流末尾时抛出配置错误。
    /// </remarks>
    private static async Task ReadExactAsync(Stream stream, byte[] bytes, CancellationToken ct)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(offset), ct);
            if (read == 0) throw new FileStorageException(FileStorageError.Configuration, "本地对象头不完整。");
            offset += read;
        }
    }
    /// <summary>
    /// 将带长度前缀的 JSON 文件头写入流。
    /// </summary>
    /// <param name="stream">接收文件头的输出流。</param>
    /// <param name="header">要序列化的文件头。</param>
    /// <param name="ct">用于取消写入的令牌。</param>
    /// <remarks>
    /// 长度前缀使用四字节小端序，后续写入 UTF-8 JSON。
    /// </remarks>
    private static async Task WriteHeaderAsync(Stream stream, Header header, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var size = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(size, bytes.Length);
        await stream.WriteAsync(size.AsMemory(), ct);
        await stream.WriteAsync(bytes.AsMemory(), ct);
    }
    /// <summary>
    /// 安全地打开文件对象以供异步顺序读取。
    /// </summary>
    /// <param name="path">要打开的文件路径。</param>
    /// <returns>支持异步顺序读取的文件流。</returns>
    /// <remarks>
    /// 路径含重解析点，或文件及其目录不存在时拒绝读取或映射为未找到错误。
    /// </remarks>
    private static FileStream OpenRead(string path)
    {
        LocalPaths.CheckLinks(path);
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan); }
        catch (FileNotFoundException ex) { throw new FileStorageException(FileStorageError.NotFound, "文件不存在。", ex); }
        catch (DirectoryNotFoundException ex) { throw new FileStorageException(FileStorageError.NotFound, "文件不存在。", ex); }
    }
    /// <summary>
    /// 按写入模式提交临时对象文件。
    /// </summary>
    /// <param name="temporary">已完成写入的临时文件路径。</param>
    /// <param name="path">目标文件路径。</param>
    /// <param name="mode">创建或覆盖写入模式。</param>
    /// <remarks>
    /// 目标路径须通过链接检查；CreateOnly 模式下目标已存在时抛出 AlreadyExists 错误。
    /// </remarks>
    private static void Commit(string temporary, string path, FileWriteMode mode)
    {
        LocalPaths.CheckLinks(path);
        try { File.Move(temporary, path, mode == FileWriteMode.Overwrite); }
        catch (IOException ex) when (mode == FileWriteMode.CreateOnly && File.Exists(path))
        { throw new FileStorageException(FileStorageError.AlreadyExists, "对象已存在。", ex); }
    }
    /// <inheritdoc />
    public async Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileStorageStreams.ValidateSource(source, request.Length);
        var path = ObjectPath(request.Address);
        LocalPaths.EnsureParent(path);
        LocalPaths.Capacity(path, request.Length);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var header = new Header { Length = request.Length, ContentType = request.ContentType, Metadata = new(request.Metadata) };
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous))
            {
                await WriteHeaderAsync(output, header, cancellationToken);
                await FileStorageStreams.CopyExactAsync(source, output, request.Length, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Commit(temporary, path, request.Mode);
            return new FileWriteResult(this.Location(request.Address), header.Properties());
        }
        finally { LocalPaths.DeleteIfPresent(temporary); }
    }
    /// <inheritdoc />
    public async Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        await using var input = OpenRead(ObjectPath(address));
        var header = await ReadHeaderAsync(input, cancellationToken);
        using var content = new BoundedReadStream(input, header.Length);
        await consume(content, cancellationToken);
    }
    /// <inheritdoc />
    public async Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var input = OpenRead(ObjectPath(address));
            return (await ReadHeaderAsync(input, cancellationToken)).Properties();
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.NotFound) { return null; }
    }
    /// <inheritdoc />
    public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LocalPaths.DeleteIfPresent(ObjectPath(address));
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public async Task ReadRangeAsync(FileAddress address, long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        await using var input = OpenRead(ObjectPath(address));
        var header = await ReadHeaderAsync(input, cancellationToken);
        if (offset < 0 || length < 0 || offset > header.Length || length > header.Length - offset) throw new ArgumentOutOfRangeException(nameof(offset));
        input.Seek(offset, SeekOrigin.Current);
        using var content = new BoundedReadStream(input, length);
        await consume(content, cancellationToken);
    }
}
