namespace Bing.Core.Data
{
    /// <summary>
    /// 获取对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public interface IGetObject<out T>
    {
        /// <summary>
        /// 获取对象
        /// </summary>
        T Get();
    }
}
