using Bing.Helpers;
using Bing.Utils.Parameters;

namespace Bing.Parameters
{
    /// <summary>
    /// Url参数生成器(<see cref="UrlParameterBuilder"/>) 扩展
    /// </summary>
    public static class UrlParameterBuilderExtensions
    {
        /// <summary>
        /// 从Request加载表单参数
        /// </summary>
        /// <param name="builder">Url参数生成器</param>
        public static void LoadForm(this UrlParameterBuilder builder)
        {
            var form = Web.Request?.Form;
            if (form == null)
                return;
            foreach (var key in form.Keys)
            {
                if (form.ContainsKey(key))
                    builder.Add(key, form[key]);
            }
        }

        /// <summary>
        /// 从Request加载查询参数
        /// </summary>
        /// <param name="builder">Url参数生成器</param>
        public static void LoadQuery(this UrlParameterBuilder builder)
        {
            var query = Web.Request?.Query;
            if (query == null)
                return;
            foreach (var key in query.Keys)
            {
                if (query.ContainsKey(key))
                    builder.Add(key, query[key]);
            }
        }
    }
}
