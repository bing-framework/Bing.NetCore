namespace Bing.Auditing
{
    /// <summary>
    /// 审计序列化器
    /// </summary>
    public interface IAuditSerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        string Serialize(object obj);
    }
}
