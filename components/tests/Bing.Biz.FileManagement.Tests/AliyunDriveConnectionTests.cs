using System.Text.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.RemoteDrive;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>通过两套 SQLite 验证阿里云盘 OAuth 回调配置边界。</summary>
public sealed class AliyunDriveConnectionTests
{
    private const string RegisteredRedirectUri = "https://application.test/files/oauth-callback";

    private static FileTestEnvironment Setup(bool freeSql, bool configureRedirect)
    {
        var env = new FileTestEnvironment(freeSql);
        env.CustomizeServices = services =>
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(env.Root, "keys")));
            services.AddScoped<IRemoteDriveCredentialProtector, RemoteDriveCredentialProtection>();
            services.AddBingAliyunDriveStorage("alipan", options =>
            {
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                options.MaximumFileLength = 1024;
                if (configureRedirect)
                    options.AllowedRedirectUris.Add(RegisteredRedirectUri);
            });
        };
        return env;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartRejectsUnregisteredRedirectWithoutPersistingConnection(bool freeSql)
    {
        await using var env = Setup(freeSql, configureRedirect: true);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            env.Get<FileStorageConnectionService>().StartAsync(
                "alipan", "My drive", RemoteDriveOwnership.Personal,
                "https://attacker.test/callback"));

        Assert.Contains("回调", error.Message);
        Assert.Empty(await store.ListConnectionsAsync(scope.Key, env.Identity.UserId, includeShared: true, 0, 50));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAllowsRegisteredRedirectAndPersistsAuthorization(bool freeSql)
    {
        await using var env = Setup(freeSql, configureRedirect: true);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);

        var started = await env.Get<FileStorageConnectionService>().StartAsync(
            "alipan", "My drive", RemoteDriveOwnership.Personal, RegisteredRedirectUri);

        Assert.StartsWith("https://openapi.alipan.com/oauth/authorize?", started.AuthorizationUrl);
        Assert.Contains(Uri.EscapeDataString(RegisteredRedirectUri), started.AuthorizationUrl);
        var saved = Assert.Single(await store.ListConnectionsAsync(scope.Key, env.Identity.UserId, includeShared: true, 0, 50));
        Assert.Equal(started.Id, saved.Id);
        Assert.Equal(RemoteDriveConnectionState.Authorizing, saved.State);
        var authorization = JsonSerializer.Deserialize<RemoteDriveAuthorization>(
            env.Get<IRemoteDriveCredentialProtector>().Unprotect(saved.Id, saved.EncryptedAuthorization));
        Assert.Equal(RegisteredRedirectUri, authorization!.RedirectUri);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartFailsClosedWhenRedirectAllowlistIsMissing(bool freeSql)
    {
        await using var env = Setup(freeSql, configureRedirect: false);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);

        var error = await Assert.ThrowsAsync<FileStorageException>(() =>
            env.Get<FileStorageConnectionService>().StartAsync(
                "alipan", "My drive", RemoteDriveOwnership.Personal, RegisteredRedirectUri));

        Assert.Equal(FileStorageError.Configuration, error.Error);
        Assert.Empty(await store.ListConnectionsAsync(scope.Key, env.Identity.UserId, includeShared: true, 0, 50));
    }
}
