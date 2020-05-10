using System;
using Newtonsoft.Json;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// 清除空格转换器
    /// </summary>
    public class TrimmingConverter : JsonConverter
    {
        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var text = value as string;
            if (text == null)
                writer.WriteNull();
            else
                writer.WriteValue(text.Trim());
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
            if (reader.TokenType == JsonToken.String)
                if (reader.Value != null)
                    return (reader.Value as string)?.Trim();
            return reader.Value;
        }

        /// <summary>
        /// 确定此实例是否可以转换指定的对象类型
        /// </summary>
        /// <param name="objectType">对象类型</param>
        public override bool CanConvert(Type objectType) => objectType == typeof(string);
    }
}
