using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Bing.Utils.Configs
{
    /// <summary>
    /// 配置文件 操作辅助类
    /// </summary>
    public static class ConfigUtil
    {
        #region GetJsonConfig(获取Json配置文件)

        /// <summary>
        /// 获取Json配置文件
        /// </summary>
        /// <param name="configFileName">配置文件名。默认：appsettings.json</param>
        /// <param name="basePath">基路径</param>
        /// <returns></returns>
        public static IConfigurationRoot GetJsonConfig(string configFileName = "appsettings.json", string basePath = "")
        {
            basePath = string.IsNullOrWhiteSpace(basePath)
                ? Directory.GetCurrentDirectory()
                : Path.Combine(Directory.GetCurrentDirectory(), basePath);

            var configuration = new ConfigurationBuilder().SetBasePath(basePath)
                .AddJsonFile(configFileName, false, true)
                .Build();

            return configuration;
        }

        #endregion

        #region GetXmlConfig(获取Xml配置文件)

        /// <summary>
        /// 获取Xml配置文件
        /// </summary>
        /// <param name="configFileName">配置文件名。默认：appsettings.xml</param>
        /// <param name="basePath">基路径</param>
        /// <returns></returns>
        public static IConfigurationRoot GetXmlConfig(string configFileName = "appsettings.xml", string basePath = "")
        {
            basePath = string.IsNullOrWhiteSpace(basePath)
                ? Directory.GetCurrentDirectory()
                : Path.Combine(Directory.GetCurrentDirectory(), basePath);

            var configuration=new ConfigurationBuilder().AddXmlFile(config =>
            {
                config.Path = configFileName;
                config.FileProvider=new PhysicalFileProvider(basePath);
            });

            return configuration.Build();
        }

        #endregion
    }
}
