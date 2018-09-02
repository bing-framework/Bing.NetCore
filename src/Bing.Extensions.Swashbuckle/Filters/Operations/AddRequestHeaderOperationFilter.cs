using System.Collections.Generic;
using System.Linq;
using Bing.Extensions.Swashbuckle.Attributes;
using Bing.Extensions.Swashbuckle.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 添加请求头 操作过滤器
    /// </summary>
    public class AddRequestHeaderOperationFilter:IOperationFilter
    {
        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            var swaggerRequestHeaders = context.GetControllerAndActionAttributes<SwaggerRequestHeaderAttribute>().ToList();
            if (!swaggerRequestHeaders.Any())
            {
                return;
            }

            foreach (var requestHeader in swaggerRequestHeaders)
            {
                if (operation.Parameters == null)
                {
                    operation.Parameters = new List<IParameter>();
                }

                var request =
                    operation.Parameters.FirstOrDefault(x => x.In == "header" && x.Name == requestHeader.Name);
                if (request != null)
                {
                    operation.Parameters.Remove(request);
                }
                operation.Parameters.Add(new NonBodyParameter()
                {
                    Name = requestHeader.Name,
                    In = "header",
                    Description = requestHeader.Description,
                    Required = requestHeader.Required,
                    Type = "string",
                    Default = requestHeader.Default
                });
            }
        }
    }
}
