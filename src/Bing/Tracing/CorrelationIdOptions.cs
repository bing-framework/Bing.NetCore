namespace Bing.Tracing
{
    /// <summary>
    /// 跟踪关联ID选项
    /// </summary>
    public class CorrelationIdOptions
    {
        /// <summary>
        /// Http请求头名称
        /// </summary>
        public string HttpHeaderName { get; set; } = "X-Correlation-Id";

        /// <summary>
        /// 是否将跟踪关联ID设置在响应头
        /// </summary>
        public bool SetResponseHeader { get; set; } = true;
    }
}
