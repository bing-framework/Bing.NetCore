using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// 中国时间转换器
    /// <para>
    /// [JsonConverter(typeof(ChinaDateTimeConverter))]
    /// public DateTime Birthday { get; set; }
    /// </para>
    /// </summary>
    public class ChinaDateTimeConverter : DateTimeConverterBase
    {
        /// <summary>
        /// 时间转换器
        /// </summary>
        private static readonly IsoDateTimeConverter DtConverter = new IsoDateTimeConverter() { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" };

        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DtConverter.WriteJson(writer, value, serializer);
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
            return DtConverter.ReadJson(reader, objectType, existingValue, serializer);
        }
    }
}
