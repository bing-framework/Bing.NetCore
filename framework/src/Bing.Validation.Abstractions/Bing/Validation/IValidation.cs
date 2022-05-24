namespace Bing.Validation
{
    /// <summary>
    /// 验证操作
    /// </summary>
    public interface IValidation
    {
        /// <summary>
        /// 验证
        /// </summary>
        IValidationResult Validate();
    }
}
