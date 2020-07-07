using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bing.Helpers;

namespace Bing.Reflection
{
    /// <summary>
    /// 反射 操作
    /// </summary>
    public static partial class Reflections
    {
        #region GetDescription(获取类型描述)

        /// <summary>
        /// 获取类型描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static string GetDescription<T>()
        {
            var type = typeof(T);
            var attribute = GetAttribute<DescriptionAttribute>(type);
            return attribute?.Description ?? type.Name;
        }

        /// <summary>
        /// 获取类型描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="type">类型</param>
        public static string GetDescription(Type type)
        {
            var attribute = GetAttribute<DescriptionAttribute>(type);
            return attribute?.Description ?? type.Name;
        }

        /// <summary>
        /// 获取类型描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static string GetDescription(TypeInfo typeInfo)
        {
            var attribute = GetAttribute<DescriptionAttribute>(typeInfo);
            return attribute?.Description ?? typeInfo.Name;
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="memberName">成员名称</param>
        public static string GetDescription<T>(string memberName) => GetDescription(Common.GetType<T>(), memberName);

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="memberName">成员名称</param>
        public static string GetDescription(Type type, string memberName) => GetDescription(type.GetTypeInfo(), memberName);

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="memberName">成员名称</param>
        public static string GetDescription(TypeInfo typeInfo, string memberName)
        {
            if (typeInfo is null)
                return string.Empty;
            return string.IsNullOrWhiteSpace(memberName)
                ? string.Empty
                : GetDescription(typeInfo, typeInfo.GetField(memberName));
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="member">成员</param>
        public static string GetDescription(MemberInfo member)
        {
            if (member is null)
                return string.Empty;
            var attribute = GetAttribute<DescriptionAttribute>(member);
            return attribute is null ? member.Name : attribute.Description;
        }

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="fieldInfo">字段元数据</param>
        public static string GetDescription(Type type, FieldInfo fieldInfo) => GetDescription(type.GetTypeInfo(), fieldInfo);

        /// <summary>
        /// 获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="fieldInfo">字段元数据</param>
        public static string GetDescription(TypeInfo typeInfo, FieldInfo fieldInfo)
        {
            if (typeInfo is null || fieldInfo is null)
                return string.Empty;
            var attribute = GetAttribute<DescriptionAttribute>(fieldInfo);
            return attribute is null ? fieldInfo.Name : attribute.Description;
        }

        #endregion

        #region GetDisplayName(获取类型显示名称)

        /// <summary>
        /// 获取类型显示名称，使用 <see cref="DisplayNameAttribute"/> 或 <see cref="DisplayAttribute"/> 设置显示名称
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static string GetDisplayName<T>() => GetDisplayName(typeof(T));

        /// <summary>
        /// 获取类型成员显示名称，，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称
        /// </summary>
        /// <param name="member">成员</param>
        private static string GetDisplayName(MemberInfo member)
        {
            if (member == null)
                return string.Empty;
            var displayNameAttribute = GetAttribute<DisplayNameAttribute>(member);
            if (displayNameAttribute != null)
                return displayNameAttribute.DisplayName;
            var displayAttribute = GetAttribute<DisplayAttribute>(member);
            if (displayAttribute is null)
                return string.Empty;
            return displayAttribute.Description;
        }

        #endregion

        #region GetDisplayNameOrDescription(获取显示名称或类型描述)

        /// <summary>
        /// 获取类型显示名称或描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>设置显示名称
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static string GetDisplayNameOrDescription<T>() => GetDisplayNameOrDescription(Common.GetType<T>());

        /// <summary>
        /// 获取类型显示名称或成员描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称
        /// </summary>
        /// <param name="member">成员</param>
        public static string GetDisplayNameOrDescription(MemberInfo member)
        {
            var result = GetDisplayName(member);
            return string.IsNullOrWhiteSpace(result) ? GetDescription(member) : result;
        }

        #endregion
    }
}
