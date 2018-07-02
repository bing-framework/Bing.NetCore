using Newtonsoft.Json.Converters;

namespace Bing.Utils.Json.Converters
{
    /// <summary>
    /// 自定义时间格式转换器
    /// <para>
    /// [JsonConverter(typeof(CustomDateFormatConverter),"yyyy-MM-dd HH:mm:ss")]
    /// public DateTime Birthday { get; set; }
    /// </para>
    /// </summary>
    public class CustomDateFormatConverter:IsoDateTimeConverter
    {
        /// <summary>
        /// 初始化一个<see cref="CustomDateFormatConverter"/>类型的实例
        /// </summary>
        /// <param name="format">格式化字符串</param>
        public CustomDateFormatConverter(string format)
        {
            this.DateTimeFormat = format;
        }
    }
}
