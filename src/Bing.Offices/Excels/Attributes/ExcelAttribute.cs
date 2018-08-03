using System;

namespace Bing.Offices.Excels.Attributes
{
    /// <summary>
    /// Excel特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ExcelAttribute : Attribute
    {
        /// <summary>
        /// 数据库时间格式化。如果字段是DateTime类型则不需要设置，数据库如果是string类型，这个需要设置这个数据库格式
        /// </summary>
        public string DbDateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 导出时间格式化。如果当前为空，则无需格式化日期
        /// </summary>
        public string ExportDateTimeFormat { get; set; }

        /// <summary>
        /// 导入时间格式化。如果当前为空，则无需格式化日期
        /// </summary>
        public string ImportDateTimeFormat { get; set; }

        /// <summary>
        /// 时间格式化。相当于同时设置了 <see cref="ExportDateTimeFormat"/> 以及 <see cref="ImportDateTimeFormat"/>
        /// </summary>
        public string DateTimeFormat { get; set; }

        /// <summary>
        /// 数字格式化。参数是Pattern，使用的对象是DecimalFormat
        /// </summary>
        public string NumberFormat { get; set; }

        /// <summary>
        /// 高度。导出时在Excel中每行的高度，单位为字符，一个汉字=2个字符。
        /// 优先选择@ExportParams中的 height
        /// </summary>
        public double Height { get; set; } = 10;

        /// <summary>
        /// 宽度。导出时在excel中每个列的宽 单位为字符，一个汉字=2个字符 如：以列名列内容中较合适的长度。
        /// 例如：姓名列6 【姓名一般三个字】性别列4【男女占1，但是列标题两个汉字】 限制1-255
        /// </summary>
        public double Width { get; set; } = 10;

        /// <summary>
        /// 图片类型，1=从文件中读取,2=从数据库中读取，默认是文件，同样导入也是一样的
        /// </summary>
        public int ImageType { get; set; } = 1;

        /// <summary>
        /// 文字后缀，例如：% 90 => 90%
        /// </summary>
        public string Suffix { get; set; }

        /// <summary>
        /// 是否换行，即支持\n
        /// </summary>
        public bool IsWrap { get; set; } = true;

        /// <summary>
        /// 合并单元格依赖关系，例如：第2列合并是基于第1列，则{1}就可以了
        /// </summary>
        public int[] MergeRely { get; set; } = { };

        /// <summary>
        /// 纵向合并内容相同的单元格
        /// </summary>
        public bool MergeVertical { get; set; } = false;

        /// <summary>
        /// 导出时，对应数据库的字段，主要是用户区分每个字段，不能有特性重名的导出时的列名。
        /// 导出排序跟定义了特性的字段的顺序有关，可以使用a_id,b_id来确实是否使用
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 导出时，表头双行显示，聚合，排序以最小的值参与总体排序再内部排序。
        /// 导出排序跟定义特性的字段的顺序有关，可以使用a_id,b_id来确实是否使用。
        /// 优先弱于 @ExcelEntity 的name和show属性
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 排序号，展示到第几个可以使用a_id,b_id来确定不同排序
        /// </summary>
        public int OrderNum { get; set; } = 0;

        /// <summary>
        /// 值替换，导出是{a_id,b_id}，导入反过来，所以只用写一个
        /// </summary>
        public string[] Replace { get; set; } = { };

        /// <summary>
        /// 导入路径，如果是图片可以填写，默认是upload/className/ IconEntity这个类对应的就是upload/Icon/
        /// </summary>
        public string SavePath { get; set; } = "/excel/upload/img";

        /// <summary>
        /// 导出类型。1=文本,2:图片,3:函数,10=数字。默认：文本
        /// </summary>
        public int Type { get; set; } = 1;

        /// <summary>
        /// 是否自动统计数据，如果是统计，true：在最后追加一行统计，把所有数据求和
        /// 这个额处理会吞异常，请注意这一点
        /// </summary>
        public bool IsStatistics { get; set; } = false;

        /// <summary>
        /// 是否超链接，如果是需要实现接口返回对象
        /// </summary>
        public bool IsHyperlink { get; set; } = false;

        /// <summary>
        /// 是否导入字段，导入时会校验这个字段，判断这个字段是否在导入的Excel中有，如果没有说明是错误的Excel
        /// </summary>
        public bool IsImportField { get; set; } = false;

        /// <summary>
        /// 固定某一列，解决不好解析的问题
        /// </summary>
        public int FixedIndex { get; set; } = -1;

        /// <summary>
        /// 是否需要隐藏该列
        /// </summary>
        public bool IsColumnHidden { get; set; } = false;

        /// <summary>
        /// 枚举导出所使用的字段
        /// </summary>
        public string EnumExportField { get; set; }

        /// <summary>
        /// 枚举导入使用的函数
        /// </summary>
        public string EnumImportMethod { get; set; }
    }
}
