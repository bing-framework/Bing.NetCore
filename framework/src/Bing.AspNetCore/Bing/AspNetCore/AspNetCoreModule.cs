using System.ComponentModel;
using Bing.AspNetCore.Security.Claims;
using Bing.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore;

/// <summary>
/// AspNetCore模块
/// </summary>
[Description("AspNetCore模块")]
public class AspNetCoreModule : Bing.Core.Modularity.BingModule
{
    /// <inheritdoc />
    public override Bing.Core.Modularity.ModuleLevel Level => Bing.Core.Modularity.ModuleLevel.Core;

    /// <inheritdoc />
    public override int Order => 2;

    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        // 注册 HttpContextAccessor，提供 HttpContext 访问
        services.AddHttpContextAccessor();

        // 注入当前用户信息，替换 Thread.CurrentPrincipal
        services.AddTransient<System.Security.Principal.IPrincipal>(provider =>
        {
            var accessor = provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            return accessor?.HttpContext?.User;
        });

        // 注入用户会话管理
        services.AddSingleton<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();

        // 注册编码支持
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        return services;
    }

    /// <inheritdoc />
    public override void UseModule(IServiceProvider provider) => Enabled = true;
}
