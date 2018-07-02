using System;
using System.Data;
using Newtonsoft.Json;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// DataSet 转换器
    /// </summary>
    public class DataSetConverter:JsonConverter
    {
        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataSet dataSet = value as DataSet;
            DataTableConverter dataTableConverter = new DataTableConverter();
            writer.WriteStartObject();
            writer.WritePropertyName("Tables");
            writer.WriteStartArray();
            foreach (DataTable table in dataSet.Tables)
            {
                dataTableConverter.WriteJson(writer, table, serializer);
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
        /// <returns></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 确定此实例是否可以转换指定的对象类型
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(DataSet).IsAssignableFrom(objectType);
        }
    }
}
