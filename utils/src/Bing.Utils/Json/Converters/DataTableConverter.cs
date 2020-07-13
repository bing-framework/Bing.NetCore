using System;
using System.Data;
using Bing.Utils.Json.Converters.Internals;
using Newtonsoft.Json;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// DataTable 转换器
    /// </summary>
    public class DataTableConverter : JsonConverter
    {
        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataTable table = value as DataTable;
            DataRowConverter dataRowConverter = new DataRowConverter();
            writer.WriteStartObject();
            writer.WritePropertyName("Rows");
            writer.WriteStartArray();
            foreach (DataRow row in table.Rows)
            {
                dataRowConverter.WriteJson(writer, row, serializer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 确定此实例是否可以转换指定的对象类型
        /// </summary>
        /// <param name="objectType">对象类型</param>
        public override bool CanConvert(Type objectType)
        {
            return typeof(DataTable).IsAssignableFrom(objectType);
        }
    }
}
