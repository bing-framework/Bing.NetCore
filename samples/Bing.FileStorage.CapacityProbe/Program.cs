using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Bing.FileStorage;
using Bing.FileStorage.CapacityProbe;
using Bing.FileStorage.FileSystem;
using Microsoft.Extensions.DependencyInjection;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var arguments = args.Chunk(2).ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : "");
var backendName = arguments.GetValueOrDefault("--backend", "local");
var gib = int.Parse(arguments.GetValueOrDefault("--gib", "1"));
var allowQiniu = arguments.GetValueOrDefault("--allow-qiniu") == "true";
if (gib is not (1 or 100) || backendName is not ("local" or "minio" or "qiniu")) throw new ArgumentException("只支持 local/minio/qiniu 与 1/100 GiB。");
if (backendName == "qiniu" && !allowQiniu) throw new ArgumentException("七牛容量测试会产生真实流量与存储费用；需显式传入 --allow-qiniu true。");
var parent = Path.GetFullPath(arguments.GetValueOrDefault("--root", "H:/temp/Bing.FileStorage.Capacity"));
var evidence = Path.GetFullPath(arguments.GetValueOrDefault("--evidence", Path.Combine(parent, backendName + "-" + gib + "gib.json")));
var length = gib * 1024L * 1024 * 1024;
const int partSize = 16 * 1024 * 1024;
const int requestedConcurrency = 1;
const int maxActualParallelism = 1;
var tempDirectory = Path.GetTempPath();
Directory.CreateDirectory(parent);
var disk = CapacityProbeDiskSpace.Measure(backendName, parent, tempDirectory, length, partSize, maxActualParallelism);
var freeBefore = disk.AvailableBytes;
if (!disk.HasEnoughSpace)
    throw new IOException($"容量探针磁盘空间不足：目录 {disk.DirectoryPath}（卷 {disk.VolumeRoot}）需要 {disk.RequiredBytes} 字节，可用 {disk.AvailableBytes} 字节。");
var runId = Guid.NewGuid().ToString("N");
var runRoot = Path.Combine(parent, runId);
var container = "bing-capacity-" + runId;
var services = new ServiceCollection();
services.AddBingFileStorage(o => { o.DefaultStore = backendName; o.Containers.Clear(); o.Containers.Add(container); });
IAmazonS3? admin = null;
if (backendName == "local") services.AddBingFileSystemStorage("local", o => o.RootPath = runRoot);
else if (backendName == "minio")
{
    var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT") ?? throw new ArgumentException("需要测试MinIO地址。");
    if (!new Uri(endpoint).IsLoopback) throw new ArgumentException("容量探针只允许隔离的回环地址。");
    var access = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY");
    var secret = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY");
    services.AddBingMinioStorage("minio", o => { o.Endpoint = endpoint; o.AccessKey = access; o.SecretKey = secret; });
    admin = new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
    await admin.PutBucketAsync(new PutBucketRequest { BucketName = container });
}
else
{
    var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_ENDPOINT");
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme != Uri.UriSchemeHttps)
        throw new ArgumentException("七牛 Endpoint 必须是有效的 HTTPS 地址。需要 BING_FILE_STORAGE_QINIU_ENDPOINT。");
    var region = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_REGION");
    if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("需要 BING_FILE_STORAGE_QINIU_REGION。");
    var bucket = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_BUCKET");
    if (string.IsNullOrWhiteSpace(bucket)) throw new ArgumentException("需要预先创建的 BING_FILE_STORAGE_QINIU_BUCKET 实际 S3 空间名。");
    var access = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_ACCESS_KEY");
    if (string.IsNullOrWhiteSpace(access)) throw new ArgumentException("需要 BING_FILE_STORAGE_QINIU_ACCESS_KEY。");
    var secret = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_SECRET_KEY");
    if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("需要 BING_FILE_STORAGE_QINIU_SECRET_KEY。");
    services.AddBingQiniuStorage("qiniu", o =>
    {
        o.Endpoint = endpoint!;
        o.Region = region;
        o.AccessKey = access;
        o.SecretKey = secret;
        o.StorageNamespace = "bing-capacity-probe";
        o.ForcePathStyle = true;
        o.UploadMode = S3UploadMode.ServerProxy;
        o.ContainerBuckets[container] = bucket;
    });
}
using var provider = services.BuildServiceProvider();
var storage = provider.GetRequiredService<IFileStorageFactory>().GetStorage(backendName, StorageScope.ForTenant("capacity-probe"));
var multipart = (IMultipartFileStorage)storage;
using var deadline = new CancellationTokenSource(TimeSpan.FromHours(2));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; deadline.Cancel(); };
var address = new FileAddress(container, runId);
Bing.FileStorage.MultipartUpload? upload = null;
var timer = Stopwatch.StartNew();
long peakWorkingSet = 0, peakPrivateBytes = 0, peakManagedBytes = 0, minimumDiskFree = freeBefore, uploaded = 0, downloaded = 0;
var phase = "starting"; var cancellationVerified = false; string? error = null; var success = false;
using var monitorStop = new CancellationTokenSource();
var monitor = Task.Run(async () =>
{
    while (!monitorStop.IsCancellationRequested)
    {
        using var process = Process.GetCurrentProcess();
        peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
        peakPrivateBytes = Math.Max(peakPrivateBytes, process.PrivateMemorySize64);
        peakManagedBytes = Math.Max(peakManagedBytes, GC.GetTotalMemory(false));
        minimumDiskFree = Math.Min(minimumDiskFree, disk.ReadAvailableBytes());
        try { await Task.Delay(1000, monitorStop.Token); } catch (OperationCanceledException) { break; }
    }
});
try
{
    phase = "uploading";
    upload = await multipart.CreateMultipartAsync(new(address, length,
        storage.Capabilities.HasFlag(FileStorageCapabilities.CreateOnly) ? FileWriteMode.CreateOnly : FileWriteMode.Overwrite), partSize, deadline.Token);
    var parts = new List<FilePart>();
    for (var number = 1; number <= upload.PartCount; number++)
    {
        using var input = new PatternStream(upload.GetPartLength(number));
        parts.Add(await multipart.UploadPartAsync(upload, number, input, cancellationToken: deadline.Token));
        uploaded += upload.GetPartLength(number);
        if (number % 64 == 0 || number == upload.PartCount)
            Console.WriteLine(JsonSerializer.Serialize(new { phase, backendName, gib, uploaded, length, elapsedSeconds = timer.Elapsed.TotalSeconds }));
    }
    phase = "completing";
    await multipart.CompleteMultipartAsync(upload, parts, deadline.Token);
    var uploadSeconds = timer.Elapsed.TotalSeconds;
    if ((await storage.GetPropertiesAsync(address, deadline.Token))?.Length != length) throw new IOException("对象长度校验失败。");
    phase = "reading";
    await storage.ReadAsync(address, async (input, ct) =>
    {
        var buffer = new byte[128 * 1024];
        using var expected = new PatternStream(length);
        var expectedBuffer = new byte[buffer.Length];
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            var wanted = expected.Read(expectedBuffer, 0, read);
            if (wanted != read || !buffer.AsSpan(0, read).SequenceEqual(expectedBuffer.AsSpan(0, read))) throw new IOException("内容校验失败。");
            downloaded += read;
        }
        if (downloaded != length) throw new IOException("读取字节数不完整。");
    }, deadline.Token);
    phase = "cancellation";
    var cancelHandle = await multipart.CreateMultipartAsync(new(new FileAddress(container, runId + "-cancel"), partSize, FileWriteMode.Overwrite), partSize, deadline.Token);
    using var cancelled = new CancellationTokenSource();
    using var cancelInput = new PatternStream(16 * 1024 * 1024, cancelled);
    try { await multipart.UploadPartAsync(cancelHandle, 1, cancelInput, cancellationToken: cancelled.Token); }
    catch (OperationCanceledException) { cancellationVerified = true; }
    await multipart.AbortMultipartAsync(cancelHandle, deadline.Token);
    if (!cancellationVerified || await storage.GetPropertiesAsync(cancelHandle.Request.Address, deadline.Token) != null) throw new IOException("取消未保持未发布状态。");
    phase = "verified"; success = true;
    Console.WriteLine(JsonSerializer.Serialize(new { phase, uploadSeconds, totalSeconds = timer.Elapsed.TotalSeconds, uploaded, downloaded }));
}
catch (Exception ex)
{
    error = ex.GetType().Name + (ex is FileStorageException storageError ? ":" + storageError.Error : "");
    Console.Error.WriteLine("容量探针失败：" + error);
    Environment.ExitCode = 1;
}
finally
{
    phase = "cleanup";
    using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    try
    {
        if (upload != null && !success) await multipart.AbortMultipartAsync(upload, cleanup.Token);
        await storage.DeleteAsync(address, cleanup.Token);
        if (admin != null) await admin.DeleteBucketAsync(container, cleanup.Token);
        if (Directory.Exists(runRoot)) Directory.Delete(runRoot, true);
    }
    catch (Exception ex) { error = (error ?? "") + ";cleanup:" + ex.GetType().Name; success = false; Environment.ExitCode = 1; }
    monitorStop.Cancel(); await monitor;
    Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
    await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(new
    {
        runId, backendName, gib, length, success, error, uploaded, downloaded, cancellationVerified,
        elapsedSeconds = timer.Elapsed.TotalSeconds, roundTripMiBPerSecond = (uploaded + downloaded) / 1048576d / timer.Elapsed.TotalSeconds,
        peakWorkingSet, peakPrivateBytes, peakManagedBytes, freeBefore, minimumDiskFree, freeAfter = disk.ReadAvailableBytes(),
        requestedConcurrency, maxActualParallelism, maxQueuedRequests = 0, partSize,
        rootDirectory = parent,
        diskDirectory = disk.DirectoryPath, diskVolume = disk.VolumeRoot, diskRequiredBytes = disk.RequiredBytes,
        timeoutMinutes = 120, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        providerAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(
            backendName == "minio" ? typeof(BingS3StorageOptions).Assembly.Location :
            backendName == "qiniu" ? typeof(BingQiniuStorageOptions).Assembly.Location : typeof(FileSystemStorageBackend).Assembly.Location))),
        s3ProviderAssemblyFullName = backendName == "local" ? null : typeof(BingS3StorageOptions).Assembly.FullName,
        s3ProviderAssemblySha256 = backendName == "local" ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            await File.ReadAllBytesAsync(typeof(BingS3StorageOptions).Assembly.Location))),
        qiniuFacadeType = backendName == "qiniu" ? typeof(BingQiniuStorageOptions).FullName : null,
        qiniuFacadeAssemblyFullName = backendName == "qiniu" ? typeof(BingQiniuStorageOptions).Assembly.FullName : null,
        qiniuFacadeAssemblySha256 = backendName == "qiniu" ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            await File.ReadAllBytesAsync(typeof(BingQiniuStorageOptions).Assembly.Location))) : null,
        harnessAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(typeof(PatternStream).Assembly.Location))),
        completedAt = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    admin?.Dispose();
    Console.WriteLine("证据：" + evidence);
}

sealed class PatternStream : Stream
{
    private static readonly byte[] Pattern = Enumerable.Range(0, 128 * 1024).Select(i => (byte)(i * 31 + 7)).ToArray();
    private readonly long _length;
    private long _position;
    private readonly CancellationTokenSource? _cancel;
    public PatternStream(long length, CancellationTokenSource? cancel = null) { _length = length; _cancel = cancel; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = (int)Math.Min(count, _length - _position);
        var copied = 0;
        while (copied < n)
        {
            var start = (int)(_position % Pattern.Length); var block = Math.Min(n - copied, Pattern.Length - start);
            Array.Copy(Pattern, start, buffer, offset + copied, block); copied += block; _position += block;
        }
        if (_position >= 1024 * 1024) _cancel?.Cancel();
        return n;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Read(buffer, offset, count)); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var n = (int)Math.Min(buffer.Length, _length - _position); var copied = 0;
        while (copied < n) { var start = (int)(_position % Pattern.Length); var block = Math.Min(n - copied, Pattern.Length - start);
            Pattern.AsMemory(start, block).CopyTo(buffer.Slice(copied, block)); copied += block; _position += block; }
        if (_position >= 1024 * 1024) _cancel?.Cancel();
        return ValueTask.FromResult(n);
    }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
