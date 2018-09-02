using System;

namespace Bing.Extensions.Swashbuckle.Attributes
{
    /// <summary>
    /// Swagger上传，用于标识接口是否包含上传信息参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SwaggerUploadAttribute:Attribute
    {
        /// <summary>
        /// 参数名
        /// </summary>
        public string Name { get; set; } = "file";

        /// <summary>
        /// 是否必填项
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// 备注
        /// </summary>
        public string Descritpion { get; set; } = "";
    }
}
