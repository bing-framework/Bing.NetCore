#nullable enable

using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.FileStorage;
using Bing.Biz.FileManagement;
using Bing.FileStorage;
using Bing.FileStorage.Qiniu.Tests;
using Bing.MultiTenancy;
using Bing.Security.Claims;
using Bing.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>验证用户资料附件授权使用持久化 UserInfo 所有权和显式权限。</summary>
public sealed class AdminUserInfoFileAuthorizationTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DeletedUserId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    [Theory]
    [InlineData(FileBusinessAccess.Read)]
    [InlineData(FileBusinessAccess.Bind)]
    [InlineData(FileBusinessAccess.Unbind)]
    public async Task OwnerCanAccessExistingProfile(FileBusinessAccess access)
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OwnerId.ToString("D"));

        Assert.True(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), access));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OtherUserRequiresExplicitManagementPermission()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OtherUserId.ToString("D"),
            hasManagementPermission: false);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(AdminFilePermissionCatalog.ManageUserInfoAttachments,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(FileBusinessAccess.Read)]
    [InlineData(FileBusinessAccess.Bind)]
    [InlineData(FileBusinessAccess.Unbind)]
    public async Task ExplicitManagementPermissionAllowsOtherUser(FileBusinessAccess access)
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OtherUserId.ToString("D"),
            hasManagementPermission: true);

        Assert.True(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), access));
        permissions.Verify(checker => checker.IsGrantedAsync(AdminFilePermissionCatalog.ManageUserInfoAttachments,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownBusinessTypeIsRejected()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OtherUserId.ToString("D"),
            hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync("orders", OwnerId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TenantContextIsRejected()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OwnerId.ToString("D"),
            tenantAvailable: true, hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TenantClaimIsRejectedEvenWhenCurrentTenantIsUnavailable()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OwnerId.ToString("D"),
            tenantId: "tenant-a", hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnauthenticatedUserIsRejected()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OwnerId.ToString("D"),
            authenticated: false, hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE")]
    [InlineData("aaaaaaaabbbbccccddddeeeeeeeeeeee")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    public async Task NonCanonicalOrEmptyBusinessIdIsRejected(string businessId)
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OtherUserId.ToString("D"),
            hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType, businessId,
            FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MissingOrSoftDeletedProfileIsRejectedBeforePermissionCheck()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OtherUserId.ToString("D"),
            hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            Guid.NewGuid().ToString("D"), FileBusinessAccess.Read));
        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            DeletedUserId.ToString("D"), FileBusinessAccess.Read));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnknownAccessValueIsRejected()
    {
        await using var environment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(environment);
        var (authorizer, permissions) = CreateAuthorizer(environment.Repository, OwnerId.ToString("D"),
            hasManagementPermission: true);

        Assert.False(await authorizer.AuthorizeAsync(AdminUserInfoFileAuthorizer.BusinessType,
            OwnerId.ToString("D"), (FileBusinessAccess)99));
        permissions.Verify(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("admin-test-local")]
    [InlineData("qiniu-proxy")]
    public async Task RealFileService_AllowsOwnerBinding_RejectsOtherUser_AndDeletesFile(string storeName)
    {
        await using var fileEnvironment = new AdminFileHttpEnvironment();
        await using var userInfoEnvironment = new AdminUserInfoPersistenceTestEnvironment();
        await InitializeAsync(userInfoEnvironment);

        var principalAccessor = new ThreadCurrentPrincipalAccessor();
        var services = new ServiceCollection();
        fileEnvironment.Configure(services);
        RecordingS3Client? recordingClient = null;
        if (storeName == "qiniu-proxy")
        {
            recordingClient = new RecordingS3Client();
            services.AddSingleton(recordingClient);
            services.AddBingFileStorageBackend("qiniu-proxy", _ => new S3FileStorageBackend("qiniu-proxy",
                new BingS3StorageOptions
                {
                    ServiceUrl = "https://s3.cn-east-1.qiniucs.com",
                    Region = "cn-east-1",
                    AccessKey = "test-key",
                    SecretKey = "test-secret",
                    StorageNamespace = "admin-profile-test",
                    ForcePathStyle = true,
                    UploadMode = S3UploadMode.ServerProxy,
                    ContainerBuckets = new() { ["attachments"] = "admin-profile-space" }
                }, recordingClient));
        }
        services.RemoveAll<BingFileStorageOptions>();
        services.AddSingleton(new BingFileStorageOptions { DefaultStore = storeName });
        userInfoEnvironment.Configure(services, principalAccessor);
        services.RemoveAll<IFilePermissionChecker>();
        services.AddScoped<IFilePermissionChecker, AdminUserInfoFileTestPermissionChecker>();
        using var provider = services.BuildServiceProvider();
        await fileEnvironment.InitializeDatabaseAsync(provider);

        using (principalAccessor.Change(CreatePrincipal(OwnerId)))
        using (var scope = provider.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<FileManagementService>();
            var content = new byte[] { 18, 44, 93, 121, 207 };
            var session = await files.CreateUploadAsync(new CreateFileUpload
            {
                RequestId = Guid.NewGuid(),
                Container = "attachments",
                FileName = "profile.txt",
                ContentType = "text/plain",
                Length = content.Length
            });
            await fileEnvironment.RunOperationsAsync(provider);
            await files.CreatePartRequestAsync(session.Id, new PartUploadRequest
            {
                Number = 1,
                Sha256 = Convert.ToBase64String(SHA256.HashData(content))
            });
            await files.UploadPartAsync(session.Id, 1, new MemoryStream(content));
            await files.CompleteUploadAsync(session.Id);
            await fileEnvironment.RunOperationsAsync(provider);

            var ready = await files.GetAsync(session.FileId);
            Assert.Equal(FileState.Ready, ready.State);
            Assert.Equal(content.Length, ready.Length);

            var businessId = OwnerId.ToString("D");
            var binding = await files.BindAsync(session.FileId, AdminUserInfoFileAuthorizer.BusinessType, businessId);
            Assert.Equal(session.FileId, binding.FileId);
            Assert.Equal(session.FileId,
                Assert.Single(await files.ListBusinessFilesAsync(AdminUserInfoFileAuthorizer.BusinessType, businessId)).Id);
            Assert.Equal(binding.Id, Assert.Single(await files.ListBindingsAsync(session.FileId)).Id);

            var entry = await files.AuthorizeReadAsync(session.FileId);
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageFactory>()
                .GetStorage(entry.File.StoreName, entry.File.Scope());
            if (storeName == "qiniu-proxy")
                Assert.True(storage.Capabilities.HasFlag(FileStorageCapabilities.ServerVerifiedParts));
            using var actualContent = new MemoryStream();
            await storage.ReadAsync(entry.File.Address(), (stream, ct) => stream.CopyToAsync(actualContent, ct));
            Assert.Equal(content, actualContent.ToArray());

            using (principalAccessor.Change(CreatePrincipal(OtherUserId)))
            using (var otherScope = provider.CreateScope())
            {
                var otherFiles = otherScope.ServiceProvider.GetRequiredService<FileManagementService>();
                var error = await Assert.ThrowsAsync<FileManagementException>(() => otherFiles.BindAsync(
                    session.FileId, AdminUserInfoFileAuthorizer.BusinessType, businessId));
                Assert.Equal("forbidden", error.Code);
            }

            await files.UnbindAsync(session.FileId, binding.Id);
            Assert.Empty(await files.ListBusinessFilesAsync(AdminUserInfoFileAuthorizer.BusinessType, businessId));
            await files.DeleteAsync(session.FileId);
            await fileEnvironment.RunOperationsAsync(provider);

            using var verifyScope = provider.CreateScope();
            var stored = await verifyScope.ServiceProvider.GetRequiredService<IFileManagementStore>()
                .FindAsync(session.FileId);
            Assert.Equal(FileState.Deleted, stored!.File.State);
            if (recordingClient != null)
                Assert.Empty(recordingClient.Objects);
        }
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId) => new(new ClaimsIdentity(new[]
    {
        new Claim(BingClaimTypes.UserId, userId.ToString("D"))
    }, "admin-user-info-file-test"));

    private static async Task InitializeAsync(AdminUserInfoPersistenceTestEnvironment environment) =>
        await environment.InitializeAsync(new[]
        {
            CreateUserInfo(OwnerId),
            CreateUserInfo(OtherUserId),
            CreateUserInfo(DeletedUserId, isDeleted: true)
        });

    private static UserInfo CreateUserInfo(Guid id, bool isDeleted = false)
    {
        var userInfo = new UserInfo(id)
        {
            Name = $"User {id:N}",
            Nickname = $"User {id:N}"
        };
        userInfo.Init();
        userInfo.IsDeleted = isDeleted;
        return userInfo;
    }

    private static (AdminUserInfoFileAuthorizer Authorizer, Mock<IFilePermissionChecker> Permissions)
        CreateAuthorizer(IUserInfoRepository repository, string userId, bool authenticated = true,
            bool tenantAvailable = false, string? tenantId = null, bool hasManagementPermission = false)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(authenticated);
        currentUser.SetupGet(user => user.UserId).Returns(userId);
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId ?? string.Empty);
        var currentTenant = new Mock<ICurrentTenant>();
        currentTenant.SetupGet(tenant => tenant.IsAvailable).Returns(tenantAvailable);
        var permissions = new Mock<IFilePermissionChecker>();
        permissions.Setup(checker => checker.IsGrantedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasManagementPermission);

        return (new AdminUserInfoFileAuthorizer(repository, currentUser.Object, currentTenant.Object,
            permissions.Object), permissions);
    }

    private sealed class AdminUserInfoFileTestPermissionChecker : IFilePermissionChecker
    {
        private readonly ICurrentUser _currentUser;

        public AdminUserInfoFileTestPermissionChecker(ICurrentUser currentUser) => _currentUser = currentUser;

        public Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default) => Task.FromResult(
            permission is "files.host" or "files.upload" ||
            permission == "files.manage" && _currentUser.UserId == OtherUserId.ToString("D"));
    }
}
