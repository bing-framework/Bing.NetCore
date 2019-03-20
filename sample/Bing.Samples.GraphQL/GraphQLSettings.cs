using System;
using Microsoft.AspNetCore.Http;

namespace Bing.Samples.GraphQL
{
    /// <summary>
    /// GraphQL 设置
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class GraphQLSettings
    {
        /// <summary>
        /// 请求路径
        /// </summary>
        public PathString Path { get; set; } = "/api/graphql";

        /// <summary>
        /// 构建用户上下文
        /// </summary>
        public Func<HttpContext,object> BuildUserContext { get; set; }
    }
}
