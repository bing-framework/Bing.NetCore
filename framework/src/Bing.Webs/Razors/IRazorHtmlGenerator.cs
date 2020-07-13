using System.Threading.Tasks;

namespace Bing.Webs.Razors
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
