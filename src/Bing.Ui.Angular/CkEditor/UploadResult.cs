using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Ui.CkEditor
{
    /// <summary>
    /// CkEditor上传结果
    /// </summary>
    public class UploadResult : ContentResult
    {
        /// <summary>
        /// 初始化一个<see cref="UploadResult"/>类型的实例
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="message">消息</param>
        public UploadResult(string path, string message)
        {
            var num = Web.HttpContext.Request.Query["CKEditorFuncNum"].SafeString();
            Content = $"<script type='text/javascript'>window.parent.CKEDITOR.tools.callFunction({num}, '{path}', '{message}');</script>";
            ContentType = "text/html; charset=utf-8";
        }
    }
}
