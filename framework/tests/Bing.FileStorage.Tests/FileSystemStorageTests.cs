using System.Security.Cryptography;
using Bing.FileStorage.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.Tests;

public sealed class FileSystemStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bing-storage-tests", Guid.NewGuid().ToString("N"));
    private IFileStorage Storage(string tenant = "tenant-a") => new FileSystemStorageBackend("local", _root).Bind(StorageScope.ForTenant(tenant));
    private readonly FileAddress _address = new("attachments", "objects/test");
    private const int PartSize = 5 * 1024 * 1024;
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [Fact]
    public async Task CapacityFailure_IsEarly_AndDoesNotPublish()
    {
        var storage = Storage();
        // 使用卷总容量，避免并行测试删除临时文件后空闲空间增加，使 free+1 不再超限。
        var capacity = new DriveInfo(Path.GetPathRoot(_root)!).TotalSize;
        using var input = new NonSeekStream(new MemoryStream());
        var error = await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(new(_address, capacity + 1, FileWriteMode.CreateOnly), input));
        Assert.Equal(FileStorageError.CapacityExceeded, error.Error);
        Assert.Null(await storage.GetPropertiesAsync(_address));
    }
    [Fact]
    public void OfflineCleanup_Previews_AndPreservesObjectsAndRecentTemporaryFiles()
    {
        var directory = Path.Combine(_root, "bing", "h", "objects", "attachments");
        Directory.CreateDirectory(directory);
        var old = Path.Combine(directory, "x.blob." + Guid.NewGuid().ToString("N") + ".tmp");
        var recent = Path.Combine(directory, "x.blob." + Guid.NewGuid().ToString("N") + ".tmp");
        var committed = Path.Combine(directory, "x.blob");
        File.WriteAllBytes(old, new byte[] { 1 }); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));
        File.WriteAllBytes(recent, new byte[] { 2 }); File.WriteAllBytes(committed, new byte[] { 3 });
        Assert.Equal(old, Assert.Single(FileSystemStorageMaintenance.CleanupTemporaryFiles(_root, "bing", DateTimeOffset.UtcNow.AddDays(-1), false)));
        Assert.True(File.Exists(old));
        FileSystemStorageMaintenance.CleanupTemporaryFiles(_root, "bing", DateTimeOffset.UtcNow.AddDays(-1), true);
        Assert.False(File.Exists(old)); Assert.True(File.Exists(recent)); Assert.True(File.Exists(committed));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task Save_Read_Metadata_StreamOwnership_AndDelete(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var storage = Storage();
        using var source = new NonSeekStream(new MemoryStream(bytes));
        await storage.SaveAsync(new(_address, length, FileWriteMode.CreateOnly, "text/plain", new Dictionary<string, string> { ["test"] = "value" }), source);
        Assert.True(source.CanRead);
        var properties = await storage.GetPropertiesAsync(_address);
        Assert.Equal(length, properties!.Length); Assert.Equal("value", properties.Metadata["test"]); Assert.Equal("text/plain", properties.ContentType);
        Stream? captured = null;
        await storage.ReadAsync(_address, async (stream, ct) => { captured = stream; using var output = new MemoryStream(); await stream.CopyToAsync(output, ct); Assert.Equal(bytes, output.ToArray()); });
        // 回调外底层资源已关闭，不能继续访问。
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => { await captured!.ReadAsync(new byte[1], 0, 1); });
        await storage.DeleteAsync(_address); await storage.DeleteAsync(_address);
        Assert.Null(await storage.GetPropertiesAsync(_address));
        Assert.Equal(FileStorageError.NotFound, (await Assert.ThrowsAsync<FileStorageException>(() => storage.ReadAsync(_address, (_, _) => Task.CompletedTask))).Error);
    }
    [Theory]
    [InlineData(4, 3)]
    [InlineData(2, 3)]
    public async Task InvalidLength_DoesNotPublishPartialOrOverwrite(long actual, long declared)
    {
        var storage = Storage();
        await storage.SaveAsync(new(_address, 1, FileWriteMode.CreateOnly), new MemoryStream(new byte[] { 42 }));
        using var source = new NonSeekStream(new MemoryStream(new byte[actual]));
        Assert.Equal(FileStorageError.InvalidLength, (await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(new(_address, declared, FileWriteMode.Overwrite), source))).Error);
        await storage.ReadAsync(_address, (s, _) => { Assert.Equal(42, s.ReadByte()); Assert.Equal(-1, s.ReadByte()); return Task.CompletedTask; });
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task CreateOnly_IsAtomicUnderConcurrency()
    {
        var storage = Storage();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            try { await storage.SaveAsync(new(_address, 1, FileWriteMode.CreateOnly), new MemoryStream(new[] { (byte)i })); return true; }
            catch (FileStorageException ex) when (ex.Error == FileStorageError.AlreadyExists) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x)); Assert.Equal(1, (await storage.GetPropertiesAsync(_address))!.Length);
    }
    [Fact]
    public async Task CancelledWrite_PreservesExistingContent()
    {
        var storage = Storage();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(new(_address, 2, FileWriteMode.CreateOnly), new MemoryStream(new byte[2]), cts.Token));
        Assert.Null(await storage.GetPropertiesAsync(_address));
    }
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../b")]
    [InlineData("/root")]
    [InlineData("a\\b")]
    [InlineData("a//b")]
    [InlineData("con")]
    public void RejectsUnsafeObjectKeys(string key) => Assert.Throws<ArgumentException>(() => new FileAddress("attachments", key));
    [Fact]
    public async Task ScopeIsolation_AndLongUnicodeTenant()
    {
        await Storage("A").SaveAsync(new(_address, 0, FileWriteMode.CreateOnly), Stream.Null);
        Assert.Null(await Storage("a").GetPropertiesAsync(_address));
        Assert.Null(await new FileSystemStorageBackend("local", _root).Bind(StorageScope.Host).GetPropertiesAsync(_address));
        var longTenant = new string('租', 40);
        await Storage(longTenant).SaveAsync(new(_address, 0, FileWriteMode.CreateOnly), Stream.Null);
        Assert.NotNull(await Storage(longTenant).GetPropertiesAsync(_address));
    }
    [Fact]
    public async Task Range_UsesContentOffset_AndBounds()
    {
        var storage = Storage();
        await storage.SaveAsync(new(_address, 5, FileWriteMode.CreateOnly), new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }));
        await ((IFileRangeReader)storage).ReadRangeAsync(_address, 2, 2, async (s, ct) =>
        { using var output = new MemoryStream(); await s.CopyToAsync(output, ct); Assert.Equal(new byte[] { 3, 4 }, output.ToArray()); });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ((IFileRangeReader)storage).ReadRangeAsync(_address, 4, 2, (_, _) => Task.CompletedTask));
    }
    [Fact]
    public async Task Multipart_Restart_RepeatComplete_AndLatePart()
    {
        var storage = Storage(); var multi = (IMultipartFileStorage)storage;
        var handle = await multi.CreateMultipartAsync(new(_address, PartSize + 3, FileWriteMode.CreateOnly), PartSize);
        var first = new byte[PartSize]; first[0] = 1;
        var a = await multi.UploadPartAsync(handle, 1, new MemoryStream(first), Convert.ToBase64String(SHA256.HashData(first)));
        // 创建全新后端对象模拟服务重启，不共享内存会话。
        multi = (IMultipartFileStorage)Storage();
        Assert.Single(await multi.ListPartsAsync(handle));
        var b = await multi.UploadPartAsync(handle, 2, new MemoryStream(new byte[] { 2, 3, 4 }));
        var result = await multi.CompleteMultipartAsync(handle, new[] { a, b });
        Assert.Equal(PartSize + 3, result.Properties.Length);
        Assert.Equal(result.Properties.ETag, (await multi.CompleteMultipartAsync(handle, new[] { a, b })).Properties.ETag);
        Assert.Equal(FileStorageError.Conflict, (await Assert.ThrowsAsync<FileStorageException>(() => multi.UploadPartAsync(handle, 2, new MemoryStream(new byte[3])))).Error);
        await storage.ReadAsync(_address, async (s, ct) =>
        { using var output = new MemoryStream(); await s.CopyToAsync(output, ct); Assert.Equal(1, output.ToArray()[0]); Assert.Equal(new byte[] { 2, 3, 4 }, output.ToArray().TakeLast(3)); });
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Multipart_Checksum_ETag_Abort_AndHandleIsolation()
    {
        var multi = (IMultipartFileStorage)Storage();
        var handle = await multi.CreateMultipartAsync(new(_address, 1, FileWriteMode.CreateOnly), PartSize);
        Assert.Equal(FileStorageError.InvalidChecksum, (await Assert.ThrowsAsync<FileStorageException>(() =>
            multi.UploadPartAsync(handle, 1, new MemoryStream(new byte[] { 1 }), Convert.ToBase64String(new byte[32])))).Error);
        var part = await multi.UploadPartAsync(handle, 1, new MemoryStream(new byte[] { 1 }));
        Assert.Equal(FileStorageError.InvalidPart, (await Assert.ThrowsAsync<FileStorageException>(() =>
            multi.CompleteMultipartAsync(handle, new[] { new FilePart { Number = 1, Length = 1, ETag = "wrong", Sha256 = part.Sha256 } }))).Error);
        var other = (IMultipartFileStorage)Storage("other");
        Assert.Equal(FileStorageError.UploadNotFound, (await Assert.ThrowsAsync<FileStorageException>(() => other.ListPartsAsync(handle))).Error);
        await multi.AbortMultipartAsync(handle); await multi.AbortMultipartAsync(handle);
        Assert.Null(await Storage().GetPropertiesAsync(_address));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task EmptyMultipart_CanCompleteAndRetry()
    {
        var multi = (IMultipartFileStorage)Storage();
        var handle = await multi.CreateMultipartAsync(new(_address, 0, FileWriteMode.CreateOnly), PartSize);
        Assert.Equal(0, (await multi.CompleteMultipartAsync(handle, Array.Empty<FilePart>())).Properties.Length);
        Assert.Equal(0, (await multi.CompleteMultipartAsync(handle, Array.Empty<FilePart>())).Properties.Length);
    }
    [Fact]
    public async Task Factory_KeepsOldStore_AndRejectsUnconfiguredContainer()
    {
        var services = new ServiceCollection();
        services.AddBingFileStorage(o => o.DefaultStore = "new");
        services.AddBingFileSystemStorage("old", o => o.RootPath = Path.Combine(_root, "old"));
        services.AddBingFileSystemStorage("new", o => o.RootPath = Path.Combine(_root, "new"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IFileStorageFactory>();
        await factory.GetStorage("old", StorageScope.Host).SaveAsync(new(_address, 0, FileWriteMode.CreateOnly), Stream.Null);
        Assert.NotNull(await factory.GetStorage("old", StorageScope.Host).GetPropertiesAsync(_address));
        Assert.Null(await factory.GetStorage(factory.DefaultStore, StorageScope.Host).GetPropertiesAsync(_address));
        await Assert.ThrowsAsync<ArgumentException>(() => factory.GetStorage("new", StorageScope.Host).GetPropertiesAsync(new("unknown", "file")));
    }
    [Fact]
    public async Task InFlightPart_CannotReplaceFrozenCompletion()
    {
        var storage = Storage(); var multi = (IMultipartFileStorage)storage;
        var upload = await multi.CreateMultipartAsync(new(_address, 1, FileWriteMode.CreateOnly), PartSize);
        var first = await multi.UploadPartAsync(upload, 1, new MemoryStream(new byte[] { 1 }));
        using var late = new GatedStream();
        var pending = multi.UploadPartAsync(upload, 1, late);
        await late.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await multi.CompleteMultipartAsync(upload, new[] { first });
        late.Resume.TrySetResult(true);
        Assert.Equal(FileStorageError.Conflict, (await Assert.ThrowsAsync<FileStorageException>(() => pending)).Error);
        await storage.ReadAsync(_address, (stream, _) => { Assert.Equal(1, stream.ReadByte()); return Task.CompletedTask; });
    }
    [Fact]
    public void LinkedRoot_IsRejected()
    {
        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, "target"); Directory.CreateDirectory(target);
        var link = Path.Combine(_root, "link");
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
            foreach (var arg in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(start)!; process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, target);
        try { Assert.Equal(FileStorageError.AccessDenied, Assert.Throws<FileStorageException>(() => new FileSystemStorageBackend("local", link)).Error); }
        finally { Directory.Delete(link); }
    }
    private sealed class GatedStream : MemoryStream
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public GatedStream() : base(new byte[] { 9 }) { }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            Started.TrySetResult(true); await Resume.Task.WaitAsync(ct);
            return await base.ReadAsync(buffer, ct);
        }
    }
    private sealed class NonSeekStream : Stream
    {
        private readonly Stream _inner;
        public NonSeekStream(Stream inner) { _inner = inner; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
