using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// 数据类型转换器
/// </summary>
public interface ITypeConverter
{
    /// <summary>
    /// 将数据类型转换为 <see cref="DbType"/>
    /// </summary>
    /// <param name="dataType">数据类型</param>
    /// <param name="length">数据长度</param>
    DbType? ToDbType(string dataType, int? length = null);
}
