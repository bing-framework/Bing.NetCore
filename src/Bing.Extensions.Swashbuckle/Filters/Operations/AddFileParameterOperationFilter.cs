using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Extensions.Swashbuckle.Attributes;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 添加文件参数 操作过滤器。支持<see cref="SwaggerUploadAttribute"/>特性
    /// </summary>
    public class AddFileParameterOperationFilter:IOperationFilter
    {
        /// <summary>
        /// 文件参数
        /// </summary>
        private static readonly string[] FileParameters = new[]
            {"ContentType", "ContentDisposition", "Headers", "Length", "Name", "FileName"};

        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            var swaggerUpload = context.MethodInfo.GetCustomAttributes<SwaggerUploadAttribute>().FirstOrDefault();
            if (swaggerUpload == null)
            {
                return;
            }
            operation.Consumes.Add("multipart/form-data");
            RemoveExistingFileParameters(operation.Parameters);
            operation.Parameters.Add(new NonBodyParameter()
            {
                Name = swaggerUpload.Name,
                Required = swaggerUpload.Required,
                In = "formData",
                Type = "file",
                Description = swaggerUpload.Descritpion
            });
        }

        /// <summary>
        /// 移除已存在的文件参数
        /// </summary>
        /// <param name="operationParameters">操作参数列表</param>
        private void RemoveExistingFileParameters(IList<IParameter> operationParameters)
        {
            foreach (var parameter in operationParameters
                .Where(x => x.In == "query" && FileParameters.Contains(x.Name)).ToList())
            {
                operationParameters.Remove(parameter);
            }
        }
    }
}
