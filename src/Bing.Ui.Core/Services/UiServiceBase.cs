using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bing.Ui.Services
{
    /// <summary>
    /// 组件服务基类
    /// </summary>
    /// <typeparam name="TModel">组件类型</typeparam>
    public class UiServiceBase<TModel>:IUiContext<TModel>
    {
        /// <summary>
        /// HtmlHelper
        /// </summary>
        public IHtmlHelper<TModel> Helper { get; }

        /// <summary>
        /// 初始化一个<see cref="UiServiceBase{TModel}"/>类型的实例
        /// </summary>
        /// <param name="helper">HtmlHelper</param>
        public UiServiceBase(IHtmlHelper<TModel> helper)
        {
            Helper = helper;
        }
    }
}
