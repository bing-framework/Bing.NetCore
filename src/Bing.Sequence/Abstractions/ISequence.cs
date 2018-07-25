namespace Bing.Sequence.Abstractions
{
    /// <summary>
    /// 序列号生成器
    /// </summary>
    public interface ISequence
    {
        /// <summary>
        /// 生成下一个序列号
        /// </summary>
        /// <returns></returns>
        long NextValue();
    }
}
