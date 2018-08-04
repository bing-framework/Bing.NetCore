using System.Collections.Generic;

namespace Bing.Offices.Excels.Models.Parameters
{
    /// <summary>
    /// Excel 导入实体，对单元格类型做映射
    /// </summary>
    public class ExcelImportEntity:ExcelBaseEntity
    {
        /// <summary>
        /// 导入图片保存路径
        /// </summary>
        public static string ImageSavePath = "/excel/upload/img";

        /// <summary>
        /// 对应集合名称
        /// </summary>
        public string CollectionName { get; set; }

        /// <summary>
        /// 保存图片的地址
        /// </summary>
        public string SaveUrl { get; set; }

        /// <summary>
        /// 保存图片的类型，1=文件,2=数据库
        /// </summary>
        public int SaveType { get; set; }

        /// <summary>
        /// 对应导出类型
        /// </summary>
        public string ClassType { get; set; }

        /// <summary>
        /// 后缀
        /// </summary>
        public string Suffix { get; set; }

        /// <summary>
        /// 导入校验字段
        /// </summary>
        public bool ImportField { get; set; }

        /// <summary>
        /// 枚举导入静态方法
        /// </summary>
        public string EnumImportMethod { get; set; }

        /// <summary>
        /// 导入列表
        /// </summary>
        public List<ExcelImportEntity> ImportList { get; set; }
    }
}
