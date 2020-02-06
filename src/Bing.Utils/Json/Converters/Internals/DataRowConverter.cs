using System;
using System.Data;
using Newtonsoft.Json;

namespace Bing.Utils.Json.Converters.Internals
{
    /// <summary>
    /// DataRow 转换器
    /// </summary>
    internal class DataRowConverter : JsonConverter
    {
        /// <summary>
        /// 写入JSON对象
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">对象值</param>
        /// <param name="serializer">JSON序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataRow row = value as DataRow;
            writer.WriteStartObject();
            foreach (DataColumn column in row.Table.Columns)
            {
                writer.WritePropertyName(column.ColumnName);
                serializer.Serialize(writer, row[column]);
            }
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
            return typeof(DataRow).IsAssignableFrom(objectType);
        }
    }
}
