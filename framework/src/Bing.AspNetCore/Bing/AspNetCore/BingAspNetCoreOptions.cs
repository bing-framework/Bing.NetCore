namespace Bing.AspNetCore
{
    /// <summary>
    /// AspNetCore 选项配置
    /// </summary>
    public class BingAspNetCoreOptions
    {
        /// <summary>
        /// 是否输出调试信息。默认: true
        /// </summary>
        public bool IsDebug { get; set; } = true;

        /// <summary>
        /// 响应异常转换为对应的http状态码。默认: false
        /// </summary>
        public bool UseResponseExceptionToHttpCode { get; set; } = false;

        /// <summary>
        /// 是否返回所有的模型验证错误。默认:false。
        /// </summary>
        public bool ResponseAllModelError { get; set; } = false;
    }
}
