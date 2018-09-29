using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Common;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Core.Styles;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.IO;

namespace Bing.Offices.Excels.Exports
{
    /// <summary>
    /// 导出器基类
    /// </summary>
    public abstract class ExportBase:IExport
    {
        #region 字段

        /// <summary>
        /// 工作表
        /// </summary>
        protected readonly IWorkSheet Sheet;

        /// <summary>
        /// 版本
        /// </summary>
        private readonly ExcelVersion _version;

        /// <summary>
        /// 表头样式
        /// </summary>
        private CellStyle _headStyle;

        /// <summary>
        /// 正文样式
        /// </summary>
        private CellStyle _bodyStyle;

        /// <summary>
        /// 页脚样式
        /// </summary>
        private CellStyle _footStyle;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ExportBase"/>类型的实例
        /// </summary>
        /// <param name="version">版本</param>
        protected ExportBase(ExcelVersion version)
        {
            Sheet = new WorkSheet();
            _version = version;
            _headStyle = CellStyle.Head();
            _bodyStyle = CellStyle.Body();
            _footStyle = CellStyle.Foot();
        }

        #endregion

        #region 样式相关

        /// <summary>
        /// 设置单元格列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        public abstract IExport ColumnWidth(int columnIndex, int width);

        /// <summary>
        /// 设置单元格日期格式
        /// </summary>
        /// <param name="format">日期格式，默认：yyyy-mm-dd</param>
        /// <returns></returns>
        public abstract IExport DateFormat(string format = "yyyy-mm-dd");

        /// <summary>
        /// 设置表头样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExport HeadStyle(CellStyle style)
        {
            _headStyle = style;
            return this;
        }

        /// <summary>
        /// 获取表头样式
        /// </summary>
        /// <returns></returns>
        protected CellStyle GetHeadStyle()
        {
            return _headStyle;
        }

        /// <summary>
        /// 设置正文样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExport BodyStyle(CellStyle style)
        {
            _bodyStyle = style;
            return this;
        }

        /// <summary>
        /// 获取正文样式
        /// </summary>
        /// <returns></returns>
        protected CellStyle GetBodyStyle()
        {
            return _bodyStyle;
        }

        /// <summary>
        /// 设置页脚样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExport FootStyle(CellStyle style)
        {
            _footStyle = style;
            return this;
        }

        /// <summary>
        /// 获取页脚样式
        /// </summary>
        /// <returns></returns>
        protected CellStyle GetFootStyle()
        {
            return _footStyle;
        }

        #endregion

        public abstract IExport Title(string title);

        #region Head(添加表头)

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="titles">列标题</param>
        /// <returns></returns>
        public IExport Head(params string[] titles)
        {
            Sheet.AddHeadRow(titles);
            return this;
        }
        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="cells">单元格集合</param>
        /// <returns></returns>
        public IExport Head(params ICell[] cells)
        {
            Sheet.AddHeadRow(cells);
            return this;
        }

        #endregion

        #region Body(添加正文)

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <returns></returns>
        public IExport Body<T>(IEnumerable<T> list) where T : class
        {
            return Body(list, typeof(T).GetProperties().Select(t => t.Name).ToArray());
        }

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <param name="propertiesExpression">属性表达式，范例：t =&gt; new object[]{t.A,t.B}</param>
        /// <returns></returns>
        public IExport Body<T>(IEnumerable<T> list, Expression<Func<T, object[]>> propertiesExpression) where T : class
        {
            return Body(list, Lambda.GetNames(propertiesExpression).ToArray());
        }

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">实体集合</param>
        /// <param name="propertyNames">属性列表，范例："A","B"</param>
        /// <returns></returns>
        public IExport Body<T>(IEnumerable<T> list, params string[] propertyNames) where T : class
        {
            list.CheckNotNull("list");
            propertyNames.CheckNotNull("propertyNames");
            foreach (var entity in list)
            {
                AddEntity(entity, propertyNames);
            }
            AdjustColumnWidth(list.FirstOrDefault(), propertyNames);
            return this;
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">实体</param>
        /// <param name="propertyNames">属性名集合</param>
        private void AddEntity<T>(T entity, IEnumerable<string> propertyNames) where T : class
        {
            var values = GetPropertyValues(entity, propertyNames);
            Sheet.AddBodyRow(values.ToArray());
        }

        /// <summary>
        /// 获取属性值集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">实体</param>
        /// <param name="propertyNames">属性名集合</param>
        /// <returns></returns>
        private List<object> GetPropertyValues<T>(T entity, IEnumerable<string> propertyNames) where T : class
        {
            var type = entity.GetType();
            return propertyNames.Select(type.GetProperty).Select(property =>
            {
                if (property == null)
                {
                    return "";
                }
                object result = property.GetValue(entity);
                if (property.PropertyType == typeof(bool))
                {
                    result = Conv.ToBool(result).Description();
                }
                return result;
            }).ToList();
        }

        /// <summary>
        /// 调整列宽
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">实体</param>
        /// <param name="propertyNames">属性名集合</param>
        private void AdjustColumnWidth<T>(T entity, string[] propertyNames) where T : class
        {
            if (entity == null)
            {
                return;
            }
            var type = entity.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                AdjustColumnWidth(type.GetProperty(propertyNames[i]), i);
            }
        }

        /// <summary>
        /// 调整列宽
        /// </summary>
        /// <param name="property">属性信息</param>
        /// <param name="index">索引</param>
        private void AdjustColumnWidth(PropertyInfo property, int index)
        {
            if (property == null)
            {
                return;
            }
            if (Reflection.IsDate(property))
            {
                ColumnWidth(index, 12);
            }
        }

        #endregion

        #region Foot(添加页脚)

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="values">值</param>
        /// <returns></returns>
        public IExport Foot(params string[] values)
        {
            Sheet.AddFootRow(values);
            return this;
        }
        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cells">单元格集合</param>
        /// <returns></returns>
        public IExport Foot(params ICell[] cells)
        {
            Sheet.AddFootRow(cells);
            return this;
        }

        #endregion

        #region Write(写入文件)

        /// <summary>
        /// 写入文件
        /// </summary>
        /// <param name="direcotry">目录，不包括文件名</param>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <returns></returns>
        public IExport WriteToFile(string direcotry, string fileName = "")
        {
            using (var stream = new FileStream(GetFilePath(direcotry, fileName), FileMode.Create))
            {
                WriteStream(stream);
            }
            return this;
        }

        /// <summary>
        /// 获取文件路径
        /// </summary>
        /// <param name="directory">目录，不包含文件名</param>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <returns></returns>
        private string GetFilePath(string directory, string fileName)
        {
            return FileUtil.JoinPath(directory, GetFileName(fileName));
        }

        /// <summary>
        /// 获取文件名
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns></returns>
        private string GetFileName(string fileName)
        {
            if (fileName.IsEmpty())
            {
                fileName = Sheet.Title;
            }

            if (fileName.IsEmpty())
            {
                fileName = DateTime.Now.ToDateString();
            }
            return fileName + "." + _version.ToString().ToLower();
        }

        /// <summary>
        /// 写入流
        /// </summary>
        /// <param name="stream">流</param>
        protected abstract void WriteStream(Stream stream);

        #endregion

        #region Download(下载)

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <returns></returns>
        public IExport Download(string fileName = "")
        {
            return Download(fileName, Encoding.UTF8);
        }

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="fileName">文件名，不包括扩展名</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public IExport Download(string fileName, Encoding encoding)
        {
            using (var stream = new MemoryStream())
            {
                WriteStream(stream);

            }
            return this;
        }

        #endregion

        /// <summary>
        /// 写入到字节数组
        /// </summary>
        /// <returns></returns>
        public abstract byte[] WriteToBuffer();

        /// <summary>
        /// 写入到内存流
        /// </summary>
        /// <param name="stream">内存流</param>
        public abstract void WriteToStream(Stream stream);
    }
}
