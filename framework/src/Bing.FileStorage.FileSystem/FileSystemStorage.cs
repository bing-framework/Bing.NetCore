using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Bing.FileStorage.FileSystem;

/// <summary>内容与属性放入同一个不可拆分的文件，再同卷原子提交；内部格式不能直接当作静态文件发布。</summary>
internal sealed partial class FileSystemStorage : IFileStorage, IMultipartFileStorage, IFileRangeReader
{
    private readonly string _root;
    public FileSystemStorage(string name, StorageScope scope, string root, string space)
    {
        Name = name; Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _root = LocalPaths.Under(root, space, scope.Key);
    }
    public string Name { get; }
    public StorageScope Scope { get; }
    public FileStorageCapabilities Capabilities => FileStorageCapabilities.CreateOnly | FileStorageCapabilities.Multipart |
        FileStorageCapabilities.Range | FileStorageCapabilities.RequiresCompletionCleanup;
    private string ObjectPath(FileAddress address) => LocalPaths.Under(_root, "objects", address.Container, address.Key.Replace('/', Path.DirectorySeparatorChar) + ".blob");

    private sealed class Header
    {
        public string Format { get; set; } = "bing-storage-v1";
        public long Length { get; set; }
        public string ContentType { get; set; } = "";
        public string ETag { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public FileProperties Properties() => new() { Length = Length, ContentType = ContentType, ETag = ETag, LastModifiedUtc = CreatedAt, Metadata = Metadata };
    }
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
    private static async Task WriteHeaderAsync(Stream stream, Header header, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var size = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(size, bytes.Length);
        await stream.WriteAsync(size.AsMemory(), ct);
        await stream.WriteAsync(bytes.AsMemory(), ct);
    }
    private static FileStream OpenRead(string path)
    {
        LocalPaths.CheckLinks(path);
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan); }
        catch (FileNotFoundException ex) { throw new FileStorageException(FileStorageError.NotFound, "文件不存在。", ex); }
        catch (DirectoryNotFoundException ex) { throw new FileStorageException(FileStorageError.NotFound, "文件不存在。", ex); }
    }
    private static void Commit(string temporary, string path, FileWriteMode mode)
    {
        LocalPaths.CheckLinks(path);
        try { File.Move(temporary, path, mode == FileWriteMode.Overwrite); }
        catch (IOException ex) when (mode == FileWriteMode.CreateOnly && File.Exists(path))
        { throw new FileStorageException(FileStorageError.AlreadyExists, "对象已存在。", ex); }
    }
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
    public async Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        await using var input = OpenRead(ObjectPath(address));
        var header = await ReadHeaderAsync(input, cancellationToken);
        using var content = new BoundedReadStream(input, header.Length);
        await consume(content, cancellationToken);
    }
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
    public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LocalPaths.DeleteIfPresent(ObjectPath(address));
        return Task.CompletedTask;
    }
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
