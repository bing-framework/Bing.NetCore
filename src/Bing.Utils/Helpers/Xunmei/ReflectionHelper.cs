using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Class ReflectionHelper.
    /// </summary>
    public class ReflectionHelper
    {
        #region 构造函数 (1) 

        /// <summary>
        /// Prevents a default instance of the <see cref="ReflectionHelper"/> class from being created.
        /// </summary>
        private ReflectionHelper()
        {
        }

        #endregion 构造函数 

        #region 变量
        private static Dictionary<string, Type> dicClassTypes = new Dictionary<string, Type>();
        private static List<Type> cacheInheritTypes = new List<Type>();
        #endregion

        #region 公共方法 (3) 

        /// <summary>
        /// 反射程序集下T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assembly">The DLL path.</param>
        /// <param name="throwEx">if set to <c>true</c> [throw ex].</param>
        /// <returns>``0.</returns>
        public static T Load<T>(Assembly assembly, bool throwEx) where T : class
        {
            try
            {
                Type target = typeof(T);
                foreach (Type type in assembly.GetTypes())
                {
                    if (target.IsInterface)
                    {
                        if (type.GetInterface(target.Name) == null)
                            continue;
                    }
                    else
                    {
                        if (type.FullName != target.FullName && !type.IsAssignableFrom(target))
                            continue;
                    }

                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var obj = ctor.Invoke(null) as T;
                        if (obj != null)
                        {
                            return obj;
                        }
                    }
                }
            }
            catch
            {
                if (throwEx)
                    throw;
            }
            return null;
        }

        /// <summary>
        /// 反射程序集下T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dllPath">The DLL path.</param>
        /// <param name="throwEx">if set to <c>true</c> [throw ex].</param>
        /// <returns>``0.</returns>
        public static T Load<T>(string dllPath, bool throwEx) where T : class
        {
            try
            {
                if (!File.Exists(dllPath))
                    return null;
                Assembly assembly = Assembly.LoadFrom(dllPath);
                return Load<T>(assembly, throwEx);
            }
            catch
            {
                if (throwEx)
                    throw;
            }
            return null;
        }

        /// <summary>
        /// 反射程序集下T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dllPath">The DLL path.</param>
        /// <returns>``0.</returns>
        public static T Load<T>(string dllPath) where T : class
        {
            return Load<T>(dllPath, false);
        }

        /// <summary>
        /// 反射程序集下所有T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dllPath">The DLL path.</param>
        /// <returns>List{``0}.</returns>
        public static List<T> Loads<T>(string dllPath) where T : class
        {
            if (!File.Exists(dllPath))
                return null;

            Assembly assembly = Assembly.LoadFrom(dllPath);
            return Loads<T>(assembly, false);
        }

        /// <summary>
        /// 反射程序集下所有T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static List<T> Loads<T>(Assembly assembly) where T : class
        {
            return Loads<T>(assembly, false);
        }

        /// <summary>
        /// 反射程序集下所有T对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assembly"></param>
        /// <param name="throwEx"></param>
        /// <returns></returns>
        public static List<T> Loads<T>(Assembly assembly, bool throwEx) where T : class
        {
            var list = new List<T>();
            Type target = typeof(T);
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (target.IsInterface)
                    {
                        if (type.GetInterface(target.Name) == null) continue;
                    }
                    else
                    {
                        if (type.FullName != target.FullName && !type.IsAssignableFrom(target) && !target.IsAssignableFrom(type)) continue;
                    }
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var obj = ctor.Invoke(null) as T;
                        if (obj != null)
                        {
                            list.Add(obj);
                        }
                    }
                }
            }
            catch
            {
                if (throwEx)
                    throw;
            }
            return list;
        }

        /// <summary>
        /// 根据类型名称，获取对象类型
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        public static Type GetAssemblyClassType(string fullName)
        {
            if (dicClassTypes.ContainsKey(fullName))
                return dicClassTypes.Where(x => x.Key.Equals(fullName, StringComparison.CurrentCultureIgnoreCase)).Select(x => x.Value).FirstOrDefault();

            var ass = AppDomain.CurrentDomain.GetAssemblies();
            Type type = null;
            foreach (var assem in ass)
            {
                type = assem.GetType(fullName);
                if (type == null) continue;

                break;
            }
            dicClassTypes.Add(fullName, type);
            return type;
        }

        /// <summary>
        /// 获取指定继承接口所有类型列表
        /// </summary>
        /// <returns></returns>
        public static List<Type> GetInheritClassTypes<T>()
        {
            if (cacheInheritTypes.Count > 0)
                return cacheInheritTypes;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            cacheInheritTypes = assemblies.SelectMany(x => x.GetTypes().Where(t => t.GetInterfaces().Contains(typeof(T)))).ToList();
            return cacheInheritTypes;
        }

        /// <summary>
        /// 获取指定实体中指定属性值
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public static string GetDbNotifyValue(object dto, string attributeName)
        {
            if (dto == null)
                return string.Empty;
            var type = dto.GetType();

            foreach (PropertyInfo pi in type.GetProperties())
            {
                if (string.Compare(pi.Name, attributeName, true) != 0)
                {
                    continue;
                }
                try
                {
                    var value = pi.GetValue(dto, null);
                    if (value == null)
                        return string.Empty;

                    return value.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }

            return string.Empty;
        }

        #endregion 公共方法 
    }
}
