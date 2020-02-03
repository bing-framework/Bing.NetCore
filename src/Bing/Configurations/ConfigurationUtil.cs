using System.IO;
using Bing.Extensions;
using Bing.Extensions;
using Microsoft.Extensions.Configuration;

namespace Bing.Configurations
{
    /// <summary>
    /// 配置工具
    /// </summary>
    public static class ConfigurationUtil
    {
        /// <summary>
        /// 构建配置
        /// </summary>
        /// <param name="options">配置构建起选项</param>
        /// <returns></returns>
        public static IConfigurationRoot BuildConfiguration(ConfigurationBuilderOptions options = null)
        {
            options = options ?? new ConfigurationBuilderOptions();
            if (options.BasePath.IsEmpty())
            {
                options.BasePath = Directory.GetCurrentDirectory();
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(options.BasePath)
                .AddJsonFile($"{options.FileName}.json", true, true);

            if (!options.EnvironmentName.IsEmpty())
            {
                builder = builder.AddJsonFile($"{options.FileName}.{options.EnvironmentName}.json", true, true);
            }

            builder = builder.AddEnvironmentVariables();
            if (options.EnvironmentName == "Development")
            {

            }

            return builder.Build();
        }
    }
}
