#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Admin.Modules;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>两个宿主均通过真实文件模块注册三个新 Provider，不访问云端。</summary>
public sealed class AdminRemoteStorageRegistrationTests
{
    /// <summary>普通存储同样使用配置的密钥环，恢复后游标及应用下载授权仍可解密。</summary>
    [Fact]
    public void Module_PreservesProtectedValuesAcrossProvidersWithoutDriveTemplates()
    {
        var root = Path.Combine(Path.GetTempPath(), "bing-admin-keyring-tests", Guid.NewGuid().ToString("N"));
        try
        {
            string Protect(IServiceProvider provider, string value) => provider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Bing.FileManagement.Download.v1").Protect(value);
            using var first = WithoutDriveTemplates(root).BuildServiceProvider();
            var protectedValue = Protect(first, "restore-proof");
            Assert.NotEmpty(Directory.GetFiles(root, "key-*.xml"));
            using var restored = WithoutDriveTemplates(root).BuildServiceProvider();
            Assert.Equal("restore-proof", restored.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Bing.FileManagement.Download.v1").Unprotect(protectedValue));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>显式配置错误不能静默回退到机器默认密钥。</summary>
    [Fact]
    public void Module_RejectsRelativeKeyRingWithoutDriveTemplates()
        => Assert.Throws<ArgumentException>(() => WithoutDriveTemplates("relative-key-ring"));

    private static ServiceCollection WithoutDriveTemplates(string keyRingPath)
    {
        var values = new Dictionary<string, string?>
        {
            ["FileManagement:Enabled"] = "true", ["FileManagement:RunWorker"] = "false",
            ["FileManagement:DefaultStore"] = "minio-v1",
            ["FileManagement:Stores:minio-v1:Provider"] = "Minio",
            ["FileManagement:Stores:minio-v1:AccessKey"] = "acceptance-key",
            ["FileManagement:Stores:minio-v1:SecretKey"] = "acceptance-secret",
            ["FileManagement:KeyRingPath"] = keyRingPath,
            ["FileManagement:KeyRingApplicationName"] = "Bing.Admin.KeyRing.Tests",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused;"
        };
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        new FileStorageModule().AddServices(services);
        return services;
    }

    [Fact]
    public void Module_RegistersNativeOssAndDynamicDriveTemplatesWithoutDatabaseLookup()
    {
        var services = Services(true); new FileStorageModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IFileStorageFactory>();
        Assert.Equal("oss-v1", factory.DefaultStore);
        var oss = factory.GetStorage("oss-v1", StorageScope.Host);
        Assert.True(oss.Capabilities.HasFlag(FileStorageCapabilities.Range));
        Assert.True(oss.Capabilities.HasFlag(FileStorageCapabilities.SignedDownload));
        Assert.True(oss.Capabilities.HasFlag(FileStorageCapabilities.ServerVerifiedParts));
        Assert.False(oss.Capabilities.HasFlag(FileStorageCapabilities.SignedParts));
        Assert.False(oss.Capabilities.HasFlag(FileStorageCapabilities.CreateOnly));
        var templates = provider.GetServices<RemoteDriveTemplate>().OrderBy(x => x.Name).ToArray();
        Assert.Equal(2, templates.Length);
        foreach (var template in templates)
        {
            var storage = factory.GetStorage(template.StoreName(Guid.NewGuid()), StorageScope.ForTenant("tenant"));
            Assert.Equal(FileStorageCapabilities.Multipart | FileStorageCapabilities.ServerVerifiedParts | FileStorageCapabilities.RequiresCompletionCleanup, storage.Capabilities);
            Assert.IsAssignableFrom<IFileStorageLimitProvider>(storage);
            Assert.Equal(1024, template.MaximumFileLength);
        }
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRemoteDriveStateStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFileStorageTargetStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRemoteDriveCredentialProtector>());
    }

    [Fact]
    public void Module_RequiresPersistentKeysWhenDriveTemplatesAreConfigured()
        => Assert.Throws<InvalidOperationException>(() => new FileStorageModule().AddServices(Services(false)));

    [Fact]
    public async Task Module_BindsAllowedAliyunDriveRedirectUris()
    {
        var services = Services(true); new FileStorageModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var template = provider.GetServices<RemoteDriveTemplate>().Single(x => x.Name == "alipan-v1");

        var authorization = await template.Client.BeginAuthorizationAsync("https://admin.example.test/files/oauth-callback", "csrf");

        Assert.Equal("https://admin.example.test/files/oauth-callback", authorization.RedirectUri);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            template.Client.BeginAuthorizationAsync("https://unregistered.example.test/callback", "csrf"));
    }

    private static ServiceCollection Services(bool keys)
    {
        var values = new Dictionary<string, string?>
        {
            ["FileManagement:Enabled"] = "true", ["FileManagement:RunWorker"] = "false",
            ["FileManagement:DefaultStore"] = "oss-v1",
            ["FileManagement:Stores:oss-v1:Provider"] = "AliyunOss",
            ["FileManagement:Stores:oss-v1:AccessKeyId"] = "test-key",
            ["FileManagement:Stores:oss-v1:AccessKeySecret"] = "test-secret",
            ["FileManagement:Stores:oss-v1:ContainerBuckets:attachments"] = "test-private-bucket",
            ["FileManagement:Stores:local-v1:Provider"] = "FileSystem",
            ["FileManagement:Stores:local-v1:RootPath"] = Path.Combine(Path.GetTempPath(), "bing-test-stage-unused"),
            ["FileManagement:RemoteDrives:alipan-v1:Provider"] = "AliyunDrive",
            ["FileManagement:RemoteDrives:alipan-v1:ClientId"] = "test-client",
            ["FileManagement:RemoteDrives:alipan-v1:ClientSecret"] = "test-secret",
            ["FileManagement:RemoteDrives:alipan-v1:MaximumFileLength"] = "1024",
            ["FileManagement:RemoteDrives:alipan-v1:AllowedRedirectUris:0"] = "https://admin.example.test/files/oauth-callback",
            ["FileManagement:RemoteDrives:pan115-v1:Provider"] = "Pan115",
            ["FileManagement:RemoteDrives:pan115-v1:ClientId"] = "test-client",
            ["FileManagement:RemoteDrives:pan115-v1:MaximumFileLength"] = "1024",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused;"
        };
        if (keys) values["FileManagement:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "bing-test-keys-unused");
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }
}
