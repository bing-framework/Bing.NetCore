#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Bing.AspNetCore.Security.Claims;
using Bing.Biz.FileManagement.AspNetCore;
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

    [Fact]
    public Task UserInfoHttp_LocalLifecycle_EnforcesOwnershipAndBusinessPermissions()
        => RunUserInfoHttpCycleAsync(null);

    [AdminFileManagementMySqlFact]
    public async Task UserInfoHttp_MySql_LocalLifecycle_UsesRealProfileAndFileRepositories()
    {
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!);
        await RunUserInfoHttpCycleAsync(database.DatabaseConnectionString);
    }

    /// <summary>同一实际控制器串联内容、真实资料授权和数据库；不使用允许所有业务的替身。</summary>
    private static async Task RunUserInfoHttpCycleAsync(string? mysqlConnectionString)
    {
        await using var files = new AdminFileHttpEnvironment(mysqlConnectionString);
        await using var profiles = new AdminUserInfoPersistenceTestEnvironment(mysqlConnectionString);
        var contexts = new HttpContextAccessor();
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            files.Configure(services);
            services.AddSingleton<IHttpContextAccessor>(contexts);
            profiles.Configure(services, new HttpContextCurrentPrincipalAccessor(contexts));
            services.RemoveAll<IFilePermissionChecker>();
            services.AddScoped<IFilePermissionChecker, ClaimFilePermissionChecker>();
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            // 仅测试认证边界：从固定身份构造真实 Claims，生产宿主仍使用自己的认证。
            app.Use(async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var role = context.Request.Headers["X-Test-Identity"].ToString();
                    var id = role is "other" or "manager" or "profile-manager" ? OtherUserId : OwnerId;
                    var claims = new System.Collections.Generic.List<Claim>
                    {
                        new(BingClaimTypes.UserId, id.ToString("D")),
                        new("permission", "files.upload"), new("permission", "files.host")
                    };
                    if (role == "manager") claims.Add(new("permission", "files.manage"));
                    if (role == "profile-manager") claims.Add(new("permission", "files.user-info.manage"));
                    // CurrentUser 按框架契约解析 GUID 租户，不能用任意字符串冒充租户身份。
                    if (role == "tenant") claims.Add(new(BingClaimTypes.TenantId, "22222222-3333-4444-5555-666666666666"));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "acceptance"));
                }
                await next();
            });
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        await files.InitializeDatabaseAsync(server.Services);
        await InitializeAsync(profiles);
        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-file-test");
        var bytes = System.Text.Encoding.UTF8.GetBytes("用户资料附件 HTTP 验收\n");
        var create = await client.PostAsJsonAsync("/api/files/upload-sessions", new CreateFileUpload
        {
            RequestId = Guid.NewGuid(), FileName = "资料附件.txt", ContentType = "text/plain", Length = bytes.Length
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var session = (await create.Content.ReadFromJsonAsync<UploadSessionView>())!;
        await files.RunOperationsAsync(server.Services);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/files/upload-sessions/{session.Id}/part-requests",
            new PartUploadRequest { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/files/upload-sessions/{session.Id}/parts/1", new ByteArrayContent(bytes))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/files/upload-sessions/{session.Id}/complete", null)).StatusCode);
        await files.RunOperationsAsync(server.Services);
        var route = $"/api/files/{session.FileId}";
        var bindingBody = new { businessType = "user-info", businessId = OwnerId.ToString("D") };
        var bound = await client.PostAsJsonAsync(route + "/bindings", bindingBody);
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);
        var binding = (await bound.Content.ReadFromJsonAsync<FileBinding>())!;
        Assert.Equal(bytes, await client.GetByteArrayAsync(route + "/content"));
        var deletion = await client.DeleteAsync(route);
        Assert.Equal("bound", (await deletion.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());

        foreach (var identity in new[] { "other", "manager", "profile-manager", "tenant" })
        {
            client.DefaultRequestHeaders.Remove("X-Test-Identity");
            client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
            var read = await client.GetAsync(route + "/content");
            Assert.Equal(identity is "manager" or "profile-manager" ? HttpStatusCode.OK :
                identity == "tenant" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, read.StatusCode);
            var bind = await client.PostAsJsonAsync(route + "/bindings", bindingBody);
            Assert.Equal(identity == "profile-manager" ? HttpStatusCode.OK :
                identity == "tenant" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, bind.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-Test-Identity");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route + $"/bindings/{binding.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(route)).StatusCode);
        await files.RunOperationsAsync(server.Services);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route + "/content")).StatusCode);
        using var verify = server.Services.CreateScope();
        var entry = (await verify.ServiceProvider.GetRequiredService<IFileManagementStore>().FindAsync(session.FileId))!;
        Assert.Equal(FileState.Deleted, entry.File.State);
        Assert.Null(await verify.ServiceProvider.GetRequiredService<IFileStorageFactory>()
            .GetStorage(entry.File.StoreName, entry.File.Scope()).GetPropertiesAsync(entry.File.Address()));
    }

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
    [InlineData("admin-test-local", false)]
    [InlineData("qiniu-proxy", false)]
    [InlineData("admin-test-local", true)]
    public async Task RealFileService_AllowsOwnerBinding_RejectsOtherUser_AndDeletesFile(string storeName, bool personal)
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
            var attachmentId = session.FileId;
            if (personal)
            {
                // 用真实 Local 内容承载个人归属，以隔离验证真实 UserInfo 授权和公共副本发布；
                // 网盘 Provider 的中转/远端链路由 RemoteDriveWorkflowTests 单独覆盖。
                var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
                var source = (await store.FindAsync(session.FileId))!;
                source.File.Ownership = Bing.FileStorage.RemoteDrive.RemoteDriveOwnership.Personal;
                Assert.True(await store.TryUpdateAsync(source, source.File.Version));
                Assert.Equal("requires_copy", (await Assert.ThrowsAsync<FileManagementException>(() =>
                    files.BindAsync(session.FileId, AdminUserInfoFileAuthorizer.BusinessType, businessId))).Code);
                var copies = scope.ServiceProvider.GetRequiredService<FileBindingCopyService>();
                var copy = await copies.CreateAsync(session.FileId, Guid.NewGuid(), AdminUserInfoFileAuthorizer.BusinessType, businessId);
                await fileEnvironment.RunOperationsAsync(provider);
                attachmentId = await copies.BindAsync(copy.Id);
                Assert.NotEqual(session.FileId, attachmentId);
                Assert.Equal(FileState.Ready, (await store.FindAsync(session.FileId))!.File.State);
                Assert.Empty((await store.FindAsync(session.FileId))!.Bindings);
            }
            var binding = await files.BindAsync(attachmentId, AdminUserInfoFileAuthorizer.BusinessType, businessId);
            Assert.Equal(attachmentId, binding.FileId);
            Assert.Equal(attachmentId,
                Assert.Single(await files.ListBusinessFilesAsync(AdminUserInfoFileAuthorizer.BusinessType, businessId)).Id);
            Assert.Equal(binding.Id, Assert.Single(await files.ListBindingsAsync(attachmentId)).Id);

            var entry = await files.AuthorizeReadAsync(attachmentId);
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
                    attachmentId, AdminUserInfoFileAuthorizer.BusinessType, businessId));
                Assert.Equal("forbidden", error.Code);
            }

            await files.UnbindAsync(attachmentId, binding.Id);
            Assert.Empty(await files.ListBusinessFilesAsync(AdminUserInfoFileAuthorizer.BusinessType, businessId));
            await files.DeleteAsync(attachmentId);
            await fileEnvironment.RunOperationsAsync(provider);

            using var verifyScope = provider.CreateScope();
            var stored = await verifyScope.ServiceProvider.GetRequiredService<IFileManagementStore>()
                .FindAsync(attachmentId);
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
