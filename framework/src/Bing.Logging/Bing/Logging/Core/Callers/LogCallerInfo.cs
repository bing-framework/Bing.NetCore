namespace Bing.Logging.Core.Callers
{
    /// <summary>
    /// 日志调用者信息
    /// </summary>
    public readonly struct LogCallerInfo : ILogCallerInfo
    {
        /// <summary>
        /// 初始化一个<see cref="LogCallerInfo"/>类型的实例
        /// </summary>
        /// <param name="memberName">成员名称（方法名）</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="lineNumber">行号</param>
        public LogCallerInfo(string memberName, string filePath = null, int lineNumber = 0)
        {
            MemberName = memberName;
            FilePath = filePath;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// 成员名称（方法名）
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 行号
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// 转换为参数
        /// </summary>
        public dynamic ToParams() => new { MemberName, FilePath, LineNumber };
    }
}
