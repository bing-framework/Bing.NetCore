namespace Bing.Utils.Parameters
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    public interface IParameterManager
    {
        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="name">参数名</param>
        object GetValue(string name);
    }
}
