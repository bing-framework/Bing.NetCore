namespace Bing.Utils.IdGenerators
{
    /// <summary>
    /// ID 生成器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public interface IIdGenerator<out T>
    {
        /// <summary>
        /// 创建 ID
        /// </summary>
        T Create();
    }
}
