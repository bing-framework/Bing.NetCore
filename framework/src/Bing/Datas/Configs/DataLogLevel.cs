namespace Bing.Datas.Configs
{
    /// <summary>
    /// 数据日志级别
    /// </summary>
    public enum DataLogLevel
    {
        /// <summary>
        /// 输出全部日志，包括连接数据库，提交事务等大量信息
        /// </summary>
        All,

        /// <summary>
        /// 仅输出Sql
        /// </summary>
        Sql,

        /// <summary>
        /// 关闭日志
        /// </summary>
        Off
    }
}
