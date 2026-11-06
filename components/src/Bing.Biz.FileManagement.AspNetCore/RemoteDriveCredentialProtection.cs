using Bing.FileStorage.RemoteDrive;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bing.Biz.FileManagement.AspNetCore;

public sealed class RemoteDriveCredentialProtection : IRemoteDriveCredentialProtector
{
    private readonly IDataProtectionProvider _protection;
    private readonly IOptions<KeyManagementOptions> _keys;
    public RemoteDriveCredentialProtection(IDataProtectionProvider protection, IOptions<KeyManagementOptions> keys) { _protection = protection; _keys = keys; }
    private IDataProtector Protector(Guid id)
    {
        if (_keys.Value.XmlRepository == null) throw new InvalidOperationException("启用网盘必须配置持久 Data Protection 密钥环。");
        return _protection.CreateProtector("Bing.FileManagement.RemoteDriveCredentials.v1", id.ToString("N"));
    }
    public string Protect(Guid connectionId, string plaintext) => Protector(connectionId).Protect(plaintext);
    public string Unprotect(Guid connectionId, string ciphertext) => Protector(connectionId).Unprotect(ciphertext);
}

internal sealed class RemoteDriveKeyRingGuard : IHostedService
{
    private readonly IEnumerable<RemoteDriveTemplate> _templates;
    private readonly IOptions<KeyManagementOptions> _keys;
    public RemoteDriveKeyRingGuard(IEnumerable<RemoteDriveTemplate> templates, IOptions<KeyManagementOptions> keys) { _templates = templates; _keys = keys; }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_templates.Any() && _keys.Value.XmlRepository == null) throw new InvalidOperationException("启用网盘必须配置持久 Data Protection 密钥环；多节点需共享密钥和 ApplicationName。");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class RemoteDriveCredentialProtectionRegistration
{
    public static IServiceCollection AddBingRemoteDriveCredentialProtection(this IServiceCollection services, string keyRingPath, string applicationName = "Bing.FileManagement")
    {
        if (!Path.IsPathFullyQualified(keyRingPath) || string.IsNullOrWhiteSpace(applicationName)) throw new ArgumentException("必须配置绝对密钥目录和应用名。");
        services.AddDataProtection().SetApplicationName(applicationName).PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        services.AddScoped<IRemoteDriveCredentialProtector, RemoteDriveCredentialProtection>();
        return services;
    }
}
