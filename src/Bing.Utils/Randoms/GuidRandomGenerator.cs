using Bing.Helpers;

namespace Bing.Utils.Randoms
{
    /// <summary>
    /// Guid随机数生成器，每次创建一个新的Guid字符串，去掉了Guid的分隔符
    /// </summary>
    public class GuidRandomGenerator : IRandomGenerator
    {
        /// <summary>
        /// 生成随机数
        /// </summary>
        public string Generate() => Id.Guid();

        /// <summary>
        /// Guid 随机数生成器实例
        /// </summary>
        public static readonly IRandomGenerator Instance = new GuidRandomGenerator();
    }
}
