#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

#endregion

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 枚举值辅助工具
    /// </summary>
    /// <example>
    /// private enum Test
    /// {
    /// 	[Description("测试1")]
    ///		Enum1,
    /// 	[Description("测试2")]
    /// 	Enum2,
    /// 	[Description("测试3")]
    /// 	Enum3
    /// }
    ///
    /// EnumHelper.GetDescription(Test.Enum2);
    /// </example>
    public static class EnumHelper
    {
        private static Dictionary<Type, IDictionary<string, string>> cache =
            new Dictionary<Type, IDictionary<string, string>>();

        private static Dictionary<Type, IDictionary<int, string>> cacheEnumNumber =
            new Dictionary<Type, IDictionary<int, string>>();

        private static Dictionary<Type, Dictionary<int, string>> cacheEnumKeyAndValue =
            new Dictionary<Type, Dictionary<int, string>>();

        /// <summary>
        /// 获取枚举值的的描述信息。
        /// </summary>
        /// <param name="type">枚举类型。</param>
        /// <param name="fieldName">枚举项。</param>
        /// <returns>返回枚举项的Description信息，如果该项没有包含Description则返回枚举项的名称。</returns>
        public static string GetDescription(Type type, string fieldName)
        {
            var info = GetDescriptions(type);
            if (info.ContainsKey(fieldName))
            {
                return info[fieldName];
            }
            return fieldName;
        }

        /// <summary>
        /// 获取枚举值的的描述信息。
        /// </summary>
        /// <param name="obj">枚举对象。</param>
        /// <returns>返回枚举项的Description信息，如果该项没有包含Description则返回枚举项的ToString()内容。</returns>
        public static string GetDescription(object obj)
        {
            var info = GetDescriptions(obj.GetType());
            if (info.ContainsKey(obj.ToString()))
            {
                return info[obj.ToString()];
            }
            return obj.ToString();
        }

        /// <summary>
        /// 获取给定枚举的所有值(string)的描述信息。
        /// </summary>
        /// <param name="type">枚举对象。</param>
        /// <returns>返回枚举对象所有值的描述信息集合。</returns>
        public static IDictionary<string, string> GetDescriptions(Type type)
        {
            if (cache.ContainsKey(type))
            {
                return cache[type];
            }

            lock (cache)
            {
                if (cache.ContainsKey(type))
                {
                    return cache[type];
                }

                FieldInfo[] fields = type.GetFields();
                IDictionary<string, string> list = new Dictionary<string, string>();

                foreach (FieldInfo field in fields)
                {
                    if (field.IsSpecialName)
                        continue;

                    object[] attrs = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                    if (attrs.Length > 0)
                    {
                        string description = ((DescriptionAttribute)attrs[0]).Description;
                        if (description == String.Empty)
                        {
                            description = attrs[0].ToString();
                        }
                        list.Add(field.Name, description);
                    }
                }
                cache.Add(type, list);
                return list;
            }
        }
    }
}
