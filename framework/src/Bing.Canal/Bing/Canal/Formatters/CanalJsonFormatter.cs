using CanalSharp.Protocol;

namespace Bing.Canal.Formatters
{
    /// <summary>
    /// Canal Json 格式化器
    /// </summary>
    public class CanalJsonFormatter : IFormatter
    {
        /// <summary>
        /// 格式化
        /// </summary>
        /// <param name="entry">对象</param>
        public object Format(Entry entry) => entry;
    }
}
