using System;
using System.Collections.Generic;

namespace Bing.Offices.Excels.Models.Parameters
{
    /// <summary>
    /// Excel导出实体
    /// </summary>
    public class ExcelExportEntity:ExcelBaseEntity,IComparable<ExcelExportEntity>
    {
        /// <summary>
        /// 字典键
        /// </summary>
        public object Key { get; set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; } = 10;

        /// <summary>
        /// 高度
        /// </summary>
        public double Height { get; set; } = 10;

        /// <summary>
        /// 图片类型，1=文件,2=数据库
        /// </summary>
        public int ExportImageType { get; set; } = 0;

        /// <summary>
        /// 排序顺序
        /// </summary>
        public int OrderNum { get; set; } = 0;

        /// <summary>
        /// 是否支持换行
        /// </summary>
        public bool IsWarp { get; set; }

        /// <summary>
        /// 是否需要合并
        /// </summary>
        public bool NeedMerge { get; set; }

        /// <summary>
        /// 单元格纵向合并
        /// </summary>
        public bool MergeVertical { get; set; }

        /// <summary>
        /// 合并依赖
        /// </summary>
        public int[] MergeRely { get; set; }

        /// <summary>
        /// 后缀
        /// </summary>
        public string Suffix { get; set; }

        /// <summary>
        /// 统计
        /// </summary>
        public bool IsStatistics { get; set; }

        /// <summary>
        /// 数值格式化
        /// </summary>
        public string NumFormat { get; set; }

        /// <summary>
        /// 是否隐藏列
        /// </summary>
        public bool IsColumnHidden { get; set; }

        /// <summary>
        /// 枚举导出属性字段
        /// </summary>
        public string EnumExportField { get; set; }

        /// <summary>
        /// 导出列表
        /// </summary>
        public List<ExcelExportEntity> ExportList { get; set; }

        public ExcelExportEntity(string name)
        {
            Name = name;
        }

        public ExcelExportEntity(string name, object key)
        {
            Name = name;
            Key = key;
        }

        public ExcelExportEntity(string name, object key, int width)
        {
            Name = name;
            Width = width;
            Key = key;
        }


        public int CompareTo(ExcelExportEntity other)
        {
            return OrderNum - other.OrderNum;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((Key == null ? 0 : Key.GetHashCode()));
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null)
            {
                return false;
            }

            if (GetType() != obj.GetType())
            {
                return false;
            }

            ExcelExportEntity other = (ExcelExportEntity) obj;
            if (Key == null)
            {
                if (other.Key != null)
                {
                    return false;
                }
            }
            else if (!Key.Equals(other.Key))
            {
                return false;
            }

            return true;
        }
    }
}
