using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Bing.Utils.Extensions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 枚举 操作
    /// </summary>
    public static class Enum
    {
        #region Parse(获取实例)
        /// <summary>
        /// 获取实例
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名或值，范例：Enum1枚举有成员A=0，则传入"A"或"0"获取 Enum1.A</param>
        /// <returns></returns>
        public static TEnum Parse<TEnum>(object member)
        {
            string value = member.SafeString();
            if (value.IsEmpty())
            {
                if (typeof(TEnum).IsGenericType)
                {
                    return default(TEnum);
                }
                throw new ArgumentNullException(nameof(member));
            }
            return (TEnum)System.Enum.Parse(Common.GetType<TEnum>(), value, true);
        }
        #endregion

        #region GetName(获取成员名)
        /// <summary>
        /// 获取成员名
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名、值、实例均可，范例：Enum1枚举有成员A=0，则传入Enum1.A或0，获取成员名"A"</param>
        /// <returns></returns>
        public static string GetName<TEnum>(object member)
        {
            return GetName(Common.GetType<TEnum>(), member);
        }

        /// <summary>
        /// 获取成员名
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可，范例：Enum1枚举有成员A=0，则传入Enum1.A或0，获取成员名"A"</param>
        /// <returns></returns>
        public static string GetName(Type type, object member)
        {
            if (type == null)
            {
                return string.Empty;
            }
            if (member == null)
            {
                return string.Empty;
            }
            if (member is string)
            {
                return member.ToString();
            }
            if (type.GetTypeInfo().IsEnum == false)
            {
                return string.Empty;
            }
            return System.Enum.GetName(type, member);
        }
        #endregion

        #region GetNames(获取枚举所有成员名称)
        /// <summary>
        /// 获取枚举所有成员名称
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns></returns>
        public static string[] GetNames<TEnum>()
        {
            return GetNames(typeof(TEnum));
        }

        /// <summary>
        /// 获取枚举所有成员名称
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <returns></returns>
        public static string[] GetNames(Type type)
        {
            return System.Enum.GetNames(type);
        }
        #endregion

        #region GetValue(获取成员值)
        /// <summary>
        /// 获取成员值
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名、值、实例均可，范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        /// <returns></returns>
        public static int GetValue<TEnum>(object member)
        {
            return GetValue(Common.GetType<TEnum>(), member);
        }
        /// <summary>
        /// 获取成员值
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可，范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        /// <returns></returns>
        public static int GetValue(Type type, object member)
        {
            string value = member.SafeString();
            if (value.IsEmpty())
            {
                throw new ArgumentNullException(nameof(member));
            }
            return (int)System.Enum.Parse(type, member.ToString(), true);
        }
        #endregion

        #region GetDescription(获取描述)
        /// <summary>
        /// 获取描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <typeparam name="T">枚举</typeparam>
        /// <param name="member">成员名、值、实例均可,范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        /// <returns></returns>
        public static string GetDescription<T>(object member)
        {
            return Reflection.GetDescription<T>(GetName<T>(member));
        }
        /// <summary>
        /// 获取描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可,范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        /// <returns></returns>
        public static string GetDescription(Type type, object member)
        {
            return Reflection.GetDescription(type, GetName(type, member));
        }
        #endregion

        #region GetItems(获取描述项集合)
        /// <summary>
        /// 获取描述项集合，文本设置为Description，值为Value
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns></returns>
        public static List<Item> GetItems<TEnum>()
        {
            return GetItems(typeof(TEnum));
        }

        /// <summary>
        /// 获取描述项集合，文本设置为Description，值为Value
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <returns></returns>
        public static List<Item> GetItems(Type type)
        {
            type = Common.GetType(type);
            if (type.IsEnum == false)
            {
                throw new InvalidOperationException($"类型 {type} 不是枚举");
            }
            var result = new List<Item>();
            foreach (var field in type.GetFields())
            {
                AddItem(type, result, field);
            }
            return result.OrderBy(t => t.SortId).ToList();
        }

        /// <summary>
        /// 验证是否枚举类型
        /// </summary>
        /// <param name="enumType">类型</param>
        private static void ValidateEnum(Type enumType)
        {
            if (enumType.IsEnum == false)
            {
                throw new InvalidOperationException(string.Format("类型 {0} 不是枚举", enumType));
            }
        }

        /// <summary>
        /// 添加描述项
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="result">集合</param>
        /// <param name="field">字段</param>
        private static void AddItem(Type type, ICollection<Item> result, FieldInfo field)
        {
            if (!field.FieldType.IsEnum)
            {
                return;
            }
            var value = GetValue(type, field.Name);
            var description = Reflection.GetDescription(field);
            result.Add(new Item(description, value, value));
        }
        #endregion

        #region GetEnumItemByDescription(获取指定描述信息的枚举项)
        /// <summary>
        /// 获取指定描述信息的枚举项
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="desc">枚举项描述信息</param>
        /// <returns></returns>
        public static TEnum GetEnumItemByDescription<TEnum>(string desc)
        {
            if (desc.IsEmpty())
            {
                throw new ArgumentNullException(nameof(desc));
            }
            Type type = typeof(TEnum);
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            FieldInfo fieldInfo =
                fieldInfos.FirstOrDefault(p => p.GetCustomAttribute<DescriptionAttribute>(false).Description == desc);
            if (fieldInfo == null)
            {
                throw new ArgumentNullException($"在枚举（{type.FullName}）中，未发现描述为“{desc}”的枚举项。");
            }
            return (TEnum)System.Enum.Parse(type, fieldInfo.Name);
        }
        #endregion

        #region GetDictionary(获取枚举字典)
        /// <summary>
        /// 获取枚举字典
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns></returns>
        public static Dictionary<int, string> GetDictionary<TEnum>()
        {
            Type enumType = Common.GetType<TEnum>().GetTypeInfo();
            ValidateEnum(enumType);

            Dictionary<int, string> dic = new Dictionary<int, string>();
            foreach (var field in enumType.GetFields())
            {
                AddItem<TEnum>(dic, field);
            }
            return dic;
        }

        /// <summary>
        /// 添加描述项
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="result">集合</param>
        /// <param name="field">字典</param>
        private static void AddItem<TEnum>(Dictionary<int, string> result, FieldInfo field)
        {
            if (!field.FieldType.GetTypeInfo().IsEnum)
            {
                return;
            }
            var value = GetValue<TEnum>(field.Name);
            var description = Reflection.GetDescription(field);
            result.Add(value, description);
        }
        #endregion
    }
}
