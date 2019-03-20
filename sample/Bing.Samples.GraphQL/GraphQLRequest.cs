using Newtonsoft.Json.Linq;

namespace Bing.Samples.GraphQL
{
    /// <summary>
    /// GraphQL 请求
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class GraphQLRequest
    {
        /// <summary>
        /// 操作名称
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// 查询
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// 变量
        /// </summary>
        public JObject Variables { get; set; }
    }
}
