namespace Bing.IM.Models
{
    /// <summary>
    /// 文件上传结果
    /// </summary>
    public class FileUploadResult
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件地址
        /// </summary>
        public string FileUrl { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMsg { get; set; }
    }
}
