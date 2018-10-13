using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bing.Ui.Services
{
    /// <summary>
    /// Ui上下文
    /// </summary>
    /// <typeparam name="TModel">组件类型</typeparam>
    public interface IUiContext<TModel>
    {
        /// <summary>
        /// HtmlHelper
        /// </summary>
        IHtmlHelper<TModel> Helper { get; }
    }
}
