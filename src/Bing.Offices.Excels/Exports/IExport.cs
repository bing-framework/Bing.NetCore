using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Exports
{
    /// <summary>
    /// 导出器
    /// </summary>
    public interface IExport
    {
        /// <summary>
        /// 设置单元格日期格式
        /// </summary>
        /// <param name="format">日期格式，默认：yyyy-mm-dd</param>
        /// <returns></returns>
        IExport DateFormat(string format = "yyyy-mm-dd");

        /// <summary>
        /// 设置单元格列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        IExport ColumnWidth(int columnIndex, int width);

        /// <summary>
        /// 设置表头样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExport HeadStyle(CellStyle style);

        /// <summary>
        /// 设置正文样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExport BodyStyle(CellStyle style);

        /// <summary>
        /// 设置页脚样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExport FootStyle(CellStyle style);

        /// <summary>
        /// 添加标题
        /// </summary>
        /// <param name="title">标题</param>
        /// <returns></returns>
        IExport Title(string title);

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="titles">列标题</param>
        /// <returns></returns>
        IExport Head(params string[] titles);

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="cells">单元格集合</param>
        /// <returns></returns>
        IExport Head(params ICell[] cells);

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <returns></returns>
        IExport Body<T>(IEnumerable<T> list) where T : class;

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <param name="propertiesExpression">属性表达式，范例：t =&gt; new object[]{t.A,t.B}</param>
        /// <returns></returns>
        IExport Body<T>(IEnumerable<T> list, Expression<Func<T, object[]>> propertiesExpression) where T : class;

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <param name="propertyNames">属性列表，范例："A","B"</param>
        /// <returns></returns>
        IExport Body<T>(IEnumerable<T> list, params string[] propertyNames) where T : class;

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="values">值</param>
        /// <returns></returns>
        IExport Foot(params string[] values);

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cells">单元格集合</param>
        /// <returns></returns>
        IExport Foot(params ICell[] cells);

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <returns></returns>
        IExport Download(string fileName = "");

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        IExport Download(string fileName, Encoding encoding);

        /// <summary>
        /// 写入到文件
        /// </summary>
        /// <param name="direcotry">目录，不包括文件名</param>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <returns></returns>
        IExport WriteToFile(string direcotry, string fileName = "");

        /// <summary>
        /// 写入到字节数组
        /// </summary>
        /// <returns></returns>
        byte[] WriteToBuffer();

        /// <summary>
        /// 写入到内存流
        /// </summary>
        /// <param name="stream">内存流</param>
        void WriteToStream(Stream stream);
    }
}
