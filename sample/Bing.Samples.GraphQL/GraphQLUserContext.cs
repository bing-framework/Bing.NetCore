using System.Security.Claims;

namespace Bing.Samples.GraphQL
{
    /// <summary>
    /// GraphQL 用户上下文
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class GraphQLUserContext
    {
        /// <summary>
        /// 用户
        /// </summary>
        public ClaimsPrincipal User { get; set; }
    }
}
