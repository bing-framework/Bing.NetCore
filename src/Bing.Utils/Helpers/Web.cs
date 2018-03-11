using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Web操作
    /// </summary>
    public static class Web
    {
        #region 属性

        /// <summary>
        /// Http上下文访问器
        /// </summary>
        public static IHttpContextAccessor HttpContextAccessor { get; set; }

        /// <summary>
        /// 当前Http上下文
        /// </summary>
        public static HttpContext HttpContext => HttpContextAccessor?.HttpContext;

        /// <summary>
        /// 宿主环境
        /// </summary>
        public static IHostingEnvironment Environment { get; set; }

        #endregion

        #region RootPath(根路径)

        /// <summary>
        /// 根路径
        /// </summary>
        public static string RootPath => Environment?.ContentRootPath;

        #endregion

        #region WebRootPath(Web根路径)

        /// <summary>
        /// Web根路径，即wwwroot
        /// </summary>
        public static string WebRootPath => Environment?.WebRootPath;

        #endregion

        #region GetFiles(获取客户端文件集合)

        /// <summary>
        /// 获取客户端文件集合
        /// </summary>
        /// <returns></returns>
        public static List<IFormFile> GetFiles()
        {
            var result=new List<IFormFile>();
            var files = HttpContext.Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return result;
            }

            result.AddRange(files.Where(file => file?.Length > 0));
            return result;
        }

        #endregion

        #region GetFile(获取客户端文件)

        /// <summary>
        /// 获取客户端文件
        /// </summary>
        /// <returns></returns>
        public static IFormFile GetFile()
        {
            var files = GetFiles();
            return files.Count == 0 ? null : files[0];
        }

        #endregion
    }
}
