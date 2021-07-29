using CanalSharp.Protocol;

namespace Bing.Canal
{
    /// <summary>
    /// Canal 格式化器
    /// </summary>
    public interface IFormatter
    {
        /// <summary>
        /// 格式化
        /// </summary>
        /// <param name="entry">对象</param>
        object Format(Entry entry);
    }
}
