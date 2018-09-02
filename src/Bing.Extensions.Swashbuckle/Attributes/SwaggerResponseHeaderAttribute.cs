using System;

namespace Bing.Extensions.Swashbuckle.Attributes
{
    /// <summary>
    /// Swagger响应请求头，用于标识接口响应请求头参数信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Method,AllowMultiple = true)]
    public class SwaggerResponseHeaderAttribute:Attribute
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 初始化一个<see cref="SwaggerResponseHeaderAttribute"/>类型的实例
        /// </summary>
        /// <param name="statusCode">状态码</param>
        /// <param name="name">名称</param>
        /// <param name="type">类型</param>
        /// <param name="description">备注</param>
        public SwaggerResponseHeaderAttribute(int statusCode, string name, string type, string description)
        {
            StatusCode = statusCode;
            Name = name;
            Type = type;
            Description = description;
        }
    }
}
