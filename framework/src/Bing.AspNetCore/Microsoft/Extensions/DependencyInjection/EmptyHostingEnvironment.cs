using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.DependencyInjection
{
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0
    /// <summary>
    /// 空主机环境变量
    /// </summary>
    internal class EmptyHostingEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        /// <summary>
        /// 环境名称
        /// </summary>
        public string EnvironmentName { get; set; }

        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Web根路径，即wwwroot
        /// </summary>
        public string WebRootPath { get; set; }

        /// <summary>
        /// Web根路径文件提供程序
        /// </summary>
        public IFileProvider WebRootFileProvider { get; set; }

        /// <summary>
        /// 根路径
        /// </summary>
        public string ContentRootPath { get; set; }

        /// <summary>
        /// 根路径文件提供程序
        /// </summary>
        public IFileProvider ContentRootFileProvider { get; set; }

    }
#endif
}
