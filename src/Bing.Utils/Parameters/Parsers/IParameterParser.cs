using System.Collections.Generic;

namespace Bing.Utils.Parameters.Parsers
{
    /// <summary>
    /// 参数解析器
    /// </summary>
    public interface IParameterParser
    {
        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="name">参数名</param>
        /// <returns></returns>
        object GetValue(string name);

        /// <summary>
        /// 获取值
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="name">参数名</param>
        /// <returns></returns>
        T GetValue<T>(string name);

        /// <summary>
        /// 获取字典
        /// </summary>
        /// <returns></returns>
        IDictionary<string, object> GetDictionary();

        /// <summary>
        /// 是否包含指定键
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        bool HasKey(string key);

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="data">数据</param>
        void LoadData(string data);

        /// <summary>
        /// 转换为Json
        /// </summary>
        /// <param name="isConvertToSingleQuotes">是否将双引号转换为单引号</param>
        /// <returns></returns>
        string ToJson(bool isConvertToSingleQuotes = false);
    }
}
