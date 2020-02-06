using System;
using System.Runtime.Serialization;

namespace Bing.Exceptions
{
    /// <summary>
    /// 配置异常
    /// </summary>
    [Serializable]
    public class ConfigException : Exception
    {
        /// <summary>
        /// 初始化一个<see cref="ConfigException"/>类型的实例
        /// </summary>
        public ConfigException() : base("缺少相关配置。")
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConfigException"/>类型的实例
        /// </summary>
        /// <param name="configKey">配置键</param>
        public ConfigException(string configKey) : base($"缺少相关配置。{configKey}")
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConfigException"/>类型的实例
        /// </summary>
        /// <param name="msgFormat">格式化消息</param>
        /// <param name="objects">格式化参数</param>
        public ConfigException(string msgFormat, params object[] objects) : base(string.Format(msgFormat, objects))
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConfigException"/>类型的实例
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="innerException">错误来源</param>
        public ConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConfigException"/>类型的实例
        /// </summary>
        /// <param name="info">消息</param>
        /// <param name="context">错误来源</param>
        public ConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
