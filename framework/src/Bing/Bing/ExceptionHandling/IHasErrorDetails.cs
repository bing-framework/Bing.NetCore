namespace Bing.ExceptionHandling
{
    /// <summary>
    /// 错误详情
    /// </summary>
    public interface IHasErrorDetails
    {
        /// <summary>
        /// 错误详情
        /// </summary>
        string Details { get; }
    }
}
