using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Bing.Utils.Helpers;
using Microsoft.Extensions.PlatformAbstractions;

namespace Bing.Reflections
{
    ///// <summary>
    ///// 应用程序类型查找器
    ///// </summary>
    //public class AppDomainTypeFinder : ITypeFinder
    //{
    //    /// <summary>
    //    /// 跳过的程序集
    //    /// </summary>
    //    private const string SkipAssemblies = "^System|^Mscorlib|^msvcr120|^Netstandard|^Microsoft|^Autofac|^AutoMapper|^EntityFramework|^Newtonsoft|^Castle|^NLog|^Pomelo|^AspectCore|^Xunit|^Nito|^Npgsql|^Exceptionless|^MySqlConnector|^Anonymously Hosted|^libuv|^api-ms|^clrcompression|^clretwrc|^clrjit|^coreclr|^dbgshim|^e_sqlite3|^hostfxr|^hostpolicy|^MessagePack|^mscordaccore|^mscordbi|^mscorrc|sni|sos|SOS.NETCore|^sos_amd64|^SQLitePCLRaw|^StackExchange|^Swashbuckle|WindowsBase|ucrtbase|^DotNetCore.CAP|^MongoDB|^Confluent.Kafka|^librdkafka|^EasyCaching|^RabbitMQ|^Consul|^Dapper|^EnyimMemcachedCore|^Pipelines|^DnsClient|^IdentityModel|^zlib";

    //    #region GetAssemblies(获取程序集列表)

    //    /// <summary>
    //    /// 获取程序集列表
    //    /// </summary>
    //    /// <returns></returns>
    //    public virtual List<Assembly> GetAssemblies()
    //    {
    //        LoadAssemblies(PlatformServices.Default.Application.ApplicationBasePath);
    //        return GetAssembliesFromCurrentAppDomain();
    //    }

    //    /// <summary>
    //    /// 加载程序集到当前应用程序域
    //    /// </summary>
    //    /// <param name="path">目录绝对路径</param>
    //    protected void LoadAssemblies(string path)
    //    {
    //        foreach (var file in Directory.GetFiles(path, "*.dll"))
    //        {
    //            if (Match(Path.GetFileName(file)) == false)
    //            {
    //                continue;
    //            }

    //            LoadAssemblyToAppDomain(file);
    //        }
    //    }

    //    /// <summary>
    //    /// 程序集是否匹配
    //    /// </summary>
    //    /// <param name="assemblyName">程序集名称</param>
    //    /// <returns></returns>
    //    protected virtual bool Match(string assemblyName)
    //    {
    //        if (assemblyName.StartsWith($"{PlatformServices.Default.Application.ApplicationName}.Views"))
    //        {
    //            return false;
    //        }
    //        if (assemblyName.StartsWith($"{PlatformServices.Default.Application.ApplicationName}.PrecompiledViews"))
    //        {
    //            return false;
    //        }

    //        return Regex.IsMatch(assemblyName, SkipAssemblies, RegexOptions.IgnoreCase | RegexOptions.Compiled) == false;
    //    }

    //    /// <summary>
    //    /// 将程序集添加到当前应用程序域
    //    /// </summary>
    //    /// <param name="file">程序集文件</param>
    //    private void LoadAssemblyToAppDomain(string file)
    //    {
    //        try
    //        {
    //            var assemblyName = AssemblyName.GetAssemblyName(file);
    //            AppDomain.CurrentDomain.Load(assemblyName);
    //        }
    //        catch (BadImageFormatException)
    //        {
    //        }
    //    }

    //    /// <summary>
    //    /// 从当前应用程序域获取程序集列表
    //    /// </summary>
    //    /// <returns></returns>
    //    private List<Assembly> GetAssembliesFromCurrentAppDomain()
    //    {
    //        var result = new List<Assembly>();
    //        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    //        {
    //            if (Match(assembly))
    //            {
    //                result.Add(assembly);
    //            }
    //        }
    //        return result.Distinct().ToList();
    //    }

    //    /// <summary>
    //    /// 程序集是否匹配
    //    /// </summary>
    //    /// <param name="assembly">程序集</param>
    //    /// <returns></returns>
    //    private bool Match(Assembly assembly)
    //    {
    //        return !Regex.IsMatch(assembly.FullName, SkipAssemblies, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    //    }

    //    #endregion

    //    #region Find(查找类型列表)

    //    /// <summary>
    //    /// 查找类型列表
    //    /// </summary>
    //    /// <typeparam name="T">查找类型</typeparam>
    //    /// <param name="assemblies">在指定的程序集列表中查找</param>
    //    /// <returns></returns>
    //    public List<Type> Find<T>(List<Assembly> assemblies = null)
    //    {
    //        return Find(typeof(T), assemblies);
    //    }

    //    /// <summary>
    //    /// 查找类型列表
    //    /// </summary>
    //    /// <param name="findType">查找类型</param>
    //    /// <param name="assemblies">在指定的程序集列表中查找</param>
    //    /// <returns></returns>
    //    public List<Type> Find(Type findType, List<Assembly> assemblies = null)
    //    {
    //        assemblies = assemblies ?? GetAssemblies();
    //        return Reflection.FindTypes(findType, assemblies.ToArray());
    //    }        

    //    #endregion
    //}
}
