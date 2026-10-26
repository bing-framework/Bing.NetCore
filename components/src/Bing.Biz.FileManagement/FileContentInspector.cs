using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bing.FileStorage;

namespace Bing.Biz.FileManagement;

/// <summary>真实内容检查；只读取有界文件头，病毒扫描通过 clamd INSTREAM 流式传输。</summary>
public sealed class FileContentInspector
{
    private readonly FileManagementOptions _options;
    public FileContentInspector(FileManagementOptions options) { _options = options; }

    public static void ValidateDeclaration(FileContainerPolicy policy, string name, string contentType, long length)
    {
        var extension = Path.GetExtension(name);
        if (length < 0 || length > policy.MaxFileLength ||
            policy.AllowedExtensions.Count > 0 && !policy.AllowedExtensions.Any(x => x.Equals(extension, StringComparison.OrdinalIgnoreCase)) ||
            policy.AllowedContentTypes.Count > 0 && !policy.AllowedContentTypes.Any(x => x.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
            throw new FileManagementException("content_policy", "文件不符合容器内容策略。");
    }
    private static FileContainerPolicy Policy(FileRecord file)
        => JsonSerializer.Deserialize<FileContainerPolicy>(file.ContentPolicyJson) ?? throw new FileManagementException("configuration", "内容策略不可读取。");
    public static bool RequiresInspection(FileRecord file)
    { var policy = Policy(file); return policy.RequireMalwareScan || policy.FileHeaders.Count != 0; }

    public async Task InspectAsync(FileRecord file, IFileStorage storage, CancellationToken ct = default)
    {
        var policy = Policy(file);
        ValidateDeclaration(policy, file.FileName, file.ContentType, file.Length);
        var extension = Path.GetExtension(file.FileName);
        var header = policy.FileHeaders.FirstOrDefault(x => x.Key.Equals(extension, StringComparison.OrdinalIgnoreCase)).Value;
        if (header != null)
        {
            var expected = Convert.FromHexString(header);
            await storage.ReadAsync(file.Address(), async (stream, token) =>
            {
                var actual = new byte[expected.Length]; var offset = 0;
                while (offset < actual.Length)
                {
                    var read = await stream.ReadAsync(actual.AsMemory(offset), token);
                    if (read == 0) break;
                    offset += read;
                }
                if (offset != expected.Length || !actual.AsSpan().SequenceEqual(expected))
                    throw new FileManagementException("content_rejected", "文件头与声明格式不一致。");
            }, ct);
        }
        if (policy.RequireMalwareScan)
        {
            if (string.IsNullOrWhiteSpace(_options.ClamAv.Host) || file.Length > _options.ClamAv.MaxStreamLength)
                throw new FileManagementException("scan_configuration", "扫描服务未配置或文件超过扫描上限。");
            await storage.ReadAsync(file.Address(), (stream, token) => ScanClamAsync(stream, file.Length, token), ct);
        }
    }

    private async Task ScanClamAsync(Stream input, long length, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.ClamAv.Timeout);
        var token = timeout.Token;
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.ClamAv.Host!, _options.ClamAv.Port, token);
            await using var network = client.GetStream();
            await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), token);
            var buffer = new byte[64 * 1024]; var size = new byte[4]; long sent = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), token);
                if (read == 0) break;
                sent = checked(sent + read);
                if (sent > length || sent > _options.ClamAv.MaxStreamLength) throw new FileManagementException("scan_size", "扫描长度超过声明。");
                BinaryPrimitives.WriteUInt32BigEndian(size, (uint)read);
                await network.WriteAsync(size, token); await network.WriteAsync(buffer.AsMemory(0, read), token);
            }
            if (sent != length) throw new FileManagementException("scan_size", "扫描长度与声明不一致。");
            await network.WriteAsync(new byte[4], token);
            var response = new byte[4096]; var used = 0;
            while (used < response.Length)
            {
                var read = await network.ReadAsync(response.AsMemory(used, 1), token);
                if (read == 0) throw new IOException("扫描响应提前结束。");
                if (response[used++] == 0) break;
            }
            if (used == 0 || response[used - 1] != 0) throw new IOException("扫描响应无终止符。");
            var result = Encoding.UTF8.GetString(response, 0, used - 1);
            if (result == "stream: OK") return;
            if (result.StartsWith("stream: ", StringComparison.Ordinal) && result.EndsWith(" FOUND", StringComparison.Ordinal))
                throw new FileManagementException("malware_detected", "文件未通过病毒扫描。");
            throw new FileManagementException("scan_failed", "扫描服务没有确认文件通过。");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("扫描服务超时。"); }
        catch (SocketException) { throw new IOException("扫描服务不可用。"); }
    }
}
