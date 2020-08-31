using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>
    /// 视图结果(<see cref="ViewResult"/>) 扩展
    /// </summary>
    public static class BingViewResultExtensions
    {
        /// <summary>
        /// 转换成Html
        /// </summary>
        /// <param name="result">视图结果</param>
        /// <param name="httpContext">Http上下文</param>
        public static string ToHtml(this ViewResult result, HttpContext httpContext)
        {
            var feature = httpContext.Features.Get<IRoutingFeature>();
            var routeData = feature.RouteData;
            var viewName = result.ViewName ?? routeData.Values["action"] as string;
            var actionContext = new ActionContext(httpContext, routeData, new CompiledPageActionDescriptor());
            var options = httpContext.RequestServices.GetRequiredService<IOptions<MvcViewOptions>>();
            var htmlHelperOptions = options.Value.HtmlHelperOptions;
            var viewEngineResult = result.ViewEngine?.FindView(actionContext, viewName, true) ?? options.Value
                                       .ViewEngines.Select(x => x.FindView(actionContext, viewName, true))
                                       .FirstOrDefault(x => x != null);
            var view = viewEngineResult.View;
            var builder = new StringBuilder();

            using (var output = new StringWriter(builder))
            {
                var viewContext = new ViewContext(actionContext, view, result.ViewData, result.TempData, output,
                    htmlHelperOptions);

                view.RenderAsync(viewContext).GetAwaiter().GetResult();
            }

            return builder.ToString();
        }
    }
}
