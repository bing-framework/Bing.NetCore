using System.IO;
using System.Text.Encodings.Web;

namespace Bing.Ui.Builders
{
    /// <summary>
    /// 空标签生成器
    /// </summary>
    public class EmptyTagBuilder:TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="EmptyTagBuilder"/>类型的实例
        /// </summary>
        public EmptyTagBuilder() : base("i")
        {
        }

        /// <summary>
        /// 写入流
        /// </summary>
        /// <param name="writer">流写入器</param>
        /// <param name="encoder">Html编码</param>
        public override void WriteTo(TextWriter writer, HtmlEncoder encoder)
        {            
        }

        /// <summary>
        /// 输出
        /// </summary>
        public override string ToString()
        {
            return string.Empty;
        }
    }
}
