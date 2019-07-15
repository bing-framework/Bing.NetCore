using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Bing.Finders;
using Microsoft.Extensions.PlatformAbstractions;

namespace Bing.Reflections
{
    /// <summary>
    /// 应用程序目录程序集查找器
    /// </summary>
    public class AppDomainAllAssemblyFinder : FinderBase<Assembly>, IAllAssemblyFinder
    {
        /// <summary>
        /// 跳过的程序集
        /// </summary>
        protected const string SkipAssemblies =
            "^System|^Mscorlib|^msvcr120|^Netstandard|^Microsoft|^Autofac|^AutoMapper|^EntityFramework|^Newtonsoft|^Castle|^NLog|^Pomelo|^AspectCore|^Xunit|^Nito|^Npgsql|^Exceptionless|^MySqlConnector|^Anonymously Hosted|^libuv|^api-ms|^clrcompression|^clretwrc|^clrjit|^coreclr|^dbgshim|^e_sqlite3|^hostfxr|^hostpolicy|^MessagePack|^mscordaccore|^mscordbi|^mscorrc|sni|sos|SOS.NETCore|^sos_amd64|^SQLitePCLRaw|^StackExchange|^Swashbuckle|WindowsBase|ucrtbase|^DotNetCore.CAP|^MongoDB|^Confluent.Kafka|^librdkafka|^EasyCaching|^RabbitMQ|^Consul|^Dapper|^EnyimMemcachedCore|^Pipelines|^DnsClient|^IdentityModel|^zlib|^testhost|^dotMemory|^NSubstitute|^Hangfire";

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected override Assembly[] FindAllItems()
        {
            LoadAssemblies(PlatformServices.Default.Application.ApplicationBasePath);
            return GetAssembliesFromCurrentAppDomain().ToArray();
        }

        /// <summary>
        /// 加载程序集到当前应用程序域
        /// </summary>
        /// <param name="path">目录绝对路径</param>
        protected void LoadAssemblies(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*.dll"))
            {
                if (Match(Path.GetFileName(file)) == false)
                {
                    continue;
                }

                LoadAssemblyToAppDomain(file);
            }
        }

        /// <summary>
        /// 程序集是否匹配
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <returns></returns>
        protected virtual bool Match(string assemblyName)
        {
            if (assemblyName.StartsWith($"{PlatformServices.Default.Application.ApplicationName}.Views"))
            {
                return false;
            }
            if (assemblyName.StartsWith($"{PlatformServices.Default.Application.ApplicationName}.PrecompiledViews"))
            {
                return false;
            }

            return Regex.IsMatch(assemblyName, SkipAssemblies, RegexOptions.IgnoreCase | RegexOptions.Compiled) == false;
        }

        /// <summary>
        /// 将程序集添加到当前应用程序域
        /// </summary>
        /// <param name="file">程序集文件</param>
        private void LoadAssemblyToAppDomain(string file)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);
                AppDomain.CurrentDomain.Load(assemblyName);
            }
            catch (BadImageFormatException)
            {
            }
        }

        /// <summary>
        /// 从当前应用程序域获取程序集列表
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Assembly> GetAssembliesFromCurrentAppDomain()
        {
            var result = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Match(assembly))
                {
                    result.Add(assembly);
                }
            }
            return result.Distinct();
        }

        /// <summary>
        /// 程序集是否匹配
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns></returns>
        private bool Match(Assembly assembly)
        {
            return !Regex.IsMatch(assembly.FullName, SkipAssemblies, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
