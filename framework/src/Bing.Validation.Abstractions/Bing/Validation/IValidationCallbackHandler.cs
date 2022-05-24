namespace Bing.Validation
{
    /// <summary>
    /// 验证回调处理器
    /// </summary>
    public interface IValidationCallbackHandler
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="results">验证结果集合</param>
        void Handle(IValidationResult results);
    }
}
