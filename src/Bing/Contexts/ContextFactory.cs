using Bing.Utils.Helpers;

namespace Bing.Contexts
{
    /// <summary>
    /// 上下文工厂
    /// </summary>
    public static class ContextFactory
    {
        /// <summary>
        /// 创建上下文
        /// </summary>
        /// <returns></returns>
        public static IContext Create()
        {
            if (Web.HttpContext == null)
            {
                return NullContext.Instance;
            }

            return new WebContext();
        }
    }
}
