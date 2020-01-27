using Bing.Core.Data;

namespace Bing.Release
{
    /// <summary>
    /// 可关闭的接口
    /// </summary>
    public interface ICloseable
    {
        /// <summary>
        /// 关闭
        /// </summary>
        void Close();

        /// <summary>
        /// 关闭后事件
        /// </summary>
        event DataHandler Closed;
    }
}
