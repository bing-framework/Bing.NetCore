using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Json序列化动态属性转换约定
    /// </summary>
    /// <remarks>
    /// 处理场景 树形结构数据 后台代码实体定义 为 Id Childrens 但是前台树形控件所需数据结构为  id,nodes
    /// 这个时候可以使用该属性约定转换类 动态设置 序列化后字段名称
    /// </remarks>
    /// <example>
    ///     JsonSerializerSettings setting = new JsonSerializerSettings();
    ///     setting.ContractResolver = new PropsContractResolver(new Dictionary《string, string》 { { "Id", "id" }, { "Text", "text" }, { "Childrens", "nodes" } });
    ///     string AA = JsonConvert.SerializeObject(cc, Formatting.Indented, setting);
    /// </example>
    public class PropsContractResolver : DefaultContractResolver
    {
        private Dictionary<string, string> dictProps = null;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dictPropertyName">传入的属性数组</param>
        public PropsContractResolver(Dictionary<string, string> dictPropertyName)
        {
            //指定字段要序列化成什么名称
            this.dictProps = dictPropertyName;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected override string ResolvePropertyName(string propertyName)
        {
            string newPropertyName = string.Empty;
            if (dictProps != null && dictProps.TryGetValue(propertyName, out newPropertyName))
            {
                return newPropertyName;
            }
            else
            {
                return ToCamelCase(propertyName);// base.ResolvePropertyName(propertyName);
            }
        }

        /// <summary>
        /// camel格式化
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
            {
                return value;
            }

            char[] chars = value.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = (i + 1 < chars.Length);
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    break;
                }

                char c;
#if !(DOTNET || PORTABLE)
                c = char.ToLower(chars[i], System.Globalization.CultureInfo.InvariantCulture);
#else
                c = char.ToLowerInvariant(chars[i]);
#endif
                chars[i] = c;
            }

            return new string(chars);
        }
    }
}
