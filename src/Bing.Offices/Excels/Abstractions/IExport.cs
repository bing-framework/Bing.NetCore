using System.Collections.Generic;
using Bing.Offices.Excels.Enums;
using Bing.Offices.Excels.Models.Parameters;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 导出器
    /// </summary>
    public interface IExport
    {
        /// <summary>
        /// 保存到指定文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        void SaveToFile(string fileName);

        /// <summary>
        /// 保存到指定文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="format">Excel格式类型</param>
        void SaveToFile(string fileName, ExcelFormat format);

        /// <summary>
        /// 保存到二进制流
        /// </summary>
        /// <returns></returns>
        byte[] SaveToBuffer();
    }
}
