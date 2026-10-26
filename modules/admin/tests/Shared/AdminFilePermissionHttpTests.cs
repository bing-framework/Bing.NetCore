#nullable enable

using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.FileStorage;
using Bing.Admin.Service.Implements;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Systems.Domain.Events;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.AspNetCore.Authorization.JwtBearer;
using Bing.AspNetCore.Security.Claims;
using Bing.AspNetCore.ExceptionHandling;
using Bing.AspNetCore.Mvc.ExceptionHandling;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.Caching;
using Bing.DependencyInjection;
using Bing.EasyCaching;
using Bing.Events.Messages;
using Bing.ExceptionHandling;
using Bing.Logging;
using EasyCaching.Core;
using Bing.Identity.JwtBearer.Abstractions;
using Bing.MultiTenancy;
using Bing.Permissions.Identity.Results;
using Bing.Security.Claims;
using Bing.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
using Bing.Admin.EFCore.Tests;
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>验证 Admin 实际登录令牌与文件权限资源的 HTTP 鉴权闭环。</summary>
public sealed class AdminFilePermissionHttpTests
{
    [Fact]
    public async Task DisabledFilePermissionModule_RejectsPermission()
    {
        var catalog = new AdminFilePermissionTestCatalog();
        catalog.FileModule.Enabled = false;
        var roleIds = string.Join(",", catalog.OwnerRoles);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.IsAuthenticated).Returns(true);
        currentUser.Setup(user => user.FindClaims(BingClaimTypes.ApplicationCode))
            .Returns(new[] { new Claim(BingClaimTypes.ApplicationCode, ApplicationCode.Admin) });
        currentUser.Setup(user => user.FindClaims(BingClaimTypes.ApplicationId))
            .Returns(new[] { new Claim(BingClaimTypes.ApplicationId, catalog.ApplicationId.ToString()) });
        currentUser.Setup(user => user.FindClaims(BingClaimTypes.RoleIds))
            .Returns(new[] { new Claim(BingClaimTypes.RoleIds, roleIds) });

        var checker = new AdminFilePermissionChecker(currentUser.Object, catalog.ModuleRepository.Object,
            catalog.OperationRepository.Object, catalog.PermissionRepository.Object);
        Assert.False(await checker.IsGrantedAsync(AdminFilePermissionCatalog.Upload));
    }

    [Fact]
    public async Task AdminLoginToken_EnforcesFilePermissionsAndPublicRoute()
    {
        await using var environment = new AdminFileHttpEnvironment();
        await using var roleEnvironment = new AdminRolePersistenceTestEnvironment();
        var catalog = new AdminFilePermissionTestCatalog();
        var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                environment.Configure(services, configureTestAuthentication: false);
                ConfigureJwtAndAdminLogin(services, catalog);
                roleEnvironment.Configure(services);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));

        using (server)
        {
            await environment.InitializeDatabaseAsync(server.Services);
            await roleEnvironment.InitializeDatabaseAsync(server.Services, catalog);
            using (var roleScope = server.Services.CreateScope())
            {
                var mixedRoleUser = catalog.CreateUser("mixed-role-status");
                var persistedRoles = await roleScope.ServiceProvider.GetRequiredService<IRoleRepository>()
                    .GetRolesAsync(mixedRoleUser.Id);
                Assert.Equal(catalog.RolesForUser(mixedRoleUser.Id).Select(role => role.Id).OrderBy(id => id),
                    persistedRoles.Select(role => role.Id).OrderBy(id => id));
                Assert.Contains(persistedRoles, role => role.Enabled);
                Assert.Contains(persistedRoles, role => !role.Enabled);
            }
            Assert.Equal(FileVisibility.Public,
                server.Services.GetRequiredService<FileManagementOptions>().Containers["public"].Visibility);
            using var anonymous = server.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/files")).StatusCode);

            using var owner = server.CreateClient();
            owner.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(owner, "owner"));
            var privateFile = await UploadAsync(owner, server.Services, environment, "attachments", "中文 私有.txt");
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/files/{privateFile}/content")).StatusCode);

            using var denyUpload = server.CreateClient();
            denyUpload.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(denyUpload, "deny-upload"));
            Assert.Equal(HttpStatusCode.Forbidden,
                (await CreateSessionAsync(denyUpload, "attachments", "denied.txt")).StatusCode);

            using var noPublish = server.CreateClient();
            var noPublishToken = await LoginAsync(noPublish, "no-publish");
            var noPublishJwt = new JwtSecurityTokenHandler().ReadJwtToken(noPublishToken);
            Assert.Equal(string.Join(",", catalog.NoPublishRoles),
                noPublishJwt.Claims.Single(claim => claim.Type == BingClaimTypes.RoleIds).Value);
            noPublish.DefaultRequestHeaders.Authorization = new("Bearer", noPublishToken);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await CreateSessionAsync(noPublish, "public", "not-public.txt")).StatusCode);

            using var mixedRoles = server.CreateClient();
            var mixedRoleToken = await LoginAsync(mixedRoles, "mixed-role-status");
            var mixedRoleJwt = new JwtSecurityTokenHandler().ReadJwtToken(mixedRoleToken);
            var activeMixedRoleIds = catalog.RolesForUser(catalog.CreateUser("mixed-role-status").Id)
                .Where(role => role.Enabled).Select(role => role.Id);
            Assert.Equal(string.Join(",", activeMixedRoleIds),
                mixedRoleJwt.Claims.Single(claim => claim.Type == BingClaimTypes.RoleIds).Value);
            mixedRoles.DefaultRequestHeaders.Authorization = new("Bearer", mixedRoleToken);
            Assert.Equal(HttpStatusCode.Accepted,
                (await CreateSessionAsync(mixedRoles, "attachments", "active-upload.txt")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await CreateSessionAsync(mixedRoles, "public", "disabled-publish.txt")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await mixedRoles.GetAsync("/api/files?manage=true")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await mixedRoles.GetAsync("/api/files")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await mixedRoles.DeleteAsync($"/api/files/{privateFile}")).StatusCode);

            using var disabledUpload = server.CreateClient();
            disabledUpload.DefaultRequestHeaders.Authorization = new("Bearer",
                await LoginAsync(disabledUpload, "disabled-upload-status"));
            Assert.Equal(HttpStatusCode.Forbidden,
                (await CreateSessionAsync(disabledUpload, "attachments", "disabled-upload.txt")).StatusCode);

            using var disabledHost = server.CreateClient();
            disabledHost.DefaultRequestHeaders.Authorization = new("Bearer",
                await LoginAsync(disabledHost, "disabled-host-status"));
            Assert.Equal(HttpStatusCode.Forbidden, (await disabledHost.GetAsync("/api/files")).StatusCode);

            using var noHost = server.CreateClient();
            noHost.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(noHost, "no-host"));
            Assert.Equal(HttpStatusCode.Forbidden, (await noHost.GetAsync("/api/files")).StatusCode);

            using var noManage = server.CreateClient();
            noManage.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(noManage, "owner"));
            Assert.Equal(HttpStatusCode.Forbidden, (await noManage.GetAsync("/api/files?manage=true")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await noPublish.DeleteAsync($"/api/files/{privateFile}")).StatusCode);

            using var manager = server.CreateClient();
            manager.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(manager, "manager"));
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/files?manage=true")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/files/{privateFile}/content")).StatusCode);

            var publicFile = await UploadAsync(owner, server.Services, environment, "public", "中文 公开.txt");
            using var publicResponse = await anonymous.GetAsync($"/api/files/public/{publicFile}/content");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            Assert.Equal("public payload", await publicResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound,
                (await anonymous.GetAsync($"/api/files/public/{privateFile}/content")).StatusCode);

            Assert.Equal(HttpStatusCode.Accepted, (await manager.DeleteAsync($"/api/files/{privateFile}")).StatusCode);
            await environment.RunOperationsAsync(server.Services);
            Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/files/{privateFile}")).StatusCode);

            var ownerUserId = catalog.CreateUser("owner").Id;
            Assert.True(await roleEnvironment.DisableRolesForUserAsync(server.Services, ownerUserId) > 0);
            using (var roleScope = server.Services.CreateScope())
            {
                var persistedRoles = await roleScope.ServiceProvider.GetRequiredService<IRoleRepository>()
                    .GetRolesAsync(ownerUserId);
                Assert.NotEmpty(persistedRoles);
                Assert.All(persistedRoles, role => Assert.False(role.Enabled));
            }

            using var freshOwnerLogin = server.CreateClient();
            freshOwnerLogin.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            using var disabledLoginResponse = await freshOwnerLogin.PostAsJsonAsync("/api/OAuth/signIn",
                new AdminLoginRequest { UserName = "owner", Password = "valid-test-password" });
            var disabledLoginBody = await disabledLoginResponse.Content.ReadAsStringAsync();
            using var disabledLoginDocument = JsonDocument.Parse(disabledLoginBody);
            Assert.Null(FindAccessToken(disabledLoginDocument.RootElement));
        }
    }

    [Fact]
    public async Task PermissionResourceInitializer_IsExplicitAndIdempotent()
    {
        var applicationId = Guid.NewGuid();
        var application = new Bing.Admin.Systems.Domain.Models.Application(applicationId)
            { Name = "Admin", Code = ApplicationCode.Admin };
        var modules = new List<Module>();
        var operations = new List<Operation>();
        var applications = new Mock<IApplicationRepository>();
        var moduleRepository = new Mock<IModuleRepository>();
        var operationRepository = new Mock<IOperationRepository>();
        var unitOfWork = new Mock<IAdminUnitOfWork>();
        applications.Setup(repository => repository.GetByCodeAsync(ApplicationCode.Admin)).ReturnsAsync(application);
        moduleRepository.Setup(repository => repository.GetModulesAsync(applicationId)).ReturnsAsync(() => modules.ToList());
        moduleRepository.Setup(repository => repository.GenerateSortIdAsync(applicationId, null)).ReturnsAsync(1);
        moduleRepository.Setup(repository => repository.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
            .Callback<Module, CancellationToken>((module, _) => modules.Add(module)).Returns(Task.CompletedTask);
        operationRepository.Setup(repository => repository.GetAllOperationsAsync(applicationId)).ReturnsAsync(() => operations.ToList());
        operationRepository.Setup(repository => repository.GenerateSortIdAsync(applicationId, It.IsAny<Guid>()))
            .ReturnsAsync(() => operations.Count + 1);
        operationRepository.Setup(repository => repository.AddAsync(It.IsAny<Operation>(), It.IsAny<CancellationToken>()))
            .Callback<Operation, CancellationToken>((operation, _) => operations.Add(operation)).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var initializer = new AdminFilePermissionResourceInitializer(unitOfWork.Object, applications.Object,
            moduleRepository.Object, operationRepository.Object);
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.Single(modules);
        Assert.Equal(AdminFilePermissionCatalog.ModuleUri, modules[0].Url);
        Assert.Equal(AdminFilePermissionCatalog.Operations.Select(item => item.Code).OrderBy(code => code),
            operations.Select(operation => operation.Code).OrderBy(code => code));
        Assert.All(operations, operation => Assert.Equal(modules[0].Id, operation.ModuleId));
        unitOfWork.Verify(work => work.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void ConfigureJwtAndAdminLogin(IServiceCollection services, AdminFilePermissionTestCatalog catalog)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
        services.AddSingleton<IExceptionNotifier>(NullExceptionNotifier.Instance);
        services.AddTransient<IExceptionToErrorInfoConverter, DefaultExceptionToErrorInfoConverter>();
        services.AddTransient<IHttpExceptionStatusCodeFinder, DefaultHttpExceptionStatusCodeFinder>();
        services.Configure<BingExceptionHandlingOptions>(_ => { });
        services.Configure<BingExceptionHttpStatusCodeOptions>(_ => { });
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<BingExceptionFilter>());
        services.AddBingLogging();
        services.AddSingleton<ILog>(NullLog.Instance);
        services.AddSingleton<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();
        services.AddTransient<ICurrentUser, CurrentUser>();
        services.AddTransient<ICurrentTenant, PrincipalCurrentTenant>();
        services.AddBingAdminFileStoragePermissions();

        var fileOptions = new FileManagementOptions();
        fileOptions.Containers["public"] = new FileContainerPolicy { Visibility = FileVisibility.Public };
        services.Replace(ServiceDescriptor.Singleton(fileOptions));

        var cacheConfiguration = new CacheConfiguration("admin-file-permission-tests", "jwt-memory");
        services.AddMemoryCaching(cacheConfiguration);
        services.Replace(ServiceDescriptor.Singleton<ICache>(provider => new MemoryCacheManager(
            provider.GetRequiredService<IEasyCachingProviderFactory>(), cacheConfiguration)));

        var jwtConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtOptions:Secret"] = "admin-file-permission-test-signing-key-2026-10-24",
            ["JwtOptions:Issuer"] = "bing-admin-file-tests",
            ["JwtOptions:Audience"] = "bing-admin-file-tests",
            ["JwtOptions:AccessExpireMinutes"] = "15",
            ["JwtOptions:RefreshExpireMinutes"] = "60",
            ["JwtOptions:ThrowEnabled"] = "false"
        }).Build();
        services.AddJwt(jwtConfiguration);
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            options.Filters.Add(new AuthorizeFilter("jwt")));

        var users = new Dictionary<string, User>(StringComparer.Ordinal)
        {
            ["owner"] = catalog.CreateUser("owner"),
            ["deny-upload"] = catalog.CreateUser("deny-upload"),
            ["no-publish"] = catalog.CreateUser("no-publish"),
            ["no-host"] = catalog.CreateUser("no-host"),
            ["manager"] = catalog.CreateUser("manager"),
            ["mixed-role-status"] = catalog.CreateUser("mixed-role-status"),
            ["disabled-upload-status"] = catalog.CreateUser("disabled-upload-status"),
            ["disabled-host-status"] = catalog.CreateUser("disabled-host-status")
        };
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.SingleAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate, CancellationToken _) =>
            {
                var user = users.Values.SingleOrDefault(predicate.Compile());
                return user == null ? null : new User(user.Id)
                {
                    UserName = user.UserName,
                    Nickname = user.Nickname,
                    Enabled = user.Enabled
                };
            });
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByCodeAsync(ApplicationCode.Admin))
            .ReturnsAsync(new Bing.Admin.Systems.Domain.Models.Application(catalog.ApplicationId)
                { Name = "Admin", Code = ApplicationCode.Admin });
        var signInManager = new Mock<ISignInManager>();
        signInManager.Setup(manager => manager.SignInAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync((User user, string _, bool __, bool ___) => new SignInResult(SignInState.Succeeded, user.Id.ToString()));
        var unitOfWork = new Mock<IAdminUnitOfWork>();
        unitOfWork.Setup(work => work.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var eventBus = new Mock<IMessageEventBus>();
        eventBus.Setup(bus => bus.PublishAsync(It.IsAny<UserLoginMessageEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        services.AddSingleton(userRepository.Object);
        services.AddSingleton(applicationRepository.Object);
        services.AddSingleton(signInManager.Object);
        services.AddSingleton(unitOfWork.Object);
        services.AddSingleton(eventBus.Object);
        services.AddScoped<ISecurityService>(provider =>
        {
            var service = ActivatorUtilities.CreateInstance<SecurityService>(provider);
            service.LazyServiceProvider = provider.GetRequiredService<ILazyServiceProvider>();
            return service;
        });

        services.AddSingleton(catalog.ModuleRepository.Object);
        services.AddSingleton(catalog.OperationRepository.Object);
        services.AddSingleton(catalog.PermissionRepository.Object);
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName)
    {
        using var response = await client.PostAsJsonAsync("/api/OAuth/signIn", new AdminLoginRequest
        {
            UserName = userName,
            Password = "valid-test-password"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        return FindAccessToken(document.RootElement)
            ?? throw new InvalidOperationException($"Admin 登录响应未返回访问令牌：{responseBody}");
    }

    private static string? FindAccessToken(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "accessToken", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                var nested = FindAccessToken(property.Value);
                if (nested != null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindAccessToken(item);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private static async Task<Guid> UploadAsync(HttpClient client, IServiceProvider services,
        AdminFileHttpEnvironment environment, string container, string fileName)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("public payload");
        var response = await CreateSessionAsync(client, container, fileName, payload.Length);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<UploadSessionView>())!;
        using (response)
        {
            await environment.RunOperationsAsync(services);
            using var partRequest = await client.PostAsJsonAsync($"/api/files/upload-sessions/{session.Id}/part-requests",
                new PartUploadRequest { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(payload)) });
            Assert.Equal(HttpStatusCode.OK, partRequest.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PutAsync($"/api/files/upload-sessions/{session.Id}/parts/1", new ByteArrayContent(payload))).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted,
                (await client.PostAsync($"/api/files/upload-sessions/{session.Id}/complete", content: null)).StatusCode);
            await environment.RunOperationsAsync(services);
        }
        return session.FileId;
    }

    private static Task<HttpResponseMessage> CreateSessionAsync(HttpClient client, string container, string fileName,
        long length = 5) => client.PostAsJsonAsync("/api/files/upload-sessions", new CreateFileUpload
        {
            RequestId = Guid.NewGuid(),
            FileName = fileName,
            ContentType = "text/plain",
            Container = container,
            Length = length,
            LastModified = 123456
        });

}

internal sealed class AdminFilePermissionTestCatalog
{
    private readonly Dictionary<Guid, List<Role>> _rolesByUser = new();
    private readonly List<Permission> _permissionRecords = new();
    private readonly Dictionary<Guid, Operation> _operationsById = new();
    private readonly Dictionary<string, User> _usersByName = new(StringComparer.Ordinal);

    public AdminFilePermissionTestCatalog()
    {
        ApplicationId = Guid.NewGuid();
        var module = new Module(Guid.NewGuid(), "", 0)
        {
            ApplicationId = ApplicationId,
            Name = "文件管理",
            Url = AdminFilePermissionCatalog.ModuleUri,
            Enabled = true
        };
        var operations = AdminFilePermissionCatalog.Operations.ToDictionary(
            item => item.Code,
            item => new Operation(Guid.NewGuid())
            {
                ApplicationId = ApplicationId,
                ModuleId = module.Id,
                Code = item.Code,
                Name = item.Name,
                Enabled = true
            }, StringComparer.Ordinal);

        ModuleRepository = new Mock<IModuleRepository>();
        FileModule = module;
        ModuleRepository.Setup(repository => repository.GetModulesAsync(ApplicationId))
            .ReturnsAsync(() => new List<Module> { FileModule });
        OperationRepository = new Mock<IOperationRepository>();
        OperationRepository.Setup(repository => repository.FindByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids
                .Where(_operationsById.ContainsKey).Select(id => _operationsById[id]).ToList());
        foreach (var operation in operations.Values) _operationsById.Add(operation.Id, operation);

        PermissionRepository = new Mock<IPermissionRepository>();
        PermissionRepository.Setup(repository => repository.GetResourceIdsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync((Guid appId, Guid roleId, bool isDeny) => appId == ApplicationId
                ? _permissionRecords.Where(record => record.RoleId == roleId && record.IsDeny == isDeny)
                    .Select(record => record.ResourceId).ToList()
                : new List<Guid>());

        OwnerRoles = AddUser("owner", operations, new[]
        {
            AdminFilePermissionCatalog.Upload, AdminFilePermissionCatalog.Publish, AdminFilePermissionCatalog.Host
        });
        DenyUploadRoles = AddUser("deny-upload", operations, new[] { AdminFilePermissionCatalog.Upload, AdminFilePermissionCatalog.Host },
            new[] { AdminFilePermissionCatalog.Upload });
        NoPublishRoles = AddUser("no-publish", operations, new[] { AdminFilePermissionCatalog.Upload, AdminFilePermissionCatalog.Host });
        NoHostRoles = AddUser("no-host", operations, new[] { AdminFilePermissionCatalog.Upload });
        ManagerRoles = AddUser("manager", operations, new[]
        {
            AdminFilePermissionCatalog.Upload, AdminFilePermissionCatalog.Manage, AdminFilePermissionCatalog.Host
        });
        AddUser("mixed-role-status", operations,
            new[] { AdminFilePermissionCatalog.Upload, AdminFilePermissionCatalog.Host },
            disabledGranted: new[]
            {
                AdminFilePermissionCatalog.Publish,
                AdminFilePermissionCatalog.Manage
            }, disabledDenied: new[] { AdminFilePermissionCatalog.Upload });
        AddUser("disabled-upload-status", operations, new[] { AdminFilePermissionCatalog.Host },
            disabledGranted: new[] { AdminFilePermissionCatalog.Upload });
        AddUser("disabled-host-status", operations, new[] { AdminFilePermissionCatalog.Upload },
            disabledGranted: new[] { AdminFilePermissionCatalog.Host });
    }

    public Guid ApplicationId { get; }
    public Module FileModule { get; }
    public IReadOnlyList<Guid> OwnerRoles { get; }
    public IReadOnlyList<Guid> DenyUploadRoles { get; }
    public IReadOnlyList<Guid> NoPublishRoles { get; }
    public IReadOnlyList<Guid> NoHostRoles { get; }
    public IReadOnlyList<Guid> ManagerRoles { get; }
    public Mock<IModuleRepository> ModuleRepository { get; }
    public Mock<IOperationRepository> OperationRepository { get; }
    public Mock<IPermissionRepository> PermissionRepository { get; }

    public List<Role> RolesForUser(Guid userId) => _rolesByUser.TryGetValue(userId, out var roles) ? roles : new List<Role>();
    public User CreateUser(string userName) => _usersByName[userName];

    public IReadOnlyList<(Guid UserId, Role Role)> RoleAssignments => _rolesByUser
        .SelectMany(user => user.Value.Select(role => (user.Key, role))).ToList();

    private IReadOnlyList<Guid> AddUser(string userName, IReadOnlyDictionary<string, Operation> operations,
        IEnumerable<string> granted, IEnumerable<string>? denied = null, IEnumerable<string>? disabledGranted = null,
        IEnumerable<string>? disabledDenied = null)
    {
        var roleIds = new List<Guid>();
        var disabledRoleIds = new HashSet<Guid>();
        AddRole(userName + "-allow", granted, denied: null, roleIds, operations);
        if (denied != null)
            AddRole(userName + "-deny", Array.Empty<string>(), denied, roleIds, operations);
        if (disabledGranted != null)
        {
            AddRole(userName + "-disabled", disabledGranted, denied: null, roleIds, operations);
            disabledRoleIds.Add(roleIds[^1]);
        }
        if (disabledDenied != null)
        {
            AddRole(userName + "-disabled-deny", Array.Empty<string>(), disabledDenied, roleIds, operations);
            disabledRoleIds.Add(roleIds[^1]);
        }

        var userId = Guid.NewGuid();
        _rolesByUser[userId] = roleIds.Select(id => new Role(id, id + ",", 1)
        {
            Code = id.ToString("N"),
            Name = userName,
            NormalizedName = userName.ToUpperInvariant(),
            Type = "Test",
            Enabled = !disabledRoleIds.Contains(id)
        }).ToList();
        _usersByName[userName] = new User(userId)
        {
            UserName = userName,
            Nickname = userName,
            Enabled = true
        };
        return roleIds;
    }

    private void AddRole(string name, IEnumerable<string> granted, IEnumerable<string>? denied, List<Guid> roleIds,
        IReadOnlyDictionary<string, Operation> operations)
    {
        var roleId = Guid.NewGuid();
        roleIds.Add(roleId);
        foreach (var code in granted)
            _permissionRecords.Add(new Permission(Guid.NewGuid())
            {
                RoleId = roleId,
                ResourceId = operations[code].Id,
                IsDeny = false
            });
        if (denied == null) return;
        foreach (var code in denied)
            _permissionRecords.Add(new Permission(Guid.NewGuid())
            {
                RoleId = roleId,
                ResourceId = operations[code].Id,
                IsDeny = true
            });
    }
}

internal sealed class PrincipalCurrentTenant : ICurrentTenant
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    public PrincipalCurrentTenant(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Id);
    public string? Id => _httpContextAccessor.HttpContext?.User.FindFirst(BingClaimTypes.TenantId)?.Value;
    public string? Name => _httpContextAccessor.HttpContext?.User.FindFirst(BingClaimTypes.TenantName)?.Value;
    public IDisposable Change(string id, string? name = null) => throw new NotSupportedException();
}
