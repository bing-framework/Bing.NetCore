using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Http;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Bing.Samples.GraphQL
{
    /// <summary>
    /// GraphQL 中间件
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class GraphQLMiddleware
    {
        /// <summary>
        /// 执行方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 设置
        /// </summary>
        private readonly GraphQLSettings _settings;

        /// <summary>
        /// 执行器
        /// </summary>
        private readonly IDocumentExecuter _executer;

        /// <summary>
        /// 写入器
        /// </summary>
        private readonly IDocumentWriter _writer;

        /// <summary>
        /// 初始化一个<see cref="GraphQLMiddleware"/>类型的实例
        /// </summary>
        public GraphQLMiddleware(RequestDelegate next
            , GraphQLSettings settings
            , IDocumentExecuter executer
            , IDocumentWriter writer)
        {
            _next = next;
            _settings = settings;
            _executer = executer;
            _writer = writer;
        }

        public async Task Invoke(HttpContext context, ISchema schema)
        {
            if (!IsGraphqlRequest(context))
            {
                await _next(context);
                return;
            }

            await ExecuteAsync(context, schema);
        }
        
        /// <summary>
        /// 是否GraphQL请求
        /// </summary>
        /// <returns></returns>
        private bool IsGraphqlRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments(_settings.Path) &&
                   string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 执行
        /// </summary>
        private async Task ExecuteAsync(HttpContext context, ISchema schema)
        {
            var request = Descrialize<GraphQLRequest>(context.Request.Body);

            var result = await _executer.ExecuteAsync(_ =>
            {
                _.Schema = schema;
                _.Query = request.Query;
                _.OperationName = request.OperationName;
                _.Inputs = request.Variables.ToInputs();
                _.UserContext = _settings.BuildUserContext?.Invoke(context);
                _.ValidationRules = DocumentValidator.CoreRules().Concat(new[] {new InputValidationRule()});
            });

            await WriteResponseAsync(context, result);
        }

        /// <summary>
        /// 写入响应流
        /// </summary>
        private async Task WriteResponseAsync(HttpContext context, ExecutionResult result)
        {
            var json = await _writer.WriteToStringAsync(result);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode =
                result.Errors?.Any() == true ? (int) HttpStatusCode.BadRequest : (int) HttpStatusCode.OK;

            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        public static T Descrialize<T>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return new JsonSerializer().Deserialize<T>(jsonReader);
                }
            }
        }
    }
}
