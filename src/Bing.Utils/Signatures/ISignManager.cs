namespace Bing.Utils.Signatures
{
    /// <summary>
    /// 签名管理器
    /// </summary>
    public interface ISignManager
    {
        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISignManager Add(string key, object value);

        /// <summary>
        /// 签名
        /// </summary>
        /// <returns></returns>
        string Sign();

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        bool Verify(string sign);
    }
}
