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
        /// <summary>
        /// 枚举值字段
        /// </summary>
        private const string EnumValueField = "value__";

        #region Parse(获取实例)

        /// <summary>
        /// 获取实例
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名或值，范例：Enum1枚举有成员A=0，则传入"A"或"0"获取 Enum1.A</param>
        public static TEnum Parse<TEnum>(object member)
        {
            var value = member.SafeString();
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
        public static string GetName<TEnum>(object member) where TEnum : struct =>
            GetName(Common.GetType<TEnum>(), member);

        /// <summary>
        /// 获取成员名
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可，范例：Enum1枚举有成员A=0，则传入Enum1.A或0，获取成员名"A"</param>
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
        public static string[] GetNames<TEnum>() where TEnum : struct => GetNames(typeof(TEnum));

        /// <summary>
        /// 获取枚举所有成员名称
        /// </summary>
        /// <param name="type">枚举类型</param>
        public static string[] GetNames(Type type) => System.Enum.GetNames(type);

        #endregion

        #region GetValue(获取成员值)

        /// <summary>
        /// 获取成员值
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名、值、实例均可，范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        public static int GetValue<TEnum>(object member) where TEnum : struct => GetValue(Common.GetType<TEnum>(), member);

        /// <summary>
        /// 获取成员值
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可，范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        /// <exception cref="ArgumentNullException">成员为空</exception>
        public static int GetValue(Type type, object member)
        {
            string value = member.SafeString();
            if (value.IsEmpty())
                throw new ArgumentNullException(nameof(member));
            return (int)System.Enum.Parse(type, member.ToString(), true);
        }

        #endregion

        #region GetDescription(获取描述)

        /// <summary>
        /// 获取描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="member">成员名、值、实例均可,范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        public static string GetDescription<TEnum>(object member) where TEnum : struct => Reflection.GetDescription<TEnum>(GetName<TEnum>(member));

        /// <summary>
        /// 获取描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="member">成员名、值、实例均可,范例:Enum1枚举有成员A=0,可传入"A"、0、Enum1.A，获取值0</param>
        public static string GetDescription(Type type, object member) => Reflection.GetDescription(type, GetName(type, member));

        #endregion

        #region GetItems(获取描述项集合)

        /// <summary>
        /// 获取描述项集合，文本设置为Description，值为Value
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        public static List<Item> GetItems<TEnum>() where TEnum : struct => GetItems(typeof(TEnum));

        /// <summary>
        /// 获取描述项集合，文本设置为Description，值为Value
        /// </summary>
        /// <param name="type">枚举类型</param>
        public static List<Item> GetItems(Type type)
        {
            type = Common.GetType(type);
            ValidateEnum(type);
            var result = new List<Item>();
            foreach (var field in type.GetFields())
                AddItem(type, result, field);
            return result.OrderBy(t => t.SortId).ToList();
        }

        /// <summary>
        /// 验证是否枚举类型
        /// </summary>
        /// <param name="enumType">类型</param>
        private static void ValidateEnum(Type enumType)
        {
            if (enumType.IsEnum == false)
                throw new InvalidOperationException($"类型 {enumType} 不是枚举");
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
                return;
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
        public static TEnum GetEnumItemByDescription<TEnum>(string desc) where TEnum : struct
        {
            if (desc.IsEmpty())
                throw new ArgumentNullException(nameof(desc));
            var type = typeof(TEnum);
            var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            var fieldInfo =
                fieldInfos.FirstOrDefault(p => p.GetCustomAttribute<DescriptionAttribute>(false).Description == desc);
            if (fieldInfo == null)
                throw new ArgumentNullException($"在枚举（{type.FullName}）中，未发现描述为“{desc}”的枚举项。");
            return (TEnum)System.Enum.Parse(type, fieldInfo.Name);
        }

        #endregion

        #region GetDictionary(获取枚举字典)

        /// <summary>
        /// 获取枚举字典
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        public static IDictionary<int, string> GetDictionary<TEnum>() where TEnum : struct
        {
            var enumType = Common.GetType<TEnum>().GetTypeInfo();
            ValidateEnum(enumType);
            var dic = new Dictionary<int, string>();
            foreach (var field in enumType.GetFields())
                AddItem<TEnum>(dic, field);
            return dic;
        }

        /// <summary>
        /// 添加描述项
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="result">集合</param>
        /// <param name="field">字典</param>
        private static void AddItem<TEnum>(IDictionary<int, string> result, FieldInfo field) where TEnum : struct
        {
            if (!field.FieldType.GetTypeInfo().IsEnum)
                return;
            var value = GetValue<TEnum>(field.Name);
            var description = Reflection.GetDescription(field);
            result.Add(value, description);
        }

        #endregion

        #region GetMemberInfos(获取枚举成员信息)

        /// <summary>
        /// 获取枚举成员信息
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        public static IEnumerable<Tuple<int, string, string>> GetMemberInfos<TEnum>() where TEnum : struct
        {
            var type = typeof(TEnum);
            ValidateEnum(type);
            var fields = type.GetFields();
            ICollection<Tuple<int, string, string>> collection = new HashSet<Tuple<int, string, string>>();
            foreach (var field in fields.Where(x => x.Name != EnumValueField))
            {
                var value = GetValue<TEnum>(field.Name);
                var description = Reflection.GetDescription(field);
                collection.Add(new Tuple<int, string, string>(value, field.Name,
                    string.IsNullOrWhiteSpace(description) ? field.Name : description));
            }

            return collection;
        }

        #endregion
    }
}
