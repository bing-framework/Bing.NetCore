using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Bing.Utils.Extensions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 反射 操作
    /// </summary>
    public static class Reflection
    {
        #region GetDescription(获取类型描述)
        /// <summary>
        /// 获取类型描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public static string GetDescription<T>()
        {
            return GetDescription(Common.GetType<T>());
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="memberName">成员名称</param>
        /// <returns></returns>
        public static string GetDescription<T>(string memberName)
        {
            return GetDescription(Common.GetType<T>(), memberName);
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="memberName">成员名称</param>
        /// <returns></returns>
        public static string GetDescription(Type type, string memberName)
        {
            if (type == null)
            {
                return string.Empty;
            }
            return memberName.IsEmpty()
                ? string.Empty
                : GetDescription(type.GetTypeInfo().GetMember(memberName).FirstOrDefault());
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static string GetDescription(MemberInfo member)
        {
            if (member == null)
            {
                return string.Empty;
            }
            var attribute = member.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute == null ? member.Name : attribute.Description;
        }
        #endregion

        #region GetDisplayName(获取类型显示名称)
        /// <summary>
        /// 获取类型显示名称，使用<see cref="DisplayNameAttribute"/>设置显示名称
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public static string GetDisplayName<T>()
        {
            return GetDisplayName(Common.GetType<T>());
        }

        /// <summary>
        /// 获取类型显示名称，使用<see cref="DisplayNameAttribute"/>设置显示名称
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        private static string GetDisplayName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }
            var attribute = type.GetCustomAttribute(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
            return attribute != null ? attribute.DisplayName : string.Empty;
        }

        /// <summary>
        /// 获取类型成员显示名称，，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        private static string GetDisplayName(MemberInfo member)
        {
            if (member == null)
            {
                return string.Empty;
            }
            var displayNameAttribute = member.GetCustomAttribute(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
            if (displayNameAttribute != null)
            {
                return displayNameAttribute.DisplayName;
            }
            var displayAttribute = member.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;
            if (displayAttribute == null)
            {
                return string.Empty;
            }
            return displayAttribute.Description;
        }

        #endregion

        #region GetDisplayNameOrDescription(获取显示名称或类型描述)
        /// <summary>
        /// 获取类型显示名称或描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>设置显示名称
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public static string GetDisplayNameOrDescription<T>()
        {
            var type = Common.GetType<T>();
            var result = GetDisplayName(type);
            if (result.IsEmpty())
            {
                result = GetDescription(type);
            }
            return result;
        }

        /// <summary>
        /// 获取类型显示名称或成员描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static string GetDisplayNameOrDescription(MemberInfo member)
        {
            var result = GetDisplayName(member);
            if (!result.IsEmpty())
            {
                return result;
            }
            return GetDescription(member);
        }
        #endregion

        #region GetTypesByInterface(获取实现了接口的所有具体类型)
        /// <summary>
        /// 获取实现了接口的所有具体类型
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <param name="assembly">在该程序集中查找</param>
        /// <returns></returns>
        public static List<TInterface> GetTypesByInterface<TInterface>(Assembly assembly)
        {
            var typeInterface = typeof(TInterface);
            return
                assembly.GetTypes()
                    .Where(
                        t =>
                            typeInterface.GetTypeInfo().IsAssignableFrom(t) && t != typeInterface &&
                            t.GetTypeInfo().IsAbstract == false)
                    .Select(t => CreateInstance<TInterface>(t))
                    .ToList();
        }
        #endregion

        #region CreateInstance(动态创建实例)
        /// <summary>
        /// 动态创建实例
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="parameters">传递给构造函数的参数</param>
        /// <returns></returns>
        public static T CreateInstance<T>(Type type, params object[] parameters)
        {
            return Utils.Helpers.Conv.To<T>(Activator.CreateInstance(type, parameters));
        }

        /// <summary>
        /// 动态创建实例
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="className">类名，包括命名空间,如果类型不处于当前执行程序集中，需要包含程序集名，范例：Test.Core.Test2,Test.Core</param>
        /// <param name="parameters">传递给构造函数的参数</param>
        /// <returns></returns>
        public static T CreateInstance<T>(string className, params object[] parameters)
        {
            Type type = Type.GetType(className) ?? Assembly.GetCallingAssembly().GetType(className);
            return CreateInstance<T>(type, parameters);
        }
        #endregion

        #region GetAssembly(获取程序集)
        /// <summary>
        /// 获取程序集
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <returns></returns>
        public static Assembly GetAssembly(string assemblyName)
        {
            return Assembly.Load(new AssemblyName(assemblyName));
        }
        #endregion

        #region GetAssemblies(从目录获取所有程序集)
        /// <summary>
        /// 从目录获取所有程序集
        /// </summary>
        /// <param name="directoryPath">目录绝对路径</param>
        /// <returns></returns>
        public static List<Assembly> GetAssemblies(string directoryPath)
        {
            return
                Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .ToList()
                    .Where(t => t.EndsWith(".exe") || t.EndsWith(".dll"))
                    .Select(path => Assembly.Load(new AssemblyName(path)))
                    .ToList();
        }
        #endregion

        #region GetAttribute(获取特性信息)

        /// <summary>
        /// 获取特性信息
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        /// <returns></returns>
        public static TAttribute GetAttribute<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute
        {
            return (TAttribute)memberInfo.GetCustomAttributes(typeof(TAttribute), false).FirstOrDefault();
        }

        #endregion

        #region GetAttributes(获取特性信息数据)

        /// <summary>
        /// 获取特性信息数组
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        /// <returns></returns>
        public static TAttribute[] GetAttributes<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute
        {
            return Array.ConvertAll(memberInfo.GetCustomAttributes(typeof(TAttribute), false), x => (TAttribute)x);
        }

        #endregion

        #region IsBool(是否布尔类型)
        /// <summary>
        /// 是否布尔类型
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static bool IsBool(MemberInfo member)
        {
            if (member == null)
            {
                return false;
            }
            switch (member.MemberType)
            {
                case MemberTypes.TypeInfo:
                    return member.ToString() == "System.Boolean";
                case MemberTypes.Property:
                    return IsBool((PropertyInfo)member);
            }
            return false;
        }

        /// <summary>
        /// 是否布尔类型
        /// </summary>
        /// <param name="property">属性</param>
        /// <returns></returns>
        public static bool IsBool(PropertyInfo property)
        {
            return property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?);
        }
        #endregion

        #region IsEnum(是否枚举类型)
        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static bool IsEnum(MemberInfo member)
        {
            if (member == null)
            {
                return false;
            }
            switch (member.MemberType)
            {
                case MemberTypes.TypeInfo:
                    return ((TypeInfo)member).IsEnum;
                case MemberTypes.Property:
                    return IsEnum((PropertyInfo)member);
            }
            return false;
        }

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="property">属性</param>
        /// <returns></returns>
        public static bool IsEnum(PropertyInfo property)
        {
            if (property.PropertyType.GetTypeInfo().IsEnum)
            {
                return true;
            }
            var value = Nullable.GetUnderlyingType(property.PropertyType);
            if (value == null)
            {
                return false;
            }
            return value.GetTypeInfo().IsEnum;
        }
        #endregion

        #region IsDate(是否日期类型)
        /// <summary>
        /// 是否日期类型
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static bool IsDate(MemberInfo member)
        {
            if (member == null)
            {
                return false;
            }
            switch (member.MemberType)
            {
                case MemberTypes.TypeInfo:
                    return member.ToString() == "System.DateTime";
                case MemberTypes.Property:
                    return IsDate((PropertyInfo)member);
            }
            return false;
        }

        /// <summary>
        /// 是否日期类型
        /// </summary>
        /// <param name="property">属性</param>
        /// <returns></returns>
        public static bool IsDate(PropertyInfo property)
        {
            if (property.PropertyType == typeof(DateTime))
            {
                return true;
            }
            if (property.PropertyType == typeof(DateTime?))
            {
                return true;
            }
            return false;
        }
        #endregion

        #region IsInt(是否整型)
        /// <summary>
        /// 是否整型
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static bool IsInt(MemberInfo member)
        {
            if (member == null)
            {
                return false;
            }
            switch (member.MemberType)
            {
                case MemberTypes.TypeInfo:
                    return member.ToString() == "System.Int32" || member.ToString() == "System.Int16" ||
                           member.ToString() == "System.Int64";
                case MemberTypes.Property:
                    return IsInt((PropertyInfo)member);
            }
            return false;
        }

        /// <summary>
        /// 是否整型
        /// </summary>
        /// <param name="property">成员</param>
        /// <returns></returns>
        public static bool IsInt(PropertyInfo property)
        {
            if (property.PropertyType == typeof(int))
            {
                return true;
            }
            if (property.PropertyType == typeof(int?))
            {
                return true;
            }
            if (property.PropertyType == typeof(short))
            {
                return true;
            }
            if (property.PropertyType == typeof(short?))
            {
                return true;
            }
            if (property.PropertyType == typeof(long))
            {
                return true;
            }
            if (property.PropertyType == typeof(long?))
            {
                return true;
            }
            return false;
        }
        #endregion

        #region IsNumber(是否数值类型)
        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static bool IsNumber(MemberInfo member)
        {
            if (member == null)
            {
                return false;
            }

            if (IsInt(member))
            {
                return true;
            }
            switch (member.MemberType)
            {
                case MemberTypes.TypeInfo:
                    return member.ToString() == "System.Double" || member.ToString() == "System.Decimal" ||
                           member.ToString() == "System.Single";
                case MemberTypes.Property:
                    return IsNumber((PropertyInfo)member);
            }
            return false;
        }

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="property">属性</param>
        /// <returns></returns>
        public static bool IsNumber(PropertyInfo property)
        {
            if (property.PropertyType == typeof(double))
            {
                return true;
            }
            if (property.PropertyType == typeof(double?))
            {
                return true;
            }
            if (property.PropertyType == typeof(decimal))
            {
                return true;
            }
            if (property.PropertyType == typeof(decimal?))
            {
                return true;
            }
            if (property.PropertyType == typeof(float))
            {
                return true;
            }
            if (property.PropertyType == typeof(float?))
            {
                return true;
            }
            return false;
        }
        #endregion

        #region IsGenericCollection(是否泛型集合)
        /// <summary>
        /// 是否泛型集合
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool IsGenericCollection(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }
            var typeDefinition = type.GetGenericTypeDefinition();
            return typeDefinition == typeof(IEnumerable<>)
                   || typeDefinition == typeof(IReadOnlyCollection<>)
                   || typeDefinition == typeof(IReadOnlyList<>)
                   || typeDefinition == typeof(ICollection<>)
                   || typeDefinition == typeof(IList<>)
                   || typeDefinition == typeof(List<>);
        }
        #endregion

    }
}
