using System;
using System.Collections.Generic;

namespace Bing.Offices.Excels.Models.Parameters
{
    /// <summary>
    /// Excel 集合
    /// </summary>
    public class ExcelCollectionParams
    {
        /// <summary>
        /// 集合对应的名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Excel 列的名称
        /// </summary>
        public string ExcelName { get; set; }

        /// <summary>
        /// 实体对象
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Excel参数
        /// </summary>
        public IDictionary<string,ExcelImportEntity> ExcelParams { get; set; }
    }
}
