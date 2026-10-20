using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.DependencyInjection;
#if NETCOREAPP3_0_OR_GREATER
/// <summary>
/// 空主机环境变量
/// </summary>
internal class EmptyHostingEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    /// <inheritdoc />
    public string EnvironmentName { get; set; }

    /// <inheritdoc />
    public string ApplicationName { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// Web根路径，即wwwroot
    /// </remarks>
    public string WebRootPath { get; set; }

    /// <inheritdoc />
    public IFileProvider WebRootFileProvider { get; set; }

    /// <inheritdoc />
    public string ContentRootPath { get; set; }

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; }

}
#endif
