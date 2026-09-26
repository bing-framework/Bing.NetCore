using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Bing.DynamicApi.Contracts;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 验证动态 API 与 ASP.NET Core MVC 的集成行为。
/// </summary>
public class BingDynamicApiIntegrationTest : BingAspNetCoreTestBase
{
    /// <summary>
    /// 获取 MVC 控制器发现器使用的应用部件管理器。
    /// </summary>
    private readonly ApplicationPartManager _partManager;

    /// <summary>
    /// 获取 MVC 生成的操作描述集合。
    /// </summary>
    private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;

    /// <summary>
    /// 初始化 BingDynamicApiIntegrationTest 类的新实例。
    /// </summary>
    /// <param name="client">用于发送集成测试请求的客户端。</param>
    /// <param name="partManager">MVC 应用部件管理器。</param>
    /// <param name="actionDescriptorProvider">MVC 操作描述集合提供程序。</param>
    public BingDynamicApiIntegrationTest(HttpClient client, ApplicationPartManager partManager,
        IActionDescriptorCollectionProvider actionDescriptorProvider)
    {
        InitClient(client);
        _partManager = partManager;
        _actionDescriptorProvider = actionDescriptorProvider;
    }

    /// <summary>
    /// 验证已启用的动态应用服务会加入 MVC 控制器集合，禁用服务不会加入。
    /// </summary>
    [Fact]
    public void DynamicService_IsInMvcControllerFeature()
    {
        var feature = new ControllerFeature();
        _partManager.PopulateFeature(feature);
        feature.Controllers.Select(type => type.AsType().FullName).ShouldContain(typeof(DynamicApiTestAppService).FullName);
        feature.Controllers.Select(type => type.AsType().FullName).ShouldNotContain(typeof(DisabledDynamicApiTestAppService).FullName);
    }

    /// <summary>
    /// 验证动态服务操作描述及其路由和路径参数绑定信息。
    /// </summary>
    [Fact]
    public void DynamicService_ActionsAreInMvcActionDescriptors()
    {
        var descriptors = _actionDescriptorProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(IDynamicApiTestAppService))
            .ToArray();
        var actions = descriptors
            .Select(action => $"{action.MethodInfo.Name}:{action.AttributeRouteInfo?.Template}")
            .ToArray();

        actions.ShouldContain($"{nameof(IDynamicApiTestAppService.GetAsync)}:api/app/v1/dynamic-api-test/{{id}}");
        actions.ShouldNotContain(action => action.StartsWith($"{nameof(IDynamicApiTestAppService.HiddenAsync)}:", StringComparison.Ordinal));
        var get = descriptors.Single(action => action.MethodInfo.Name == nameof(IDynamicApiTestAppService.GetAsync));
        get.MethodInfo.DeclaringType.ShouldBe(typeof(IDynamicApiTestAppService));
        get.Parameters.Select(parameter => parameter.Name).ShouldContain("id");
        var id = get.Parameters.Single(parameter => parameter.Name == "id");
        id.BindingInfo.BinderModelName.ShouldBe("id");
        id.BindingInfo.BindingSource.ShouldBe(Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Path);
        id.BindingInfo.BinderType?.FullName.ShouldBe("Bing.AspNetCore.Mvc.DynamicApi.BingDynamicApiRouteValueModelBinder");
    }

    /// <summary>
    /// 验证路径标识绑定及动态 API 返回的原始 JSON 响应。
    /// </summary>
    [Fact]
    public async Task GetAsync_UsesRouteIdAndReturnsUnwrappedJson()
    {
        var id = Guid.NewGuid();
        using var response = await Client.GetAsync($"/api/app/v1/dynamic-api-test/{id:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe($"item:{id:D}:True");
    }

    /// <summary>
    /// 验证 object 类型标识能够从路由值绑定。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_BindsObjectIdentifierFromRoute()
    {
        using var response = await Client.GetAsync("/api/app/v1/dynamic-api-test/object/object-id");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("object:object-id");
    }

    /// <summary>
    /// 验证动态控制器通过依赖注入获取作用域服务代理。
    /// </summary>
    [Fact]
    public async Task DynamicController_UsesTheScopedServiceInterfaceProxy()
    {
        using var response = await Client.GetAsync("/api/app/v1/dynamic-api-test/activation");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("resolved-through-di-proxy");
    }

    /// <summary>
    /// 验证动态控制器支持仅注册接口代理的应用服务。
    /// </summary>
    [Fact]
    public async Task DynamicController_UsesAnInterfaceOnlyServiceProxy()
    {
        using var host = CreateDynamicApiServiceHost(
            typeof(InterfaceProxyDynamicApiAppService),
            typeof(IInterfaceProxyDynamicApiAppService),
            typeof(InterfaceOnlyDynamicApiProxy));
        await host.StartAsync();
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/api/app/v1/interface-proxy-dynamic-api/status");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Response body: {body}");
        body.ShouldBe("interface-only-proxy");
    }

    /// <summary>
    /// 验证显式控制器实现应用服务接口时仍保留原有路由。
    /// </summary>
    [Fact]
    public async Task ExplicitControllerThatImplementsAppService_PreservesItsOriginalRoute()
    {
        using var host = CreateDynamicApiServiceHost(
            typeof(ExplicitDynamicApiController),
            typeof(IExplicitDynamicApiControllerService),
            typeof(ExplicitDynamicApiController));
        await host.StartAsync();
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/api/explicit-app-service/legacy");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("explicit-controller-route");
    }

    /// <summary>
    /// 验证启动时会拒绝重复的查询参数绑定名称。
    /// </summary>
    [Fact]
    public async Task Startup_RejectsDuplicateQueryBindingNames()
    {
        using var host = CreateDynamicApiServiceHost(
            typeof(DuplicateQueryBindingAppService),
            typeof(IDuplicateQueryBindingAppService),
            typeof(DuplicateQueryBindingAppService));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => host.StartAsync());

        exception.Message.ShouldContain("绑定来源 Query");
        exception.Message.ShouldContain("重复参数名称 'q'");
    }

    /// <summary>
    /// 验证启动时会拒绝重复的表单参数绑定名称。
    /// </summary>
    [Fact]
    public async Task Startup_RejectsDuplicateFormBindingNames()
    {
        using var host = CreateDynamicApiServiceHost(
            typeof(DuplicateFormBindingAppService),
            typeof(IDuplicateFormBindingAppService),
            typeof(DuplicateFormBindingAppService));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => host.StartAsync());

        exception.Message.ShouldContain("绑定来源 Form");
        exception.Message.ShouldContain("重复参数名称 'tag'");
    }

    /// <summary>
    /// 验证不同绑定来源可以使用相同的参数名称。
    /// </summary>
    [Fact]
    public async Task SameBindingNameFromDifferentSources_IsAllowed()
    {
        using var host = CreateDynamicApiServiceHost(
            typeof(DistinctSourceBindingAppService),
            typeof(IDistinctSourceBindingAppService),
            typeof(DistinctSourceBindingAppService));
        await host.StartAsync();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/app/v1/distinct-source-binding/distinct?value=query-value");
        request.Headers.Add("value", "header-value");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("query-value:header-value");
    }

    /// <summary>
    /// 验证服务接口参数特性控制模型绑定及 API 描述信息。
    /// </summary>
    [Fact]
    public async Task InterfaceParameterAttributes_ControlBindingAndMetadata()
    {
        using var response = await Client.GetAsync("/api/app/v1/dynamic-api-test/lookup?q=interface-value");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("interface-value");

        var definition = await GetResponseAsObjectAsync<BingDynamicApiDefinition>("/api/dynamic-api/v1/definition");
        var lookup = definition.Services.Single(item => item.ServiceTypeName == typeof(IDynamicApiTestAppService).FullName)
            .Methods.Single(item => item.MethodName == nameof(IDynamicApiTestAppService.LookupAsync));
        lookup.Parameters.Single().Name.ShouldBe("q");
        lookup.Parameters.Single().BindingSource.ShouldBe("Query");
    }

    /// <summary>
    /// 验证服务契约上的授权特性会应用到操作，描述端点不默认匿名。
    /// </summary>
    [Fact]
    public async Task AuthorizationFromServiceContract_IsAppliedAndDefinitionIsNotAnonymous()
    {
        using var response = await Client.GetAsync("/api/app/v1/dynamic-api-test/protected");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var protectedAction = _actionDescriptorProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Single(action => action.MethodInfo.Name == nameof(IDynamicApiTestAppService.GetProtectedAsync));
        protectedAction.EndpointMetadata.OfType<IAuthorizeData>()
            .ShouldContain(attribute => attribute.Policy == "MyClaimTestPolicy");

        var definitionAction = _actionDescriptorProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Single(action => action.AttributeRouteInfo?.Template == "api/dynamic-api/{version}/definition");
        definitionAction.EndpointMetadata.OfType<IAllowAnonymous>().ShouldBeEmpty();
    }

    /// <summary>
    /// 验证复杂请求体绑定及原始对象响应。
    /// </summary>
    [Fact]
    public async Task CreateAsync_BindsComplexBodyAndReturnsOriginalObject()
    {
        using var response = await Client.PostAsync("/api/app/v1/dynamic-api-test",
            new StringContent("{\"name\":\"sample\",\"quantity\":7}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonHelper.ToObject<DynamicApiTestResponse>(json);
        result.Name.ShouldBe("sample");
        result.Quantity.ShouldBe(7);
        json.ShouldNotContain("\"Code\"");
    }

    /// <summary>
    /// 验证显式 HTTP 特性会覆盖默认方法路由。
    /// </summary>
    [Fact]
    public async Task ExplicitHttpAttribute_OverridesDefaultMethodRoute()
    {
        var id = Guid.NewGuid();
        using var response = await Client.PatchAsync(
            $"/api/app/v1/dynamic-api-test/rename/{id:D}?name=renamed", new StringContent(string.Empty));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe($"{id:D}:renamed");
    }

    /// <summary>
    /// 验证文件上传使用 multipart 绑定，下载返回远程流内容。
    /// </summary>
    [Fact]
    public async Task UploadAndDownload_UseMultipartAndRemoteStreamContent()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("dynamic-upload")), "file", "upload.txt");
        using var upload = await Client.PostAsync("/api/app/v1/dynamic-api-test/upload", form);

        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await upload.Content.ReadAsStringAsync()).ShouldBe("dynamic-upload");

        using var download = await Client.GetAsync("/api/app/v1/dynamic-api-test/download");
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).ShouldBe("dynamic-download");
        download.Content.Headers.ContentDisposition?.FileName.ShouldBe("download.txt");
    }

    /// <summary>
    /// 验证 API 描述反映 MVC 最终路由、参数绑定来源及流响应信息。
    /// </summary>
    [Fact]
    public async Task DefinitionEndpoint_DescribesFinalMvcRoutesAndBindings()
    {
        var definition = await GetResponseAsObjectAsync<BingDynamicApiDefinition>("/api/dynamic-api/v1/definition");
        definition.ApiVersion.ShouldBe("v1");
        var service = definition.Services.Single(item => item.ServiceTypeName == typeof(IDynamicApiTestAppService).FullName);
        service.ServiceName.ShouldBe("dynamic-api-test");

        var get = service.Methods.Single(item => item.MethodName == nameof(IDynamicApiTestAppService.GetAsync));
        get.HttpMethod.ShouldBe("GET");
        get.RouteTemplate.ShouldBe("api/app/v1/dynamic-api-test/{id}");
        get.Parameters.Single(parameter => !parameter.IsCancellationToken).BindingSource.ShouldBe("Path");
        get.Parameters.Single(parameter => parameter.IsCancellationToken).BindingSource.ShouldBe("Special");
        service.Methods.ShouldNotContain(item => item.MethodName == nameof(IDynamicApiTestAppService.HiddenAsync));

        var create = service.Methods.Single(item => item.MethodName == nameof(IDynamicApiTestAppService.CreateAsync));
        create.HttpMethod.ShouldBe("POST");
        create.Parameters.Single().BindingSource.ShouldBe("Body");

        var upload = service.Methods.Single(item => item.MethodName == nameof(IDynamicApiTestAppService.UploadAsync));
        upload.Parameters.Single().BindingSource.ShouldBe("FormFile");
        var download = service.Methods.Single(item => item.MethodName == nameof(IDynamicApiTestAppService.DownloadAsync));
        download.HttpMethod.ShouldBe("GET");
        download.IsStreamResult.ShouldBeTrue();
    }

    /// <summary>
    /// 验证未注册的 API 版本返回未找到状态。
    /// </summary>
    [Fact]
    public async Task UnknownVersion_ReturnsNotFound()
    {
        using var response = await Client.GetAsync("/api/dynamic-api/v99/definition");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// 验证动态路由与显式控制器路由冲突时启动失败。
    /// </summary>
    [Fact]
    public async Task Startup_RejectsDynamicRouteCollisionWithExplicitController()
    {
        using var host = CreateCollisionHost(typeof(ExplicitCollisionController));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => host.StartAsync());

        exception.Message.ShouldContain("动态 API 路由冲突");
    }

    /// <summary>
    /// 验证显式控制器与 API 描述端点冲突时启动失败。
    /// </summary>
    [Fact]
    public async Task Startup_RejectsExplicitControllerCollisionWithDefinitionEndpoint()
    {
        using var host = CreateCollisionHost(typeof(DefinitionCollisionController));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => host.StartAsync());

        exception.Message.ShouldContain("动态 API 路由冲突");
        exception.Message.ShouldContain("api/dynamic-api/v1/definition");
    }

    /// <summary>
    /// 创建包含指定显式控制器的冲突测试宿主。
    /// </summary>
    /// <param name="controllerType">加入宿主的显式控制器类型。</param>
    /// <returns>用于验证路由冲突的测试宿主。</returns>
    private static IHost CreateCollisionHost(Type controllerType)
    {
        return new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddScoped<IDynamicApiTestAppService, DynamicApiTestAppService>();
                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                        manager.FeatureProviders.Add(new ExplicitCollisionControllerFeatureProvider(controllerType)));
                    services.AddBingDynamicApi(typeof(DynamicApiTestAppService).Assembly, options =>
                        options.TypePredicate = type => type == typeof(DynamicApiTestAppService));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();
    }

    /// <summary>
    /// 创建仅发现指定应用服务的动态 API 测试宿主。
    /// </summary>
    /// <param name="implementationType">动态 API 服务实现类型。</param>
    /// <param name="serviceType">服务注册所用的契约类型。</param>
    /// <param name="injectedType">注入到服务契约的实现类型。</param>
    /// <returns>已配置动态 API 服务的测试宿主。</returns>
    private static IHost CreateDynamicApiServiceHost(Type implementationType, Type serviceType, Type injectedType)
    {
        return new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddScoped(serviceType, injectedType);
                    services.AddBingDynamicApi(implementationType.Assembly, options =>
                        options.TypePredicate = type => type == implementationType);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();
    }

    /// <summary>
    /// 将指定控制器加入 MVC 控制器发现结果。
    /// </summary>
    private sealed class ExplicitCollisionControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        /// <summary>
        /// 获取需要加入控制器发现结果的类型。
        /// </summary>
        private readonly Type _controllerType;

        /// <summary>
        /// 初始化 ExplicitCollisionControllerFeatureProvider 类的新实例。
        /// </summary>
        /// <param name="controllerType">需要加入发现结果的控制器类型。</param>
        public ExplicitCollisionControllerFeatureProvider(Type controllerType) => _controllerType = controllerType;

        /// <inheritdoc />
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            var controller = _controllerType.GetTypeInfo();
            if (!feature.Controllers.Contains(controller))
                feature.Controllers.Add(controller);
        }
    }

    /// <summary>
    /// 提供与动态 API 路由冲突的显式控制器。
    /// </summary>
    [ApiController]
    [Route("api/app/v1/dynamic-api-test")]
    private sealed class ExplicitCollisionController : ControllerBase
    {
        /// <summary>
        /// 返回请求中的对象标识。
        /// </summary>
        /// <param name="id">请求路径中的对象标识。</param>
        /// <returns>包含对象标识文本的操作结果。</returns>
        [HttpGet("{id}")]
        public ActionResult<string> Get(Guid id) => id.ToString();
    }

    /// <summary>
    /// 提供与动态 API 描述端点路由冲突的显式控制器。
    /// </summary>
    [ApiController]
    [Route("api/dynamic-api/v1/definition")]
    private sealed class DefinitionCollisionController : ControllerBase
    {
        /// <summary>
        /// 返回用于验证路由冲突的固定文本。
        /// </summary>
        /// <returns>包含冲突标记文本的操作结果。</returns>
        [HttpGet]
        public ActionResult<string> Get() => "collision";
    }
}
