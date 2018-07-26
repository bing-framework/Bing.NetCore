using Bing.Sequence.Core;

namespace Bing.Sequence.Abstractions
{
    /// <summary>
    /// 区间管理器
    /// </summary>
    public interface ISequenceRangeManager
    {
        /// <summary>
        /// 获取指定区间名的下一个区间
        /// </summary>
        /// <param name="name">区间名</param>
        /// <returns></returns>
        SequenceRange NextRange(string name);

        /// <summary>
        /// 初始化
        /// </summary>
        void Init();
    }
}
