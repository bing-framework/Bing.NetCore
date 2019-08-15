using Bing.Ui.Builders;

namespace Bing.Ui.Zorro.Forms.Builders
{
    /// <summary>
    /// NgZorro文件上传生成器
    /// </summary>
    public class UploadBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="UploadBuilder"/>类型的实例
        /// </summary>
        public UploadBuilder() : base("nz-upload") { }

        /// <summary>
        /// 添加nzAccept
        /// </summary>
        /// <param name="value">至</param>
        public void Accept(string value)
        {
            AddAttribute("nzAccept", value);
        }

        /// <summary>
        /// 添加nzFileType
        /// </summary>
        /// <param name="value">治啊</param>
        public void FileType(string value)
        {
            AddAttribute("nzFileType", value);
        }
    }
}
