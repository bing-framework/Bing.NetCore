using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bing.Extensions.Swashbuckle.Attributes;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 添加响应请求头 操作过滤器
    /// </summary>
    public class AddResponseHeadersOperationFilter:IOperationFilter
    {
        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            var swaggerResponseHeaders =
                context.MethodInfo.GetCustomAttributes<SwaggerResponseHeaderAttribute>().ToList();
            if (!swaggerResponseHeaders.Any())
            {
                return;
            }

            foreach (var responseHeader in swaggerResponseHeaders)
            {
                var response = operation.Responses.FirstOrDefault(x =>
                    x.Key == ((int) responseHeader.StatusCode).ToString(CultureInfo.InvariantCulture)).Value;
                if (response == null)
                {
                    continue;
                }
                if (response.Headers == null)
                {
                    response.Headers = new Dictionary<string, Header>();
                }
                response.Headers.Add(responseHeader.Name,
                    new Header() {Description = responseHeader.Description, Type = responseHeader.Type});
            }
        }
    }
}
