using System.Security.Cryptography;
using System.Text;

namespace Bing.Resilience.Operations;

/// <summary>持久命令保护。密钥由宿主管理并保留到旧操作排空；不在 Journal 保存访问令牌。</summary>
public interface IOperationDataProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedData);
}
/// <summary>版本化 AES-GCM 密钥环；提供密钥轮换后的历史解密能力。</summary>
public sealed class AesGcmOperationDataProtector : IOperationDataProtector, IDisposable
{
    private readonly Dictionary<string, byte[]> _keys;
    private readonly string _active;
    private bool _disposed;
    public AesGcmOperationDataProtector(string activeKeyId, IReadOnlyDictionary<string, byte[]> keys)
    {
        if (!keys.ContainsKey(activeKeyId) || keys.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 64 ||
            x.Key.Any(c => !char.IsLetterOrDigit(c) && c != '-') || x.Value.Length != 32))
            throw new ResilienceConfigurationException("持久操作需要有效的 AES-256 密钥环。");
        _active = activeKeyId; _keys = keys.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
    }
    public string Protect(string plaintext)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AesGcmOperationDataProtector));
        var nonce = RandomNumberGenerator.GetBytes(12); var bytes = Encoding.UTF8.GetBytes(plaintext); var cipher = new byte[bytes.Length]; var tag = new byte[16];
        try
        {
            using var aes = Create(_keys[_active]);
            aes.Encrypt(nonce, bytes, cipher, tag, Encoding.UTF8.GetBytes("bing.operations.v1:" + _active));
            return string.Join(".", "v1", _active, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public string Unprotect(string protectedData)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AesGcmOperationDataProtector));
        if (string.IsNullOrWhiteSpace(protectedData)) throw new ResilienceConfigurationException("持久操作数据为空。");
        var parts = protectedData.Split('.');
        if (parts.Length != 5 || parts[0] != "v1" || !_keys.TryGetValue(parts[1], out var key))
            throw new ResilienceConfigurationException("缺少持久操作历史密钥或数据格式无效。");
        byte[] plaintext = Array.Empty<byte>();
        try
        {
            var nonce = Convert.FromBase64String(parts[2]);
            var tag = Convert.FromBase64String(parts[3]);
            var cipher = Convert.FromBase64String(parts[4]);
            if (nonce.Length != 12 || tag.Length != 16) throw new ResilienceConfigurationException("持久操作数据认证字段无效。");
            plaintext = new byte[cipher.Length];
            using var aes = Create(key);
            aes.Decrypt(nonce, cipher, tag, plaintext, Encoding.UTF8.GetBytes("bing.operations.v1:" + parts[1]));
            return new UTF8Encoding(false, true).GetString(plaintext);
        }
        catch (ResilienceConfigurationException) { throw; }
        catch (Exception error) when (error is FormatException or ArgumentException or CryptographicException or DecoderFallbackException)
        { throw new ResilienceConfigurationException("持久操作数据格式或认证校验失败。", error); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
    private static AesGcm Create(byte[] key) =>
#if NET8_0_OR_GREATER
        new(key, 16);
#else
        new(key);
#endif
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var key in _keys.Values) CryptographicOperations.ZeroMemory(key);
    }
}

/// <summary>仅经持久存储的新事务读取才能创建的提交凭证；不以“正在入队”作为 202 依据。</summary>
public sealed class DurableOperationReceipt : IResilienceDurableCommitProof
{
    public string OperationId { get; }
    public string ScopeHash { get; }
    public string KeyHash { get; }
    public string Fingerprint { get; }
    public DateTimeOffset Deadline { get; }
    private readonly IResilienceOperationStore _store;
    private DurableOperationReceipt(OperationRecord record, IResilienceOperationStore store)
    { _store = store; OperationId = record.Id; ScopeHash = record.ScopeHash; KeyHash = record.KeyHash; Fingerprint = record.Fingerprint; Deadline = record.Deadline; }
    public async ValueTask<bool> VerifyAsync(CancellationToken token)
    {
        var current = await VerifyCommittedAsync(_store, OperationId, token).ConfigureAwait(false);
        return current.ScopeHash == ScopeHash && current.KeyHash == KeyHash && current.Fingerprint == Fingerprint && current.Deadline == Deadline;
    }
    public static async ValueTask<DurableOperationReceipt> VerifyCommittedAsync(IResilienceOperationStore store, string operationId, CancellationToken token = default)
    {
        if (!store.IsDurable) throw new ResilienceConfigurationException("受理凭证需要持久存储。");
        await using var transaction = await store.BeginAsync(token).ConfigureAwait(false);
        var record = await transaction.FindAsync(operationId, token).ConfigureAwait(false)
            ?? throw new ResilienceConfigurationException("操作尚未持久提交。");
        if (record.Kind is not ("replay" or "saga") || record.State is "Failed" or "Expired" or "Cancelled" or "ManualIntervention")
            throw new ResilienceConfigurationException("当前操作不能出具持久受理凭证。");
        return new(record, store);
    }
}
