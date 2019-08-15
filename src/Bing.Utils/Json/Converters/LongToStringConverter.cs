using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// 长整型转字符串转换器
    /// </summary>
    public class LongToStringConverter : JsonConverter
    {
        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }

        /// <summary>
        /// 读取JSON对象
        /// </summary>
        /// <param name="reader">JSON读取器</param>
        /// <param name="objectType">对象类型</param>
        /// <param name="existingValue">存在值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return JToken.ReadFrom(reader).Value<long>();
        }

        /// <summary>
        /// 确定此实例是否可以转换指定的对象类型
        /// </summary>
        /// <param name="objectType">对象类型</param>
        public override bool CanConvert(Type objectType)
        {
            return typeof(long) == objectType;
        }
    }
}
