namespace Bing.Sequence.Abstractions
{
    /// <summary>
    /// 序列号生成器构建者
    /// 参考：https://gitee.com/xuan698400/xsequence
    /// </summary>
    public interface ISequenceBuilder
    {
        /// <summary>
        /// 构建一个序列号生成器
        /// </summary>
        /// <returns></returns>
        ISequence Build();
    }
}
