using System.Collections.Generic;

namespace Bing.MockData
{
    /// <summary>
    /// 随机数生成器
    /// </summary>
    public interface IRandomGenerator
    {
        /// <summary>
        /// 生成随机数
        /// </summary>
        /// <returns></returns>
        string Generate();

        /// <summary>
        /// 批量生产随机数
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        List<string> BatchGenerate(int maxLength);
    }
}
