namespace Bing.Sequence.Abstractions
{
    /// <summary>
    /// 序列号区间生成器
    /// </summary>
    public interface IRangeSequence : ISequence
    {
        /// <summary>
        /// 设置区间管理器
        /// </summary>
        /// <param name="manager">区间管理器</param>
        void SetRangeManager(ISequenceRangeManager manager);

        /// <summary>
        /// 设置序列号名称
        /// </summary>
        /// <param name="name">名称</param>
        void SetName(string name);
    }
}
