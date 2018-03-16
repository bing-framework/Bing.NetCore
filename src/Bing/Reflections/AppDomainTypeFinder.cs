using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Reflections
{
    /// <summary>
    /// 应用程序类型查找器
    /// </summary>
    public class AppDomainTypeFinder : ITypeFinder
    {
        /// <summary>
        /// 跳过的程序集
        /// </summary>
        private const string SKIP_ASSEMBLIES = "^System|^Mscorlib|^Netstandard|^Microsoft|^Autofac|^AutoMapper|^EntityFramework|^Newtonsoft|^Castle|^NLog|^Pomelo|^AspectCore|^Nito|^Npgsql|^Exceptionless|^MySqlConnector|^Anonymously Hosted";

        #region GetAssemblies(获取程序集列表)

        /// <summary>
        /// 获取程序集列表
        /// </summary>
        /// <returns></returns>
        public virtual List<Assembly> GetAssemblies()
        {
            return GetAssembliesFromCurrentAppDomain();
        }

        /// <summary>
        /// 从当前应用程序域获取程序集列表
        /// </summary>
        /// <returns></returns>
        private List<Assembly> GetAssembliesFromCurrentAppDomain()
        {
            var result = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Match(assembly))
                {
                    result.Add(assembly);
                }
            }
            return result.Distinct().ToList();
        }

        /// <summary>
        /// 程序集是否匹配
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private bool Match(Assembly assembly)
        {
            return !Regex.IsMatch(assembly.FullName, SKIP_ASSEMBLIES, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// 加载程序集到当前应用程序域
        /// </summary>
        /// <param name="path">目录绝对路径</param>
        protected void LoadAssemblies(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*.dll"))
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);
                if (Match(assemblyName))
                {
                    AppDomain.CurrentDomain.Load(assemblyName);
                }
            }
        }

        /// <summary>
        /// 程序集是否匹配
        /// </summary>
        /// <param name="assemblyName">程序集名</param>
        /// <returns></returns>
        private bool Match(AssemblyName assemblyName)
        {
            return !Regex.IsMatch(assemblyName.FullName, SKIP_ASSEMBLIES,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        #endregion

        #region Find(查找类型列表)

        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <typeparam name="T">查找类型</typeparam>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        /// <returns></returns>
        public List<Type> Find<T>(List<Assembly> assemblies = null)
        {
            return Find(typeof(T), assemblies);
        }

        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <param name="findType">查找类型</param>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        /// <returns></returns>
        public List<Type> Find(Type findType, List<Assembly> assemblies = null)
        {
            assemblies = assemblies ?? GetAssemblies();
            var result = new List<Type>();
            foreach (var assembly in assemblies)
            {
                result.AddRange(GetTypes(findType, assembly));
            }
            return result.Distinct().ToList();
        }

        /// <summary>
        /// 获取类型列表
        /// </summary>
        /// <param name="findType">查找类型</param>
        /// <param name="assembly">在指定的程序集列表中查找</param>
        /// <returns></returns>
        private List<Type> GetTypes(Type findType, Assembly assembly)
        {
            var result = new List<Type>();
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                return result;
            }
            if (types == null)
            {
                return result;
            }
            foreach (var type in types)
            {
                AddType(result, findType, type);
            }
            return result;
        }

        /// <summary>
        /// 添加类型
        /// </summary>
        /// <param name="result">类型列表</param>
        /// <param name="findType">查找类型</param>
        /// <param name="type">目标类型</param>
        private void AddType(List<Type> result, Type findType, Type type)
        {
            if (type.IsInterface || type.IsAbstract)
            {
                return;
            }
            if (findType.IsAssignableFrom(type) == false && MatchGeneric(findType, type) == false)
            {
                return;
            }
            result.Add(type);
        }

        /// <summary>
        /// 泛型匹配
        /// </summary>
        /// <param name="findType">查找类型</param>
        /// <param name="type">目标类型</param>
        /// <returns></returns>
        private bool MatchGeneric(Type findType, Type type)
        {
            if (findType.IsGenericTypeDefinition == false)
            {
                return false;
            }
            var definition = findType.GetGenericTypeDefinition();
            foreach (var implementedInterface in type.FindInterfaces((filter, criteria) => true, null))
            {
                if (implementedInterface.IsGenericType == false)
                {
                    continue;
                }
                return definition.IsAssignableFrom(implementedInterface.GetGenericTypeDefinition());
            }
            return false;
        }

        #endregion

    }
}
