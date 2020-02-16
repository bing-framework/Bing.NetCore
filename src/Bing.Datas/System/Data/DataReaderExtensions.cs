using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace System.Data
{
    /// <summary>
    /// 数据读取器(<see cref="IDataReader"/>) 扩展
    /// </summary>
    public static class DataReaderExtensions
    {
        /// <summary>
        /// 是否DBNull
        /// </summary>
        /// <param name="this">IDataReader</param>
        /// <param name="name">名称</param>
        // ReSharper disable once InconsistentNaming
        public static bool IsDBNull(this IDataReader @this, string name) => @this.IsDBNull(@this.GetOrdinal(name));

        /// <summary>
        /// 转换为数据表
        /// </summary>
        /// <param name="this">IDataReader</param>
        public static DataTable ToDataTable(this IDataReader @this)
        {
            var dt = new DataTable();
            dt.Load(@this);
            return dt;
        }

        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="this">IDataReader</param>
        public static T ToEntity<T>(this IDataReader @this) where T : new()
        {
            var type = typeof(T);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var entity = new T();
            var hash = new HashSet<string>(Enumerable.Range(0, @this.FieldCount).Select(@this.GetName));
            foreach (var property in properties)
            {
                if (hash.Contains(property.Name))
                {
                    var valueType = property.PropertyType;
                    property.SetValue(entity,@this[property.Name]);
                }
            }
            throw new NotImplementedException();
        }
    }
}
