#nullable enable
using System;
using System.Collections.Generic;
using Bing.Admin.Modules;
using Bing.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>验证 Admin 文件模块对七牛存储的真实配置装配。</summary>
public sealed class AdminQiniuStorageRegistrationTests
{
    [Theory]
    [InlineData("http://s3.cn-east-1.qiniucs.com", "cn-east-1", true)]
    [InlineData("https://s3.cn-east-1.qiniucs.com", "cn-north-1", true)]
    [InlineData("https://s3.cn-east-1.qiniucs.com", "cn-east-1", false)]
    public void AddServices_RejectsInvalidQiniuConfiguration(string endpoint, string region, bool includeBucket)
    {
        var services = CreateServices(CreateConfiguration(endpoint, region, includeBucket));

        Assert.Throws<ArgumentException>(() => new FileStorageModule().AddServices(services));
    }

    [Fact]
    public void AddServices_RegistersQiniuWithServerVerifiedPartsByDefault()
    {
        var services = CreateServices(CreateConfiguration(
            "https://s3.cn-east-1.qiniucs.com", "cn-east-1", includeBucket: true));
        new FileStorageModule().AddServices(services);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IFileStorageFactory>();
        var storage = factory.GetStorage(factory.DefaultStore, StorageScope.Host);

        Assert.Equal("qiniu-v1", factory.DefaultStore);
        Assert.Equal(FileStorageCapabilities.Multipart | FileStorageCapabilities.SignedDownload |
                     FileStorageCapabilities.ServerVerifiedParts | FileStorageCapabilities.Range,
            storage.Capabilities);
        Assert.False(storage.Capabilities.HasFlag(FileStorageCapabilities.SignedParts));
    }

    private static ServiceCollection CreateServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        return services;
    }

    private static IConfiguration CreateConfiguration(string endpoint, string region, bool includeBucket)
    {
        var values = new Dictionary<string, string?>
        {
            ["FileManagement:Enabled"] = "true",
            ["FileManagement:RunWorker"] = "false",
            ["FileManagement:DefaultStore"] = "qiniu-v1",
            ["FileManagement:Stores:qiniu-v1:Provider"] = "Qiniu",
            ["FileManagement:Stores:qiniu-v1:Endpoint"] = endpoint,
            ["FileManagement:Stores:qiniu-v1:Region"] = region,
            ["FileManagement:Stores:qiniu-v1:AccessKey"] = "test-access-key",
            ["FileManagement:Stores:qiniu-v1:SecretKey"] = "test-secret-key",
            ["FileManagement:Stores:qiniu-v1:StorageNamespace"] = "admin-qiniu-test",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused;"
        };
        if (includeBucket)
            values["FileManagement:Stores:qiniu-v1:ContainerBuckets:attachments"] = "test-s3-space";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
