namespace Bing.Utils.Parameters.Formats
{
    /// <summary>
    /// Url参数格式化器
    /// </summary>
    public class UrlParameterFormat : ParameterFormatBase
    {
        /// <summary>
        /// Url参数格式化器实例
        /// </summary>
        public static IParameterFormat Instance { get; } = new UrlParameterFormat();

        /// <summary>
        /// 格式化分割符
        /// </summary>
        protected override string FormatSeparator => "=";

        /// <summary>
        /// 连接符
        /// </summary>
        protected override string JoinSeparator => "&";
    }
}
