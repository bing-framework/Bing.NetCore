using System.Threading.Tasks;

namespace Bing.AspNetCore.Mvc.UI.RazorPages
{
    /// <summary>
    /// Razor Html生成器
    /// </summary>
    public interface IRazorHtmlGenerator
    {
        /// <summary>
        /// 生成Html文件
        /// </summary>
        Task Generate();
    }
}
