using System;

namespace Bing.Extensions.Swashbuckle.Attributes
{
    /// <summary>
    /// Swagger请求头，用于标识接口请求头参数信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple = true)]
    public class SwaggerRequestHeaderAttribute:Attribute
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 是否必填项
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// 备注
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object Default { get; set; } = null;

        /// <summary>
        /// 初始化一个<see cref="SwaggerRequestHeaderAttribute"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="description">备注</param>
        public SwaggerRequestHeaderAttribute(string name, string description = null)
        {
            Name = name;
            Description = description;
        }
    }
}
